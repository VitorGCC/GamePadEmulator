using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GamepadEmulator.Core
{
    public class InterceptionBlocker
    {
        private IntPtr context = IntPtr.Zero;
        private Thread? workerThread;
        private bool isRunning;
        private HashSet<Keys> blockedKeys = new HashSet<Keys>();

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);
        private const uint MAPVK_VSC_TO_VK = 1;

        public bool Initialize()
        {
            try
            {
                context = Interception.interception_create_context();
                return context != IntPtr.Zero;
            }
            catch (DllNotFoundException)
            {
                Logger.Error("interception.dll não encontrada. O emulador usará Fallback (Low Level Hooks).");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao inicializar Interception", ex);
                return false;
            }
        }

        public void Start(HashSet<Keys> keysToBlock)
        {
            if (isRunning || context == IntPtr.Zero) return;

            blockedKeys = new HashSet<Keys>(keysToBlock);
            isRunning = true;

            // Define o filtro para todos os eventos de teclado
            Interception.interception_set_filter(context, Interception.interception_is_keyboard, (ushort)Interception.FilterKeyState.All);

            workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            workerThread.Start();
            
            Logger.Info("Interception (Kernel Blocker) ATIVADO com sucesso.");
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;

            // Envia um stroke falso para desengasgar o interception_wait (ou apenas destrói o contexto)
            if (context != IntPtr.Zero)
            {
                Interception.interception_destroy_context(context);
                context = IntPtr.Zero;
            }

            Logger.Info("Interception (Kernel Blocker) DESATIVADO.");
        }

        private void WorkerLoop()
        {
            Interception.Stroke stroke = new Interception.Stroke();
            int device;

            try
            {
                // interception_wait bloqueia a thread até receber input
                while (isRunning && context != IntPtr.Zero && (device = Interception.interception_wait(context)) != 0)
                {
                    if (Interception.interception_receive(context, device, ref stroke, 1) > 0)
                    {
                        bool block = false;

                        // Se é um evento de teclado (device > 0 e <= 10 geralmente)
                        if (Interception.interception_is_keyboard(device) != 0)
                        {
                            // Mapeia o Scan Code do hardware para Virtual Key (Windows)
                            Keys vKey = (Keys)MapVirtualKey(stroke.Key.Code, MAPVK_VSC_TO_VK);

                            if (vKey != Keys.None && blockedKeys.Contains(vKey))
                            {
                                // A tecla está na lista de bloqueio (ex: WASD do jogo sendo simulado como Controle)
                                block = true;
                            }
                        }

                        // Se NÃO for para bloquear, encaminha o input pro Windows
                        if (!block)
                        {
                            Interception.interception_send(context, device, ref stroke, 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Logger.Error("Erro na thread do Interception", ex);
                }
            }
        }
    }
}
