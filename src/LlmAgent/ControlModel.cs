namespace LlmAgent;

internal sealed class ControlModel
{
    public string GlobalContext { get; set; } = string.Empty;
    public string StartPrompt { get; set; } = string.Empty;
    public Dictionary<string, PromptModel> Prompts { get; set; } = new();
}

internal sealed class PromptModel
{
    public string Prompt { get; set; } = string.Empty;
    public List<NextPromptModel> Next { get; set; } = new();
}

internal sealed class NextPromptModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
