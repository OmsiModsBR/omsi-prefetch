using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace OmsiLaunch.Modules.Prefetch;

// The preloader's windows run on the main thread; the session runs on its own
// thread and asks for them through Post. Before OMSI starts the OmsiLaunch
// tray is not shown yet, so the first-open notice is this program's window.
internal sealed class Ui
{
    private readonly Control marshal = new();
    private FirstOpenWindow? notice;
    private MonitorWindow? monitor;

    public Ui()
    {
        marshal.CreateControl();
        _ = marshal.Handle;
    }

    public ApplicationContext Context { get; } = new();

    private void Post(Action action)
    {
        try { if (marshal.IsHandleCreated) marshal.BeginInvoke(action); }
        catch (InvalidOperationException) { }
    }

    public void ShowFirstOpen(string map, Func<long> held) => Post(() => { notice = new FirstOpenWindow(map, held); notice.Show(); });

    public void Prepared() => Post(() => notice?.Done());

    public void ShowMonitor(Session session) => Post(() =>
    {
        if (monitor is null || monitor.IsDisposed) monitor = new MonitorWindow(session);
        monitor.Show();
    });

    public void Exit() => Post(() =>
    {
        notice?.Close();
        monitor?.Close();
        Context.ExitThread();
    });

    public static string Size(long bytes) => bytes >= 1L << 30
        ? (bytes / (double)(1L << 30)).ToString("0.00", CultureInfo.CurrentCulture) + " GB"
        : (bytes / (double)(1L << 20)).ToString("0", CultureInfo.CurrentCulture) + " MB";
}

// "Preloading is being set up": shown on the first open of a map, until OMSI
// is started. It never takes the focus.
internal sealed class FirstOpenWindow : Form
{
    private readonly Label progress = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProgressBar bar = new() { Dock = DockStyle.Bottom, Height = 10, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    private readonly Func<long> held;

    public FirstOpenWindow(string map, Func<long> held)
    {
        this.held = held;
        Text = Strings.Product;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; MinimizeBox = true; TopMost = true; ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        Font = SystemFonts.MessageBoxFont ?? DefaultFont;
        ClientSize = new Size(460, 190);
        Padding = new Padding(14, 12, 14, 12);
        var title = new Label { Text = Strings.FirstOpenTitle, Dock = DockStyle.Top, Height = 26, Font = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold) };
        var text = new Label { Text = Strings.FirstOpenText(map), Dock = DockStyle.Fill };
        Controls.Add(text);
        Controls.Add(bar);
        Controls.Add(progress);
        Controls.Add(title);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        timer.Tick += (_, _) => progress.Text = Strings.Preparing(Ui.Size(this.held()));
        timer.Start();
        progress.Text = Strings.Preparing(Ui.Size(0));
    }

    protected override bool ShowWithoutActivation => true;

    public void Done()
    {
        timer.Stop();
        bar.Style = ProgressBarStyle.Continuous; bar.Value = 100;
        progress.Text = Strings.Ready;
        var close = new System.Windows.Forms.Timer { Interval = 4000 };
        close.Tick += (_, _) => { close.Dispose(); Close(); };
        close.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

// The monitor (setting "monitor", flag /preload-monitor): what is held, what
// the game uses, what it used that memory did not hold. Hidden by default.
internal sealed class MonitorWindow : Form
{
    private readonly Session session;
    private readonly Label[] lines = Enumerable.Range(0, 5).Select(_ => new Label { AutoSize = false, Height = 18, Dock = DockStyle.Top }).ToArray();
    private readonly ProgressBar bar = new() { Dock = DockStyle.Top, Height = 16, Maximum = 100 };
    private readonly ComboBox filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly CheckBox pinned = new() { Text = Strings.Pinned, AutoSize = true };
    private readonly ListView list = new() { View = View.Details, VirtualMode = true, FullRowSelect = true, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    private List<Holding> shown = new();

    public MonitorWindow(Session session)
    {
        this.session = session;
        Text = Strings.Product;
        Font = SystemFonts.MessageBoxFont ?? DefaultFont;
        ClientSize = new Size(1100, 680);
        StartPosition = FormStartPosition.WindowsDefaultLocation;
        filter.Items.AddRange(Strings.Filters);
        filter.SelectedIndex = 0;
        filter.SelectedIndexChanged += (_, _) => Refresh(null, EventArgs.Empty);
        pinned.CheckedChanged += (_, _) => TopMost = pinned.Checked;
        var widths = new[] { 170, 70, 80, 80, 680 };
        foreach (var (column, width) in Strings.Columns.Zip(widths)) list.Columns.Add(column, width);
        list.RetrieveVirtualItem += Item;
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight };
        tools.Controls.Add(filter);
        tools.Controls.Add(pinned);
        Controls.Add(list);
        Controls.Add(tools);
        foreach (var line in lines.Reverse()) Controls.Add(line);
        Controls.Add(bar);
        Padding = new Padding(10);
        timer.Tick += Refresh;
        timer.Start();
        Refresh(null, EventArgs.Empty);
    }

    protected override bool ShowWithoutActivation => true;

    private void Refresh(object? sender, EventArgs e)
    {
        var holdings = session.Holdings;
        var summary = holdings.Summarize();
        var percent = summary.Budget > 0 ? (int)Math.Min(100, summary.Held * 100 / summary.Budget) : 0;
        var memory = Native.Memory();
        var phase = Strings.Phase(session.Phase);
        Text = Strings.MonitorTitle(Ui.Size(summary.Held), Ui.Size(summary.Budget), percent);
        bar.Value = percent;
        lines[0].Text = Strings.Summary1(Ui.Size(summary.Held), Ui.Size(summary.Budget), percent, phase);
        lines[1].Text = Strings.Summary2(summary.UsedHeld, Ui.Size(summary.UsedHeldBytes), summary.UnusedHeld, Ui.Size(summary.UnusedHeldBytes), summary.Outside, Ui.Size(summary.OutsideBytes));
        lines[2].Text = Strings.Summary3(Ui.Size((long)memory.AvailPhys), Ui.Size((long)memory.TotalPhys), memory.MemoryLoad);
        lines[3].Text = Strings.Summary4(Ui.Size(summary.Tiers[0]), Ui.Size(summary.Tiers[1]), Ui.Size(summary.Tiers[2]), Ui.Size(summary.Tiers[3]), Ui.Size(summary.Tiers[4]));
        var center = session.Center is { } c ? c.X + "," + c.Y : Strings.Unknown;
        lines[4].Text = Strings.Summary5(center, session.Tiles?.Computed ?? 0, session.Tiles?.Total ?? 0, session.Feed?.Connected == true ? Strings.Connected : Strings.Waiting);
        var mode = filter.SelectedIndex;
        shown = holdings.Snapshot().Where(h => mode switch
        {
            1 => h.Used,
            2 => h.State == HoldState.Held && !h.Used,
            3 => h.State == HoldState.Outside,
            _ => true
        }).OrderByDescending(h => h.Used).ThenBy(h => h.UsedAtMs).ToList();
        list.VirtualListSize = shown.Count;
        list.Invalidate();
    }

    private void Item(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= shown.Count) { e.Item = new ListViewItem(new[] { "", "", "", "", "" }); return; }
        var holding = shown[e.ItemIndex];
        var item = new ListViewItem(new[]
        {
            Strings.State(holding),
            (holding.Size / 1048576.0).ToString("0.00", CultureInfo.CurrentCulture),
            holding.State == HoldState.Outside ? "" : Strings.Tier(holding.Tier),
            holding.Used ? "+" + (holding.UsedAtMs / 1000).ToString(CultureInfo.CurrentCulture) + " s" : "",
            holding.Path.StartsWith(session.Root, StringComparison.OrdinalIgnoreCase) ? holding.Path[(session.Root.Length + 1)..] : holding.Path
        });
        if (holding.State == HoldState.Outside) item.BackColor = Color.FromArgb(255, 226, 180);
        else if (holding.Used) item.BackColor = Color.FromArgb(198, 236, 198);
        else if (holding.State == HoldState.Held) item.ForeColor = Color.FromArgb(110, 110, 110);
        e.Item = item;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the monitor only hides it while the session runs.
        if (e.CloseReason == CloseReason.UserClosing && session.Phase != "stopping") { e.Cancel = true; Hide(); return; }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}
