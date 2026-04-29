using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
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
    public ObservableCollection<string> TreeFlat { get; } = new();
    public ObservableCollection<ConsoleEntry> Consoles { get; } = new();
    public ObservableCollection<NetEntry> Nets { get; } = new();
    public ObservableCollection<FileEntry> Files { get; } = new();
    public ObservableCollection<string> Evals { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();

        TreeViewCtrl.ItemsSource = TreeFlat;
        ConsoleList.ItemsSource = Consoles;
        NetList.ItemsSource     = Nets;
        FileList.ItemsSource    = Files;
        EvalList.ItemsSource    = Evals;

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
        try
        {
            int port = int.Parse(PortBox.Text);
            await _client.ConnectAsync(HostBox.Text, port);
        }
        catch (Exception ex)
        {
            SetStatus("error: " + ex.Message, false);
        }
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e) => _client.Disconnect("user");

    private void OnRefreshTreeClick(object sender, RoutedEventArgs e) => _client.Send("tree.refresh");

    private void OnClearConsoleClick(object sender, RoutedEventArgs e) => Consoles.Clear();

    private async void OnEvalRunClick(object sender, RoutedEventArgs e) => await DoEvalAsync();
    private async void OnEvalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; await DoEvalAsync(); }
    }

    private async System.Threading.Tasks.Task DoEvalAsync()
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
                    TreeFlat.Clear();
                    if (payload?["root"] is JsonObject ro)
                    {
                        var node = BuildNode(ro);
                        FlattenInto(node, 0);
                    }
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
                case "network":
                    HandleNet(payload);
                    if (Nets.Count > 500) Nets.RemoveAt(0);
                    break;
                case "file":
                    var fe = new FileEntry
                    {
                        Path  = payload?["path"]?.GetValue<string>() ?? "",
                        Kind  = payload?["kind"]?.GetValue<string>() ?? "",
                        Size  = payload?["size"]?.GetValue<long>() ?? 0,
                        Mtime = DateTimeOffset.FromUnixTimeMilliseconds(payload?["mtime"]?.GetValue<long>() ?? 0).ToLocalTime(),
                    };
                    // 去重: 同 path 替换
                    for (int i = 0; i < Files.Count; i++) if (Files[i].Path == fe.Path) { Files.RemoveAt(i); break; }
                    Files.Add(fe);
                    break;
            }
        });
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
                    var entry = FindByUrl(url) ?? new NetEntry { Url = url };
                    entry.Status = payload?["status"]?.GetValue<int>() ?? 0;
                    entry.Bytes  = payload?["bytes"]?.GetValue<long>() ?? 0;
                    entry.Ms     = payload?["ms"]?.GetValue<long>() ?? 0;
                    if (!Nets.Contains(entry)) Nets.Add(entry);
                    else { var i = Nets.IndexOf(entry); Nets[i] = entry; }
                    break;
                }
            case "error":
                {
                    var entry = FindByUrl(url) ?? new NetEntry { Url = url };
                    entry.Error = payload?["error"]?.GetValue<string>();
                    if (!Nets.Contains(entry)) Nets.Add(entry);
                    else { var i = Nets.IndexOf(entry); Nets[i] = entry; }
                    break;
                }
        }
    }

    private NetEntry? FindByUrl(string url)
    {
        for (int i = Nets.Count - 1; i >= 0; i--)
            if (Nets[i].Url == url && Nets[i].Status == 0 && Nets[i].Error is null) return Nets[i];
        return null;
    }

    private void FlattenInto(TreeNodeVm node, int depth)
    {
        TreeFlat.Add(new string(' ', depth * 2) + node.Display);
        foreach (var c in node.Children) FlattenInto(c, depth + 1);
    }

    private static TreeNodeVm BuildNode(JsonObject o)
    {
        var node = new TreeNodeVm
        {
            Id      = o["id"]?.GetValue<int>() ?? 0,
            Kind    = o["kind"]?.GetValue<string>() ?? "?",
            Name    = o["name"]?.GetValue<string>(),
            X       = o["x"]?.GetValue<double>() ?? 0,
            Y       = o["y"]?.GetValue<double>() ?? 0,
            W       = o["w"]?.GetValue<double>() ?? 0,
            H       = o["h"]?.GetValue<double>() ?? 0,
            Visible = o["visible"]?.GetValue<bool>() ?? true,
        };
        if (o["children"] is JsonArray arr)
            foreach (var c in arr) if (c is JsonObject co) node.Children.Add(BuildNode(co));
        return node;
    }
}
