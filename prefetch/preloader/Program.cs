using System.Text;
using System.Windows.Forms;

namespace OmsiLaunch.Modules.Prefetch;

// Entry point. OmsiLaunch starts this program before OMSI with the command
// line "--omsilaunch-session <session id>" and writes the session context as
// the first line on its standard input (see the OmsiLaunch Runtime Modules
// reference, "Session programs").
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--omsilaunch-session" || !Guid.TryParse(args[1], out var sessionId))
        {
            MessageBox.Show(Strings.Product + ": this program is started by OmsiLaunch for a session; it is not meant to be run by itself.", Strings.Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 2;
        }
        var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        SessionContext context;
        try
        {
            var first = input.ReadLine();
            if (first is null || first.Length > 1 << 20) return 2;
            context = SessionContext.Parse(first);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException) { return 2; }
        if (!Guid.TryParse(context.SessionId, out var contextSession) || contextSession != sessionId) return 2;

        Log.Debug = PreloadSettings.From(context.Settings).DebugLog;
        Log.Open(Path.Combine(context.DataDirectory, "logs"));
        var controller = new Controller(Console.OpenStandardOutput());
        controller.Listen(input);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var ui = new Ui();
        var session = new Session(context, controller, ui);
        var worker = new Thread(() =>
        {
            try { session.Prepare(); }
            catch (Exception exception)
            {
                // OMSI still starts: the module degrades to whatever was held.
                Log.Write("prepare failed: " + exception);
                controller.Error("prefetch: the preparation failed: " + exception.Message);
            }
            controller.Ready();
            ui.Prepared();
            try { session.RunSession(); }
            catch (Exception exception) { Log.Write("session failed: " + exception); }
            try { session.Shutdown(); }
            catch (Exception exception) { Log.Write("shutdown failed: " + exception); }
            ui.Exit();
        }) { Name = "session" };
        worker.Start();
        Application.Run(ui.Context);
        worker.Join(TimeSpan.FromSeconds(15));
        Log.Write("exit");
        Log.Close();
        return 0;
    }
}
