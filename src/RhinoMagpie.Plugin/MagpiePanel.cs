using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoMagpie.Agent;

namespace RhinoMagpie.Plugin;

[Guid("b2a952a2-34e6-4c81-9846-c152dad14abb")]
public sealed class MagpiePanel : Panel
{
    private sealed class DocumentChat
    {
        public ModelingSession Session { get; } = new();
        public StringBuilder Transcript { get; } = new();
    }

    private readonly Dictionary<uint, DocumentChat> chats = [];
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromMinutes(10),
    };
    private readonly HttpClient mcpHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private readonly DropDown models = new();
    private readonly Button refresh = new() { Text = "刷新模型" };
    private readonly Button send = new() { Text = "发送" };
    private readonly Button stop = new() { Text = "停止", Enabled = false };
    private readonly Button clear = new() { Text = "新对话" };
    private readonly CheckBox autoTools = new() { Text = "本次会话自动执行修改", Checked = false };
    private readonly TextArea transcript = new() { ReadOnly = true, Wrap = true };
    private readonly TextArea prompt = new() { Height = 90, Wrap = true };
    private readonly Label status = new() { Text = "在 Magpie 连接 Rhino 9 AI，然后发送建模要求。", Wrap = WrapMode.Word };
    private CancellationTokenSource? running;
    private uint documentSerial;

    public MagpiePanel()
    {
        Padding = new Padding(10);
        MinimumSize = new Size(300, 420);
        Size = new Size(420, 640);
        SetModelChoices([]);
        var layout = new DynamicLayout { Spacing = new Size(6, 8) };
        layout.AddRow(new Label { Text = "Magpie", Font = SystemFonts.Bold(17) });
        layout.AddSeparateRow(models, refresh);
        layout.AddRow(status);
        layout.Add(transcript, xscale: true, yscale: true);
        layout.AddRow(autoTools);
        layout.AddRow(prompt);
        layout.AddSeparateRow(send, stop, clear);
        Content = layout;
        Shown += (_, _) => Application.Instance.AsyncInvoke(() =>
        {
            if (ParentWindow is Form floating && floating.GetType().Name == "TabPanelDockBarFloatingForm")
            {
                floating.MinimumSize = new Size(340, 500);
                if (floating.Size.Width < 400 || floating.Size.Height < 540)
                    floating.Size = new Size(460, 660);
            }
        });
        send.Click += async (_, _) => await SendAsync();
        stop.Click += (_, _) =>
        {
            running?.Cancel();
            status.Text = "正在停止，已发出的 Rhino 操作可能仍会完成。";
        };
        clear.Click += (_, _) =>
        {
            if (chats.TryGetValue(documentSerial, out var existing) && existing.Session.ToolCompletionUnknown)
            {
                status.Text = new UnconfirmedToolException().Message;
                return;
            }
            if (documentSerial != 0) chats[documentSerial] = new DocumentChat();
            transcript.Text = "";
            autoTools.Checked = false;
        };
        refresh.Click += async (_, _) => await RefreshAsync();
        prompt.KeyDown += async (_, args) =>
        {
            if (args.Key == Keys.Enter && args.Modifiers == Keys.Control)
            {
                args.Handled = true;
                await SendAsync();
            }
        };
        RhinoDoc.ActiveDocumentChanged += OnActiveDocumentChanged;
        RhinoDoc.CloseDocument += OnDocumentClosed;
        SelectDocument(RhinoDoc.ActiveDoc);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            running?.Cancel();
            RhinoDoc.ActiveDocumentChanged -= OnActiveDocumentChanged;
            RhinoDoc.CloseDocument -= OnDocumentClosed;
            http.Dispose();
            mcpHttp.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnActiveDocumentChanged(object? sender, DocumentEventArgs args) =>
        SelectDocument(args.Document);

    private void OnDocumentClosed(object? sender, DocumentEventArgs args)
    {
        if (args.DocumentSerialNumber == documentSerial) running?.Cancel();
        chats.Remove(args.DocumentSerialNumber);
    }

    private void SelectDocument(RhinoDoc? doc)
    {
        uint serial = doc?.RuntimeSerialNumber ?? 0;
        if (serial == documentSerial) return;
        running?.Cancel();
        documentSerial = serial;
        autoTools.Checked = false;
        if (serial == 0) transcript.Text = "";
        else
        {
            if (!chats.ContainsKey(serial)) chats[serial] = new DocumentChat();
            transcript.Text = chats[serial].Transcript.ToString();
        }
        status.Text = "当前文档 · " + (string.IsNullOrWhiteSpace(doc?.Name) ? "未命名" : doc.Name);
        SetBusy(running is not null);
    }

    private void SetModelChoices(IReadOnlyList<ModelChoice> choices)
    {
        models.DataStore = new[] { new ModelChoice("", "跟随 Magpie 当前模型 / 自定义组") }.Concat(choices);
        models.SelectedIndex = 0;
    }

    private async Task RefreshAsync()
    {
        refresh.Enabled = false;
        try
        {
            var config = MagpieGateway.ReadConfig();
            var choices = await new MagpieGateway(http).GetModelsAsync(config, CancellationToken.None);
            SetModelChoices(choices);
            status.Text = $"Magpie 已连接 · {choices.Count} 个模型及策略 · 当前 {config.Model}";
        }
        catch (Exception error) { status.Text = FriendlyError(error); }
        finally { refresh.Enabled = running is null; }
    }

    private async Task SendAsync()
    {
        if (running is not null || string.IsNullOrWhiteSpace(prompt.Text)) return;
        var doc = RhinoDoc.ActiveDoc;
        if (doc is null) return;
        SelectDocument(doc);
        var chat = chats[doc.RuntimeSerialNumber];
        if (chat.Session.ToolCompletionUnknown)
        {
            status.Text = new UnconfirmedToolException().Message;
            return;
        }
        string input = prompt.Text.Trim();
        using var cancellation = new CancellationTokenSource();
        running = cancellation;
        SetBusy(true);
        uint undo = 0;
        try
        {
            var config = MagpieGateway.ReadConfig();
            if (models.SelectedValue is ModelChoice { Id.Length: > 0 } selected)
                config = config with { Model = selected.Id };
            var endpoint = DocumentMcp.EnsureConnected(doc);
            var mcp = new RhinoMcpClient(mcpHttp, endpoint);
            if (!doc.UndoRecordingEnabled || doc.UndoRecordingIsActive)
                throw new InvalidOperationException("请先完成其他 Rhino 命令，并启用文档撤销后再发送。");
            undo = doc.BeginUndoRecord("Magpie");
            if (undo == 0)
                throw new InvalidOperationException("Rhino 无法创建独立撤销记录，请结束当前操作后重试。");
            Append(chat, "你", input);
            prompt.Text = "";
            status.Text = "运行中 · " + config.Model;
            bool automatic = autoTools.Checked == true;
            await Task.Run(() => chat.Session.RunAsync(input, config, new MagpieGateway(http), mcp,
                (call, token) => OnUiAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != doc.RuntimeSerialNumber)
                        throw new OperationCanceledException(token);
                    return call.ReadOnly || automatic || ConfirmTool(call, token);
                }, token),
                (arguments, token) => OnUiAsync(() => AskUser(arguments, token), token),
                (role, text) => Application.Instance.AsyncInvoke(() => Append(chat, role, text)),
                cancellation.Token), cancellation.Token);
            if (documentSerial == doc.RuntimeSerialNumber) status.Text = "完成 · Ctrl+Z 撤销本轮修改";
        }
        catch (OperationCanceledException)
        {
            Append(chat, "状态", "已停止。已发出的操作可能已完成，请先检查当前模型再继续。");
            status.Text = "已停止";
        }
        catch (Exception error)
        {
            string message = FriendlyError(error);
            Append(chat, "错误", message);
            status.Text = message;
        }
        finally
        {
            if (undo != 0 && RhinoDoc.FromRuntimeSerialNumber(doc.RuntimeSerialNumber) is { } live)
                live.EndUndoRecord(undo);
            running = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        bool blocked = chats.TryGetValue(documentSerial, out var chat) && chat.Session.ToolCompletionUnknown;
        send.Enabled = !busy && !blocked;
        clear.Enabled = !busy && !blocked;
        refresh.Enabled = !busy;
        models.Enabled = !busy;
        autoTools.Enabled = !busy;
        stop.Enabled = busy;
    }

    private void Append(DocumentChat chat, string role, string text)
    {
        chat.Transcript.AppendLine(role).AppendLine(text).AppendLine();
        if (chats.TryGetValue(documentSerial, out var visible) && ReferenceEquals(chat, visible))
        {
            transcript.Text = chat.Transcript.ToString();
            transcript.CaretIndex = transcript.Text.Length;
        }
    }

    private bool ConfirmTool(ToolInvocation call, CancellationToken token)
    {
        using var dialog = new Dialog<bool> { Title = "允许 Magpie 执行 Rhino 操作", ClientSize = new Size(620, 440), Resizable = true };
        var allow = new Button { Text = "允许执行" };
        var decline = new Button { Text = "拒绝" };
        allow.Click += (_, _) => dialog.Close(true);
        decline.Click += (_, _) => dialog.Close(false);
        var layout = new DynamicLayout { Padding = 12, Spacing = new Size(8, 8) };
        layout.AddRow(new Label { Text = call.Name });
        layout.Add(new TextArea { ReadOnly = true, Text = call.Arguments.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) }, yscale: true);
        layout.AddSeparateRow(allow, decline);
        dialog.Content = layout;
        dialog.DefaultButton = allow;
        dialog.AbortButton = decline;
        using var registration = token.Register(() => Application.Instance.AsyncInvoke(() => dialog.Close(false)));
        return dialog.ShowModal(this);
    }

    private string AskUser(JsonObject arguments, CancellationToken token)
    {
        using var dialog = new Dialog<string> { Title = "Magpie 需要你的选择", ClientSize = new Size(580, 420), Resizable = true };
        var response = new TextArea { Height = 100, Wrap = true };
        var submit = new Button { Text = "提交回答" };
        var cancel = new Button { Text = "取消" };
        submit.Click += (_, _) => dialog.Close(response.Text);
        cancel.Click += (_, _) => dialog.Close("The user cancelled. Stop and await further instructions.");
        var layout = new DynamicLayout { Padding = 12, Spacing = new Size(8, 8) };
        layout.Add(new TextArea
        {
            ReadOnly = true, Wrap = true,
            Text = arguments.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
        }, yscale: true);
        layout.AddRow(new Label { Text = "输入你的选择或补充要求" });
        layout.AddRow(response);
        layout.AddSeparateRow(submit, cancel);
        dialog.Content = layout;
        dialog.DefaultButton = submit;
        dialog.AbortButton = cancel;
        using var registration = token.Register(() => Application.Instance.AsyncInvoke(() => dialog.Close("Cancelled")));
        return dialog.ShowModal(this) ?? "The user cancelled.";
    }

    private static Task<TResult> OnUiAsync<TResult>(Func<TResult> action, CancellationToken token)
    {
        var source = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.Instance.AsyncInvoke(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                source.TrySetResult(action());
            }
            catch (OperationCanceledException) { source.TrySetCanceled(token); }
            catch (Exception error) { source.TrySetException(error); }
        });
        return source.Task;
    }

    private static string FriendlyError(Exception error) => error switch
    {
        HttpRequestException => "无法连接本机服务，请检查 Magpie 和 Rhino MCP。",
        JsonException => "服务返回了无法解析的 JSON，请检查模型工具调用兼容性。",
        InvalidOperationException or ArgumentException => error.Message,
        UnconfirmedToolException => error.Message,
        _ => "操作失败，请检查 Rhino 插件安装和文件访问权限。",
    };
}
