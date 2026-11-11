using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RhinoMagpie.Agent;

public sealed record ModelChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

public sealed class MagpieGateway(HttpClient client)
{
    public const string Version = "0.3.4";

    public static string ConfigPath => Path.Combine(ConfigurationDirectory(), "settings.json");

    public static string EnsureConfigurationDirectory(string? userProfile = null) =>
        Directory.CreateDirectory(ConfigurationDirectory(userProfile)).FullName;

    private static string ConfigurationDirectory(string? userProfile = null) => Path.Combine(
        userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "rhino9-magpie");

    public static BridgeConfig ReadConfig() =>
        File.Exists(ConfigPath) ? BridgeConfig.Parse(File.ReadAllText(ConfigPath))
        : throw new InvalidOperationException("请在 Magpie 的 Agent 页连接「Magpie-Rhino」，选择模型或自定义组，再回到这里发送。");

    public async Task<IReadOnlyList<ModelChoice>> GetModelsAsync(BridgeConfig config, CancellationToken token)
    {
        using var request = Request(config, HttpMethod.Get, "/v1/models");
        var result = await SendAsync(request, token);
        return (result["data"]?.AsArray() ?? throw new InvalidOperationException("Magpie 模型列表无效。"))
            .OfType<JsonObject>()
            .Where(model => model["tool_call"]?.GetValue<bool>() != false)
            .Select(model => new ModelChoice(model["id"]!.GetValue<string>(),
                model["magpie_label"]?.GetValue<string>() ??
                model["display_name"]?.GetValue<string>() ?? model["id"]!.GetValue<string>()))
            .ToList();
    }

    public async Task<JsonObject> CompleteAsync(
        BridgeConfig config, JsonArray messages, JsonArray tools, CancellationToken token,
        Action<string>? onText = null, Action<ChatUsage?>? onUsage = null, bool requestUsage = false)
    {
        var payload = new JsonObject
        {
            ["model"] = config.Model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["stream"] = true,
        };
        if (requestUsage)
            payload["stream_options"] = new JsonObject { ["include_usage"] = true };

        while (true)
        {
            using var request = Request(config, HttpMethod.Post, "/v1/chat/completions");
            request.Content = JsonContent.Create(payload);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            bool streamed = string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream",
                StringComparison.OrdinalIgnoreCase);
            if (requestUsage && !streamed && response.StatusCode == HttpStatusCode.BadRequest &&
                await RejectsUsageOptionAsync(response, token).ConfigureAwait(false))
            {
                requestUsage = false;
                payload = payload.DeepClone().AsObject();
                payload.Remove("stream_options");
                continue;
            }
            EnsureSuccess(response);

            JsonObject message;
            ChatUsage? usage;
            if (streamed)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                (message, usage) = await StreamingChatReader.ReadCompletionAsync(stream, onText, token).ConfigureAwait(false);
            }
            else
            {
                var body = await response.Content.ReadFromJsonAsync<JsonObject>(token).ConfigureAwait(false);
                if (body?["choices"]?[0]?["finish_reason"]?.GetValue<string>() is "length" or "content_filter")
                    throw new InvalidOperationException("模型输出被截断，请缩小任务后重试。");
                message = body?["choices"]?[0]?["message"]?.DeepClone().AsObject()
                    ?? throw new InvalidOperationException("所选模型服务未返回有效的聊天消息。");
                usage = ChatUsage.Parse(body?["usage"]);
            }
            NormalizeMessage(message);
            if (!streamed)
                onText?.Invoke(message["content"]?.GetValue<string>() ?? "");
            onUsage?.Invoke(usage);
            return message;
        }
    }

    private static async Task<bool> RejectsUsageOptionAsync(HttpResponseMessage response, CancellationToken token)
    {
        const string option = """['"`]?(?<![\w.])(?:stream_options(?:\.include_usage)?|include_usage)(?!\w|\.\w|\[)['"`]?""";
        const string rejection = @"not supported|unsupported|unknown|unrecognized|not allowed";
        const string field = "parameter|argument|field|option";
        const string explicitRejection =
            $@"(?:{option}\s+(?:(?:is|are)\s+)?(?:{rejection})\b|\b(?:{rejection})(?:\s+(?:request\s+)?(?:{field})(?:\s+supplied)?)?\s*:?\s+{option})";
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // Retry only a complete, small error response, before exposing any stream content.
        const int maxErrorChars = 8192;
        string errorText;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var reader = new StreamReader(stream, leaveOpen: true);
            var buffer = new char[maxErrorChars + 1];
            int size = await reader.ReadBlockAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (size > maxErrorChars) return false;
            errorText = new string(buffer, 0, size);
        }
        catch (Exception error) when (error is IOException or HttpRequestException)
        {
            return false;
        }

        static string? ReadString(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        try
        {
            if (JsonNode.Parse(errorText) is not JsonObject body ||
                body.ContainsKey("choices") || body.ContainsKey("usage") || body["error"] is not JsonObject error)
                return false;
            string? parameter = ReadString(error["param"]);
            bool namesOption = parameter is "stream_options" or "stream_options.include_usage" or "include_usage";
            if (parameter is not null && !namesOption) return false;
            if (namesOption && ReadString(error["code"]) is
                "unsupported_parameter" or "unknown_parameter" or "unrecognized_parameter" or "unrecognized_request_argument")
                return true;

            var message = ReadString(error["message"]);
            return message is not null &&
                (Regex.IsMatch(message, explicitRejection, options) ||
                    (namesOption && Regex.IsMatch(message.Trim(), $@"^(?:{rejection})(?:\s+(?:{field}))?[.!]?$", options)));
        }
        catch (JsonException)
        {
            return string.Equals(response.Content.Headers.ContentType?.MediaType, "text/plain", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(errorText.Trim(), $@"^{explicitRejection}[.!]?$", options);
        }
        catch (ArgumentException) { return false; }
    }

    private static void NormalizeMessage(JsonObject message)
    {
        if (message["content"] is JsonArray blocks)
            message["content"] = string.Join("\n", blocks.OfType<JsonObject>()
                .Where(block => block["type"]?.GetValue<string>() == "text")
                .Select(block => block["text"]?.GetValue<string>()));
        if (message["content"] is not null &&
            (message["content"] is not JsonValue content || !content.TryGetValue<string>(out _)))
            throw new InvalidOperationException("模型返回了无法识别的消息内容，请检查该模型的工具调用兼容性。");
        if (message["tool_calls"] is not null and not JsonArray)
            throw new InvalidOperationException("模型返回了无效的工具调用列表，尚未执行，请重新发送。");
        if (message["tool_calls"] is JsonArray calls)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var call in calls)
            {
                string? id = call?["id"]?.GetValue<string>();
                string? name = call?["function"]?["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || string.IsNullOrWhiteSpace(name) ||
                    call?["function"]?["arguments"] is not JsonValue arguments ||
                    !arguments.TryGetValue<string>(out _))
                    throw new InvalidOperationException("模型返回了不完整或重复的工具调用，尚未执行，请重新发送。");
            }
        }
        if (message["tool_calls"] is not JsonArray { Count: > 0 } &&
            string.IsNullOrWhiteSpace(message["content"]?.GetValue<string>()))
            throw new InvalidOperationException("模型返回了空消息，请检查 Magpie 中该模型的状态后重试。");
        message["role"] = "assistant";
    }

    private static HttpRequestMessage Request(BridgeConfig config, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, config.BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Headers.UserAgent.ParseAdd("RhinoMagpie/" + Version);
        return request;
    }

    private async Task<JsonObject> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        EnsureSuccess(response);
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(token).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Magpie 返回了空响应。");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Magpie HTTP {(int)response.StatusCode}。请检查 Magpie 的模型、组策略及用量记录。");
    }
}
