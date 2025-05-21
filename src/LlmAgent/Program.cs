using System.Globalization;
using System.Text.Json;
using LlmAgent;
using OpenAI;
using OpenAI.Chat;

var arg = Arguments.Parse(args);
if (arg is null) return -1;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    if (!cts.IsCancellationRequested)
    {
        e.Cancel = true;
    }
    cts.Cancel();
};

var client = new OpenAIClient(
    new(arg.ApiKey),
    new()
    {
        Endpoint = arg.ApiUrl,
        UserAgentApplicationId = "nike4613/llmagent"
    });

var chat = client.GetChatClient(arg.Model);

var jsonCtx = new JsonContext();

var agent = new AgentLoop(chat, jsonCtx, $"Working directory: {Environment.CurrentDirectory}");

agent.AddTool("sum_int", "Add two integers and return the result.",
    jsonCtx.ArithmeticArgs,
    jsonCtx.Int32,
    (args, ct) =>
    {
        return new(args.Lhs + args.Rhs);
    });

agent.OnMessage += (message) =>
{
    switch (message)
    {
        case SystemChatMessage sys:
            Console.WriteLine($"System: {sys.Content[0].Text}");
            break;
        case UserChatMessage usr:
            Console.WriteLine($"User: {usr.Content[0].Text}");
            break;
        case AssistantChatMessage ass:
            Console.WriteLine($"Assistant: {ass.Content[0].Text}");
            break;
        case ToolChatMessage tool:
            Console.WriteLine($"Tool: {tool.Content[0].Text}");
            break;
    }
};

await agent.Run(arg.Prompt, cts.Token).ConfigureAwait(false);

return 0;
