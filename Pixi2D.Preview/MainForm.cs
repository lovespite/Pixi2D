using System.Windows.Forms;
using Pixi2D.Core;
using Pixi2D.Markup;

namespace Pixi2D.Preview;

/// <summary>
/// Pixi2D PXML 预览器主窗体 (v0.1)。
/// </summary>
/// <remarks>
/// 当前版本提供:
/// <list type="bullet">
/// <item>打开 / 拖拽 .pxml 文件</item>
/// <item>FileSystemWatcher 热重载 (200ms 防抖)</item>
/// <item>展示加载得到的 DisplayObject 树 (TreeView) 与属性</item>
/// <item>解析错误显示在底部状态栏 / 日志区</item>
/// </list>
/// 实际 Direct2D 渲染将在后续阶段集成 (需要在 Panel 上承载 HwndRenderTarget)。
/// </remarks>
public sealed class MainForm : Form
{
    private readonly TextBox _path = new() { Dock = DockStyle.Top, ReadOnly = true };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly TextBox _log = new() { Dock = DockStyle.Bottom, Multiline = true, Height = 120, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new("就绪");

    private FileSystemWatcher? _watcher;
    private string? _currentFile;
    private System.Windows.Forms.Timer? _debounce;

    public MainForm()
    {
        Text = "Pixi2D Preview";
        Width = 900;
        Height = 600;
        AllowDrop = true;

        _status.Items.Add(_statusLabel);
        Controls.Add(_tree);
        Controls.Add(_path);
        Controls.Add(_log);
        Controls.Add(_status);

        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("文件(&F)");
        var openItem = new ToolStripMenuItem("打开(&O)...", null, (_, _) => PromptOpen()) { ShortcutKeys = Keys.Control | Keys.O };
        var reloadItem = new ToolStripMenuItem("重新载入(&R)", null, (_, _) => Reload()) { ShortcutKeys = Keys.F5 };
        var exitItem = new ToolStripMenuItem("退出(&X)", null, (_, _) => Close());
        fileMenu.DropDownItems.AddRange(new ToolStripItem[] { openItem, reloadItem, new ToolStripSeparator(), exitItem });
        menu.Items.Add(fileMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        DragEnter += (_, e) => e!.Effect = (e.Data?.GetDataPresent(DataFormats.FileDrop) ?? false) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e!.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                Load(files[0]);
        };
    }

    public void QueueLoad(string path) => BeginInvoke(() => Load(path));

    private void PromptOpen()
    {
        using var dlg = new OpenFileDialog { Filter = "Pixi2D XML (*.pxml)|*.pxml|All files (*.*)|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            Load(dlg.FileName);
    }

    private void Reload()
    {
        if (_currentFile is not null) Load(_currentFile);
    }

    private void Load(string path)
    {
        _currentFile = path;
        _path.Text = path;
        try
        {
            var loader = new PxmlLoader();
            var root = loader.LoadFromFile(path);
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            _tree.Nodes.Add(BuildTreeNode(root, loader.Host));
            _tree.ExpandAll();
            _tree.EndUpdate();
            Log($"[OK] 加载成功: {path}");
            _statusLabel.Text = $"已加载: {Path.GetFileName(path)}";
            SetupWatcher(path);
        }
        catch (Exception ex)
        {
            Log($"[ERROR] {ex.GetType().Name}: {ex.Message}");
            _statusLabel.Text = "加载失败";
        }
    }

    private static TreeNode BuildTreeNode(DisplayObject obj, ScriptHost host)
    {
        var label = obj.GetType().Name;
        if (!string.IsNullOrEmpty(obj.Name)) label += $"  #{obj.Name}";
        var node = new TreeNode(label) { Tag = obj };
        if (obj is Container container)
        {
            foreach (var child in container)
                node.Nodes.Add(BuildTreeNode(child, host));
        }
        return node;
    }

    private void SetupWatcher(string path)
    {
        _watcher?.Dispose();
        var dir = Path.GetDirectoryName(path)!;
        var file = Path.GetFileName(path);
        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => DebouncedReload();
        _watcher.Renamed += (_, _) => DebouncedReload();
    }

    private void DebouncedReload()
    {
        BeginInvoke(() =>
        {
            _debounce?.Stop();
            _debounce ??= new System.Windows.Forms.Timer { Interval = 200 };
            _debounce.Tick -= OnDebounceTick;
            _debounce.Tick += OnDebounceTick;
            _debounce.Start();
        });
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce!.Stop();
        Reload();
    }

    private void Log(string line)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher?.Dispose();
            _debounce?.Dispose();
        }
        base.Dispose(disposing);
    }
}
