using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pixi2D.Debugger.Connection;
using Pixi2D.Debugger.Models;

namespace Pixi2D.Debugger;

public sealed partial class MainWindow : Window
{
    private readonly DebugClient _client = new();
    private readonly DispatcherQueue _ui;

    public ObservableCollection<TreeNodeVm> TreeRoots { get; } = new();
    public ObservableCollection<ConsoleEntry> Consoles { get; } = new();
    public ObservableCollection<NetEntry> Nets { get; } = new();
    public ObservableCollection<FileEntry> Files { get; } = new();
    public ObservableCollection<string> Evals { get; } = new();
    public ObservableCollection<PropertyRow> Props { get; } = new();

    private TreeNodeVm? _selected;

    public MainWindow()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();

        ElementTree.ItemsSource = TreeRoots;
        ConsoleList.ItemsSource = Consoles;
        NetList.ItemsSource     = Nets;
        FileList.ItemsSource    = Files;
        EvalList.ItemsSource    = Evals;
        PropList.ItemsSource    = Props;

        _client.OnFrame        += OnFrame;
        _client.OnConnected    += () => _ui.TryEnqueue(() => SetStatus("connected", true));
        _client.OnDisconnected += reason => _ui.TryEnqueue(() => SetStatus("disconnected (" + reason + ")", false));
    }

    private void SetStatus(string text, bool connected)
    {
        StatusText.Text = text;
        ConnectBtn.IsEnabled = !connected;
        DisconnectBtn.IsEnabled = connected;
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        try { await _client.ConnectAsync(HostBox.Text, int.Parse(PortBox.Text)); }
        catch (Exception ex) { SetStatus("error: " + ex.Message, false); }
    }
    private void OnDisconnectClick(object sender, RoutedEventArgs e) => _client.Disconnect("user");
    private void OnRefreshTreeClick(object sender, RoutedEventArgs e) => _client.Send("tree.refresh");
    private void OnClearConsoleClick(object sender, RoutedEventArgs e) => Consoles.Clear();

    private async void OnEvalRunClick(object sender, RoutedEventArgs e) => await DoEvalAsync();
    private async void OnEvalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; await DoEvalAsync(); }
    }
    private async Task DoEvalAsync()
    {
        var code = EvalInput.Text;
        if (string.IsNullOrWhiteSpace(code)) return;
        Evals.Add("> " + code);
        EvalInput.Text = "";
        var result = await _client.EvalAsync(code, TimeSpan.FromSeconds(10));
        if (result is null) { Evals.Add("<timeout>"); return; }
        var ok = result["ok"]?.GetValue<bool>() ?? false;
        Evals.Add(ok
            ? "= " + (result["value"]?.ToString() ?? "undefined")
            : "! " + (result["error"]?.ToString() ?? "unknown error"));
    }

    private void OnTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeNodeVm vm) SelectNode(vm);
    }

    private void SelectNode(TreeNodeVm vm)
    {
        _selected = vm;
        SelectedPath.Text = vm.Display;
        Props.Clear();
        AddProp("Name",        "string", vm.Name ?? "");
        AddProp("Kind",        "readonly", vm.Kind);
        AddProp("Id",          "readonly", vm.Id.ToString(CultureInfo.InvariantCulture));
        AddProp("X",           "number", F(vm.X));
        AddProp("Y",           "number", F(vm.Y));
        AddProp("Width",       "number", F(vm.W));
        AddProp("Height",      "number", F(vm.H));
        AddProp("ScaleX",      "number", F(vm.ScaleX));
        AddProp("ScaleY",      "number", F(vm.ScaleY));
        AddProp("Rotation",    "number", F(vm.Rotation));
        AddProp("Alpha",       "number", F(vm.Alpha));
        AddProp("AnchorX",     "number", F(vm.AnchorX));
        AddProp("AnchorY",     "number", F(vm.AnchorY));
        AddProp("Visible",     "bool",   vm.Visible.ToString().ToLowerInvariant());
        AddProp("Interactive", "bool",   vm.Interactive.ToString().ToLowerInvariant());
        AddProp("AcceptFocus", "bool",   vm.AcceptFocus.ToString().ToLowerInvariant());
    }

    private void AddProp(string name, string kind, string value) => Props.Add(new PropertyRow { Name = name, Kind = kind, Value = value });
    private static string F(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

    private void OnPropTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is string n) CommitProp(n, tb.Text);
    }
    private void OnPropTextKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox tb && tb.Tag is string n)
        {
            e.Handled = true;
            CommitProp(n, tb.Text);
        }
    }
    private void OnPropBoolClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is string n) CommitProp(n, cb.IsChecked == true ? "true" : "false");
    }

    private async void CommitProp(string name, string text)
    {
        if (_selected is null) return;
        var row = FindRow(name);
        if (row is null) return;
        JsonNode? val;
        try
        {
            val = row.Kind switch
            {
                "number" => JsonValue.Create(double.Parse(text, CultureInfo.InvariantCulture)),
                "bool"   => JsonValue.Create(bool.Parse(text)),
                "string" => JsonValue.Create(text),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            ShowError($"{name}: invalid value ({ex.Message})");
            return;
        }
        if (val is null) return;

        var reply = await _client.RequestAsync("tree.setProperty",
            new JsonObject { ["id"] = _selected.Id, ["name"] = name, ["value"] = val },
            TimeSpan.FromSeconds(5));
        if (reply is null) { ShowError($"{name}: timeout"); return; }
        if (reply["ok"]?.GetValue<bool>() == true)
        {
            PropInfo.IsOpen = false;
            // optimistic local update; tree.update will reconcile on next push
        }
        else
        {
            ShowError($"{name}: {reply["error"]?.ToString() ?? "unknown error"}");
        }
    }
    private PropertyRow? FindRow(string name) { foreach (var p in Props) if (p.Name == name) return p; return null; }
    private void ShowError(string msg) { PropInfo.Message = msg; PropInfo.IsOpen = true; }

    private void OnFrame(string type, JsonNode? payload)
    {
        _ui.TryEnqueue(() =>
        {
            switch (type)
            {
                case "hello":
                    HelloText.Text = "Host=" + payload?["host"] + " v" + payload?["version"] + " pid=" + payload?["pid"] + " pxml=" + payload?["pxmlPath"];
                    break;
                case "tree.update":
                    if (payload?["root"] is JsonObject ro) ReconcileRoot(ro);
                    break;
                case "console":
                    Consoles.Add(new ConsoleEntry
                    {
                        Level = payload?["level"]?.GetValue<string>() ?? "log",
                        Text  = payload?["text"]?.GetValue<string>() ?? "",
                        Ts    = DateTimeOffset.FromUnixTimeMilliseconds(payload?["ts"]?.GetValue<long>() ?? 0).ToLocalTime(),
                    });
                    if (Consoles.Count > 1000) Consoles.RemoveAt(0);
                    break;
                case "network": HandleNet(payload); if (Nets.Count > 500) Nets.RemoveAt(0); break;
                case "file":
                    var fe = new FileEntry
                    {
                        Path  = payload?["path"]?.GetValue<string>() ?? "",
                        Kind  = payload?["kind"]?.GetValue<string>() ?? "",
                        Size  = payload?["size"]?.GetValue<long>() ?? 0,
                        Mtime = DateTimeOffset.FromUnixTimeMilliseconds(payload?["mtime"]?.GetValue<long>() ?? 0).ToLocalTime(),
                    };
                    for (int i = 0; i < Files.Count; i++) if (Files[i].Path == fe.Path) { Files.RemoveAt(i); break; }
                    Files.Add(fe);
                    break;
            }
        });
    }

    // ---- Tree reconcile (preserves expansion state) ----
    private void ReconcileRoot(JsonObject root)
    {
        if (TreeRoots.Count == 0)
        {
            TreeRoots.Add(BuildNode(root));
            return;
        }
        // Pixi2D 始终单根 (Stage)
        ReconcileNode(TreeRoots[0], root);
    }
    private void ReconcileNode(TreeNodeVm vm, JsonObject json)
    {
        UpdateScalars(vm, json);
        var arr = json["children"] as JsonArray;
        var existing = new Dictionary<int, TreeNodeVm>();
        foreach (var c in vm.Children) existing[c.Id] = c;

        // Build new children list in incoming order, reusing existing VMs by id
        var newKids = new List<TreeNodeVm>(arr?.Count ?? 0);
        if (arr is not null)
        {
            foreach (var node in arr)
            {
                if (node is not JsonObject o) continue;
                int id = o["id"]?.GetValue<int>() ?? 0;
                if (existing.TryGetValue(id, out var reuse))
                {
                    ReconcileNode(reuse, o);
                    newKids.Add(reuse);
                }
                else
                {
                    newKids.Add(BuildNode(o));
                }
            }
        }
        // Apply diff to vm.Children with minimal churn
        // Simple approach: clear & re-add when sequence differs, else in-place
        bool same = vm.Children.Count == newKids.Count;
        if (same)
            for (int i = 0; i < newKids.Count; i++)
                if (!ReferenceEquals(vm.Children[i], newKids[i])) { same = false; break; }
        if (!same)
        {
            vm.Children.Clear();
            foreach (var k in newKids) vm.Children.Add(k);
        }
    }
    private static void UpdateScalars(TreeNodeVm vm, JsonObject o)
    {
        vm.Kind        = o["kind"]?.GetValue<string>() ?? vm.Kind;
        vm.Name        = o["name"]?.GetValue<string>();
        vm.X           = o["x"]?.GetValue<double>() ?? 0;
        vm.Y           = o["y"]?.GetValue<double>() ?? 0;
        vm.W           = o["w"]?.GetValue<double>() ?? 0;
        vm.H           = o["h"]?.GetValue<double>() ?? 0;
        vm.ScaleX      = o["scaleX"]?.GetValue<double>() ?? 1;
        vm.ScaleY      = o["scaleY"]?.GetValue<double>() ?? 1;
        vm.Rotation    = o["rotation"]?.GetValue<double>() ?? 0;
        vm.Alpha       = o["alpha"]?.GetValue<double>() ?? 1;
        vm.AnchorX     = o["anchorX"]?.GetValue<double>() ?? 0;
        vm.AnchorY     = o["anchorY"]?.GetValue<double>() ?? 0;
        vm.Visible     = o["visible"]?.GetValue<bool>() ?? true;
        vm.Interactive = o["interactive"]?.GetValue<bool>() ?? false;
        vm.AcceptFocus = o["acceptFocus"]?.GetValue<bool>() ?? false;
    }
    private static TreeNodeVm BuildNode(JsonObject o)
    {
        var n = new TreeNodeVm { Id = o["id"]?.GetValue<int>() ?? 0 };
        UpdateScalars(n, o);
        if (o["children"] is JsonArray arr)
            foreach (var c in arr) if (c is JsonObject co) n.Children.Add(BuildNode(co));
        return n;
    }

    private void HandleNet(JsonNode? payload)
    {
        var phase = payload?["phase"]?.GetValue<string>() ?? "";
        var url   = payload?["url"]?.GetValue<string>() ?? "";
        switch (phase)
        {
            case "start":
                Nets.Add(new NetEntry { Url = url, Method = payload?["method"]?.GetValue<string>() ?? "" });
                break;
            case "end":
            {
                var entry = FindOpen(url) ?? new NetEntry { Url = url };
                entry.Status = payload?["status"]?.GetValue<int>() ?? 0;
                entry.Bytes  = payload?["bytes"]?.GetValue<long>() ?? 0;
                entry.Ms     = payload?["ms"]?.GetValue<long>() ?? 0;
                if (!Nets.Contains(entry)) Nets.Add(entry); else { var i = Nets.IndexOf(entry); Nets[i] = entry; }
                break;
            }
            case "error":
            {
                var entry = FindOpen(url) ?? new NetEntry { Url = url };
                entry.Error = payload?["error"]?.GetValue<string>();
                if (!Nets.Contains(entry)) Nets.Add(entry); else { var i = Nets.IndexOf(entry); Nets[i] = entry; }
                break;
            }
        }
    }
    private NetEntry? FindOpen(string url)
    {
        for (int i = Nets.Count - 1; i >= 0; i--)
            if (Nets[i].Url == url && Nets[i].Status == 0 && Nets[i].Error is null) return Nets[i];
        return null;
    }
}
