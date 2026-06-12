namespace Throne.Application.Terminals;

public interface IClaudeSessionSettingsWriter
{
    Task<string> WriteAsync(string intentId, string workspacePath, CancellationToken ct);
}
