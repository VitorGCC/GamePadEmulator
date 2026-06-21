using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Xml.Serialization;

using MaterialSkin;
using MaterialSkin.Controls;

namespace GamepadEmulator;

public partial class ConfigForm : MaterialForm
{
    private readonly MaterialSkinManager materialSkinManager;
    public KeyMapping KeyMap { get; private set; }
    private Button? currentMappingButton = null;
    // Evita que a inicialização programática dos sliders sobrescreva valores carregados
    private bool _initializing = false;

    // Controles da interface
    private TextBox txtProfileName = new();
    private TrackBar trkSensitivity = new();
    private TrackBar trkDeadZone = new();
    private TrackBar trkMouseSensitivity = new(); // Novo controle deslizante para sensibilidade do mouse
    private Label lblSensitivity = new();
    private Label lblDeadZone = new();
    private Label lblMouseSensitivity = new(); // Nova label para sensibilidade do mouse

    // Controles do D-pad
    private Button btnMapDPadUp = new();
    private Button btnMapDPadDown = new();
    private Button btnMapDPadLeft = new();
    private Button btnMapDPadRight = new();

    // Controles do analógico esquerdo
    private Button btnMapLeftStickUp = new();
    private Button btnMapLeftStickDown = new();
    private Button btnMapLeftStickLeft = new();
    private Button btnMapLeftStickRight = new();

    // Botões de face
    private Button btnMapA = new();
    private Button btnMapB = new();
    private Button btnMapX = new();
    private Button btnMapY = new();

    // Botões de ombro
    private Button btnMapLB = new();
    private Button btnMapRB = new();
    private Button btnMapLT = new();
    private Button btnMapRT = new();

    // Botões especiais
    private Button btnMapStart = new();
    private Button btnMapBack = new();

    // AimColor
    private CheckBox chkAimColorEnabled = new();
    private TextBox txtAimColorHex = new();
    private TrackBar trkAimColorFov = new();
    private TrackBar trkAimColorTolerance = new();
    private TrackBar trkAimColorStrength = new();
    private TrackBar trkAimColorCaptureHz = new();
    private ComboBox cboAimColorMode = new();
    private Label lblAimFov = new();
    private Label lblAimTol = new();
    private Label lblAimStr = new();
    private Label lblAimHz = new();
    private Panel pnlColorPreview = new();

    // Recoil
    private CheckBox chkRecoilEnabled = new();
    private TrackBar trkRecoilStrength = new();
    private Label lblRecoilStr = new();

    // Botões de ação
    private Button btnSave = new();
    private Button btnCancel = new();

    public ConfigForm(KeyMapping keyMap)
    {
        // Inicializar componentes
        InitializeComponent();

        // Inicializar o Material Skin Manager
        materialSkinManager = MaterialSkinManager.Instance;
        materialSkinManager.AddFormToManage(this);
        materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;

        // Clonar o mapeamento atual para não modificar o original até confirmar
        KeyMap = CloneKeyMapping(keyMap);

        // Preencher os campos com valores atuais (sem disparar write-back dos handlers)
        _initializing = true;
        txtProfileName.Text = KeyMap.ProfileName;
        trkSensitivity.Value = ClampToTrack(trkSensitivity, (int)(KeyMap.Sensitivity * 10));
        trkDeadZone.Value = ClampToTrack(trkDeadZone, (int)(KeyMap.DeadZone * 100));
        trkMouseSensitivity.Value = ClampToTrack(trkMouseSensitivity, (int)(KeyMap.SensitivityX * 10));

        // AimColor
        chkAimColorEnabled.Checked = KeyMap.AimColorEnabled;
        txtAimColorHex.Text = KeyMap.AimColorHex;
        trkAimColorFov.Value = ClampToTrack(trkAimColorFov, KeyMap.AimColorFov);
        trkAimColorTolerance.Value = ClampToTrack(trkAimColorTolerance, KeyMap.AimColorTolerance);
        trkAimColorStrength.Value = ClampToTrack(trkAimColorStrength, KeyMap.AimColorStrength);
        trkAimColorCaptureHz.Value = ClampToTrack(trkAimColorCaptureHz, KeyMap.AimColorCaptureHz);
        UpdateColorPreview();
        SelectAimColorMode(KeyMap.AimColorActivationMode);

        // Recoil
        chkRecoilEnabled.Checked = KeyMap.RecoilEnabled;
        trkRecoilStrength.Value = ClampToTrack(trkRecoilStrength, KeyMap.RecoilStrength);

        SetAimColorControlsEnabled(KeyMap.AimColorEnabled);
        _initializing = false;

        UpdateButtonLabels();
    }

    // Garante que um valor caiba no intervalo do TrackBar (evita ArgumentException)
    private static int ClampToTrack(TrackBar track, int value)
        => Math.Clamp(value, track.Minimum, track.Maximum);

    // Clone profundo via serialização — copia TODOS os campos automaticamente,
    // inclusive parâmetros da engine adicionados no futuro.
    private KeyMapping CloneKeyMapping(KeyMapping source)
    {
        var serializer = new XmlSerializer(typeof(KeyMapping));
        using var ms = new MemoryStream();
        serializer.Serialize(ms, source);
        ms.Position = 0;
        return (KeyMapping)serializer.Deserialize(ms)!;
    }

    private void UpdateButtonLabels()
    {
        // Atualizar os textos dos botões com as teclas configuradas

        // D-pad
        btnMapDPadUp.Text = $"Cima: {KeyMap.DPadUpKey}";
        btnMapDPadDown.Text = $"Baixo: {KeyMap.DPadDownKey}";
        btnMapDPadLeft.Text = $"Esquerda: {KeyMap.DPadLeftKey}";
        btnMapDPadRight.Text = $"Direita: {KeyMap.DPadRightKey}";

        // Analógico esquerdo
        btnMapLeftStickUp.Text = $"Cima: {KeyMap.LeftStickUpKey}";
        btnMapLeftStickDown.Text = $"Baixo: {KeyMap.LeftStickDownKey}";
        btnMapLeftStickLeft.Text = $"Esquerda: {KeyMap.LeftStickLeftKey}";
        btnMapLeftStickRight.Text = $"Direita: {KeyMap.LeftStickRightKey}";

        // Botões de face
        btnMapA.Text = $"A: {KeyMap.ButtonA}";
        btnMapB.Text = $"B: {KeyMap.ButtonB}";
        btnMapX.Text = $"X: {KeyMap.ButtonX}";
        btnMapY.Text = $"Y: {KeyMap.ButtonY}";

        // Botões de ombro
        btnMapLB.Text = $"LB: {KeyMap.ButtonLB}";
        btnMapRB.Text = $"RB: {KeyMap.ButtonRB}";
        btnMapLT.Text = $"LT: {KeyMap.ButtonLT}";
        btnMapRT.Text = $"RT: {KeyMap.ButtonRT}";

        // Botões especiais
        btnMapStart.Text = $"Start: {KeyMap.ButtonStart}";
        btnMapBack.Text = $"Back: {KeyMap.ButtonBack}";

        // Sensibilidade e zona morta
        lblSensitivity.Text = $"Sensibilidade Analógico: {KeyMap.Sensitivity:F1}";
        lblDeadZone.Text = $"Zona Morta: {KeyMap.DeadZone:P0}";
        lblMouseSensitivity.Text = $"Sensibilidade Mouse: {KeyMap.SensitivityX:F1}";
    }

    private void BtnMapKey_Click(object? sender, EventArgs e)
    {
        // Cancelar mapeamento anterior se existir
        if (currentMappingButton != null)
        {
            currentMappingButton.BackColor = SystemColors.Control;
        }

        Button? btn = sender as Button;
        if (btn == null) return;

        currentMappingButton = btn;
        currentMappingButton.BackColor = Color.Yellow;
        currentMappingButton.Text = "Pressione uma tecla...";

        // Focar o botão para capturar a tecla
        currentMappingButton.Focus();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Capturar tecla pressionada quando estiver em modo de mapeamento
        if (currentMappingButton != null)
        {
            // Ignorar algumas teclas específicas
            if (keyData == Keys.Escape)
            {
                // Cancelar mapeamento
                currentMappingButton.BackColor = SystemColors.Control;
                UpdateButtonLabels();
                currentMappingButton = null;
                return true;
            }

            // Definir a tecla no mapeamento
            AssignKeyToButton(currentMappingButton, keyData);

            // Resetar estado
            currentMappingButton.BackColor = SystemColors.Control;
            currentMappingButton = null;

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void AssignKeyToButton(Button button, Keys key)
    {
        // Atribuir a tecla ao botão correto no mapeamento

        // D-pad
        if (button == btnMapDPadUp)
            KeyMap.DPadUpKey = key;
        else if (button == btnMapDPadDown)
            KeyMap.DPadDownKey = key;
        else if (button == btnMapDPadLeft)
            KeyMap.DPadLeftKey = key;
        else if (button == btnMapDPadRight)
            KeyMap.DPadRightKey = key;

        // Analógico esquerdo
        else if (button == btnMapLeftStickUp)
            KeyMap.LeftStickUpKey = key;
        else if (button == btnMapLeftStickDown)
            KeyMap.LeftStickDownKey = key;
        else if (button == btnMapLeftStickLeft)
            KeyMap.LeftStickLeftKey = key;
        else if (button == btnMapLeftStickRight)
            KeyMap.LeftStickRightKey = key;

        // Botões de face
        else if (button == btnMapA)
            KeyMap.ButtonA = key;
        else if (button == btnMapB)
            KeyMap.ButtonB = key;
        else if (button == btnMapX)
            KeyMap.ButtonX = key;
        else if (button == btnMapY)
            KeyMap.ButtonY = key;

        // Botões de ombro
        else if (button == btnMapLB)
            KeyMap.ButtonLB = key;
        else if (button == btnMapRB)
            KeyMap.ButtonRB = key;
        else if (button == btnMapLT)
            KeyMap.ButtonLT = key;
        else if (button == btnMapRT)
            KeyMap.ButtonRT = key;

        // Botões especiais
        else if (button == btnMapStart)
            KeyMap.ButtonStart = key;
        else if (button == btnMapBack)
            KeyMap.ButtonBack = key;

        UpdateButtonLabels();
    }

    private void TrkSensitivity_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.Sensitivity = trkSensitivity.Value / 10.0f;
        lblSensitivity.Text = $"Sensibilidade Analógico: {KeyMap.Sensitivity:F1}";
    }

    private void TrkDeadZone_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.DeadZone = trkDeadZone.Value / 100.0f;
        lblDeadZone.Text = $"Zona Morta: {KeyMap.DeadZone:P0}";
    }

    private void TrkMouseSensitivity_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        float value = trkMouseSensitivity.Value / 10.0f;
        KeyMap.SensitivityX = value;
        KeyMap.SensitivityY = value;
        KeyMap.MouseSensitivity = value;
        lblMouseSensitivity.Text = $"Sensibilidade Mouse: {value:F1}";
    }

    // ── AimColor Event Handlers ──────────────────────────────────────

    private void ChkAimColorEnabled_CheckedChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorEnabled = chkAimColorEnabled.Checked;
        SetAimColorControlsEnabled(chkAimColorEnabled.Checked);
    }

    private void SetAimColorControlsEnabled(bool enabled)
    {
        txtAimColorHex.Enabled = enabled;
        trkAimColorFov.Enabled = enabled;
        trkAimColorTolerance.Enabled = enabled;
        trkAimColorStrength.Enabled = enabled;
        trkAimColorCaptureHz.Enabled = enabled;
        cboAimColorMode.Enabled = enabled;
    }

    private void TxtAimColorHex_TextChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorHex = txtAimColorHex.Text.Trim();
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        try
        {
            string hex = (KeyMap.AimColorHex ?? "#ff00d0").Trim().TrimStart('#');
            if (hex.Length == 6)
                pnlColorPreview.BackColor = ColorTranslator.FromHtml("#" + hex);
        }
        catch { pnlColorPreview.BackColor = Color.Magenta; }
    }

    private void SelectAimColorMode(string mode)
    {
        string m = (mode ?? "fire").ToLowerInvariant();
        for (int i = 0; i < cboAimColorMode.Items.Count; i++)
        {
            if (cboAimColorMode.Items[i]?.ToString()?.ToLowerInvariant() == m)
            { cboAimColorMode.SelectedIndex = i; return; }
        }
        cboAimColorMode.SelectedIndex = 0;
    }

    private void TrkAimColorFov_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorFov = trkAimColorFov.Value;
        lblAimFov.Text = $"FOV: {trkAimColorFov.Value}px";
    }

    private void TrkAimColorTolerance_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorTolerance = trkAimColorTolerance.Value;
        lblAimTol.Text = $"Tolerância: {trkAimColorTolerance.Value}";
    }

    private void TrkAimColorStrength_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorStrength = trkAimColorStrength.Value;
        lblAimStr.Text = $"Força: {trkAimColorStrength.Value}%";
    }

    private void TrkAimColorCaptureHz_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorCaptureHz = trkAimColorCaptureHz.Value;
        lblAimHz.Text = $"Captura: {trkAimColorCaptureHz.Value}Hz";
    }

    private void CboAimColorMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.AimColorActivationMode = cboAimColorMode.SelectedItem?.ToString()?.ToLowerInvariant() ?? "fire";
    }

    // ── Recoil Event Handlers ────────────────────────────────────────

    private void ChkRecoilEnabled_CheckedChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.RecoilEnabled = chkRecoilEnabled.Checked;
        trkRecoilStrength.Enabled = chkRecoilEnabled.Checked;
    }

    private void TrkRecoilStrength_ValueChanged(object? sender, EventArgs e)
    {
        if (_initializing) return;
        KeyMap.RecoilStrength = trkRecoilStrength.Value;
        lblRecoilStr.Text = $"Força Recoil: {trkRecoilStrength.Value}";
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // Salvar nome do perfil
        KeyMap.ProfileName = txtProfileName.Text.Trim();
        if (string.IsNullOrEmpty(KeyMap.ProfileName))
        {
            MessageBox.Show("O nome do perfil não pode estar vazio.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void InitializeComponent()
    {
        // Configuração da janela
        Text = "EmuShot — Configurar Controles";
        ClientSize = new Size(650, 920);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        // Nome do perfil
        Label lblProfileName = new Label
        {
            Text = "Nome do Perfil:",
            Location = new Point(20, 20),
            Size = new Size(100, 20)
        };

        txtProfileName = new TextBox
        {
            Location = new Point(130, 20),
            Size = new Size(200, 20)
        };

        // Configurações de sensibilidade
        lblSensitivity = new Label
        {
            Text = "Sensibilidade Analógico: 1.0",
            Location = new Point(20, 60),
            Size = new Size(170, 20)
        };

        trkSensitivity = new TrackBar
        {
            Location = new Point(200, 60),
            Size = new Size(200, 45),
            Minimum = 1,
            Maximum = 30,
            Value = 10,
            TickFrequency = 5
        };
        trkSensitivity.ValueChanged += TrkSensitivity_ValueChanged;

        // Configurações de zona morta
        lblDeadZone = new Label
        {
            Text = "Zona Morta: 10%",
            Location = new Point(20, 100),
            Size = new Size(170, 20)
        };

        trkDeadZone = new TrackBar
        {
            Location = new Point(200, 100),
            Size = new Size(200, 45),
            Minimum = 0,
            Maximum = 50,
            Value = 10,
            TickFrequency = 5
        };
        trkDeadZone.ValueChanged += TrkDeadZone_ValueChanged;

        // Novo: Configurações de sensibilidade do mouse
        lblMouseSensitivity = new Label
        {
            Text = "Sensibilidade Mouse: 3.0",
            Location = new Point(20, 140),
            Size = new Size(170, 20),
            ForeColor = Color.DarkRed // Destacar como novo recurso
        };

        trkMouseSensitivity = new TrackBar
        {
            Location = new Point(200, 140),
            Size = new Size(200, 45),
            Minimum = 5,     // Mínimo 0.5
            Maximum = 100,   // Máximo 10.0
            Value = 30,      // Padrão 3.0
            TickFrequency = 10
        };
        trkMouseSensitivity.ValueChanged += TrkMouseSensitivity_ValueChanged;

        // Grupo de D-pad (separado)
        GroupBox grpDPad = new GroupBox
        {
            Text = "D-Pad",
            Location = new Point(20, 190),
            Size = new Size(250, 140)
        };

        btnMapDPadUp = new Button
        {
            Text = "Cima: Up",
            Location = new Point(85, 20),
            Size = new Size(100, 25),
            Parent = grpDPad
        };
        btnMapDPadUp.Click += BtnMapKey_Click;

        btnMapDPadDown = new Button
        {
            Text = "Baixo: Down",
            Location = new Point(85, 85),
            Size = new Size(100, 25),
            Parent = grpDPad
        };
        btnMapDPadDown.Click += BtnMapKey_Click;

        btnMapDPadLeft = new Button
        {
            Text = "Esquerda: Left",
            Location = new Point(20, 50),
            Size = new Size(100, 25),
            Parent = grpDPad
        };
        btnMapDPadLeft.Click += BtnMapKey_Click;

        btnMapDPadRight = new Button
        {
            Text = "Direita: Right",
            Location = new Point(140, 50),
            Size = new Size(100, 25),
            Parent = grpDPad
        };
        btnMapDPadRight.Click += BtnMapKey_Click;

        // Grupo de analógico esquerdo (separado)
        GroupBox grpLeftStick = new GroupBox
        {
            Text = "Analógico Esquerdo",
            Location = new Point(300, 190),
            Size = new Size(250, 140)
        };

        btnMapLeftStickUp = new Button
        {
            Text = "Cima: W",
            Location = new Point(85, 20),
            Size = new Size(100, 25),
            Parent = grpLeftStick
        };
        btnMapLeftStickUp.Click += BtnMapKey_Click;

        btnMapLeftStickDown = new Button
        {
            Text = "Baixo: S",
            Location = new Point(85, 85),
            Size = new Size(100, 25),
            Parent = grpLeftStick
        };
        btnMapLeftStickDown.Click += BtnMapKey_Click;

        btnMapLeftStickLeft = new Button
        {
            Text = "Esquerda: A",
            Location = new Point(20, 50),
            Size = new Size(100, 25),
            Parent = grpLeftStick
        };
        btnMapLeftStickLeft.Click += BtnMapKey_Click;

        btnMapLeftStickRight = new Button
        {
            Text = "Direita: D",
            Location = new Point(140, 50),
            Size = new Size(100, 25),
            Parent = grpLeftStick
        };
        btnMapLeftStickRight.Click += BtnMapKey_Click;

        // Grupo de botões de face
        GroupBox grpFace = new GroupBox
        {
            Text = "Botões de Face",
            Location = new Point(20, 340),
            Size = new Size(250, 140)
        };

        btnMapY = new Button
        {
            Text = "Y: Q",
            Location = new Point(85, 20),
            Size = new Size(80, 25),
            Parent = grpFace
        };
        btnMapY.Click += BtnMapKey_Click;

        btnMapA = new Button
        {
            Text = "A: Space",
            Location = new Point(85, 85),
            Size = new Size(80, 25),
            Parent = grpFace
        };
        btnMapA.Click += BtnMapKey_Click;

        btnMapX = new Button
        {
            Text = "X: E",
            Location = new Point(20, 50),
            Size = new Size(80, 25),
            Parent = grpFace
        };
        btnMapX.Click += BtnMapKey_Click;

        btnMapB = new Button
        {
            Text = "B: Ctrl",
            Location = new Point(150, 50),
            Size = new Size(80, 25),
            Parent = grpFace
        };
        btnMapB.Click += BtnMapKey_Click;

        // Grupo de botões de ombro
        GroupBox grpShoulder = new GroupBox
        {
            Text = "Botões de Ombro e Gatilhos",
            Location = new Point(300, 340),
            Size = new Size(250, 140)
        };

        btnMapLB = new Button
        {
            Text = "LB: R",
            Location = new Point(20, 30),
            Size = new Size(80, 25),
            Parent = grpShoulder
        };
        btnMapLB.Click += BtnMapKey_Click;

        btnMapRB = new Button
        {
            Text = "RB: F",
            Location = new Point(150, 30),
            Size = new Size(80, 25),
            Parent = grpShoulder
        };
        btnMapRB.Click += BtnMapKey_Click;

        btnMapLT = new Button
        {
            Text = "LT: RMouse",
            Location = new Point(20, 70),
            Size = new Size(80, 25),
            Parent = grpShoulder
        };
        btnMapLT.Click += BtnMapKey_Click;

        btnMapRT = new Button
        {
            Text = "RT: LMouse",
            Location = new Point(150, 70),
            Size = new Size(80, 25),
            Parent = grpShoulder
        };
        btnMapRT.Click += BtnMapKey_Click;

        // Grupo de botões especiais
        GroupBox grpSpecial = new GroupBox
        {
            Text = "Botões Especiais",
            Location = new Point(150, 490),
            Size = new Size(280, 80)
        };

        btnMapStart = new Button
        {
            Text = "Start: Enter",
            Location = new Point(20, 30),
            Size = new Size(100, 25),
            Parent = grpSpecial
        };
        btnMapStart.Click += BtnMapKey_Click;

        btnMapBack = new Button
        {
            Text = "Back: Tab",
            Location = new Point(160, 30),
            Size = new Size(100, 25),
            Parent = grpSpecial
        };
        btnMapBack.Click += BtnMapKey_Click;

        // ── Grupo AimColor ──────────────────────────────────────────
        GroupBox grpAimColor = new GroupBox
        {
            Text = "🎨 Assistência de Mira por Cor",
            Location = new Point(20, 580),
            Size = new Size(610, 180),
            ForeColor = Color.MediumOrchid
        };

        chkAimColorEnabled = new CheckBox
        {
            Text = "Ativar AimColor",
            Location = new Point(15, 22),
            Size = new Size(140, 20),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        chkAimColorEnabled.CheckedChanged += ChkAimColorEnabled_CheckedChanged;

        // Cor alvo + preview
        Label lblAimColor = new Label
        {
            Text = "Cor:",
            Location = new Point(170, 23),
            Size = new Size(30, 18),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        txtAimColorHex = new TextBox
        {
            Location = new Point(205, 20),
            Size = new Size(80, 22),
            Parent = grpAimColor,
            Text = "#ff00d0"
        };
        txtAimColorHex.TextChanged += TxtAimColorHex_TextChanged;

        pnlColorPreview = new Panel
        {
            Location = new Point(290, 20),
            Size = new Size(22, 22),
            Parent = grpAimColor,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.Magenta
        };

        // Modo de ativação
        Label lblMode = new Label
        {
            Text = "Modo:",
            Location = new Point(330, 23),
            Size = new Size(40, 18),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        cboAimColorMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(375, 20),
            Size = new Size(100, 24),
            Parent = grpAimColor
        };
        cboAimColorMode.Items.AddRange(new object[] { "fire", "ads", "always" });
        cboAimColorMode.SelectedIndexChanged += CboAimColorMode_SelectedIndexChanged;

        // FOV
        lblAimFov = new Label
        {
            Text = "FOV: 120px",
            Location = new Point(15, 55),
            Size = new Size(90, 18),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        trkAimColorFov = new TrackBar
        {
            Location = new Point(110, 50),
            Size = new Size(180, 30),
            Minimum = 20, Maximum = 400, Value = 120, TickFrequency = 40,
            Parent = grpAimColor
        };
        trkAimColorFov.ValueChanged += TrkAimColorFov_ValueChanged;

        // Tolerância
        lblAimTol = new Label
        {
            Text = "Tolerância: 60",
            Location = new Point(310, 55),
            Size = new Size(100, 18),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        trkAimColorTolerance = new TrackBar
        {
            Location = new Point(415, 50),
            Size = new Size(180, 30),
            Minimum = 1, Maximum = 255, Value = 60, TickFrequency = 25,
            Parent = grpAimColor
        };
        trkAimColorTolerance.ValueChanged += TrkAimColorTolerance_ValueChanged;

        // Força
        lblAimStr = new Label
        {
            Text = "Força: 35%",
            Location = new Point(15, 95),
            Size = new Size(90, 18),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        trkAimColorStrength = new TrackBar
        {
            Location = new Point(110, 90),
            Size = new Size(180, 30),
            Minimum = 0, Maximum = 100, Value = 35, TickFrequency = 10,
            Parent = grpAimColor
        };
        trkAimColorStrength.ValueChanged += TrkAimColorStrength_ValueChanged;

        // Captura Hz
        lblAimHz = new Label
        {
            Text = "Captura: 90Hz",
            Location = new Point(310, 95),
            Size = new Size(100, 18),
            Parent = grpAimColor,
            ForeColor = Color.White
        };
        trkAimColorCaptureHz = new TrackBar
        {
            Location = new Point(415, 90),
            Size = new Size(180, 30),
            Minimum = 15, Maximum = 240, Value = 90, TickFrequency = 30,
            Parent = grpAimColor
        };
        trkAimColorCaptureHz.ValueChanged += TrkAimColorCaptureHz_ValueChanged;

        // ── Grupo Recoil ────────────────────────────────────────────
        GroupBox grpRecoil = new GroupBox
        {
            Text = "🔫 Compensação de Recoil",
            Location = new Point(20, 770),
            Size = new Size(610, 70),
            ForeColor = Color.OrangeRed
        };

        chkRecoilEnabled = new CheckBox
        {
            Text = "Ativar Anti-Recoil",
            Location = new Point(15, 28),
            Size = new Size(140, 20),
            Parent = grpRecoil,
            ForeColor = Color.White
        };
        chkRecoilEnabled.CheckedChanged += ChkRecoilEnabled_CheckedChanged;

        lblRecoilStr = new Label
        {
            Text = "Força Recoil: 5",
            Location = new Point(180, 30),
            Size = new Size(110, 18),
            Parent = grpRecoil,
            ForeColor = Color.White
        };
        trkRecoilStrength = new TrackBar
        {
            Location = new Point(300, 25),
            Size = new Size(280, 30),
            Minimum = 0, Maximum = 20, Value = 5, TickFrequency = 2,
            Parent = grpRecoil
        };
        trkRecoilStrength.ValueChanged += TrkRecoilStrength_ValueChanged;

        // ── Botões de ação ──────────────────────────────────────────
        btnSave = new Button
        {
            Text = "Salvar",
            Location = new Point(450, 855),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };
        btnSave.Click += BtnSave_Click;

        btnCancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(550, 855),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel
        };
        btnCancel.Click += BtnCancel_Click;

        // Adicionar controles à janela
        Controls.Add(lblProfileName);
        Controls.Add(txtProfileName);
        Controls.Add(lblSensitivity);
        Controls.Add(trkSensitivity);
        Controls.Add(lblDeadZone);
        Controls.Add(trkDeadZone);
        Controls.Add(lblMouseSensitivity);
        Controls.Add(trkMouseSensitivity);
        Controls.Add(grpDPad);
        Controls.Add(grpLeftStick);
        Controls.Add(grpFace);
        Controls.Add(grpShoulder);
        Controls.Add(grpSpecial);
        Controls.Add(grpAimColor);
        Controls.Add(grpRecoil);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }
}