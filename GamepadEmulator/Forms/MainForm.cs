using GamepadEmulator;

using MaterialSkin;
using MaterialSkin.Controls;

public partial class MainForm : MaterialForm
{
    private readonly MaterialSkinManager materialSkinManager;
    private VirtualGamepad? gamepad;
    private bool emulatorRunning = false;
    private bool emulatorActive = true;
    private string currentProfilePath = string.Empty;

    // Controles da interface
    private MaterialButton btnStart = new();
    private MaterialButton btnConfig = new();
    private MaterialLabel lblStatus = new();
    private MaterialLabel lblProfile = new();
    private MaterialComboBox cboProfiles = new();
    private MaterialCard pnlInfo = new();
    private MaterialLabel lblInfo = new();
    private NotifyIcon trayIcon = new();

    public MainForm()
    {
        InitializeComponent();

        // Inicializar o Material Skin Manager
        materialSkinManager = MaterialSkinManager.Instance;
        materialSkinManager.AddFormToManage(this);
        materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;
        materialSkinManager.ColorScheme = new ColorScheme(
            Primary.Green600, Primary.Green700,
            Primary.Green200, Accent.LightBlue200,
            TextShade.WHITE
        );

        InitializeGamepad();
        LoadProfiles();

        // Configurar ícone da bandeja
        trayIcon.Icon = SystemIcons.Application;
        trayIcon.Text = "GameController Emulator - Ativo";
        trayIcon.Visible = true;
        trayIcon.BalloonTipTitle = "GameController Emulator";

        // Adicionar menu de contexto ao ícone da bandeja
        ContextMenuStrip trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Ativar/Desativar (F12)", null, TrayToggle_Click);
        trayMenu.Items.Add("Configurações", null, (s, e) => BtnConfig_Click(s, e));
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("Sair", null, (s, e) => Close());
        trayIcon.ContextMenuStrip = trayMenu;
    } // O construtor MainForm() termina aqui

    // O método Gamepad_StateChanged deve ficar FORA do construtor
    private void Gamepad_StateChanged(object sender, VirtualGamepad.EmulatorStateChangedEventArgs e)
    {
        // Atualizar estado em thread-safe
        this.BeginInvoke(new Action(() => {
            emulatorActive = e.IsActive;

            // Atualizar texto e ícone
            if (emulatorActive)
            {
                lblStatus.Text = "Emulador ATIVO";
                lblStatus.ForeColor = Color.Green;
                trayIcon.Icon = SystemIcons.Application;
                trayIcon.Text = "GameController Emulator - Ativo";
                ShowNotification("Emulador Ativado", "Teclado e mouse estão funcionando como controle");
            }
            else
            {
                lblStatus.Text = "Emulador INATIVO";
                lblStatus.ForeColor = Color.Red;
                trayIcon.Icon = SystemIcons.Error;
                trayIcon.Text = "GameController Emulator - Inativo";
                ShowNotification("Emulador Desativado", "Teclado e mouse estão funcionando normalmente");
            }
        }));
    }
    private void InitializeGamepad()
        {
            try
            {
                gamepad = new VirtualGamepad();

                // Adicionar handler para evento de mudança de estado
                gamepad.StateChanged += Gamepad_StateChanged;

                // Tentar carregar o perfil padrão
                string profilesFolder = Path.Combine(AppContext.BaseDirectory, "Profiles");
                if (!Directory.Exists(profilesFolder))
                {
                    Directory.CreateDirectory(profilesFolder);
                }

                currentProfilePath = Path.Combine(profilesFolder, "Default.profile");
                if (File.Exists(currentProfilePath))
                {
                    gamepad.KeyMap = KeyMapping.LoadProfile(currentProfilePath);
                }
                else
                {
                    // Criar perfil padrão se não existir
                    gamepad.KeyMap.SaveProfile(currentProfilePath);
                }

                // Atualizar status
                lblStatus.Text = "Pronto para iniciar";
                lblStatus.ForeColor = Color.Blue;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao inicializar: {ex.Message}\n\nCertifique-se de que o driver ViGEmBus está instalado.",
                    "Erro de Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Erro: Driver não encontrado";
                lblStatus.ForeColor = Color.Red;
                btnStart.Enabled = false;
            }
        }

        

        private void LoadProfiles()
        {
            try
            {
                string profilesFolder = Path.Combine(AppContext.BaseDirectory, "Profiles");
                if (!Directory.Exists(profilesFolder))
                    return;

                cboProfiles.Items.Clear();
                foreach (string file in Directory.GetFiles(profilesFolder, "*.profile"))
                {
                    cboProfiles.Items.Add(Path.GetFileNameWithoutExtension(file));
                }

                if (cboProfiles.Items.Count > 0)
                {
                    cboProfiles.SelectedIndex = 0;
                }
            }
            catch
            {
                // Silenciar erros não críticos de carregamento de perfil
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (emulatorRunning)
            {
                StopEmulator();
            }
            else
            {
                StartEmulator();
            }
        }

        private void StartEmulator()
        {
            if (gamepad == null || btnStart.Enabled == false)
                return;

            if (gamepad.Start())
            {
                emulatorRunning = true;
                emulatorActive = true;
                btnStart.Text = "Parar Emulador";
                lblStatus.Text = "Emulador ATIVO";
                lblStatus.ForeColor = Color.Green;

                // Desabilitar algumas opções enquanto o emulador estiver rodando
                btnConfig.Enabled = false;
                cboProfiles.Enabled = false;

                // Notificar o usuário
                ShowNotification("Emulador iniciado", "Pressione F12 para ativar/desativar");
            }
            else
            {
                MessageBox.Show("Não foi possível iniciar o emulador. Verifique se o driver está instalado corretamente.",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopEmulator()
        {
            if (gamepad == null)
                return;

            gamepad.Stop();
            emulatorRunning = false;
            emulatorActive = false;
            btnStart.Text = "Iniciar Emulador";
            lblStatus.Text = "Emulador parado";
            lblStatus.ForeColor = Color.Blue;

            // Reabilitar opções
            btnConfig.Enabled = true;
            cboProfiles.Enabled = true;

            // Notificar o usuário
            ShowNotification("Emulador desativado", "O emulador foi parado com sucesso");
        }

        private void TrayToggle_Click(object? sender, EventArgs e)
        {
            if (gamepad != null && emulatorRunning)
            {
                // Alternar o estado do emulador
                gamepad.ToggleActiveState();
            }
        }

        private void ShowNotification(string title, string message)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = message;
            trayIcon.ShowBalloonTip(2000);
        }

        private void BtnConfig_Click(object? sender, EventArgs e)
        {
            if (gamepad == null)
                return;

            using ConfigForm configForm = new(gamepad.KeyMap);
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                // Atualizar mapeamento
                gamepad.KeyMap = configForm.KeyMap;

                // Salvar perfil
                string profilesFolder = Path.Combine(AppContext.BaseDirectory, "Profiles");
                string profilePath = Path.Combine(profilesFolder, $"{gamepad.KeyMap.ProfileName}.profile");

                if (gamepad.KeyMap.SaveProfile(profilePath))
                {
                    LoadProfiles();

                    // Selecionar o perfil na lista
                    for (int i = 0; i < cboProfiles.Items.Count; i++)
                    {
                        if (cboProfiles.Items[i].ToString() == gamepad.KeyMap.ProfileName)
                        {
                            cboProfiles.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        private void CboProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboProfiles.SelectedIndex < 0 || gamepad == null)
                return;

            string profileName = cboProfiles.SelectedItem.ToString() ?? "Default";
            string profilePath = Path.Combine(AppContext.BaseDirectory, "Profiles", $"{profileName}.profile");

            if (File.Exists(profilePath))
            {
                gamepad.KeyMap = KeyMapping.LoadProfile(profilePath);
                currentProfilePath = profilePath;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Remover o ícone da bandeja
            trayIcon.Visible = false;
            trayIcon.Dispose();

            if (emulatorRunning)
            {
                StopEmulator();
            }

            gamepad?.Dispose();

            // Liberar hooks de teclado e mouse
            InputBlocker.ReleaseHooks();

            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            // Configuração do formulário
            Text = "GamePad Emulator UI";
            ClientSize = new Size(450, 440);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Icon = SystemIcons.Application;
            StartPosition = FormStartPosition.CenterScreen;

            // Painel de informações
            pnlInfo = new MaterialCard
            {
                Location = new Point(20, 80),
                Size = new Size(410, 80)
            };

            lblInfo = new MaterialLabel
            {
                Text = "Use seu mouse e teclado como um controle Xbox 360.\nConfigure as teclas e crie perfis.",
                Location = new Point(10, 20),
                Size = new Size(390, 40),
                Parent = pnlInfo,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Status
            lblStatus = new MaterialLabel
            {
                Text = "Pronto",
                Location = new Point(20, 180),
                Size = new Size(410, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Linha de ajuda F12
            MaterialLabel lblHelp = new MaterialLabel
            {
                Text = "Pressione F12 para ativar/desativar",
                Location = new Point(20, 210),
                Size = new Size(410, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Seletor de perfil
            lblProfile = new MaterialLabel
            {
                Text = "Perfil:",
                Location = new Point(20, 245),
                Size = new Size(60, 20)
            };

            cboProfiles = new MaterialComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(85, 240),
                Size = new Size(190, 36)
            };
            cboProfiles.SelectedIndexChanged += CboProfiles_SelectedIndexChanged;

            // Botão de iniciar/parar
            btnStart = new MaterialButton
            {
                Text = "Iniciar Emulador",
                Location = new Point(75, 300),
                Size = new Size(300, 40),
                Type = MaterialButton.MaterialButtonType.Contained,
                UseAccentColor = false
            };
            btnStart.Click += BtnStart_Click;

            // Botão de configuração
            btnConfig = new MaterialButton
            {
                Text = "Configurar Controles",
                Location = new Point(75, 360),
                Size = new Size(300, 40),
                Type = MaterialButton.MaterialButtonType.Outlined
            };
            btnConfig.Click += BtnConfig_Click;

            // Adicionar controles ao formulário
            Controls.Add(pnlInfo);
            Controls.Add(lblStatus);
            Controls.Add(lblHelp);
            Controls.Add(lblProfile);
            Controls.Add(cboProfiles);
            Controls.Add(btnStart);
            Controls.Add(btnConfig);
        }
    }
