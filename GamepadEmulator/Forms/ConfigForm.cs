using System;
using System.Windows.Forms;
using System.Drawing;

using MaterialSkin;
using MaterialSkin.Controls;

namespace GamepadEmulator;

public partial class ConfigForm : MaterialForm
{
    private readonly MaterialSkinManager materialSkinManager;
    public KeyMapping KeyMap { get; private set; }
    private Button? currentMappingButton = null;

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

        // Preencher os campos com valores atuais
        txtProfileName.Text = KeyMap.ProfileName;
        trkSensitivity.Value = (int)(KeyMap.Sensitivity * 10);
        trkDeadZone.Value = (int)(KeyMap.DeadZone * 100);
        trkMouseSensitivity.Value = (int)(KeyMap.MouseSensitivity * 10); // Novo controle

        UpdateButtonLabels();
    }

    private KeyMapping CloneKeyMapping(KeyMapping source)
    {
        // Criar um novo mapeamento com os mesmos valores
        KeyMapping clone = new KeyMapping
        {
            ProfileName = source.ProfileName,

            // D-pad (separado)
            DPadUpKey = source.DPadUpKey,
            DPadDownKey = source.DPadDownKey,
            DPadLeftKey = source.DPadLeftKey,
            DPadRightKey = source.DPadRightKey,

            // Analógico esquerdo (separado)
            LeftStickUpKey = source.LeftStickUpKey,
            LeftStickDownKey = source.LeftStickDownKey,
            LeftStickLeftKey = source.LeftStickLeftKey,
            LeftStickRightKey = source.LeftStickRightKey,

            // Botões de face
            ButtonA = source.ButtonA,
            ButtonB = source.ButtonB,
            ButtonX = source.ButtonX,
            ButtonY = source.ButtonY,

            // Botões de ombro
            ButtonLB = source.ButtonLB,
            ButtonRB = source.ButtonRB,
            ButtonLT = source.ButtonLT,
            ButtonRT = source.ButtonRT,

            // Botões especiais
            ButtonStart = source.ButtonStart,
            ButtonBack = source.ButtonBack,

            // Configurações
            Sensitivity = source.Sensitivity,
            DeadZone = source.DeadZone,
            MouseSensitivity = source.MouseSensitivity  // Nova propriedade
        };

        return clone;
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
        lblMouseSensitivity.Text = $"Sensibilidade Mouse: {KeyMap.MouseSensitivity:F1}"; // Nova label
    }

    private void BtnMapKey_Click(object sender, EventArgs e)
    {
        // Cancelar mapeamento anterior se existir
        if (currentMappingButton != null)
        {
            currentMappingButton.BackColor = SystemColors.Control;
        }

        // Iniciar novo mapeamento
        currentMappingButton = (Button)sender;
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

    private void TrkSensitivity_ValueChanged(object sender, EventArgs e)
    {
        KeyMap.Sensitivity = trkSensitivity.Value / 10.0f;
        lblSensitivity.Text = $"Sensibilidade Analógico: {KeyMap.Sensitivity:F1}";
    }

    private void TrkDeadZone_ValueChanged(object sender, EventArgs e)
    {
        KeyMap.DeadZone = trkDeadZone.Value / 100.0f;
        lblDeadZone.Text = $"Zona Morta: {KeyMap.DeadZone:P0}";
    }

    private void TrkMouseSensitivity_ValueChanged(object sender, EventArgs e)
    {
        KeyMap.MouseSensitivity = trkMouseSensitivity.Value / 10.0f;
        lblMouseSensitivity.Text = $"Sensibilidade Mouse: {KeyMap.MouseSensitivity:F1}";
    }

    private void BtnSave_Click(object sender, EventArgs e)
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

    private void BtnCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void InitializeComponent()
    {
        // Configuração da janela
        Text = "Configurar Controles";
        ClientSize = new Size(650, 650); // Aumentado para acomodar novos controles
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

        // Botões de ação
        btnSave = new Button
        {
            Text = "Salvar",
            Location = new Point(450, 590),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };
        btnSave.Click += BtnSave_Click;

        btnCancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(550, 590),
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
        Controls.Add(lblMouseSensitivity); // Novo
        Controls.Add(trkMouseSensitivity); // Novo
        Controls.Add(grpDPad);
        Controls.Add(grpLeftStick);
        Controls.Add(grpFace);
        Controls.Add(grpShoulder);
        Controls.Add(grpSpecial);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }
}