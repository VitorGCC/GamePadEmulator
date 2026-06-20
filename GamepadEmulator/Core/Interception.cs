using System;
using System.Runtime.InteropServices;

namespace GamepadEmulator.Core
{
    // Define os tipos e estruturas do Interception
    public static class Interception
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int Predicate(int device);

        [Flags]
        public enum KeyState : ushort
        {
            Down = 0x00,
            Up = 0x01,
            E0 = 0x02,
            E1 = 0x04,
            TermSrvSetLED = 0x08,
            TermSrvShadow = 0x10,
            TermSrvVKPacket = 0x20
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyStroke
        {
            public ushort Code;
            public KeyState State;
            public uint Information;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseStroke
        {
            public ushort State;
            public ushort Flags;
            public short Rolling;
            public int X;
            public int Y;
            public uint Information;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Stroke
        {
            [FieldOffset(0)] public KeyStroke Key;
            [FieldOffset(0)] public MouseStroke Mouse;
        }

        public enum FilterKeyState : ushort
        {
            None = 0x0000,
            All = 0xFFFF,
            Down = KeyState.Up,
            Up = KeyState.Up << 1,
            E0 = KeyState.E0 << 1,
            E1 = KeyState.E1 << 1,
            TermSrvSetLED = KeyState.TermSrvSetLED << 1,
            TermSrvShadow = KeyState.TermSrvShadow << 1,
            TermSrvVKPacket = KeyState.TermSrvVKPacket << 1
        }

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr interception_create_context();

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void interception_destroy_context(IntPtr context);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_get_precedence(IntPtr context, int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void interception_set_precedence(IntPtr context, int device, int precedence);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void interception_set_filter(IntPtr context, Predicate predicate, ushort filter);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_receive(IntPtr context, int device, ref Stroke stroke, uint nstroke);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_wait(IntPtr context);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_send(IntPtr context, int device, ref Stroke stroke, uint nstroke);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_is_keyboard(int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_is_mouse(int device);

        [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int interception_is_invalid(int device);
    }
}
