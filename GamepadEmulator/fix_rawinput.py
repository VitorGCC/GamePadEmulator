import re

file_path = "Core/VirtualGamepad.cs"
with open(file_path, "r") as f:
    content = f.read()

# Fix RawInputManager namespace reference
content = re.sub(r'RawInputManager\.Initialize', 'GamepadEmulator.Core.RawInputManager.Initialize', content)
content = re.sub(r'RawInputManager\.GetAndResetDeltas', 'GamepadEmulator.Core.RawInputManager.GetAndResetDeltas', content)

with open(file_path, "w") as f:
    f.write(content)
