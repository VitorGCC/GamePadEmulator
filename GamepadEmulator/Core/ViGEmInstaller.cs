using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace GamepadEmulator.Core
{
    public static class ViGEmInstaller
    {
        private const string VIGEM_DOWNLOAD_URL = "https://github.com/nefarius/ViGEmBus/releases/latest/download/ViGEmBus_Setup_1.22.0.msi";
        private const string MSI_FILENAME = "ViGEmBus_Setup.msi";

        /// <summary>
        /// Verifica se o ViGEmBus está instalado e operante. Se não estiver, pergunta ao usuário e instala.
        /// </summary>
        /// <returns>True se o sistema estiver pronto para uso (ou acabou de instalar). False se a instalação falhou ou foi recusada.</returns>
        public static async Task<bool> CheckAndInstallAsync()
        {
            if (IsInstalled())
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                "O motor interno do EmuShot requer a instalação dos Drivers Nativos de Controle (ViGEmBus) para funcionar.\n\nDeseja baixar e instalar automaticamente agora? (Recomendado)",
                "EmuShot - Instalação de Componentes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.No)
            {
                Logger.Error("Usuário recusou a instalação do ViGEmBus. Emulador não pode iniciar.");
                return false;
            }

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), MSI_FILENAME);

                // Baixar Arquivo
                Logger.Info("Iniciando download do ViGEmBus...");
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(VIGEM_DOWNLOAD_URL);
                    response.EnsureSuccessStatusCode();
                    
                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // Instalar silenciosamente com barra de progresso do Windows
                Logger.Info("Iniciando instalação silenciosa do ViGEmBus...");
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{tempPath}\" /qb",
                    UseShellExecute = true, // Necessário para invocar o prompt de UAC (Administrador) se o MSI pedir
                    Verb = "runas"
                };

                using (Process process = Process.Start(psi)!)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0 || process.ExitCode == 3010) // 0 = Sucesso, 3010 = Sucesso mas pede Reboot
                    {
                        Logger.Info($"Instalação concluída. Código: {process.ExitCode}");
                        
                        if (process.ExitCode == 3010)
                        {
                            MessageBox.Show("A instalação foi concluída, mas o Windows pode exigir que você reinicie o computador para os novos controles funcionarem.", "EmuShot - Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        
                        // Retestar após instalação
                        return IsInstalled();
                    }
                    else
                    {
                        Logger.Error($"Falha na instalação. Código de erro: {process.ExitCode}");
                        MessageBox.Show($"Ocorreu um erro durante a instalação. (Código: {process.ExitCode})\nPor favor, tente instalar manualmente ou execute como Administrador.", "Erro de Instalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Erro fatal no processo de download/instalação do ViGEmBus", ex);
                MessageBox.Show("Não foi possível concluir a instalação automática. Verifique sua conexão com a internet.\n\nDetalhes no log.", "Erro Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static bool IsInstalled()
        {
            try
            {
                // Tenta instanciar o cliente. Se lançar ViGEmBusNotFoundException, não está instalado.
                using (var client = new ViGEmClient())
                {
                    return true;
                }
            }
            catch (VigemBusNotFoundException)
            {
                return false;
            }
            catch (Exception ex)
            {
                // Outro erro estranho
                Logger.Error("Erro desconhecido ao verificar ViGEmBus", ex);
                return false;
            }
        }
    }
}
