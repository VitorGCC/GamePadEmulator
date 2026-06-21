using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace GamepadEmulator.Core
{
    /// <summary>
    /// Assistência de mira por cor: captura uma região (FOV) ao redor do centro da tela,
    /// procura pixels próximos da cor-alvo e calcula um vetor de "pull" para o analógico
    /// direito, aproximando a mira do alvo. Roda em thread própria; o loop de input lê
    /// o último vetor calculado via GetPull().
    /// </summary>
    public class AimColorEngine : IDisposable
    {
        // Configuração (atualizada via UpdateConfig a partir do perfil)
        public volatile bool Enabled = false;
        private int _fovSize = 120;
        private int _tolerance = 60;
        private int _strength = 35;
        private int _captureHz = 90;
        private byte _tR, _tG, _tB;

        // Estado de saída (vetor de pull em unidades de analógico)
        private readonly object _lock = new object();
        private int _pullX = 0;
        private int _pullY = 0;

        // Suavização de saída (EMA) para evitar movimentos bruscos
        private double _smoothPullX = 0;
        private double _smoothPullY = 0;
        private const double SMOOTH_FACTOR = 0.45; // 0 = sem resposta, 1 = sem suavização

        // Threshold mínimo de pixels para evitar pull por ruído
        private const int MIN_PIXEL_COUNT = 4;

        // Limite de pull por ciclo para evitar "snap" instantâneo
        private const int MAX_PULL = 16000;

        // Buffers reutilizáveis (evita pressão no GC a cada frame)
        private Bitmap? _reusableBitmap;
        private byte[]? _reusableBuffer;
        private int _lastFov = 0;

        private Thread? _thread;
        private volatile bool _running = false;

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public void UpdateConfig(KeyMapping km)
        {
            Enabled = km.AimColorEnabled;
            _fovSize = Math.Clamp(km.AimColorFov, 20, 600);
            _tolerance = Math.Clamp(km.AimColorTolerance, 1, 255);
            _strength = Math.Clamp(km.AimColorStrength, 0, 100);
            _captureHz = Math.Clamp(km.AimColorCaptureHz, 15, 240);
            ParseHex(km.AimColorHex);
        }

        private void ParseHex(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return;
                hex = hex.Trim().TrimStart('#');
                if (hex.Length != 6) return;
                _tR = Convert.ToByte(hex.Substring(0, 2), 16);
                _tG = Convert.ToByte(hex.Substring(2, 2), 16);
                _tB = Convert.ToByte(hex.Substring(4, 2), 16);
            }
            catch
            {
                // hex inválido — mantém a cor anterior
            }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "AimColorCapture"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            if (_thread != null && _thread.IsAlive)
                _thread.Join(500);
            _thread = null;
            lock (_lock) { _pullX = 0; _pullY = 0; }
            _smoothPullX = 0;
            _smoothPullY = 0;

            // Liberar buffers
            _reusableBitmap?.Dispose();
            _reusableBitmap = null;
            _reusableBuffer = null;
            _lastFov = 0;
        }

        /// <summary>Último vetor de pull calculado (analógico direito).</summary>
        public (int x, int y) GetPull()
        {
            lock (_lock) { return (_pullX, _pullY); }
        }

        private void CaptureLoop()
        {
            while (_running)
            {
                int hz = _captureHz;
                int sleepMs = Math.Max(1, 1000 / Math.Max(15, hz));

                if (!Enabled)
                {
                    // Decay suave para zero quando desativado
                    _smoothPullX *= 0.5;
                    _smoothPullY *= 0.5;
                    lock (_lock)
                    {
                        _pullX = (int)_smoothPullX;
                        _pullY = (int)_smoothPullY;
                    }
                    Thread.Sleep(sleepMs);
                    continue;
                }

                try
                {
                    ComputePull();
                }
                catch (Exception ex)
                {
                    Logger.Error("Falha no ciclo do AimColor", ex);
                    lock (_lock) { _pullX = 0; _pullY = 0; }
                }

                Thread.Sleep(sleepMs);
            }
        }

        /// <summary>
        /// Garante que o bitmap e buffer reutilizáveis tenham o tamanho correto.
        /// Só realoca se o FOV mudou.
        /// </summary>
        private void EnsureBuffers(int fov)
        {
            if (fov != _lastFov || _reusableBitmap == null)
            {
                _reusableBitmap?.Dispose();
                _reusableBitmap = new Bitmap(fov, fov, PixelFormat.Format32bppArgb);
                _reusableBuffer = null; // será realocado no primeiro uso
                _lastFov = fov;
            }
        }

        private void ComputePull()
        {
            int fov = _fovSize;
            int half = fov / 2;

            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);
            if (screenW <= 0 || screenH <= 0) return;

            int originX = (screenW / 2) - half;
            int originY = (screenH / 2) - half;

            // Reutilizar bitmap (evita new Bitmap() a cada frame = zero GC pressure)
            EnsureBuffers(fov);
            using (var g = Graphics.FromImage(_reusableBitmap!))
            {
                g.CopyFromScreen(originX, originY, 0, 0, new Size(fov, fov), CopyPixelOperation.SourceCopy);
            }

            BitmapData data = _reusableBitmap!.LockBits(
                new Rectangle(0, 0, fov, fov),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            // Centroide ponderado por distância ao centro (pixels mais pertos = mais peso)
            double weightedSumX = 0, weightedSumY = 0, totalWeight = 0;
            long rawCount = 0;
            int tol = _tolerance;
            float maxDist = half * 1.414f; // diagonal máxima

            try
            {
                int stride = data.Stride;
                int bytes = stride * fov;

                // Reutilizar buffer
                if (_reusableBuffer == null || _reusableBuffer.Length != bytes)
                    _reusableBuffer = new byte[bytes];

                Marshal.Copy(data.Scan0, _reusableBuffer, 0, bytes);

                for (int y = 0; y < fov; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < fov; x++)
                    {
                        int idx = row + x * 4;
                        // formato BGRA
                        byte b = _reusableBuffer[idx];
                        byte gr = _reusableBuffer[idx + 1];
                        byte r = _reusableBuffer[idx + 2];

                        if (Math.Abs(r - _tR) <= tol &&
                            Math.Abs(gr - _tG) <= tol &&
                            Math.Abs(b - _tB) <= tol)
                        {
                            // Peso inversamente proporcional à distância do centro
                            // Pixels mais perto do crosshair = mais relevantes
                            float dx = x - half;
                            float dy = y - half;
                            float dist = MathF.Sqrt(dx * dx + dy * dy);
                            double weight = 1.0 - Math.Min(dist / maxDist, 1.0); // 1.0 no centro, 0.0 na borda
                            weight = weight * weight; // curva quadrática para dar mais prioridade ao centro

                            weightedSumX += x * weight;
                            weightedSumY += y * weight;
                            totalWeight += weight;
                            rawCount++;
                        }
                    }
                }
            }
            finally
            {
                _reusableBitmap!.UnlockBits(data);
            }

            // Threshold: ignorar ruído (poucos pixels isolados não são um alvo real)
            if (rawCount < MIN_PIXEL_COUNT || totalWeight < 0.01)
            {
                // Decay suave quando perde o alvo (em vez de cortar bruscamente para zero)
                _smoothPullX *= 0.6;
                _smoothPullY *= 0.6;
                lock (_lock)
                {
                    _pullX = (int)_smoothPullX;
                    _pullY = (int)_smoothPullY;
                }
                return;
            }

            // Centroide ponderado, relativo ao centro do FOV
            double cx = weightedSumX / totalWeight;
            double cy = weightedSumY / totalWeight;
            double offsetX = cx - half;   // >0 = alvo à direita
            double offsetY = cy - half;   // >0 = alvo abaixo

            // Converte offset em pixels para deflexão do analógico, escalado pela força.
            double gain = (_strength / 100.0) * 180.0;
            double rawPullX = Math.Clamp(offsetX * gain, -MAX_PULL, MAX_PULL);
            // Tela: Y para baixo é positivo. Analógico: cima é positivo (engine inverte Y).
            // Alvo abaixo (offsetY>0) => mirar para baixo => stickY negativo.
            double rawPullY = Math.Clamp(-offsetY * gain, -MAX_PULL, MAX_PULL);

            // EMA smoothing — evita movimentos bruscos/robóticos
            _smoothPullX = _smoothPullX + (_smoothPullX == 0 ? 1.0 : SMOOTH_FACTOR) * (rawPullX - _smoothPullX);
            _smoothPullY = _smoothPullY + (_smoothPullY == 0 ? 1.0 : SMOOTH_FACTOR) * (rawPullY - _smoothPullY);

            lock (_lock)
            {
                _pullX = (int)_smoothPullX;
                _pullY = (int)_smoothPullY;
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
