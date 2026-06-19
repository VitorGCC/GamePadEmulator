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
        
        Application.Run(new MainForm());
    }
}