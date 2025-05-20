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

var options = new ChatCompletionOptions();

var messages = new List<ChatMessage>()
{
    new SystemChatMessage(Constants.SystemPrompt.Replace("{{WORKDIR}}", Environment.CurrentDirectory, StringComparison.Ordinal)),
};

if (arg.Prompt is not null)
{
    messages.Add(new UserChatMessage(arg.Prompt));
}

while (true)
{

    var response = await chat.CompleteChatAsync(
        messages,
        options: options,
        cancellationToken: cts.Token).ConfigureAwait(false);

    var result = response.Value;
    messages.Add(new AssistantChatMessage(response.Value.Content));

    Console.WriteLine($"Assistant: {result.Content[0].Text}");

    Console.Write("User: ");
    if (Console.ReadLine() is { } line)
    {
        messages.Add(new UserChatMessage(line));
        continue;
    }
    else
    {
        break;
    }
}

return 0;
