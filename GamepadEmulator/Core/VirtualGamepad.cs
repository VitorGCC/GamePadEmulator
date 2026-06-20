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
        private Mouse? mouse;
        private bool directInputActive = false;

        private VirtualHidDevice virtualHid;

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

        // Configurações de sensibilidade ajustáveis
        public class SensitivitySettings
        {
            public float GlobalMultiplier { get; set; } = 75.0f;
            public float MicroMovementAmplification { get; set; } = 0.04f;
            public float SmoothingFactor { get; set; } = 0.05f;
            public float VelocityAmplifier { get; set; } = 4.5f;
            public float CircularMotionBoost { get; set; } = 40.0f;
            public float CircularSmoothingFactor { get; set; } = 0.01f;
            public float DiagonalMultiplier { get; set; } = 75.0f;
            public float DiagonalSmoothingFactor { get; set; } = 0.05f;
            public float MicroDiagonalAmplification { get; set; } = 0.2f;
            public float DiagonalPerfectionFactor { get; set; } = 0.4f;
        }

        private SensitivitySettings sensitivity = new SensitivitySettings();

        // Rastreamento de movimento de mouse
        private Vector2 rawMouseDelta = Vector2.Zero;

        // Detecção de mouse levantado
        private long lastPositionChangeTime = 0;
        private const long MOUSE_LIFT_DETECTION_MS = 50;

        // Valores do analógico com alta precisão
        private Vector2 analogStickValue = Vector2.Zero;

        // Taxa de polling (1000 Hz = 1ms, suficiente para jogos)
        private const int TARGET_POLL_RATE = 1000;
        private long lastPollTime = 0;
        private long performanceFrequency = 0;

        // Lista de processos de jogos
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
        public VirtualGamepad()
        {
            KeyMap = new KeyMapping();
            InitializeClient();

            // Inicializar contador de alta precisão
            QueryPerformanceFrequency(out performanceFrequency);

            // Inicializar DirectInput para teclado e mouse
            InitializeDirectInput();

            // Inicializar dispositivo HID virtual
            virtualHid = new VirtualHidDevice();
            if (!virtualHid.Initialize())
            {
                Debug.WriteLine("Aviso: Não foi possível inicializar o dispositivo HID virtual.");
            }
        }

        private void InitializeDirectInput()
        {
            try
            {
                directInput = new DirectInput();

                // Configurar teclado
                keyboard = new Keyboard(directInput);
                keyboard.Properties.BufferSize = 128;

                // Configurar mouse
                mouse = new Mouse(directInput);
                mouse.Properties.BufferSize = 128;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao inicializar DirectInput: {ex.Message}");
                directInput?.Dispose();
                directInput = null;
                keyboard?.Dispose();
                keyboard = null;
                mouse?.Dispose();
                mouse = null;
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

                if (mouse != null)
                {
                    // Modo não-exclusivo
                    mouse.SetCooperativeLevel(Process.GetCurrentProcess().MainWindowHandle,
                        CooperativeLevelFlags.Background | CooperativeLevelFlags.NonExclusive);
                    mouse.Acquire();
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

                if (mouse != null)
                {
                    mouse.Unacquire();
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

                bool isGameActive = gameProcesses.Any(game => processName.Contains(game));

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
        }

        protected virtual void OnStateChanged(bool isActive)
        {
            StateChanged?.Invoke(this, new EmulatorStateChangedEventArgs(isActive));
        }

        private void ProcessInputs()
        {
            // Timer de alta precisão
            long currentPollTime = 0;

            // Anti-flickering
            int flickerCounter = 0;

            // Estado anterior do teclado
            KeyboardState previousKeyboardState = default;
            bool firstKeyboardState = true;

            // Estado anterior do mouse
            MouseState previousMouseState = default;
            bool firstMouseState = true;

            while (isRunning && controller != null)
            {
                // Timing de alta precisão
                QueryPerformanceCounter(out currentPollTime);
                double elapsedTime = (double)(currentPollTime - lastPollTime) / performanceFrequency;

                // Limitar taxa de atualização
                double targetFrameTime = 1.0 / TARGET_POLL_RATE;
                if (elapsedTime < targetFrameTime)
                {
                    Thread.Sleep(1);
                    continue;
                }

                lastPollTime = currentPollTime;

                // Verificar se estamos em um jogo (apenas 1x por ciclo)
                bool gameActive = IsGameWindowActive();

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

                // Processar entrada via DirectInput quando em jogo
                if (directInputActive && gameActive)
                {
                    ProcessDirectInput(ref previousKeyboardState, ref firstKeyboardState,
                                     ref previousMouseState, ref firstMouseState);
                }
                else
                {
                    ProcessStandardInputs();
                }

                // Processar botões do mouse como gatilhos (sempre, em qualquer modo)
                ProcessMouseButtons();

                // Submeter atualizações do controle virtual
                controller.SubmitReport();
            }
        }

        private void ProcessDirectInput(ref KeyboardState previousKeyboardState, ref bool firstKeyboardState,
                                      ref MouseState previousMouseState, ref bool firstMouseState)
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

                // Ler movimento do mouse via DirectInput
                if (mouse != null)
                {
                    mouse.Poll();
                    MouseState currentMouseState = mouse.GetCurrentState();

                    // DirectInput já retorna valores RELATIVOS (delta) por poll.
                    // NÃO subtrair o estado anterior — usar diretamente.
                    ProcessMouseMovement(currentMouseState.X, currentMouseState.Y);
                }
            }
            catch (SharpDX.SharpDXException ex)
            {
                // Tentar reaquirir dispositivos se perdeu o acesso
                if (ex.ResultCode.Code == ResultCode.InputLost.Code)
                {
                    try
                    {
                        keyboard?.Acquire();
                        mouse?.Acquire();
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

        /// <summary>
        /// Processa botões do mouse e mapeia para gatilhos do controle.
        /// O MOVIMENTO do mouse NÃO é interceptado — o jogo lê diretamente.
        /// </summary>
        private void ProcessMouseButtons()
        {
            if (controller == null) return;

            // Botão esquerdo do mouse → RT (atirar)
            if ((GetAsyncKeyState(Keys.LButton) & 0x8000) != 0)
                controller.SetSliderValue(Xbox360Slider.RightTrigger, byte.MaxValue);

            // Botão direito do mouse → LT (mirar)
            if ((GetAsyncKeyState(Keys.RButton) & 0x8000) != 0)
                controller.SetSliderValue(Xbox360Slider.LeftTrigger, byte.MaxValue);
        }

        private void ProcessKeyboardState(KeyboardState state)
        {
            if (controller == null)
                return;

            // D-pad
            if (state.IsPressed(MapKeyToKey(KeyMap.DPadUpKey)))
                controller.SetButtonState(Xbox360Button.Up, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.DPadDownKey)))
                controller.SetButtonState(Xbox360Button.Down, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.DPadLeftKey)))
                controller.SetButtonState(Xbox360Button.Left, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.DPadRightKey)))
                controller.SetButtonState(Xbox360Button.Right, true);

            // Analógico esquerdo
            short leftThumbX = 0;
            short leftThumbY = 0;

            if (state.IsPressed(MapKeyToKey(KeyMap.LeftStickUpKey)))
                leftThumbY = short.MaxValue;

            if (state.IsPressed(MapKeyToKey(KeyMap.LeftStickDownKey)))
                leftThumbY = short.MinValue;

            if (state.IsPressed(MapKeyToKey(KeyMap.LeftStickLeftKey)))
                leftThumbX = short.MinValue;

            if (state.IsPressed(MapKeyToKey(KeyMap.LeftStickRightKey)))
                leftThumbX = short.MaxValue;

            controller.SetAxisValue(Xbox360Axis.LeftThumbX, leftThumbX);
            controller.SetAxisValue(Xbox360Axis.LeftThumbY, leftThumbY);

            // Botões de ação
            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonA)))
                controller.SetButtonState(Xbox360Button.A, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonB)))
                controller.SetButtonState(Xbox360Button.B, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonX)))
                controller.SetButtonState(Xbox360Button.X, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonY)))
                controller.SetButtonState(Xbox360Button.Y, true);

            // Botões de ombro
            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonLB)))
                controller.SetButtonState(Xbox360Button.LeftShoulder, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonRB)))
                controller.SetButtonState(Xbox360Button.RightShoulder, true);

            // Botões especiais
            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonStart)))
                controller.SetButtonState(Xbox360Button.Start, true);

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonBack)))
                controller.SetButtonState(Xbox360Button.Back, true);

            // Gatilhos
            byte rightTrigger = 0;
            byte leftTrigger = 0;

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonRT)))
                rightTrigger = byte.MaxValue;

            if (state.IsPressed(MapKeyToKey(KeyMap.ButtonLT)))
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
                case Keys.ShiftKey: return Key.LeftShift;
                case Keys.ControlKey: return Key.LeftControl;
                case Keys.Alt: return Key.LeftAlt;
                case Keys.Up: return Key.Up;
                case Keys.Down: return Key.Down;
                case Keys.Left: return Key.Left;
                case Keys.Right: return Key.Right;
                // Adicione mais mapeamentos conforme necessário
                default: return Key.Unknown;
            }
        }

        /// <summary>
        /// Mapeamento 1:1 direto do mouse para o analógico direito.
        /// Sem acumulação, sem decay, sem suavização.
        /// Quando o mouse para de se mover, o stick volta instantaneamente ao centro.
        /// Resultado: sensação idêntica ao uso nativo do mouse.
        /// </summary>
        private void ProcessMouseMovement(int deltaX, int deltaY)
        {
            if (controller == null) return;

            // Fator de conversão: pixels de movimento → deflexão do stick
            // MouseSensitivity é configurável pelo usuário (padrão: 3.0)
            float sensitivityFactor = KeyMap.MouseSensitivity * 200.0f;

            // Mapeamento direto: delta do mouse → posição do stick
            // Sem acumulação — cada frame é independente
            float rawX = deltaX * sensitivityFactor;
            float rawY = -deltaY * sensitivityFactor; // Inverter Y para movimento natural

            // Clamp para os limites do analógico
            short stickX = (short)Math.Clamp(rawX, short.MinValue, short.MaxValue);
            short stickY = (short)Math.Clamp(rawY, short.MinValue, short.MaxValue);

            controller.SetAxisValue(Xbox360Axis.RightThumbX, stickX);
            controller.SetAxisValue(Xbox360Axis.RightThumbY, stickY);
        }

        private void ResetControllerButtons()
        {
            if (controller == null) return;

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

        private bool AnyKeyPressed()
        {
            return (GetAsyncKeyState(KeyMap.DPadUpKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.DPadDownKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.DPadLeftKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.DPadRightKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.LeftStickUpKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.LeftStickDownKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.LeftStickLeftKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.LeftStickRightKey) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonA) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonB) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonX) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonY) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonLB) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonRB) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonStart) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonBack) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonLT) & 0x8000) != 0 ||
                   (GetAsyncKeyState(KeyMap.ButtonRT) & 0x8000) != 0;
        }

        public void Dispose()
        {
            Stop();

            virtualHid?.Dispose();

            // Limpar recursos do DirectInput
            mouse?.Dispose();
            mouse = null;

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