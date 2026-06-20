import re

file_path = "Core/VirtualGamepad.cs"
with open(file_path, "r") as f:
    content = f.read()

# 1. Remove private Mouse? mouse;
content = re.sub(r'\s*private Mouse\? mouse;', '', content)

# 2. Remove mouse init from InitializeDirectInput
content = re.sub(r'\s*// Configurar mouse\s*mouse = new Mouse\(directInput\);\s*mouse\.Properties\.BufferSize = 128;', '', content)

# 3. Remove mouse.Dispose()
content = re.sub(r'\s*mouse\?\.Dispose\(\);\s*mouse = null;', '', content)

# 4. Remove mouse.SetCooperativeLevel and Acquire in StartDirectInput
content = re.sub(r'\s*if \(mouse != null\)\s*\{\s*// Modo não-exclusivo\s*mouse\.SetCooperativeLevel\([^)]+\);\s*mouse\.Acquire\(\);\s*\}', '', content)

# 5. Initialize RawInputManager in StartDirectInput
content = re.sub(r'(directInputActive = true;)', r'\1\n                RawInputManager.Initialize(Process.GetCurrentProcess().MainWindowHandle);', content)

# 6. Remove mouse.Unacquire in StopDirectInput
content = re.sub(r'\s*if \(mouse != null\)\s*\{\s*mouse\.Unacquire\(\);\s*\}', '', content)

# 7. Update ProcessDirectInput signature
content = re.sub(r'ref MouseState\? previousMouseState,\s*ref bool firstMouseState', '', content)
content = re.sub(r'ref previousMouseState,\s*ref firstMouseState', '', content)
content = re.sub(r'ProcessDirectInput\(ref previousKeyboardState,\s*ref firstKeyboardState,\s*\)', 'ProcessDirectInput(ref previousKeyboardState, ref firstKeyboardState)', content)
content = re.sub(r'ProcessDirectInput\(ref KeyboardState\? previousKeyboardState,\s*ref bool firstKeyboardState,\s*\)', 'ProcessDirectInput(ref KeyboardState? previousKeyboardState, ref bool firstKeyboardState)', content)

# 8. Update ProcessDirectInput reading block
old_read_block = r'\s*// Ler movimento do mouse via DirectInput\s*if \(mouse != null\)\s*\{\s*mouse\.Poll\(\);\s*MouseState currentMouseState = mouse\.GetCurrentState\(\);\s*// DirectInput já retorna valores RELATIVOS \(delta\) por poll\.\s*// NÃO subtrair o estado anterior — usar diretamente\.\s*ProcessMouseMovement\(currentMouseState\.X, currentMouseState\.Y\);\s*\}'
new_read_block = r'''
                // Ler movimento do mouse via Raw Input (Zero-latency)
                var deltas = RawInputManager.GetAndResetDeltas();
                ProcessMouseMovement(deltas.deltaX, deltas.deltaY);'''
content = re.sub(old_read_block, new_read_block, content)

# 9. Remove mouse.Acquire in catch block
content = re.sub(r'\s*mouse\?\.Acquire\(\);', '', content)

# 10. Remove MouseState locals in ProcessInputs
content = re.sub(r'\s*// Estado anterior do mouse\s*MouseState\? previousMouseState = null;\s*bool firstMouseState = true;', '', content)


with open(file_path, "w") as f:
    f.write(content)
