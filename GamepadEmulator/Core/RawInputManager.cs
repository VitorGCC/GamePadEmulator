using System;
using System.Windows.Forms;
using SharpDX.Multimedia;
using SharpDX.RawInput;

namespace GamepadEmulator.Core
{
    public static class RawInputManager
    {
        private static bool isInitialized = false;
        public static int AccumulatedDeltaX { get; private set; } = 0;
        public static int AccumulatedDeltaY { get; private set; } = 0;
        
        private static readonly object _lock = new object();

        public static void Initialize(IntPtr windowHandle)
        {
            if (isInitialized) return;

            try
            {
                // Registra o mouse para enviar eventos Raw Input globais
                // DeviceFlags.InputSink exige que passemos o Handle da Janela que vai receber em background
                Device.RegisterDevice(UsagePage.Generic, UsageId.GenericMouse, DeviceFlags.InputSink, windowHandle);
                
                Device.MouseInput += Device_MouseInput;
                isInitialized = true;
                Logger.Info("RawInput API inicializada com sucesso para o Mouse.");
            }
            catch (Exception ex)
            {
                Logger.Error("Falha ao inicializar RawInput", ex);
            }
        }

        private static void Device_MouseInput(object? sender, MouseInputEventArgs e)
        {
            // Acumular deltas (movimento físico) e ignorar posições absolutas
            if (e.Mode == MouseMode.MoveRelative)
            {
                lock (_lock)
                {
                    AccumulatedDeltaX += e.X;
                    AccumulatedDeltaY += e.Y;
                }
            }
        }

        public static (int deltaX, int deltaY) GetAndResetDeltas()
        {
            lock (_lock)
            {
                int x = AccumulatedDeltaX;
                int y = AccumulatedDeltaY;
                AccumulatedDeltaX = 0;
                AccumulatedDeltaY = 0;
                return (x, y);
            }
        }
        
        public static void Dispose()
        {
            if (isInitialized)
            {
                Device.MouseInput -= Device_MouseInput;
            }
        }
    }
}
