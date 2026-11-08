using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace RhinoMagpie.Agent;

public sealed record ModelChoice(string Id, string Label)
{
    public override string ToString() => Label;
}

public sealed class MagpieGateway(HttpClient client)
{
    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "rhino9-magpie", "settings.json");

    public static BridgeConfig ReadConfig() =>
        File.Exists(ConfigPath) ? BridgeConfig.Parse(File.ReadAllText(ConfigPath))
        : throw new InvalidOperationException("请在 Magpie 安装 Rhino 9 AI 插件，连接并选择模型或自定义组。");

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
        BridgeConfig config, JsonArray messages, JsonArray tools, CancellationToken token)
    {
        using var request = Request(config, HttpMethod.Post, "/v1/chat/completions");
        request.Content = JsonContent.Create(new JsonObject
        {
            ["model"] = config.Model,
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["stream"] = false,
        });
        var response = await SendAsync(request, token);
        if (response["choices"]?[0]?["finish_reason"]?.GetValue<string>() is "length" or "content_filter")
            throw new InvalidOperationException("模型输出被截断，请缩小任务后重试。");
        return response["choices"]?[0]?["message"]?.DeepClone().AsObject()
            ?? throw new InvalidOperationException("Magpie 未返回有效的聊天消息。");
    }

    private static HttpRequestMessage Request(BridgeConfig config, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, config.BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Headers.UserAgent.ParseAdd("RhinoMagpie/0.2.0");
        return request;
    }

    private async Task<JsonObject> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Magpie HTTP {(int)response.StatusCode}。请检查 Magpie 的模型、组策略及用量记录。");
        var result = await response.Content.ReadFromJsonAsync<JsonObject>(token).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Magpie 返回了空响应。");
    }
}
