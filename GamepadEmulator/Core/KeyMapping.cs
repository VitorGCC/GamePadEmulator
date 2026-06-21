using System.Xml.Serialization;

namespace GamepadEmulator;

public class KeyMapping
{
    // D-pad
    public Keys DPadUpKey { get; set; } = Keys.Up;
    public Keys DPadDownKey { get; set; } = Keys.Down;
    public Keys DPadLeftKey { get; set; } = Keys.Left;
    public Keys DPadRightKey { get; set; } = Keys.Right;

    // Analógico esquerdo
    public Keys LeftStickUpKey { get; set; } = Keys.W;
    public Keys LeftStickDownKey { get; set; } = Keys.S;
    public Keys LeftStickLeftKey { get; set; } = Keys.A;
    public Keys LeftStickRightKey { get; set; } = Keys.D;

    // Botões de face
    public Keys ButtonA { get; set; } = Keys.Space;
    public Keys ButtonB { get; set; } = Keys.ControlKey;
    public Keys ButtonX { get; set; } = Keys.E;
    public Keys ButtonY { get; set; } = Keys.Q;

    // Botões de ombro e gatilhos
    public Keys ButtonLB { get; set; } = Keys.R;
    public Keys ButtonRB { get; set; } = Keys.F;
    public Keys ButtonLT { get; set; } = Keys.RButton;
    public Keys ButtonRT { get; set; } = Keys.LButton;

    // Botões especiais
    public Keys ButtonStart { get; set; } = Keys.Enter;
    public Keys ButtonBack { get; set; } = Keys.Tab;

    // Sensibilidade legada (usada pela ConfigForm enquanto UI não for atualizada)
    public float Sensitivity { get; set; } = 1.0f;
    public float MouseSensitivity { get; set; } = 3.0f;
    public float DeadZone { get; set; } = 0.0f;
    public int PollingRate { get; set; } = 1000;

    // Parâmetros completos do engine de tradução do mouse
    public float SensitivityX { get; set; } = 3.0f;
    public float SensitivityY { get; set; } = 3.0f;
    public float PowerCurve { get; set; } = 0.65f;
    public int AntiDeadzone { get; set; } = 6500;
    public float SmoothingFactor { get; set; } = 0.5f;
    public float YAxisRatio { get; set; } = 1.0f;

    // Macro de recoil
    public bool RecoilEnabled { get; set; } = false;
    public int RecoilStrength { get; set; } = 5;

    // AimColor — assistência de mira por detecção de cor na tela
    public bool AimColorEnabled { get; set; } = false;
    public int AimColorFov { get; set; } = 120;            // lado da região de captura, em pixels
    public string AimColorHex { get; set; } = "#ff00d0";   // cor-alvo (contorno do inimigo)
    public int AimColorTolerance { get; set; } = 60;       // tolerância de cor (distância por canal)
    public int AimColorStrength { get; set; } = 35;        // força do pull (0-100)
    public int AimColorCaptureHz { get; set; } = 90;       // taxa de captura da tela
    public string AimColorActivationMode { get; set; } = "fire"; // "always" | "ads" | "fire"

    // Processos de jogo adicionais (complementa a lista padrão)
    public List<string> CustomGameProcesses { get; set; } = new List<string>();

    // Nome do perfil
    public string ProfileName { get; set; } = "Default";

    public bool SaveProfile(string fileName)
    {
        try
        {
            var dirName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(dirName) && !Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

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

    public static KeyMapping LoadProfile(string fileName)
    {
        try
        {
            if (!File.Exists(fileName))
                return new KeyMapping();

            XmlSerializer serializer = new XmlSerializer(typeof(KeyMapping));
            KeyMapping mapping;
            using (FileStream stream = new FileStream(fileName, FileMode.Open))
            {
                mapping = serializer.Deserialize(stream) as KeyMapping ?? new KeyMapping();
            }

            // Migração de perfis legados: versões antigas salvavam apenas MouseSensitivity.
            // Se o XML não tem SensitivityX, herdamos o valor legado para não resetar a mira.
            string raw = File.ReadAllText(fileName);
            if (!raw.Contains("<SensitivityX>"))
            {
                mapping.SensitivityX = mapping.MouseSensitivity;
                mapping.SensitivityY = mapping.MouseSensitivity;
            }

            // Hardening: mantém valores numéricos em intervalos sãos
            mapping.RecoilStrength = Math.Clamp(mapping.RecoilStrength, 0, 20);
            mapping.AimColorStrength = Math.Clamp(mapping.AimColorStrength, 0, 100);
            mapping.AimColorTolerance = Math.Clamp(mapping.AimColorTolerance, 1, 255);
            mapping.AimColorFov = Math.Clamp(mapping.AimColorFov, 20, 600);
            mapping.AimColorCaptureHz = Math.Clamp(mapping.AimColorCaptureHz, 15, 240);

            return mapping;
        }
        catch (Exception ex)
        {
            Core.Logger.Error($"Falha ao carregar o perfil de '{fileName}'. Usando perfil padrão.", ex);
            return new KeyMapping();
        }
    }
}
