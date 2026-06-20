namespace Throne.Application.Terminals;

public interface INativeInitialPromptSubmitter
{
    Task SubmitInitialPromptAsync(
        string intentId,
        string workspacePath,
        string userPrompt,
        CancellationToken ct);
}
