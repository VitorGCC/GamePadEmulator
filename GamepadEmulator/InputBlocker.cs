using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace GamepadEmulator
{
    public class InputBlocker
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;

        private static LowLevelKeyboardProc? _keyboardProc;
        private static LowLevelMouseProc? _mouseProc;
        private static IntPtr _keyboardHookID = IntPtr.Zero;
        private static IntPtr _mouseHookID = IntPtr.Zero;

        private static bool _blockingEnabled = false;
        private static HashSet<Keys> _keysToBlock = new HashSet<Keys>();
        private static bool _blockMouseMovement = false;
        private static bool _blockMouseButtons = false;

        // Tecla de escape (não será bloqueada)
        private static Keys _escapeKey = Keys.F12;

        // Propriedade para obter/definir a tecla de escape
        public static Keys EscapeKey
        {
            get { return _escapeKey; }
            set { _escapeKey = value; }
        }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public static void StartBlocking(HashSet<Keys> keysToBlock, bool blockMouseMovement, bool blockMouseButtons, Keys escapeKey = Keys.F12)
        {
            if (_keyboardHookID == IntPtr.Zero && _mouseHookID == IntPtr.Zero)
            {
                _keysToBlock = keysToBlock;
                _blockMouseMovement = blockMouseMovement;
                _blockMouseButtons = blockMouseButtons;
                _escapeKey = escapeKey;
                _blockingEnabled = true;

                _keyboardProc = KeyboardHookCallback;
                _mouseProc = MouseHookCallback;

                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule!)
                {
                    _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
                    _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
            else
            {
                // Se os hooks já existem, apenas ativar o bloqueio
                _blockingEnabled = true;
            }
        }

        public static void StopBlocking()
        {
            _blockingEnabled = false;
        }

        public static void ReleaseHooks()
        {
            _blockingEnabled = false;

            if (_keyboardHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookID);
                _keyboardHookID = IntPtr.Zero;
            }

            if (_mouseHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookID);
                _mouseHookID = IntPtr.Zero;
            }
        }

        private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _blockingEnabled)
            {
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN ||
                    wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;

                    // Não bloquear a tecla de escape
                    if (key == _escapeKey)
                    {
                        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
                    }

                    if (_keysToBlock.Contains(key))
                    {
                        // Bloquear a tecla
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _blockingEnabled)
            {
                if (_blockMouseMovement && wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    // Bloquear movimento do mouse
                    return (IntPtr)1;
                }

                if (_blockMouseButtons &&
                    (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_LBUTTONUP ||
                     wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONUP))
                {
                    // Bloquear botões do mouse
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }
    }
}