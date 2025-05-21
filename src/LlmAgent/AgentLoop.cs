using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Json.Schema;
using OpenAI.Chat;

namespace LlmAgent;

internal sealed class AgentLoop
{
    private readonly ChatClient client;
    private readonly JsonContext ctx;
    private readonly ChatCompletionOptions options;
    private readonly Dictionary<string, ToolDefinition> tools = new();
    public List<ChatMessage> Messages { get; } = new();

    private readonly EvaluationOptions schemaEvalOpts = new()
    {
        AddAnnotationForUnknownKeywords = true,
        OutputFormat = OutputFormat.List,
    };

    private readonly JsonSchemaExporterOptions schemaExportOpts = new()
    {

    };

    public AgentLoop(ChatClient client, JsonContext ctx, string systemExtra = "")
    {
        this.client = client;
        this.ctx = ctx;
        options = new()
        {

        };

        Messages.Add(new SystemChatMessage(Constants.SystemPrompt.Replace("{{SYS_EXTRA}}", systemExtra, StringComparison.Ordinal)));
    }

    public delegate ValueTask<TResult> ToolFunction<TArguments, TResult>(TArguments args, CancellationToken cancellationToken);

    private sealed record ToolDefinition(ChatTool Tool, JsonSchema Schema, ToolFunction<JsonDocument, string> Invoke);

    public void AddMissingToolsFrom(AgentLoop other)
    {
        foreach (var (name, def) in other.tools)
        {
            if (tools.TryAdd(name, def))
            {
                options.Tools.Add(def.Tool);
            }
        }
    }

    public bool RemoveTool(string name)
    {
        if (tools.Remove(name, out var def))
        {
            _ = options.Tools.Remove(def.Tool);
            return true;
        }
        return false;
    }

    public void AddTool(string name, string description, JsonSchema paramSchema, ToolFunction<JsonDocument, string> invoke)
    {
        using var mstream = new MemoryStream();
        JsonSerializer.Serialize(mstream, paramSchema, ctx.JsonSchema);
        mstream.Position = 0;
        var tool = ChatTool.CreateFunctionTool(name, description, BinaryData.FromStream(mstream), functionSchemaIsStrict: false);
        options.Tools.Add(tool);
        tools.Add(name, new(tool, paramSchema, invoke));
    }

    public void AddTool(string name, string description, JsonTypeInfo paramType, ToolFunction<JsonDocument, string> invoke)
    {
        AddTool(name, description,
            JsonSerializer.Deserialize(
                JsonSchemaExporter.GetJsonSchemaAsNode(paramType, schemaExportOpts),
                ctx.JsonSchema)!,
            invoke);
    }

    public void AddTool<T>(string name, string description, JsonTypeInfo<T> paramType, ToolFunction<T, string> invoke)
    {
        AddTool(name, description,
            paramType,
            (doc, ct) => invoke(JsonSerializer.Deserialize(doc, paramType)!, ct));
    }

    public void AddTool<T, R>(string name, string description, JsonTypeInfo<T> paramType, JsonTypeInfo<R> resultType, ToolFunction<T, R> invoke)
    {
        AddTool(name, description,
            paramType,
            async (doc, ct) => JsonSerializer.Serialize(await invoke(JsonSerializer.Deserialize(doc, paramType)!, ct).ConfigureAwait(false), resultType));
    }

    public event Action<ChatMessage>? OnMessage;

    private void AddMessage(ChatMessage msg)
    {
        OnMessage?.Invoke(msg);
        Messages.Add(msg);
    }

    public async Task Run(string prompt, CancellationToken cancellationToken = default)
    {
        AddMessage(new UserChatMessage(prompt));

        while (true)
        {
            var response = await client.CompleteChatAsync(Messages, options, cancellationToken).ConfigureAwait(false);
            var completion = response.Value;

            AddMessage(new AssistantChatMessage(completion));

            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    // we're done
                    return;

                case ChatFinishReason.ToolCalls:
                    foreach (var tool in completion.ToolCalls)
                    {
                        if (!tools.TryGetValue(tool.FunctionName, out var tt))
                        {
                            AddMessage(new ToolChatMessage(tool.Id, $"Tool '{tool.FunctionName}' does not exist"));
                            continue;
                        }

                        var (ctool, schema, invoke) = tt;

                        JsonDocument paramDoc;
                        try
                        {
                            paramDoc = JsonDocument.Parse(tool.FunctionArguments.ToMemory(), new() { AllowTrailingCommas = true });
                        }
                        catch (Exception e)
                        {
                            AddMessage(new ToolChatMessage(tool.Id, $"Arguments is invalid JSON: {e}"));
                            continue;
                        }

                        var evaluation = schema.Evaluate(paramDoc, schemaEvalOpts);
                        if (evaluation.HasErrors)
                        {
                            // the argument block didn't follow the schema
                            var msg = "Arguments does not follow the schema. Here are the evaluation results: ";
                            msg += JsonSerializer.Serialize(evaluation, ctx.EvaluationResults);
                            AddMessage(new ToolChatMessage(tool.Id, msg));
                            continue;
                        }

                        // schema matched, OK to evaluate tool
                        string result;
                        try
                        {
                            result = await invoke(paramDoc, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            AddMessage(new ToolChatMessage(tool.Id, content: $"Exception while running tool: {e}"));
                            continue;
                        }

                        AddMessage(new ToolChatMessage(tool.Id, $"Tool output: {result}"));
                    }
                    break;

                case ChatFinishReason.FunctionCall:
                    throw new InvalidOperationException("Function calls not supported");

                case ChatFinishReason.ContentFilter:
                    throw new InvalidOperationException("Content filtered");
                case ChatFinishReason.Length:
                    throw new InvalidOperationException("Ran out of tokens");
                default: break;
            }
        }
    }
}
