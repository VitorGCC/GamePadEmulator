using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using SharpDX;
using SharpDX.DirectInput;
using CooperativeLevelFlags = SharpDX.DirectInput.CooperativeLevel;

namespace GamepadEmulator
{
    public class VirtualGamepad : IDisposable
    {
        // Cliente ViGEm e controle virtual
        private ViGEmClient? client;
        private IXbox360Controller? controller;

        // Thread para processar inputs
        private Thread? inputThread;
        private bool isRunning = false;

        // Estado de ativação do emulador
        private bool isActive = true;

        // Evento para notificação de mudança de estado
        public event EventHandler<EmulatorStateChangedEventArgs>? StateChanged;

        // DirectInput para interceptação de dispositivos
        private DirectInput? directInput;
        private Keyboard? keyboard;
        private bool directInputActive = false;

        // Configuração de mapeamento
        public KeyMapping KeyMap { get; set; }

        // Classe interna para os argumentos do evento
        public class EmulatorStateChangedEventArgs : EventArgs
        {
            public bool IsActive { get; }

            public EmulatorStateChangedEventArgs(bool isActive)
            {
                IsActive = isActive;
            }
        }

        // Engine de Tradução Avançada
        private MouseTranslationEngine mouseEngine = new MouseTranslationEngine();

        // Engine de assistência de mira por cor
        private GamepadEmulator.Core.AimColorEngine aimColorEngine = new GamepadEmulator.Core.AimColorEngine();

        // Timing de polling
        private long lastPollTime = 0;
        private long performanceFrequency = 0;

        // Detecção de janela de jogo (throttled — não checar a 1000 Hz, mas rápido o
        // suficiente para que o bloqueio de teclas e o esconder-cursor sejam imperceptíveis)
        private long lastGameCheckTime = 0;
        private bool cachedGameActive = false;
        private const long GAME_CHECK_INTERVAL_MS = 16;

        // Lista de processos de jogos (padrão; o usuário pode adicionar via KeyMap.CustomGameProcesses)
        private readonly HashSet<string> gameProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "modernwarfare", "warzone", "cod", "callofduty",
            "fifa", "nba2k", "madden", "pes",
            "rocketleague", "fortnite", "apex"
        };

        // Tecla para alternar o estado do emulador
        private readonly Keys ToggleKey = Keys.F12;
        private long lastToggleTime = 0;
        private const long TOGGLE_COOLDOWN_MS = 300;

        // API Windows para acesso a funções nativas
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);


        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryPerformanceFrequency(out long lpFrequency);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(Keys vKey);

        [DllImport("winmm.dll")]
        static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        static extern uint timeEndPeriod(uint uPeriod);

        [DllImport("user32.dll")]
        static extern int ShowCursor(bool bShow);
        public VirtualGamepad(IntPtr windowHandle)
        {
            KeyMap = new KeyMapping();
            InitializeClient();
            
            // Inicializar RawInput na thread principal com o Handle da janela
            GamepadEmulator.Core.RawInputManager.Initialize(windowHandle);

            // Inicializar contador de alta precisão
            QueryPerformanceFrequency(out performanceFrequency);

            // Inicializar DirectInput para teclado e mouse
            InitializeDirectInput();
        }

        private void InitializeDirectInput()
        {
            try
            {
                directInput = new DirectInput();

                // Configurar teclado
                keyboard = new Keyboard(directInput);
                keyboard.Properties.BufferSize = 128;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao inicializar DirectInput: {ex.Message}");
                directInput?.Dispose();
                directInput = null;
                keyboard?.Dispose();
                keyboard = null;
            }
        }

        // Remova as configurações de modo exclusivo
        private void StartDirectInput()
        {
            try
            {
                if (keyboard != null)
                {
                    // Modo não-exclusivo
                    keyboard.SetCooperativeLevel(Process.GetCurrentProcess().MainWindowHandle,
                        CooperativeLevelFlags.Background | CooperativeLevelFlags.NonExclusive);
                    keyboard.Acquire();
                }

                directInputActive = true;
                
                // Ativar bloqueio APENAS de teclas mapeadas do teclado
                // NÃO bloquear mouse (movimento e botões) — o jogo lê o mouse diretamente
                HashSet<Keys> blockedKeys = new HashSet<Keys>
                {
                    KeyMap.DPadUpKey, KeyMap.DPadDownKey, KeyMap.DPadLeftKey, KeyMap.DPadRightKey,
                    KeyMap.LeftStickUpKey, KeyMap.LeftStickDownKey, KeyMap.LeftStickLeftKey, KeyMap.LeftStickRightKey,
                    KeyMap.ButtonA, KeyMap.ButtonB, KeyMap.ButtonX, KeyMap.ButtonY,
                    KeyMap.ButtonLB, KeyMap.ButtonRB,
                    KeyMap.ButtonStart, KeyMap.ButtonBack
                };
                InputBlocker.SetBlockingState(true, blockedKeys, false, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao iniciar DirectInput: {ex.Message}");
                directInputActive = false;
            }
        }

        private void StopDirectInput()
        {
            try
            {
                if (keyboard != null)
                {
                    keyboard.Unacquire();
                }

                directInputActive = false;
                InputBlocker.SetBlockingState(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao parar DirectInput: {ex.Message}");
            }
        }

        private bool IsGameWindowActive()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return false;

                uint processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);

                Process process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName.ToLower();

                bool isGameActive = gameProcesses.Any(game => processName.Contains(game))
                    || (KeyMap.CustomGameProcesses?.Any(game =>
                            !string.IsNullOrWhiteSpace(game) && processName.Contains(game.ToLower())) ?? false);

                if (isGameActive && !directInputActive)
                {
                    StartDirectInput();

                    // Esconder o cursor ao entrar em um jogo
                    while (ShowCursor(false) >= 0) ;

                    // Importante: Deixar o jogo como janela de primeiro plano
                    SetForegroundWindow(foregroundWindow);
                }
                else if (!isGameActive && directInputActive)
                {
                    StopDirectInput();

                    // Mostrar o cursor ao sair do jogo
                    while (ShowCursor(true) < 0) ;
                }

                return isGameActive;
            }
            catch
            {
                return false;
            }
        }

        private void InitializeClient()
        {
            try
            {
                client = new ViGEmClient();
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao inicializar driver ViGEm. Verifique se o ViGEmBus está instalado.", ex);
            }
        }

        public bool Start()
        {
            if (isRunning || client == null)
                return false;

            try
            {
                // Criar e conectar o controle
                controller = client.CreateXbox360Controller();
                controller.Connect();

                // Ativar timer de alta precisão
                timeBeginPeriod(1);

                // Iniciar contagem de tempo de alta precisão
                QueryPerformanceCounter(out lastPollTime);

                // Inicialmente ativo
                isActive = true;
                OnStateChanged(isActive);

                // Iniciar engine de assistência de mira por cor
                aimColorEngine.UpdateConfig(KeyMap);
                aimColorEngine.Start();

                // Iniciar thread para processar inputs com prioridade máxima
                isRunning = true;
                inputThread = new Thread(ProcessInputs)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Highest
                };
                inputThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao iniciar emulador: {ex.Message}\nStack: {ex.StackTrace}");
                if (controller != null)
                {
                    try { controller.Disconnect(); } catch { }
                    controller = null;
                }
                isRunning = false;
                return false;
            }
        }

        public void Stop()
        {
            isRunning = false;

            // Parar engine de assistência de mira
            aimColorEngine.Stop();

            if (inputThread != null && inputThread.IsAlive)
            {
                inputThread.Join(1000);
                inputThread = null;
            }

            if (controller != null)
            {
                controller.Disconnect();
                controller = null;
            }

            // Parar DirectInput e mostrar cursor
            StopDirectInput();
            while (ShowCursor(true) < 0) ;

            // Encerrar timer de precisão
            timeEndPeriod(1);
        }

        public void ToggleActiveState()
        {
            isActive = !isActive;
            OnStateChanged(isActive);

            if (!isActive)
            {
                // Parar DirectInput quando desativado
                StopDirectInput();
                // Mostrar cursor
                while (ShowCursor(true) < 0) ;
            }
            else
            {
                // Força reavaliação imediata da janela de jogo no próximo ciclo
                // (reativa bloqueio de teclas e esconde o cursor sem atraso)
                lastGameCheckTime = 0;
            }
        }

        protected virtual void OnStateChanged(bool isActive)
        {
            StateChanged?.Invoke(this, new EmulatorStateChangedEventArgs(isActive));
        }

        private void ProcessInputs()
        {
            // Timer de alta precisão
            long currentPollTime = 0;

            // Estado anterior do teclado
            KeyboardState? previousKeyboardState = null;
            bool firstKeyboardState = true;

            while (isRunning && controller != null)
            {
                // Timing de alta precisão
                QueryPerformanceCounter(out currentPollTime);
                double elapsedTime = (double)(currentPollTime - lastPollTime) / performanceFrequency;

                // Limitar taxa de atualização
                double targetFrameTime = 1.0 / Math.Max(10, KeyMap.PollingRate);
                if (elapsedTime < targetFrameTime)
                {
                    Thread.Sleep(1);
                    continue;
                }

                lastPollTime = currentPollTime;

                // Verificar se estamos em um jogo (throttled — chamadas de sistema caras)
                long nowMs = Environment.TickCount64;
                if (nowMs - lastGameCheckTime >= GAME_CHECK_INTERVAL_MS)
                {
                    cachedGameActive = IsGameWindowActive();
                    // Sincroniza a config da assistência de mira (perfil pode ter mudado)
                    aimColorEngine.UpdateConfig(KeyMap);
                    lastGameCheckTime = nowMs;
                }
                bool gameActive = cachedGameActive;

                if (!isActive)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // Verificar F12 para toggle
                if ((GetAsyncKeyState(ToggleKey) & 0x8000) != 0)
                {
                    long currentTime = Environment.TickCount64;
                    if (currentTime - lastToggleTime > TOGGLE_COOLDOWN_MS)
                    {
                        ToggleActiveState();
                        lastToggleTime = currentTime;
                        Thread.Sleep(100);
                    }
                }

                if (!isActive)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // Resetar botões do controle virtual
                ResetControllerButtons();

                // Processar botões e eixos
                if (directInputActive && gameActive)
                {
                    ProcessDirectInput(ref previousKeyboardState, ref firstKeyboardState);
                }
                else
                {
                    ProcessStandardInputs();
                }

                // Submeter atualizações do controle virtual
                // (guarda contra Stop()/Dispose() anular o controller mid-ciclo)
                controller?.SubmitReport();
            }
        }

        private void ProcessDirectInput(ref KeyboardState? previousKeyboardState, ref bool firstKeyboardState)
        {
            try
            {
                // Ler estado do teclado via DirectInput
                if (keyboard != null)
                {
                    keyboard.Poll();
                    KeyboardState currentState = keyboard.GetCurrentState();

                    if (firstKeyboardState)
                    {
                        previousKeyboardState = currentState;
                        firstKeyboardState = false;
                    }

                    // Processar teclas para o controle Xbox
                    ProcessKeyboardState(currentState);

                    previousKeyboardState = currentState;
                }
                // Ler movimento do mouse via Raw Input (Zero-latency)
                var deltas = GamepadEmulator.Core.RawInputManager.GetAndResetDeltas();
                ProcessMouseMovement(deltas.deltaX, deltas.deltaY);
            }
            catch (SharpDX.SharpDXException ex)
            {
                // Tentar reaquirir dispositivos se perdeu o acesso
                if (ex.ResultCode.Code == ResultCode.InputLost.Code)
                {
                    try
                    {
                        keyboard?.Acquire();
                    }
                    catch
                    {
                        // Ignorar erros ao tentar reaquirir
                    }
                }
            }
            catch (Exception)
            {
                // Ignorar outros erros
            }
        }



        private void ProcessKeyboardState(KeyboardState state)
        {
            if (controller == null)
                return;

            // D-pad
            if (IsKeyPressed(state, KeyMap.DPadUpKey))
                controller.SetButtonState(Xbox360Button.Up, true);

            if (IsKeyPressed(state, KeyMap.DPadDownKey))
                controller.SetButtonState(Xbox360Button.Down, true);

            if (IsKeyPressed(state, KeyMap.DPadLeftKey))
                controller.SetButtonState(Xbox360Button.Left, true);

            if (IsKeyPressed(state, KeyMap.DPadRightKey))
                controller.SetButtonState(Xbox360Button.Right, true);

            // Analógico esquerdo
            short leftThumbX = 0;
            short leftThumbY = 0;

            if (IsKeyPressed(state, KeyMap.LeftStickUpKey))
                leftThumbY = short.MaxValue;

            if (IsKeyPressed(state, KeyMap.LeftStickDownKey))
                leftThumbY = short.MinValue;

            if (IsKeyPressed(state, KeyMap.LeftStickLeftKey))
                leftThumbX = short.MinValue;

            if (IsKeyPressed(state, KeyMap.LeftStickRightKey))
                leftThumbX = short.MaxValue;

            controller.SetAxisValue(Xbox360Axis.LeftThumbX, leftThumbX);
            controller.SetAxisValue(Xbox360Axis.LeftThumbY, leftThumbY);

            // Botões de ação
            if (IsKeyPressed(state, KeyMap.ButtonA))
                controller.SetButtonState(Xbox360Button.A, true);

            if (IsKeyPressed(state, KeyMap.ButtonB))
                controller.SetButtonState(Xbox360Button.B, true);

            if (IsKeyPressed(state, KeyMap.ButtonX))
                controller.SetButtonState(Xbox360Button.X, true);

            if (IsKeyPressed(state, KeyMap.ButtonY))
                controller.SetButtonState(Xbox360Button.Y, true);

            // Botões de ombro
            if (IsKeyPressed(state, KeyMap.ButtonLB))
                controller.SetButtonState(Xbox360Button.LeftShoulder, true);

            if (IsKeyPressed(state, KeyMap.ButtonRB))
                controller.SetButtonState(Xbox360Button.RightShoulder, true);

            // Botões especiais
            if (IsKeyPressed(state, KeyMap.ButtonStart))
                controller.SetButtonState(Xbox360Button.Start, true);

            if (IsKeyPressed(state, KeyMap.ButtonBack))
                controller.SetButtonState(Xbox360Button.Back, true);

            // Gatilhos
            byte rightTrigger = 0;
            byte leftTrigger = 0;

            if (IsKeyPressed(state, KeyMap.ButtonRT))
                rightTrigger = byte.MaxValue;

            if (IsKeyPressed(state, KeyMap.ButtonLT))
                leftTrigger = byte.MaxValue;

            controller.SetSliderValue(Xbox360Slider.RightTrigger, rightTrigger);
            controller.SetSliderValue(Xbox360Slider.LeftTrigger, leftTrigger);
        }

        // Mapeia tecla do Windows Forms para tecla do DirectInput
        private Key MapKeyToKey(Keys key)
        {
            // Mapear teclas Windows Forms para teclas DirectInput
            // Este é um mapeamento básico para teclas comuns
            switch (key)
            {
                case Keys.A: return Key.A;
                case Keys.B: return Key.B;
                case Keys.C: return Key.C;
                case Keys.D: return Key.D;
                case Keys.E: return Key.E;
                case Keys.F: return Key.F;
                case Keys.G: return Key.G;
                case Keys.H: return Key.H;
                case Keys.I: return Key.I;
                case Keys.J: return Key.J;
                case Keys.K: return Key.K;
                case Keys.L: return Key.L;
                case Keys.M: return Key.M;
                case Keys.N: return Key.N;
                case Keys.O: return Key.O;
                case Keys.P: return Key.P;
                case Keys.Q: return Key.Q;
                case Keys.R: return Key.R;
                case Keys.S: return Key.S;
                case Keys.T: return Key.T;
                case Keys.U: return Key.U;
                case Keys.V: return Key.V;
                case Keys.W: return Key.W;
                case Keys.X: return Key.X;
                case Keys.Y: return Key.Y;
                case Keys.Z: return Key.Z;
                case Keys.D0: return Key.D0;
                case Keys.D1: return Key.D1;
                case Keys.D2: return Key.D2;
                case Keys.D3: return Key.D3;
                case Keys.D4: return Key.D4;
                case Keys.D5: return Key.D5;
                case Keys.D6: return Key.D6;
                case Keys.D7: return Key.D7;
                case Keys.D8: return Key.D8;
                case Keys.D9: return Key.D9;
                case Keys.Space: return Key.Space;
                case Keys.Enter: return Key.Return;
                case Keys.Escape: return Key.Escape;
                case Keys.Tab: return Key.Tab;
                case Keys.Back: return Key.Back;
                case Keys.CapsLock: return Key.Capital;

                // Modificadores (esquerda e direita)
                case Keys.ShiftKey:
                case Keys.LShiftKey: return Key.LeftShift;
                case Keys.RShiftKey: return Key.RightShift;
                case Keys.ControlKey:
                case Keys.LControlKey: return Key.LeftControl;
                case Keys.RControlKey: return Key.RightControl;
                case Keys.Alt:
                case Keys.Menu:
                case Keys.LMenu: return Key.LeftAlt;
                case Keys.RMenu: return Key.RightAlt;
                case Keys.LWin: return Key.LeftWindowsKey;
                case Keys.RWin: return Key.RightWindowsKey;

                // Setas
                case Keys.Up: return Key.Up;
                case Keys.Down: return Key.Down;
                case Keys.Left: return Key.Left;
                case Keys.Right: return Key.Right;

                // Navegação
                case Keys.Insert: return Key.Insert;
                case Keys.Delete: return Key.Delete;
                case Keys.Home: return Key.Home;
                case Keys.End: return Key.End;
                case Keys.PageUp: return Key.PageUp;
                case Keys.PageDown: return Key.PageDown;

                // Teclas de função
                case Keys.F1: return Key.F1;
                case Keys.F2: return Key.F2;
                case Keys.F3: return Key.F3;
                case Keys.F4: return Key.F4;
                case Keys.F5: return Key.F5;
                case Keys.F6: return Key.F6;
                case Keys.F7: return Key.F7;
                case Keys.F8: return Key.F8;
                case Keys.F9: return Key.F9;
                case Keys.F10: return Key.F10;
                case Keys.F11: return Key.F11;
                case Keys.F12: return Key.F12;

                // Teclado numérico
                case Keys.NumPad0: return Key.NumberPad0;
                case Keys.NumPad1: return Key.NumberPad1;
                case Keys.NumPad2: return Key.NumberPad2;
                case Keys.NumPad3: return Key.NumberPad3;
                case Keys.NumPad4: return Key.NumberPad4;
                case Keys.NumPad5: return Key.NumberPad5;
                case Keys.NumPad6: return Key.NumberPad6;
                case Keys.NumPad7: return Key.NumberPad7;
                case Keys.NumPad8: return Key.NumberPad8;
                case Keys.NumPad9: return Key.NumberPad9;
                case Keys.Add: return Key.Add;
                case Keys.Subtract: return Key.Subtract;
                case Keys.Multiply: return Key.Multiply;
                case Keys.Divide: return Key.Divide;
                case Keys.Decimal: return Key.Decimal;

                // Teclas OEM / pontuação
                case Keys.Oemtilde: return Key.Grave;
                case Keys.OemMinus: return Key.Minus;
                case Keys.Oemplus: return Key.Equals;
                case Keys.OemOpenBrackets: return Key.LeftBracket;
                case Keys.OemCloseBrackets: return Key.RightBracket;
                case Keys.OemSemicolon: return Key.Semicolon;
                case Keys.OemQuotes: return Key.Apostrophe;
                case Keys.Oemcomma: return Key.Comma;
                case Keys.OemPeriod: return Key.Period;
                case Keys.OemQuestion: return Key.Slash;
                case Keys.OemBackslash:
                case Keys.OemPipe: return Key.Backslash;

                default: return Key.Unknown;
            }
        }

        private bool IsKeyPressed(KeyboardState state, Keys winKey)
        {
            // Tratar botões do mouse separadamente
            if (winKey == Keys.LButton || winKey == Keys.RButton || winKey == Keys.MButton || 
                winKey == Keys.XButton1 || winKey == Keys.XButton2)
            {
                return (GetAsyncKeyState(winKey) & 0x8000) != 0;
            }

            Key dKey = MapKeyToKey(winKey);
            if (dKey == Key.Unknown) return false;
            return state.IsPressed(dKey);
        }

        /// <summary>
        /// Mapeamento avançado do mouse para o analógico direito usando MouseTranslationEngine.
        /// </summary>
        private void ProcessMouseMovement(int deltaX, int deltaY)
        {
            if (controller == null) return;

            // Sincronizar todos os parâmetros do perfil com a engine
            mouseEngine.SensitivityX = KeyMap.SensitivityX;
            mouseEngine.SensitivityY = KeyMap.SensitivityY;
            mouseEngine.YAxisRatio = KeyMap.YAxisRatio;
            mouseEngine.PowerCurve = KeyMap.PowerCurve;
            mouseEngine.AntiDeadzone = KeyMap.AntiDeadzone;
            mouseEngine.SmoothingFactor = KeyMap.SmoothingFactor;

            // Passa os deltas do mouse cru para a Engine de Tradução
            var (stickX, stickY) = mouseEngine.Translate(deltaX, deltaY);

            int outX = stickX;
            int outY = stickY;

            // Assistência de mira por cor: soma o pull calculado pela engine de tela.
            if (KeyMap.AimColorEnabled && IsAimColorActive())
            {
                var (ax, ay) = aimColorEngine.GetPull();
                outX = Math.Clamp(outX + ax, short.MinValue, short.MaxValue);
                outY = Math.Clamp(outY + ay, short.MinValue, short.MaxValue);
            }

            // Compensação de recoil: enquanto o gatilho de tiro está pressionado,
            // aplica um viés CONSTANTE para baixo (Y negativo) para neutralizar o coice.
            // O pull é pequeno e fixo — não acumula com o movimento manual do jogador.
            if (KeyMap.RecoilEnabled && IsFireHeld())
            {
                int recoilPull = KeyMap.RecoilStrength * 500;
                outY = Math.Clamp(outY - recoilPull, short.MinValue, short.MaxValue);
            }

            // Envia para o controle virtual
            controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)outX);
            controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)outY);
        }

        /// <summary>
        /// Verifica se o gatilho de tiro (RT) está pressionado.
        /// </summary>
        private bool IsFireHeld()
        {
            return (GetAsyncKeyState(KeyMap.ButtonRT) & 0x8000) != 0;
        }

        /// <summary>
        /// Verifica se o gatilho de mira/ADS (LT) está pressionado.
        /// </summary>
        private bool IsAdsHeld()
        {
            return (GetAsyncKeyState(KeyMap.ButtonLT) & 0x8000) != 0;
        }

        /// <summary>
        /// Determina se a assistência de mira deve estar ativa, conforme o modo configurado.
        /// </summary>
        private bool IsAimColorActive()
        {
            switch ((KeyMap.AimColorActivationMode ?? "fire").ToLowerInvariant())
            {
                case "always": return true;
                case "ads": return IsAdsHeld();
                case "fire":
                default: return IsFireHeld();
            }
        }

        private void ResetControllerButtons()
        {
            if (controller == null) return;

            // Resetar botões
            controller.SetButtonState(Xbox360Button.A, false);
            controller.SetButtonState(Xbox360Button.B, false);
            controller.SetButtonState(Xbox360Button.X, false);
            controller.SetButtonState(Xbox360Button.Y, false);
            controller.SetButtonState(Xbox360Button.Start, false);
            controller.SetButtonState(Xbox360Button.Back, false);
            controller.SetButtonState(Xbox360Button.LeftShoulder, false);
            controller.SetButtonState(Xbox360Button.RightShoulder, false);
            controller.SetButtonState(Xbox360Button.LeftThumb, false);
            controller.SetButtonState(Xbox360Button.RightThumb, false);
            controller.SetButtonState(Xbox360Button.Guide, false);
            controller.SetButtonState(Xbox360Button.Up, false);
            controller.SetButtonState(Xbox360Button.Down, false);
            controller.SetButtonState(Xbox360Button.Left, false);
            controller.SetButtonState(Xbox360Button.Right, false);

            // Resetar gatilhos (Triggers)
            controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
            controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
        }

        private void ProcessStandardInputs()
        {
            if (controller == null) return;

            // D-pad
            if ((GetAsyncKeyState(KeyMap.DPadUpKey) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Up, true);

            if ((GetAsyncKeyState(KeyMap.DPadDownKey) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Down, true);

            if ((GetAsyncKeyState(KeyMap.DPadLeftKey) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Left, true);

            if ((GetAsyncKeyState(KeyMap.DPadRightKey) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Right, true);

            // Analógico esquerdo
            short leftThumbX = 0;
            short leftThumbY = 0;

            if ((GetAsyncKeyState(KeyMap.LeftStickUpKey) & 0x8000) != 0)
                leftThumbY = short.MaxValue;

            if ((GetAsyncKeyState(KeyMap.LeftStickDownKey) & 0x8000) != 0)
                leftThumbY = short.MinValue;

            if ((GetAsyncKeyState(KeyMap.LeftStickLeftKey) & 0x8000) != 0)
                leftThumbX = short.MinValue;

            if ((GetAsyncKeyState(KeyMap.LeftStickRightKey) & 0x8000) != 0)
                leftThumbX = short.MaxValue;

            controller.SetAxisValue(Xbox360Axis.LeftThumbX, leftThumbX);
            controller.SetAxisValue(Xbox360Axis.LeftThumbY, leftThumbY);

            // Botões de ação
            if ((GetAsyncKeyState(KeyMap.ButtonA) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.A, true);

            if ((GetAsyncKeyState(KeyMap.ButtonB) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.B, true);

            if ((GetAsyncKeyState(KeyMap.ButtonX) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.X, true);

            if ((GetAsyncKeyState(KeyMap.ButtonY) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Y, true);

            // Botões de ombro
            if ((GetAsyncKeyState(KeyMap.ButtonLB) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.LeftShoulder, true);

            if ((GetAsyncKeyState(KeyMap.ButtonRB) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.RightShoulder, true);

            // Botões especiais
            if ((GetAsyncKeyState(KeyMap.ButtonStart) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Start, true);

            if ((GetAsyncKeyState(KeyMap.ButtonBack) & 0x8000) != 0)
                controller.SetButtonState(Xbox360Button.Back, true);

            // Gatilhos
            byte rightTrigger = 0;
            byte leftTrigger = 0;

            if ((GetAsyncKeyState(KeyMap.ButtonRT) & 0x8000) != 0)
                rightTrigger = byte.MaxValue;

            if ((GetAsyncKeyState(KeyMap.ButtonLT) & 0x8000) != 0)
                leftTrigger = byte.MaxValue;

            controller.SetSliderValue(Xbox360Slider.RightTrigger, rightTrigger);
            controller.SetSliderValue(Xbox360Slider.LeftTrigger, leftTrigger);
        }

        public void Dispose()
        {
            Stop();

            aimColorEngine?.Dispose();

            // Limpar recursos do DirectInput
            keyboard?.Dispose();
            keyboard = null;

            directInput?.Dispose();
            directInput = null;

            if (client != null)
            {
                client.Dispose();
                client = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
