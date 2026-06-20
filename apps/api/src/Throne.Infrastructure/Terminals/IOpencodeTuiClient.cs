namespace Throne.Infrastructure.Terminals;

internal interface IOpencodeTuiClient
{
    Task SubmitInitialPromptAsync(
        Uri endpoint,
        string workspacePath,
        string prompt,
        CancellationToken ct);
}
