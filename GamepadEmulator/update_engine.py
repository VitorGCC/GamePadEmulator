import re

file_path = "Core/VirtualGamepad.cs"
with open(file_path, "r") as f:
    content = f.read()

# 1. Add Engine instance
field_new = """        // Engine de Tradução Avançada
        private MouseTranslationEngine mouseEngine = new MouseTranslationEngine();"""
content = re.sub(r'// Rastreamento de movimento de mouse\s*private Vector2 rawMouseDelta = Vector2\.Zero;\s*private Vector2 smoothedMouseDelta = Vector2\.Zero;\s*private Vector2 mouseOverflowBuffer = Vector2\.Zero;', field_new, content)

# 2. Update ProcessMouseMovement definition
process_old = r'''        /// <summary>\s*/// Mapeamento avançado do mouse para o analógico direito com DELTA BUFFER\.\s*/// Resolve o limite de "Max Turn Speed" de flicks rápidos acumulando excesso de movimento\.\s*/// </summary>\s*private void ProcessMouseMovement\(int deltaX, int deltaY\)\s*\{[\s\S]*?controller\.SetAxisValue\(Xbox360Axis\.RightThumbY, stickY\);\s*\}'''

process_new = '''        /// <summary>
        /// Mapeamento avançado do mouse para o analógico direito com DELTA BUFFER.
        /// Resolve o limite de "Max Turn Speed" de flicks rápidos acumulando excesso de movimento.
        /// </summary>
        private void ProcessMouseMovement(int deltaX, int deltaY)
        {
            if (controller == null) return;
            
            // Passa os deltas da USB crua para a Engine de Tradução
            var (stickX, stickY) = mouseEngine.Translate(deltaX, deltaY);

            // Envia para o controle virtual
            controller.SetAxisValue(Xbox360Axis.RightThumbX, stickX);
            controller.SetAxisValue(Xbox360Axis.RightThumbY, stickY);
        }'''
content = re.sub(process_old, process_new, content)

# 3. Apply Sensitivity config (Link with KeyMap)
# The config is set in KeyMap.MouseSensitivity. I should link it to mouseEngine.
# Let's add it to the start of ProcessMouseMovement so it updates if changed.
process_new_2 = '''        /// <summary>
        /// Mapeamento avançado do mouse para o analógico direito usando MouseTranslationEngine.
        /// </summary>
        private void ProcessMouseMovement(int deltaX, int deltaY)
        {
            if (controller == null) return;
            
            mouseEngine.Sensitivity = KeyMap.MouseSensitivity;

            // Passa os deltas da USB crua para a Engine de Tradução
            var (stickX, stickY) = mouseEngine.Translate(deltaX, deltaY);

            // Envia para o controle virtual
            controller.SetAxisValue(Xbox360Axis.RightThumbX, stickX);
            controller.SetAxisValue(Xbox360Axis.RightThumbY, stickY);
        }'''
content = content.replace(process_new, process_new_2)

with open(file_path, "w") as f:
    f.write(content)
