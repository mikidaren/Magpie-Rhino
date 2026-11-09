using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using RhinoMagpie.Agent;

namespace RhinoMagpie.Plugin;

[Guid("b2a952a2-34e6-4c81-9846-c152dad14abb")]
public sealed class MagpiePanel : Panel
{
    internal bool IsBusy => running is not null || refreshing is not null;
    private sealed class DocumentChat
    {
        public ModelingSession Session { get; } = new();
        public StringBuilder Transcript { get; } = new();
        public string Draft { get; set; } = "";
        public List<PanelMessage> Messages { get; } = [];
        public List<ReferenceInput> References { get; } = [];
        public bool Architectural { get; set; } = true;
        public string Detail { get; set; } = "developed";
        public PanelMessage? StreamingMessage { get; set; }
    }

    private sealed class PanelMessage(string role, string type, string text, string state = "done")
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Role { get; } = role;
        public string Type { get; } = type;
        public string Text { get; set; } = text;
        public string Detail { get; set; } = "";
        public string State { get; set; } = state;
        public string? Image { get; set; }
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
    private CancellationTokenSource? refreshing;
    private DateTime catalogRefreshedAt = DateTime.MinValue;
    private string catalogError = "";
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    private bool transportsDisposed;
    private uint documentSerial;
    private readonly PanelWebView web = new();
    private readonly UITimer stateTimer = new() { Interval = 0.1 };
    private string documentName = "未命名";
    private string documentUnits = "";
    private string unitsKey = "";

    public MagpiePanel()
    {
        Padding = new Padding(0);
        MinimumSize = new Size(300, 420);
        Size = new Size(420, 640);
        SetModelChoices([]);
        Content = web;
        web.ActionReceived += async message =>
        {
            try { await HandleWebActionAsync(message); }
            catch (Exception error) { status.Text = FriendlyError(error); }
        };
        stateTimer.Elapsed += (_, _) => PublishState();
        stateTimer.Start();
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
            if (refreshing is not null)
            {
                refreshing.Cancel();
                status.Text = "正在停止刷新。";
            }
            else
            {
                running?.Cancel();
                status.Text = "正在停止，已发出的 Rhino 操作返回后结束本轮。";
            }
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
            prompt.Text = "";
            autoTools.Checked = false;
            PublishState();
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
        PublishState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            stateTimer.Stop();
            stateTimer.Dispose();
            lifetime.Cancel();
            refreshing?.Cancel();
            running?.Cancel();
            RhinoDoc.ActiveDocumentChanged -= OnActiveDocumentChanged;
            RhinoDoc.CloseDocument -= OnDocumentClosed;
            DisposeTransportsWhenIdle();
        }
        base.Dispose(disposing);
    }

    private void DisposeTransportsWhenIdle()
    {
        if (!disposed || transportsDisposed || running is not null || refreshing is not null) return;
        transportsDisposed = true;
        http.Dispose();
        mcpHttp.Dispose();
        lifetime.Dispose();
        foreach (var control in new Control[] { models, refresh, send, stop, clear, autoTools, transcript, prompt, status })
            control.Dispose();
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
        if (chats.TryGetValue(documentSerial, out var previous)) previous.Draft = prompt.Text;
        running?.Cancel();
        documentSerial = serial;
        autoTools.Checked = false;
        if (serial == 0)
        {
            transcript.Text = "";
            prompt.Text = "";
        }
        else
        {
            if (!chats.ContainsKey(serial)) chats[serial] = new DocumentChat();
            transcript.Text = chats[serial].Transcript.ToString();
            prompt.Text = chats[serial].Draft;
        }
        status.Text = "当前文档 · " + (string.IsNullOrWhiteSpace(doc?.Name) ? "未命名" : doc.Name);
        documentName = string.IsNullOrWhiteSpace(doc?.Name) ? "未命名" : doc.Name;
        documentUnits = FormatUnits(doc?.ModelUnitSystem);
        unitsKey = doc?.ModelUnitSystem.ToString() ?? "";
        SetBusy(running is not null);
    }

    private void SetModelChoices(IReadOnlyList<ModelChoice> choices, string selectedId = "")
    {
        var options = new[] { new ModelChoice("", "跟随 Magpie 当前模型 / 自定义组") }.Concat(choices).ToList();
        models.DataStore = options;
        models.SelectedIndex = Math.Max(0, options.FindIndex(option => option.Id == selectedId));
    }

    private async Task RefreshAsync()
    {
        if (disposed || running is not null || refreshing is not null) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(20));
        refreshing = cancellation;
        catalogError = "";
        SetBusy(false);
        status.Text = "正在获取 Magpie 模型、自定义组和策略…";
        try
        {
            string selectedId = (models.SelectedValue as ModelChoice)?.Id ?? "";
            var config = MagpieGateway.ReadConfig();
            var choices = await new MagpieGateway(http).GetModelsAsync(config, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (disposed) return;
            SetModelChoices(choices, selectedId);
            catalogRefreshedAt = DateTime.UtcNow;
            status.Text = $"Magpie 已连接 · {choices.Count} 个模型及策略 · 当前 {config.Model}";
        }
        catch (OperationCanceledException)
        {
            if (!disposed) status.Text = catalogError = "刷新已取消或超时，保留之前的模型选择，可重试。";
        }
        catch (Exception error) { if (!disposed) status.Text = catalogError = FriendlyError(error); }
        finally
        {
            refreshing = null;
            if (!disposed) SetBusy(running is not null);
            DisposeTransportsWhenIdle();
        }
    }

    private async Task SendAsync()
    {
        if (disposed || running is not null || refreshing is not null || string.IsNullOrWhiteSpace(prompt.Text)) return;
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
            if (chat.Architectural && doc.ModelUnitSystem is Rhino.UnitSystem.None or Rhino.UnitSystem.Unset)
                throw new InvalidOperationException("请先在 Rhino 中确认文档单位，再开始建筑建模。");
            if (!doc.UndoRecordingEnabled || doc.UndoRecordingIsActive)
                throw new InvalidOperationException("请先完成其他 Rhino 命令，并启用文档撤销后再发送。");
            undo = doc.BeginUndoRecord("Magpie");
            if (undo == 0)
                throw new InvalidOperationException("Rhino 无法创建独立撤销记录，请结束当前操作后重试。");
            Append(chat, "你", input);
            prompt.Text = "";
            chat.Draft = "";
            status.Text = "运行中 · " + config.Model;
            bool automatic = autoTools.Checked == true;
            var objects = doc.Objects.GetObjectList(Rhino.DocObjects.ObjectType.AnyObject).ToList();
            var bounds = Rhino.Geometry.BoundingBox.Unset;
            foreach (var item in objects)
                bounds.Union(item.Geometry.GetBoundingBox(true));
            var context = JsonSerializer.Serialize(new
            {
                document = doc.Name ?? "未命名",
                units = doc.ModelUnitSystem.ToString(),
                metersPerDocumentUnit = RhinoMath.UnitScale(doc.ModelUnitSystem, Rhino.UnitSystem.Meters),
                tolerance = doc.ModelAbsoluteTolerance,
                liveObjectCount = objects.Count,
                boundingBoxMin = bounds.IsValid ? new[] { bounds.Min.X, bounds.Min.Y, bounds.Min.Z } : null,
                boundingBoxMax = bounds.IsValid ? new[] { bounds.Max.X, bounds.Max.Y, bounds.Max.Z } : null,
            });
            var options = new ModelingRunOptions(chat.Architectural, chat.Detail, context,
                objects.Count > 0,
                Regex.IsMatch(input, @"\d\s*(毫米|厘米|米|mm|cm|m\b|ft\b)", RegexOptions.IgnoreCase),
                chat.References.Select(reference => reference.InUnits(doc.ModelUnitSystem.ToString())).ToList(),
                doc.ModelUnitSystem.ToString());
            await Task.Run(() => chat.Session.RunAsync(input, config, new MagpieGateway(http), mcp,
                (call, token) => OnUiAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != doc.RuntimeSerialNumber)
                        throw new OperationCanceledException(token);
                    return call.Name == "confirm_design_basis"
                        ? ConfirmDesignBasis(call.Arguments["note"]?.GetValue<string>() ?? "", token)
                        : call.ReadOnly || automatic || ConfirmTool(call, token);
                }, token),
                (arguments, token) => OnUiAsync(() => AskUser(arguments, token), token),
                (role, text) => Application.Instance.AsyncInvoke(() => { if (!disposed) Append(chat, role, text); }),
                cancellation.Token, options), cancellation.Token);
            if (!disposed && documentSerial == doc.RuntimeSerialNumber)
                status.Text = chat.Session.Outcome switch
                {
                    "needs_review" => "阶段已检查，等待你的验收",
                    "paused" => "已暂停，待办已保留，可继续深化",
                    _ => "本轮已结束 · Ctrl+Z 撤销本轮修改",
                };
        }
        catch (OperationCanceledException)
        {
            if (!disposed)
            {
                Append(chat, "状态", "已停止。请检查已执行的操作，再继续或撤销。");
                if (documentSerial == doc.RuntimeSerialNumber) status.Text = "已停止";
            }
        }
        catch (Exception error)
        {
            string message = FriendlyError(error);
            if (!disposed)
            {
                Append(chat, "错误", message);
                if (documentSerial == doc.RuntimeSerialNumber) status.Text = message;
            }
        }
        finally
        {
            if (undo != 0 && RhinoDoc.FromRuntimeSerialNumber(doc.RuntimeSerialNumber) is { } live)
                RhinoApp.InvokeOnUiThread(new Action(() => live.EndUndoRecord(undo)));
            if (chat.StreamingMessage is { State: "running" } unfinished)
                unfinished.State = "paused";
            running = null;
            if (!disposed) SetBusy(false);
            DisposeTransportsWhenIdle();
        }
    }

    private void SetBusy(bool busy)
    {
        if (disposed) return;
        bool blocked = chats.TryGetValue(documentSerial, out var chat) && chat.Session.ToolCompletionUnknown;
        bool idle = !busy && refreshing is null;
        send.Enabled = idle && !blocked;
        clear.Enabled = idle && !blocked;
        refresh.Enabled = idle;
        models.Enabled = idle;
        autoTools.Enabled = idle;
        stop.Enabled = busy || refreshing is not null;
    }

    private void Append(DocumentChat chat, string role, string text)
    {
        if (role == "assistant_start")
        {
            chat.StreamingMessage = new PanelMessage("assistant", "message", "", "running");
            chat.Messages.Add(chat.StreamingMessage);
            if (chats.TryGetValue(documentSerial, out var streamVisible) && ReferenceEquals(chat, streamVisible))
                status.Text = "正在接收 Magpie 回复";
            return;
        }
        if (role == "assistant_delta")
        {
            if (chat.StreamingMessage is not null) chat.StreamingMessage.Text += text;
            return;
        }
        if (role == "Magpie" && chat.StreamingMessage is not null)
        {
            chat.StreamingMessage.Text = text;
            chat.StreamingMessage.State = "done";
        }
        else if (role == "工具")
            chat.Messages.Add(new PanelMessage("tool", "tool", ToolTitle(text), "running"));
        else if (role is "tool_input" or "工具结果" or "viewport")
        {
            var tool = chat.Messages.LastOrDefault(message => message.Type == "tool" &&
                (role == "viewport" || message.State == "running"));
            if (tool is not null)
            {
                if (role == "tool_input") tool.Detail = text;
                else if (role == "viewport") tool.Image = text;
                else
                {
                    tool.Detail += "\n\n" + text;
                    tool.State = text.StartsWith("ERROR", StringComparison.Ordinal) ? "error" : "done";
                }
            }
        }
        else if (role != "计划")
            chat.Messages.Add(new PanelMessage(role == "你" ? "user" : "system",
                role == "你" ? "message" : "notice", text));
        if (role is "状态" or "诊断" &&
            chats.TryGetValue(documentSerial, out var current) && ReferenceEquals(chat, current))
            status.Text = text;
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
        using var registration = token.Register(() => Application.Instance.AsyncInvoke(() =>
        {
            if (!dialog.IsDisposed) dialog.Close(false);
        }));
        return dialog.ShowModal(this);
    }

    private bool ConfirmDesignBasis(string note, CancellationToken token)
    {
        status.Text = "等待你确认平面与尺度";
        using var dialog = new Dialog<bool>
        {
            Title = "确认平面与尺度",
            ClientSize = new Size(520, 340),
            Resizable = true,
        };
        var accept = new Button { Text = "按此尺度开始" };
        var revise = new Button { Text = "调整后再开始" };
        accept.Click += (_, _) => dialog.Close(true);
        revise.Click += (_, _) => dialog.Close(false);
        var content = new DynamicLayout { Padding = 16, Spacing = new Size(10, 10) };
        content.AddRow(new Label { Text = "本次将采用以下平面与尺度依据", Font = SystemFonts.Bold(), Wrap = WrapMode.Word });
        content.Add(new TextArea { ReadOnly = true, Wrap = true, Text = note }, xscale: true, yscale: true);
        content.AddRow(new Label { Text = "请核对尺寸和单位。假设尺度经你确认后才会用于建模。", Wrap = WrapMode.Word });
        content.AddSeparateRow(accept, revise);
        dialog.Content = content;
        dialog.DefaultButton = revise;
        dialog.AbortButton = revise;
        using var registration = token.Register(() => Application.Instance.AsyncInvoke(() =>
        {
            if (!dialog.IsDisposed) dialog.Close(false);
        }));
        return dialog.ShowModal(this);
    }

    private string AskUser(JsonObject arguments, CancellationToken token)
    {
        using var dialog = new Dialog<string> { Title = "Magpie 需要你的选择", ClientSize = new Size(580, 420), Resizable = true };
        var questions = UserQuestion.Parse(arguments);
        var answers = new List<Func<string[]>>();
        var response = new TextArea { Height = 70, Wrap = true };
        var validation = new Label { TextColor = Colors.OrangeRed, Wrap = WrapMode.Word };
        var submit = new Button { Text = "提交回答" };
        var cancel = new Button { Text = "取消" };
        cancel.Click += (_, _) => dialog.Close("The user cancelled. Stop and await further instructions.");
        var layout = new DynamicLayout { Padding = 12, Spacing = new Size(8, 8) };
        foreach (var question in questions)
        {
            layout.AddRow(new Label { Text = question.Text, Wrap = WrapMode.Word, Font = SystemFonts.Bold() });
            if (question.AllowMultiple)
            {
                var choices = question.Options.Select(option => new CheckBox { Text = option }).ToList();
                foreach (var choice in choices) layout.AddRow(choice);
                answers.Add(() => choices.Where(choice => choice.Checked == true).Select(choice => choice.Text).ToArray());
            }
            else
            {
                var first = new RadioButton { Text = question.Options[0] };
                var choices = new[] { first }.Concat(question.Options.Skip(1)
                    .Select(option => new RadioButton(first) { Text = option })).ToList();
                foreach (var choice in choices) layout.AddRow(choice);
                answers.Add(() => choices.Where(choice => choice.Checked).Select(choice => choice.Text).ToArray());
            }
        }
        layout.AddRow(new Label { Text = "其他选择或补充说明（可选）" });
        layout.AddRow(response);
        submit.Click += (_, _) =>
        {
            var selections = answers.Select(answer => answer()).ToList();
            if (selections.Any(selection => selection.Length == 0) && string.IsNullOrWhiteSpace(response.Text))
            {
                validation.Text = "请回答每个问题，或在补充说明中填写你的要求。";
                return;
            }
            string text = string.Join("\n\n", questions.Select((question, index) =>
                question.Text + "\n" + (selections[index].Length == 0 ? "见补充说明" : string.Join("、", selections[index]))));
            if (!string.IsNullOrWhiteSpace(response.Text)) text += "\n\n补充说明\n" + response.Text.Trim();
            dialog.Close(text);
        };
        var content = new DynamicLayout { Padding = 8, Spacing = new Size(8, 8) };
        content.Add(new Scrollable { Content = layout, Border = BorderType.None }, xscale: true, yscale: true);
        content.AddRow(validation);
        content.AddSeparateRow(submit, cancel);
        dialog.Content = content;
        dialog.DefaultButton = submit;
        dialog.AbortButton = cancel;
        using var registration = token.Register(() => Application.Instance.AsyncInvoke(() =>
        {
            if (!dialog.IsDisposed) dialog.Close("The user cancelled. Stop and await further instructions.");
        }));
        string? result = dialog.ShowModal(this);
        if (result is null || result.StartsWith("The user cancelled.", StringComparison.Ordinal))
        {
            running?.Cancel();
            return "The user cancelled. Stop and await further instructions.";
        }
        return result;
    }

    private Task<TResult> OnUiAsync<TResult>(Func<TResult> action, CancellationToken token)
    {
        var source = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Application.Instance.AsyncInvoke(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (disposed) throw new OperationCanceledException(token);
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

    private static string ToolTitle(string name) => name switch
    {
        "get_context" => "读取模型环境",
        "list_objects" => "检查几何与对象",
        "run_csharp" => "运行 C# 建模代码",
        "run_python" => "运行 Python 建模代码",
        "get_viewport_image" => "查看当前模型",
        "zoom_to_object" => "调整视口",
        _ => name.Replace('_', ' '),
    };

    private void PublishState()
    {
        if (disposed) return;
        if (RhinoDoc.ActiveDoc is { } active && active.RuntimeSerialNumber == documentSerial)
        {
            documentUnits = FormatUnits(active.ModelUnitSystem);
            unitsKey = active.ModelUnitSystem.ToString();
            documentName = string.IsNullOrWhiteSpace(active.Name) ? "未命名" : active.Name;
        }
        chats.TryGetValue(documentSerial, out var chat);
        web.UpdateState(new
        {
            document = documentName,
            documentId = documentSerial.ToString(System.Globalization.CultureInfo.InvariantCulture),
            units = documentUnits,
            unitsKey,
            status = status.Text,
            busy = running is not null || refreshing is not null,
            canSend = send.Enabled,
            canStop = stop.Enabled,
            draft = prompt.Text,
            autoTools = autoTools.Checked == true,
            architectural = chat?.Architectural ?? true,
            detail = chat?.Detail ?? "developed",
            selectedModel = (models.SelectedValue as ModelChoice)?.Id ?? "",
            models = models.DataStore?.Cast<ModelChoice>().ToList() ?? [],
            catalogLoading = refreshing is not null,
            catalogError,
            messages = chat?.Messages.TakeLast(120).Where(message =>
                message.Type != "message" || message.State == "running" || message.Text.Length > 0).ToList() ?? [],
            plan = chat is not null && (chat.Session.Workflow.Steps.Count > 0 || chat.Session.Workflow.Basis.Length > 0)
                ? chat.Session.Workflow.Snapshot() : null,
            references = chat?.References.Select(reference => new
            {
                reference.Id, reference.Name, reference.Kind,
                width = reference.InUnits(unitsKey).Width,
            }).ToList(),
        });
    }

    private static string FormatUnits(Rhino.UnitSystem? units) => units switch
    {
        Rhino.UnitSystem.Millimeters => "毫米",
        Rhino.UnitSystem.Centimeters => "厘米",
        Rhino.UnitSystem.Meters => "米",
        Rhino.UnitSystem.Feet => "英尺",
        Rhino.UnitSystem.Inches => "英寸",
        null or Rhino.UnitSystem.None or Rhino.UnitSystem.Unset => "单位未确认",
        _ => units.ToString() ?? "",
    };

    private async Task HandleWebActionAsync(JsonObject message)
    {
        string? action = message["action"]?.GetValue<string>();
        if (disposed) return;
        string? sourceDocument = message["documentId"]?.GetValue<string>();
        if (action is not ("ready" or "stop") && !string.IsNullOrEmpty(sourceDocument) &&
            sourceDocument != documentSerial.ToString(System.Globalization.CultureInfo.InvariantCulture)) return;
        chats.TryGetValue(documentSerial, out var chat);
        switch (action)
        {
            case "ready":
                PublishState();
                await RefreshAsync();
                break;
            case "draft": prompt.Text = message["text"]?.GetValue<string>() ?? ""; break;
            case "stop": stop.PerformClick(); break;
            case "refresh": await RefreshAsync(); break;
            case "catalog":
                if (catalogError.Length > 0 || DateTime.UtcNow - catalogRefreshedAt > TimeSpan.FromSeconds(30))
                    await RefreshAsync();
                break;
            case "send":
                if (!send.Enabled) return;
                prompt.Text = message["text"]?.GetValue<string>() ?? "";
                await SendAsync();
                break;
            case "continue":
                if (!send.Enabled) return;
                prompt.Text = "我暂不验收当前成果。请检查实际模型和未完成项，保持已确认的平面和尺度，新增明确的深化待办及验收条件，优先改进空间、开口、立面或构件细节，不能只重复已完成列表。若现有明确条件已满足，而进一步修改需要新的设计取舍，请用 ask_user 让我选择。";
                await SendAsync();
                break;
            case "clear":
                if (clear.Enabled) clear.PerformClick();
                break;
            case "model":
                if (!models.Enabled) return;
                string? selected = message["id"]?.GetValue<string>();
                var choices = models.DataStore.Cast<ModelChoice>().ToList();
                models.SelectedIndex = Math.Max(0, choices.FindIndex(choice => choice.Id == selected));
                break;
            case "options":
                if (running is not null || refreshing is not null || chat is null) return;
                autoTools.Checked = message["autoTools"]?.GetValue<bool>() == true;
                chat.Architectural = message["architectural"]?.GetValue<bool>() != false;
                string? detail = message["detail"]?.GetValue<string>();
                if (detail is "concept" or "developed" or "detailed") chat.Detail = detail;
                break;
            case "accept":
                if (chat is null || running is not null) return;
                chat.Session.Workflow.Accept();
                status.Text = "当前成果已由你验收";
                break;
            case "attach":
                if (running is null && refreshing is null) AttachReference();
                break;
            case "removeReference":
                if (running is null && chat is not null)
                    chat.References.RemoveAll(reference => reference.Id == message["id"]?.GetValue<string>());
                break;
            case "importReference":
                if (running is null && refreshing is null && chat is not null)
                    ImportReference(chat, message["id"]?.GetValue<string>(), message["width"]?.GetValue<double>(),
                        message["units"]?.GetValue<string>());
                break;
        }
        PublishState();
    }

    private void AttachReference()
    {
        if (!chats.TryGetValue(documentSerial, out var chat)) return;
        using var dialog = new OpenFileDialog { Title = "添加平面或参考资料", MultiSelect = false };
        dialog.Filters.Add(new FileFilter("参考图与模型", ".png", ".jpg", ".jpeg", ".3dm", ".dxf", ".dwg", ".pdf"));
        if (dialog.ShowDialog(this) != DialogResult.Ok) return;
        string path = Path.GetFullPath(dialog.FileName);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".3dm" or ".dxf" or ".dwg" or ".pdf"))
            throw new InvalidOperationException("请选择 PNG、JPG、PDF、DXF、DWG 或 3DM 文件。");
        string kind = extension is ".png" or ".jpg" or ".jpeg" ? "image" : "drawing";
        string? image = null;
        if (kind == "image")
        {
            if (new FileInfo(path).Length > 8 * 1024 * 1024)
                throw new InvalidOperationException("参考图超过 8 MB，请先导出较小的清晰图片。");
            image = "data:" + (extension == ".png" ? "image/png" : "image/jpeg") +
                ";base64," + Convert.ToBase64String(File.ReadAllBytes(path));
        }
        chat.References.Add(new ReferenceInput(Guid.NewGuid().ToString("N"), Path.GetFileName(path), kind, path, ImageData: image));
        status.Text = "已添加参考资料；导入前请确认图纸单位与参考长度";
    }

    private void ImportReference(DocumentChat chat, string? id, double? width, string? units)
    {
        var reference = chat.References.FirstOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("参考资料不存在。");
        var doc = RhinoDoc.ActiveDoc;
        if (doc?.RuntimeSerialNumber != documentSerial || doc.UndoRecordingIsActive)
            throw new InvalidOperationException("请先结束当前 Rhino 操作，再导入参考资料。");
        if (reference.Kind == "image" && (width is null or <= 0 || !double.IsFinite(width.Value)))
            throw new InvalidOperationException("请填写图纸实际宽度，数值使用当前 Rhino 文档单位。");
        if (reference.Kind == "image" && units != doc.ModelUnitSystem.ToString())
            throw new InvalidOperationException("文档单位已变化，请重新确认图片实际宽度后再导入。");
        uint undo = doc.BeginUndoRecord("Magpie 参考平面");
        if (undo == 0) throw new InvalidOperationException("Rhino 无法开始参考平面的撤销记录。");
        try
        {
            if (reference.Kind == "image")
            {
                Guid objectId = doc.Objects.AddPictureFrame(Rhino.Geometry.Plane.WorldXY, reference.Path, false, width!.Value, 0, true, true);
                if (objectId == Guid.Empty) throw new InvalidOperationException("Rhino 未能导入参考图片。");
                var attributes = doc.Objects.FindId(objectId).Attributes.Duplicate();
                attributes.Name = "MAGPIE_REFERENCE_" + Path.GetFileNameWithoutExtension(reference.Name);
                doc.Objects.ModifyAttributes(objectId, attributes, true);
                doc.Objects.Lock(objectId, true);
                int index = chat.References.FindIndex(item => item.Id == id);
                chat.References[index] = reference with { Width = width, Units = doc.ModelUnitSystem.ToString() };
            }
            else if (!doc.Import(reference.Path))
                throw new InvalidOperationException("Rhino 未能完成导入，请检查格式和导入选项。");
            doc.Views.Redraw();
            status.Text = "参考平面已导入，请核对一个已知长度后开始建模";
        }
        finally { doc.EndUndoRecord(undo); }
    }
}
