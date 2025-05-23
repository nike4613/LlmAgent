using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using LlmAgent;
using OpenAI;
using OpenAI.Chat;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

var yamlDeserializer = new DeserializerBuilder()
    .WithNamingConvention(PascalCaseNamingConvention.Instance)
    .Build();

ControlModel model;
using (var tr = arg.ControlFile.OpenText())
{
    model = yamlDeserializer.Deserialize<ControlModel>(tr);
}

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

if (model.GlobalContext.Length > 0)
{
    agent.AddMessage(new SystemChatMessage("The following user message is GLOBAL CONTEXT, and should always be considered, regardless of later system instruction."));
    agent.AddMessage(new UserChatMessage(FillVariables(model.GlobalContext, arg.Variables)));
}

const string BeforePromptPreamble = "Any instructions before this point are complete, and should be ignored, unless explicitly instructed otherwise. " +
    "The user's next prompt may rely on context from before this point. All previous system instruction remains in effect.";

var prompt = model.Prompts[model.StartPrompt];
while (prompt is not null)
{
    _ = agent.RemoveTool("select_next_action");

    var evalPrompt = prompt;
    prompt = null;
    if (evalPrompt.Next.Count > 0)
    {
        agent.AddTool("select_next_action",
            $$"""
            Selects the next prompt.

            THIS TOOL MUST ONLY BE USED ONCE, AND ONLY ONCE THE USER'S PROMPT HAS BEEN COMPLETELY FINISHED.
            AFTER THIS TOOL IS USED, YOU MUST BE FINISHED.

            You must select one of the following prompts to continue:
            {{string.Join("\n", evalPrompt.Next.Select(n => $"--- \"{n.Name}\" ---\n{n.Description}"))}}
            ------
            When attempting to use one of the above, make sure the prompt name provided is EXACTLY the name inside the quotes.
            """,
            jsonCtx.ControlArgs,
            (args, ct) =>
            {
                if (!evalPrompt.Next.Any(n => n.Name == args.NextPrompt))
                {
                    throw new ArgumentException($"'{args.NextPrompt}' is not a valid continuation prompt");
                }

                if (!model.Prompts.TryGetValue(args.NextPrompt, out var nextPrompt))
                {
                    throw new ArgumentException($"'{args.NextPrompt}' is not a valid continuation prompt");
                }

                prompt = nextPrompt;
                return new($"Next action set to \"{args.NextPrompt}\"");
            });

        agent.AddMessage(new SystemChatMessage(
            BeforePromptPreamble + " After completing the user's request, make sure to invoke the `select_next_action` tool to select a continuation. " +
            "Only do so AFTER completing the user's request. Remember to consider instruction in `select_next_action` continuation descriptions."));
    }
    else
    {
        agent.AddMessage(new SystemChatMessage(
            BeforePromptPreamble + " After completing the user's request, STOP. There is nothing more for you to do."));
    }

    await agent.Run(FillVariables(evalPrompt.Prompt, arg.Variables), cts.Token).ConfigureAwait(false);
}

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

static string FillVariables(string input, Dictionary<string, string> vars)
{
    var sb = new StringBuilder();

    var lookup = vars.GetAlternateLookup<ReadOnlySpan<char>>();

    var sp = input.AsSpan();
    int offs;
    while ((offs = sp.IndexOf("{{", StringComparison.Ordinal)) >= 0)
    {
        _ = sb.Append(sp.Slice(0, offs));
        sp = sp.Slice(offs + 2);

        var idx = sp.IndexOf("}}", StringComparison.Ordinal);
        if (idx < 0)
        {
            // not actually part of a name, we're done
            _ = sb.Append("{{");
            break;
        }

        var name = sp.Slice(0, idx);
        sp = sp.Slice(idx + 2);

        if (lookup.TryGetValue(name, out var value))
        {
            // we have a replacement
            _ = sb.Append(value);
        }
        else
        {
            // we don't have a replacement, leave it as-is
            _ = sb.Append("{{");
            _ = sb.Append(name);
            _ = sb.Append("}}");
        }
    }

    // any remaining sp should be added verbatim
    _ = sb.Append(sp);

    return sb.ToString();
}
