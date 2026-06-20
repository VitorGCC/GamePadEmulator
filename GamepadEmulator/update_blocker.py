import re

file_path = "InputBlocker.cs"
with open(file_path, "r") as f:
    content = f.read()

# Add fields
new_fields = """        private static Core.InterceptionBlocker? _interceptionBlocker;
        private static bool _useInterception = false;

        public delegate IntPtr"""
content = re.sub(r'public delegate IntPtr', new_fields, content)

# Update InitializeHooks
init_old = r'''        public static void InitializeHooks\(Keys escapeKey = Keys\.F12\)\s*\{\s*if \(_keyboardHookID == IntPtr\.Zero && _mouseHookID == IntPtr\.Zero\)\s*\{\s*_escapeKey = escapeKey;\s*_keyboardProc = KeyboardHookCallback;\s*_mouseProc = MouseHookCallback;\s*using \(Process curProcess = Process\.GetCurrentProcess\(\)\)\s*using \(ProcessModule curModule = curProcess\.MainModule!\)\s*\{\s*_keyboardHookID = SetWindowsHookEx\(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle\(curModule\.ModuleName\), 0\);\s*_mouseHookID = SetWindowsHookEx\(WH_MOUSE_LL, _mouseProc, GetModuleHandle\(curModule\.ModuleName\), 0\);\s*\}\s*\}\s*\}'''

init_new = '''        public static void InitializeHooks(Keys escapeKey = Keys.F12)
        {
            _escapeKey = escapeKey;
            
            _interceptionBlocker = new Core.InterceptionBlocker();
            _useInterception = _interceptionBlocker.Initialize();
            
            if (!_useInterception)
            {
                if (_keyboardHookID == IntPtr.Zero && _mouseHookID == IntPtr.Zero)
                {
                    _keyboardProc = KeyboardHookCallback;
                    _mouseProc = MouseHookCallback;

                    using (Process curProcess = Process.GetCurrentProcess())
                    using (ProcessModule curModule = curProcess.MainModule!)
                    {
                        _keyboardHookID = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(curModule.ModuleName), 0);
                        _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
                    }
                }
            }
        }'''
content = re.sub(init_old, init_new, content)

# Update SetBlockingState
set_old = r'''        public static void SetBlockingState\(bool enable, HashSet<Keys>\? keysToBlock = null, bool blockMouseMovement = false, bool blockMouseButtons = false\)\s*\{\s*if \(keysToBlock != null\)\s*\{\s*_keysToBlock = keysToBlock;\s*\}\s*_blockMouseMovement = blockMouseMovement;\s*_blockMouseButtons = blockMouseButtons;\s*_blockingEnabled = enable;\s*\}'''

set_new = '''        public static void SetBlockingState(bool enable, HashSet<Keys>? keysToBlock = null, bool blockMouseMovement = false, bool blockMouseButtons = false)
        {
            if (keysToBlock != null)
                _keysToBlock = keysToBlock;
            _blockMouseMovement = blockMouseMovement;
            _blockMouseButtons = blockMouseButtons;
            _blockingEnabled = enable;
            
            if (_useInterception && _interceptionBlocker != null)
            {
                if (enable)
                    _interceptionBlocker.Start(_keysToBlock);
                else
                    _interceptionBlocker.Stop();
            }
        }'''
content = re.sub(set_old, set_new, content)

# Update StopBlocking
stop_old = r'''        public static void StopBlocking\(\)\s*\{\s*_blockingEnabled = false;\s*\}'''
stop_new = '''        public static void StopBlocking()
        {
            _blockingEnabled = false;
            if (_useInterception && _interceptionBlocker != null)
                _interceptionBlocker.Stop();
        }'''
content = re.sub(stop_old, stop_new, content)

# Update ReleaseHooks
rel_old = r'''        public static void ReleaseHooks\(\)\s*\{\s*_blockingEnabled = false;'''
rel_new = '''        public static void ReleaseHooks()
        {
            _blockingEnabled = false;
            if (_useInterception && _interceptionBlocker != null)
                _interceptionBlocker.Stop();'''
content = re.sub(rel_old, rel_new, content)

with open(file_path, "w") as f:
    f.write(content)
