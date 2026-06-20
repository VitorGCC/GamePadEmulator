import re

file_path = "Core/VirtualGamepad.cs"
with open(file_path, "r") as f:
    content = f.read()

# Replace state.IsPressed(MapKeyToKey(X)) with IsKeyPressed(state, X)
content = re.sub(r'state\.IsPressed\(MapKeyToKey\(([^)]+)\)\)', r'IsKeyPressed(state, \1)', content)

# Add the IsKeyPressed method right after MapKeyToKey method
map_key_method_end = """                // Adicione mais mapeamentos conforme necessário
                default: return Key.Unknown;
            }
        }"""

helper_method = """                // Adicione mais mapeamentos conforme necessário
                default: return Key.Unknown;
            }
        }

        private bool IsKeyPressed(KeyboardState state, Keys winKey)
        {
            Key dKey = MapKeyToKey(winKey);
            if (dKey == Key.Unknown) return false;
            return state.IsPressed(dKey);
        }"""

content = content.replace(map_key_method_end, helper_method)

with open(file_path, "w") as f:
    f.write(content)
