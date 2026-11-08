using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using RhinoMagpie;
using RhinoMagpie.Agent;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
    passed++;
}

if (args.FirstOrDefault() == "--live")
{
    string endpoint = args[1];
    string prompt = File.ReadAllText(args[2]);
    var config = MagpieGateway.ReadConfig();
    if (args.Length > 3) config = config with { Model = args[3] };
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromMinutes(8),
    };
    using var mcpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    Console.WriteLine("MODEL " + config.Model);
    await new ModelingSession().RunAsync(prompt, config, new MagpieGateway(client),
        new RhinoMcpClient(mcpClient, new Uri(endpoint)),
        (_, _) => Task.FromResult(true), (_, _) => Task.FromResult("Use the explicitly requested test dimensions."),
        (role, text) => Console.WriteLine(role + "\n" + text), CancellationToken.None);
    return;
}

var fixtureConfig = new BridgeConfig("group/custom-routing", "http://localhost:3425", "fixture-secret");
var handler = new FixtureHandler();
using var http = new HttpClient(handler);
var gateway = new MagpieGateway(http);
var mcp = new RhinoMcpClient(http, new Uri("http://localhost:10500/agent"));
var session = new ModelingSession();
var approvals = new List<ToolInvocation>();
await session.RunAsync("Build test geometry", fixtureConfig, gateway, mcp,
    (call, _) => { approvals.Add(call); return Task.FromResult(true); },
    (_, _) => Task.FromResult("answer"), (_, _) => { }, CancellationToken.None);
Check(handler.Models.All(model => model == "group/custom-routing"), "group ID preserved for every request");
Check(handler.Authorization.All(value => value == "Bearer fixture-secret"), "gateway authorization is sent");
Check(handler.Executed.SequenceEqual(new[] { "get_viewport_image", "run_python" }), "MCP tools execute sequentially");
Check(approvals.Count == 2 && approvals[0].ReadOnly && !approvals[1].ReadOnly, "MCP annotation controls approval");
var continuation = handler.ChatRequests[1]["messages"]!.AsArray();
Check(continuation[3]!["role"]!.GetValue<string>() == "tool" &&
      continuation[4]!["role"]!.GetValue<string>() == "tool" &&
      continuation[5]!["role"]!.GetValue<string>() == "user", "all tool responses precede image content");
Check(continuation[5]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>().StartsWith("data:image/png;base64,"),
    "viewport image forwarded to model");
Check(handler.ChatRequests[0]["tools"]!.AsArray().All(tool =>
    ModelingSession.IsAllowedTool(tool!["function"]!["name"]!.GetValue<string>())), "internal and close tools excluded");

handler.FinalOnly = true;
await session.RunAsync("Continue", fixtureConfig with { Model = "auto/new-strategy", ApiKey = "new-fixture-key" },
    gateway, mcp, (_, _) => Task.FromResult(true), (_, _) => Task.FromResult(""),
    (_, _) => { }, CancellationToken.None);
Check(handler.Models.Last() == "auto/new-strategy", "strategy change applies on next turn");
Check(handler.Authorization.Last() == "Bearer new-fixture-key", "rotated credential applies on next turn");
Check(handler.ChatRequests.Last()["messages"]!.AsArray().Count > 6, "conversation history continues");

var deniedHandler = new FixtureHandler();
using var deniedHttp = new HttpClient(deniedHandler);
await new ModelingSession().RunAsync("Build", fixtureConfig, new MagpieGateway(deniedHttp),
    new RhinoMcpClient(deniedHttp, new Uri("http://localhost:10500/agent")),
    (_, _) => Task.FromResult(false), (_, _) => Task.FromResult(""), (_, _) => { }, CancellationToken.None);
Check(deniedHandler.Executed.Count == 0, "declined tools do not reach Rhino");
Check(deniedHandler.ChatRequests[1].ToJsonString().Contains("user declined"), "denial is returned to the model");

using var cancellation = new CancellationTokenSource();
var cancelHandler = new FixtureHandler { AfterTool = () => cancellation.Cancel() };
using var cancelHttp = new HttpClient(cancelHandler);
var cancelSession = new ModelingSession();
try
{
    await cancelSession.RunAsync("Build", fixtureConfig, new MagpieGateway(cancelHttp),
        new RhinoMcpClient(cancelHttp, new Uri("http://localhost:10500/agent")),
        (_, _) => Task.FromResult(true), (_, _) => Task.FromResult(""), (_, _) => { }, cancellation.Token);
    throw new Exception("Cancellation ignored");
}
catch (OperationCanceledException) { Check(true, "cancellation stops before dispatching the next tool"); }
cancelHandler.FinalOnly = true;
cancelHandler.AfterTool = null;
await cancelSession.RunAsync("Inspect before continuing", fixtureConfig, new MagpieGateway(cancelHttp),
    new RhinoMcpClient(cancelHttp, new Uri("http://localhost:10500/agent")),
    (_, _) => Task.FromResult(true), (_, _) => Task.FromResult(""), (_, _) => { }, CancellationToken.None);
var resumed = cancelHandler.ChatRequests.Last()["messages"]!.AsArray();
Check(resumed.Count(message => message?["role"]?.GetValue<string>() == "tool") == 2,
    "interrupted turn retains a result for every pending tool call");
Check(resumed.ToJsonString().Contains("Turn interrupted"), "interruption is recorded for the model");

var errorHandler = new FixtureHandler { FailGateway = true };
using var errorHttp = new HttpClient(errorHandler);
try
{
    await new MagpieGateway(errorHttp).CompleteAsync(fixtureConfig, [], [], CancellationToken.None);
    throw new Exception("HTTP error ignored");
}
catch (InvalidOperationException error)
{
    Check(error.Message.Contains("401") && !error.Message.Contains("fixture-secret"),
        "gateway HTTP failure is reported without echoing its body or credential");
}
var brokenHandler = new FixtureHandler { FailTool = true };
using var brokenHttp = new HttpClient(brokenHandler);
var brokenSession = new ModelingSession();
for (int attempt = 0; attempt < 2; attempt++)
{
    try
    {
        await brokenSession.RunAsync("Build", fixtureConfig, new MagpieGateway(brokenHttp),
            new RhinoMcpClient(brokenHttp, new Uri("http://localhost:10500/agent")),
            (_, _) => Task.FromResult(true), (_, _) => Task.FromResult(""), (_, _) => { }, CancellationToken.None);
        throw new Exception("Unconfirmed tool completion was ignored");
    }
    catch (UnconfirmedToolException)
    {
        Check(brokenSession.ToolCompletionUnknown, "unconfirmed tool completion blocks the session");
    }
}
Check(brokenHandler.ChatRequests.Count == 1, "blocked session cannot dispatch a retry or another model request");
Console.WriteLine($"{passed} agent checks passed");

sealed class FixtureHandler : HttpMessageHandler
{
    public List<string> Models { get; } = [];
    public List<string> Authorization { get; } = [];
    public List<JsonObject> ChatRequests { get; } = [];
    public List<string> Executed { get; } = [];
    public bool FinalOnly { get; set; }
    public bool FailGateway { get; set; }
    public bool FailTool { get; set; }
    public Action? AfterTool { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!.AsObject();
        if (request.RequestUri!.AbsolutePath == "/v1/chat/completions")
        {
            Models.Add(body["model"]!.GetValue<string>());
            Authorization.Add(request.Headers.Authorization!.ToString());
            ChatRequests.Add(body);
            if (FailGateway)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("fixture-secret") };
            JsonObject message = ChatRequests.Count > 1 || FinalOnly
                ? new() { ["role"] = "assistant", ["content"] = "Verified." }
                : JsonNode.Parse("""
                {"role":"assistant","content":null,"tool_calls":[
                  {"id":"image","type":"function","function":{"name":"get_viewport_image","arguments":"{}"}},
                  {"id":"build","type":"function","function":{"name":"run_python","arguments":"{\"script\":\"test\"}"}}
                ]}
                """)!.AsObject();
            return Response(new JsonObject
            {
                ["choices"] = new JsonArray(new JsonObject { ["message"] = message, ["finish_reason"] = "stop" }),
            });
        }
        string method = body["method"]!.GetValue<string>();
        if (method == "notifications/initialized") return new HttpResponseMessage(HttpStatusCode.NoContent);
        JsonObject result;
        if (method == "initialize")
            result = new JsonObject { ["protocolVersion"] = "2025-03-26" };
        else if (method == "tools/list")
        {
            var tools = new JsonArray();
            foreach (string name in new[] { "get_viewport_image", "run_python", "_router_quit_app", "close_doc" })
                tools.Add(new JsonObject
                {
                    ["name"] = name, ["description"] = name,
                    ["inputSchema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
                    ["annotations"] = new JsonObject { ["readOnlyHint"] = name == "get_viewport_image" },
                });
            result = new JsonObject { ["tools"] = tools };
        }
        else
        {
            string name = body["params"]!["name"]!.GetValue<string>();
            Executed.Add(name);
            if (FailTool) throw new HttpRequestException("Fixture connection lost after dispatch.");
            AfterTool?.Invoke();
            result = new JsonObject
            {
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "Done" }),
            };
            if (name == "get_viewport_image")
                result["content"]!.AsArray().Add(new JsonObject
                {
                    ["type"] = "image", ["mimeType"] = "image/png", ["data"] = "iVBORw==",
                });
        }
        return Response(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = body["id"]!.DeepClone(), ["result"] = result });
    }

    private static HttpResponseMessage Response(JsonObject body) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(body),
    };
}
