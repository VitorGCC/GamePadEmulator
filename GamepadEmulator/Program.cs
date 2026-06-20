namespace GamepadEmulator;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        
        // Initialize input blocker hooks on the main thread
        InputBlocker.InitializeHooks();
        
        // Configurar logs para exceções não tratadas
        Application.ThreadException += (sender, args) =>
        {
            Core.Logger.Error("Exceção não tratada na UI Thread", args.Exception);
            MessageBox.Show("Ocorreu um erro inesperado. Verifique o arquivo logs.txt.", "Erro Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Core.Logger.Error("Exceção fatal (AppDomain)", args.ExceptionObject as Exception);
        };

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Core.Logger.Error("Falha ao rodar a aplicação principal", ex);
            throw;
        }
    }
}