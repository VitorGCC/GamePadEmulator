using System.Xml.Serialization;

namespace GamepadEmulator;

public class KeyMapping
{
    // Teclas de D-pad (separadas do analógico)
    public Keys DPadUpKey { get; set; } = Keys.Up;
    public Keys DPadDownKey { get; set; } = Keys.Down;
    public Keys DPadLeftKey { get; set; } = Keys.Left;
    public Keys DPadRightKey { get; set; } = Keys.Right;

    // Teclas para o analógico esquerdo
    public Keys LeftStickUpKey { get; set; } = Keys.W;
    public Keys LeftStickDownKey { get; set; } = Keys.S;
    public Keys LeftStickLeftKey { get; set; } = Keys.A;
    public Keys LeftStickRightKey { get; set; } = Keys.D;

    // Botões de face
    public Keys ButtonA { get; set; } = Keys.Space;
    public Keys ButtonB { get; set; } = Keys.ControlKey;
    public Keys ButtonX { get; set; } = Keys.E;
    public Keys ButtonY { get; set; } = Keys.Q;

    // Botões de ombro
    public Keys ButtonLB { get; set; } = Keys.R;
    public Keys ButtonRB { get; set; } = Keys.F;
    public Keys ButtonLT { get; set; } = Keys.RButton; // Botão direito do mouse
    public Keys ButtonRT { get; set; } = Keys.LButton; // Botão esquerdo do mouse

    // Botões especiais
    public Keys ButtonStart { get; set; } = Keys.Enter;
    public Keys ButtonBack { get; set; } = Keys.Tab;

    // Configurações de sensibilidade
    public float Sensitivity { get; set; } = 1.0f;      // Sensibilidade dos analógicos
    public float MouseSensitivity { get; set; } = 3.0f; // Sensibilidade do mouse
    public float DeadZone { get; set; } = 0.0f;         // Zona morta definida como 0 por padrão

    // Nome do perfil
    public string ProfileName { get; set; } = "Default";

    // Salvar perfil
    public bool SaveProfile(string fileName)
    {
        try
        {
            var dirName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(KeyMapping));
            using FileStream stream = new FileStream(fileName, FileMode.Create);
            serializer.Serialize(stream, this);
            return true;
        }
        catch (Exception ex)
        {
            Core.Logger.Error($"Falha ao salvar o perfil em '{fileName}'", ex);
            MessageBox.Show($"Erro ao salvar perfil: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    // Carregar perfil
    public static KeyMapping LoadProfile(string fileName)
    {
        try
        {
            if (!File.Exists(fileName))
                return new KeyMapping();

            XmlSerializer serializer = new XmlSerializer(typeof(KeyMapping));
            using FileStream stream = new FileStream(fileName, FileMode.Open);
            if (serializer.Deserialize(stream) is KeyMapping mapping)
            {
                return mapping;
            }
            return new KeyMapping();
        }
        catch (Exception ex)
        {
            Core.Logger.Error($"Falha ao carregar o perfil de '{fileName}'. Usando perfil padrão.", ex);
            return new KeyMapping();
        }
    }
}