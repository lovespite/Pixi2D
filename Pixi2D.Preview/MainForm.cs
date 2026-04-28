using System.Drawing;
using System.Windows.Forms;
using Pixi2D.Core;
using Pixi2D.Markup;
using Pixi2D.Markup.Diagnostics;

namespace Pixi2D.Preview;

/// <summary>
/// Pixi2D PXML 预览器主窗体 (v0.2)。
/// </summary>
/// <remarks>
/// 主要能力：
/// <list type="bullet">
/// <item>左侧内联文本编辑器 (等宽字体)；右侧上方对象树 / 下方 Diagnostics 面板</item>
/// <item>AutoSave: 编辑后 500ms 防抖, 自动保存当前文件</item>
/// <item>AutoHotReload: 文本变更或外部修改 → 自动重新解析, 更新树/诊断</item>
/// <item>FileSystemWatcher 协调: 自身保存时短暂屏蔽外部触发以避免循环</item>
/// <item>Diagnostics ListView: 双击行跳转到编辑器对应位置</item>
/// </list>
/// </remarks>
public sealed class MainForm : Form
{
    private const int DebounceMs = 500;
    private const int SuppressWatcherMs = 800;

    private readonly TextBox _path = new() { Dock = DockStyle.Top, ReadOnly = true, BackColor = SystemColors.Control };
    private readonly TextBox _editor = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        AcceptsTab = true,
        AcceptsReturn = true,
        WordWrap = false,
        Font = new Font("Consolas", 10.5f),
        HideSelection = false,
    };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ListView _diagnostics = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = false,
        ShowItemToolTips = true,
    };

    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new("就绪");
    private readonly ToolStripStatusLabel _countsLabel = new("Errors: 0  Warnings: 0") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly ToolStripStatusLabel _modeLabel = new("AutoSave: ON  AutoReload: ON");

    private readonly ToolStripMenuItem _autoSaveItem = new("AutoSave(&A)") { CheckOnClick = true, Checked = true };
    private readonly ToolStripMenuItem _autoReloadItem = new("AutoHotReload(&H)") { CheckOnClick = true, Checked = true };

    private FileSystemWatcher? _watcher;
    private string? _currentFile;
    private System.Windows.Forms.Timer? _debounce;
    private bool _suppressWatcher;
    private bool _editorDirty;

    public MainForm()
    {
        Text = "Pixi2D Preview";
        Width = 1200;
        Height = 760;
        AllowDrop = true;

        // ===== 菜单 =====
        var menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("文件(&F)");
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("打开(&O)...", null, (_, _) => PromptOpen()) { ShortcutKeys = Keys.Control | Keys.O });
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("保存(&S)", null, (_, _) => SaveNow()) { ShortcutKeys = Keys.Control | Keys.S });
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("重新载入(&R)", null, (_, _) => Reload()) { ShortcutKeys = Keys.F5 });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("退出(&X)", null, (_, _) => Close()));

        var optionsMenu = new ToolStripMenuItem("选项(&O)");
        _autoSaveItem.CheckedChanged += (_, _) => UpdateModeLabel();
        _autoReloadItem.CheckedChanged += (_, _) => UpdateModeLabel();
        optionsMenu.DropDownItems.Add(_autoSaveItem);
        optionsMenu.DropDownItems.Add(_autoReloadItem);

        menu.Items.Add(fileMenu);
        menu.Items.Add(optionsMenu);
        MainMenuStrip = menu;

        // ===== 诊断 ListView 列 =====
        _diagnostics.Columns.Add("Severity", 80);
        _diagnostics.Columns.Add("Line", 50);
        _diagnostics.Columns.Add("Col", 50);
        _diagnostics.Columns.Add("Element", 120);
        _diagnostics.Columns.Add("Attribute", 120);
        _diagnostics.Columns.Add("Message", 700);
        _diagnostics.DoubleClick += (_, _) => JumpToSelectedDiagnostic();

        // ===== 布局 =====
        // 主 SplitContainer: 左编辑器 / 右侧
        var outer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 540 };
        outer.Panel1.Controls.Add(_editor);
        outer.Panel1.Controls.Add(_path);
        // 右侧 上下分割: 上对象树 / 下诊断
        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 380 };
        right.Panel1.Controls.Add(_tree);
        right.Panel2.Controls.Add(_diagnostics);
        outer.Panel2.Controls.Add(right);

        _status.Items.AddRange(new ToolStripItem[] { _statusLabel, _modeLabel, _countsLabel });

        Controls.Add(outer);
        Controls.Add(_status);
        Controls.Add(menu);

        // ===== 事件 =====
        _editor.TextChanged += OnEditorTextChanged;
        DragEnter += (_, e) => e!.Effect = (e.Data?.GetDataPresent(DataFormats.FileDrop) ?? false) ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e!.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                Load(files[0]);
        };

        UpdateModeLabel();
        Load_Empty();
    }

    public void QueueLoad(string path) => BeginInvoke(() => Load(path));

    // ============================================================
    // 文件加载 / 保存
    // ============================================================
    private void PromptOpen()
    {
        using var dlg = new OpenFileDialog { Filter = "Pixi2D XML (*.pxml)|*.pxml|All files (*.*)|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            Load(dlg.FileName);
    }

    private void Load(string path)
    {
        _currentFile = path;
        _path.Text = path;
        try
        {
            var text = File.ReadAllText(path);
            // 屏蔽 TextChanged 触发 AutoSave/Reload
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.Text = text;
            _editor.TextChanged += OnEditorTextChanged;
            _editorDirty = false;
        }
        catch (Exception ex)
        {
            ShowDiagnostics([new Diagnostic(DiagnosticSeverity.Error, $"读取文件失败: {ex.Message}", path)]);
            return;
        }
        SetupWatcher(path);
        ParseAndRender(_editor.Text, path);
    }

    private void Reload()
    {
        if (_currentFile is null) return;
        Load(_currentFile);
    }

    private void Load_Empty()
    {
        _editor.Text = "<panel width=\"320\" height=\"180\" background-color=\"#202225\">\r\n    <fancy-text x=\"20\" y=\"16\" content=\"Hello, Pixi2D!\" />\r\n</panel>\r\n";
        _editorDirty = false;
        ParseAndRender(_editor.Text, virtualPath: null);
    }

    private void SaveNow()
    {
        if (_currentFile is null || !_editorDirty) return;
        try
        {
            _suppressWatcher = true;
            File.WriteAllText(_currentFile, _editor.Text);
            _editorDirty = false;
            _statusLabel.Text = $"已保存: {Path.GetFileName(_currentFile)}";
            // 短暂延后清除，避免 watcher 触发自身重载
            BeginInvoke(async () =>
            {
                await Task.Delay(SuppressWatcherMs);
                _suppressWatcher = false;
            });
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"保存失败: {ex.Message}";
            _suppressWatcher = false;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _editorDirty = true;
        ScheduleDebounce();
    }

    // ============================================================
    // 防抖：触发 AutoSave + AutoHotReload
    // ============================================================
    private void ScheduleDebounce()
    {
        _debounce?.Stop();
        _debounce ??= new System.Windows.Forms.Timer { Interval = DebounceMs };
        _debounce.Tick -= OnDebounceTick;
        _debounce.Tick += OnDebounceTick;
        _debounce.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce!.Stop();
        if (_autoSaveItem.Checked) SaveNow();
        if (_autoReloadItem.Checked) ParseAndRender(_editor.Text, _currentFile);
    }

    // ============================================================
    // 解析 & 渲染
    // ============================================================
    private void ParseAndRender(string xml, string? virtualPath)
    {
        var loader = new PxmlLoader();
        var diagnostics = new List<Diagnostic>();
        try
        {
            var root = loader.LoadFromString(xml, virtualPath);
            diagnostics.AddRange(loader.Diagnostics);
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            _tree.Nodes.Add(BuildTreeNode(root));
            _tree.ExpandAll();
            _tree.EndUpdate();
            _statusLabel.Text = virtualPath is null ? "已解析 (未保存)" : $"已加载: {Path.GetFileName(virtualPath)}";
        }
        catch (PxmlException pex)
        {
            diagnostics.AddRange(loader.Diagnostics);
            diagnostics.Add(pex.ToDiagnostic());
            _statusLabel.Text = "加载失败";
        }
        catch (Exception ex)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, $"{ex.GetType().Name}: {ex.Message}", virtualPath));
            _statusLabel.Text = "加载失败";
        }
        ShowDiagnostics(diagnostics);
    }

    private static TreeNode BuildTreeNode(DisplayObject obj)
    {
        var label = obj.GetType().Name;
        if (!string.IsNullOrEmpty(obj.Name)) label += $"  #{obj.Name}";
        var node = new TreeNode(label) { Tag = obj };
        if (obj is Container container)
            foreach (var child in container)
                node.Nodes.Add(BuildTreeNode(child));
        return node;
    }

    private void ShowDiagnostics(IReadOnlyList<Diagnostic> items)
    {
        _diagnostics.BeginUpdate();
        _diagnostics.Items.Clear();
        int errors = 0, warnings = 0;
        foreach (var d in items)
        {
            var lvi = new ListViewItem(d.Severity.ToString())
            {
                Tag = d,
                ForeColor = d.Severity switch
                {
                    DiagnosticSeverity.Error => Color.Firebrick,
                    DiagnosticSeverity.Warning => Color.DarkGoldenrod,
                    _ => Color.DimGray,
                },
                ToolTipText = d.ToString(),
            };
            lvi.SubItems.Add(d.Line > 0 ? d.Line.ToString() : "");
            lvi.SubItems.Add(d.Column > 0 ? d.Column.ToString() : "");
            lvi.SubItems.Add(d.ElementName ?? "");
            lvi.SubItems.Add(d.AttributeName ?? "");
            lvi.SubItems.Add(d.Message);
            _diagnostics.Items.Add(lvi);
            if (d.Severity == DiagnosticSeverity.Error) errors++;
            else if (d.Severity == DiagnosticSeverity.Warning) warnings++;
        }
        _diagnostics.EndUpdate();
        _countsLabel.Text = $"Errors: {errors}  Warnings: {warnings}";
    }

    private void JumpToSelectedDiagnostic()
    {
        if (_diagnostics.SelectedItems.Count == 0) return;
        if (_diagnostics.SelectedItems[0].Tag is not Diagnostic d) return;
        if (d.Line <= 0) return;

        var lines = _editor.Text.Split('\n');
        int charIndex = 0;
        for (int i = 0; i < d.Line - 1 && i < lines.Length; i++)
            charIndex += lines[i].Length + 1;
        charIndex += Math.Max(0, d.Column - 1);
        charIndex = Math.Min(charIndex, _editor.TextLength);

        _editor.Focus();
        _editor.SelectionStart = charIndex;
        _editor.SelectionLength = 0;
        _editor.ScrollToCaret();
    }

    // ============================================================
    // FileSystemWatcher
    // ============================================================
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
        _watcher.Changed += (_, _) => OnExternalChange();
        _watcher.Renamed += (_, _) => OnExternalChange();
    }

    private void OnExternalChange()
    {
        if (_suppressWatcher) return;
        BeginInvoke(() =>
        {
            if (_suppressWatcher || _currentFile is null) return;
            // 仅当编辑器没有未保存更改时才同步外部内容
            if (_editorDirty) return;
            try
            {
                var text = File.ReadAllText(_currentFile);
                if (text == _editor.Text) return;
                _editor.TextChanged -= OnEditorTextChanged;
                _editor.Text = text;
                _editor.TextChanged += OnEditorTextChanged;
                _editorDirty = false;
                if (_autoReloadItem.Checked)
                    ParseAndRender(_editor.Text, _currentFile);
            }
            catch { /* 文件被锁等情况, 忽略此次 */ }
        });
    }

    private void UpdateModeLabel()
    {
        _modeLabel.Text = $"AutoSave: {(_autoSaveItem.Checked ? "ON" : "OFF")}  AutoReload: {(_autoReloadItem.Checked ? "ON" : "OFF")}";
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
