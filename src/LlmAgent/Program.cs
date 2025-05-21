using System.ClientModel;
using System.ClientModel.Primitives;
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
        UserAgentApplicationId = "nike4613/llmagent",
        RetryPolicy = new ClientRetryPolicy()
    });

var chat = client.GetChatClient(arg.Model);

var jsonCtx = new JsonContext();

using var pwshEval = await PowershellEvaluator.CreateAsync(cts.Token).ConfigureAwait(false);

var agent = new AgentLoop(chat, jsonCtx, $"Working directory: {Environment.CurrentDirectory}");

agent.AddTool("write_file", "Sets the complete content of a file. Paths are ALWAYS relative to the original working directory.",
    jsonCtx.WriteFileArgs,
    async (args, ct) =>
    {
        await File.WriteAllTextAsync(args.File, args.Content, ct).ConfigureAwait(false);
        return "";
    });

pwshEval.RegisterTool(agent, jsonCtx);

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
            Console.WriteLine($"Assistant: {ass.Content[0].Text} {string.Join(" ", ass.ToolCalls.Select(t => $"(tool {t.FunctionName} {t.FunctionArguments})"))}");
            break;
        case ToolChatMessage tool:
            Console.WriteLine($"Tool: {tool.Content[0].Text}");
            break;
    }
};

await agent.Run(arg.Prompt, cts.Token).ConfigureAwait(false);

if (arg.SessionFile is { } sessionFile)
{
    using var fs = File.OpenWrite(sessionFile);
    fs.SetLength(0);
    foreach (var msg in agent.Messages)
    {
        var bc = BinaryContent.Create(msg);
        await bc.WriteToAsync(fs, cts.Token).ConfigureAwait(false);
        await fs.WriteAsync(new byte[] { (byte)'\n' }, cts.Token).ConfigureAwait(false);
    }
}

return 0;
