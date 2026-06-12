using System.Text;

namespace Throne.Application.Terminals;

/// <summary>
/// Renders a TOML basic string for codex <c>-c key=value</c> overrides. The value of a
/// <c>-c</c> token is parsed by codex as TOML, so any string carrying spaces, quotes or
/// newlines (a developer-instructions block, a hook command) must be a quoted, escaped
/// basic string. tmux passes the token to <c>execvp</c> raw — there is no shell layer to
/// strip quoting, so the quotes belong in the token itself.
/// </summary>
public static class CodexConfigValue
{
    public static string ToToml(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(ch); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
