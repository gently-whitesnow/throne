using System.Text;
using Tomlyn;
using Tomlyn.Syntax;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Pure transform over codex's <c>~/.codex/config.toml</c>: ensure
/// <c>[projects."&lt;path&gt;"] trust_level = "trusted"</c> while preserving every other key,
/// comment and the existing formatting. Kept I/O-free so the merge semantics (the part that could
/// corrupt the operator's global codex config) are fully unit-testable; <see cref="CodexTrustSeeder"/>
/// wraps it with the file read/write.
///
/// We parse with Tomlyn to (a) refuse to touch a config we cannot parse and (b) reliably detect
/// whether the workspace is already trusted, but the common "add a new project" path is a plain
/// text append of a fresh table — it leaves the rest of the file byte-for-byte and dodges the
/// trivia bookkeeping a full syntax-tree rewrite would need.
/// </summary>
internal static class CodexTrustDocument
{
    private const string ProjectsKey = "projects";
    private const string TrustKey = "trust_level";
    private const string TrustedValue = "trusted";

    /// <summary>
    /// Returns the updated config text when <paramref name="workspacePath"/> needed trusting, or
    /// <c>null</c> when no write is warranted: already trusted, or the existing document is shaped
    /// in a way we refuse to clobber (unparseable TOML, or a top-level <c>projects</c> key that is
    /// not a table). <paramref name="existingToml"/> may be <c>null</c>/empty for a not-yet-created
    /// file — treated as an empty document.
    /// </summary>
    public static string? WithTrustedWorkspace(string? existingToml, string workspacePath)
    {
        var baseText = existingToml ?? string.Empty;

        var doc = Toml.Parse(baseText);
        if (doc.HasErrors)
        {
            return null;
        }

        var projectTable = FindProjectTable(doc, workspacePath);
        if (projectTable is not null)
        {
            var trust = FindTrustValue(projectTable);
            if (trust is not null && IsTrusted(trust))
            {
                return null;
            }

            return FlipToTrusted(doc, projectTable, trust);
        }

        // A top-level `projects = ...` scalar would clash with a `[projects."path"]` table header,
        // producing invalid TOML — refuse rather than corrupt the file.
        if (HasTopLevelProjectsKey(doc))
        {
            return null;
        }

        return Append(baseText, workspacePath);
    }

    private static TableSyntax? FindProjectTable(DocumentSyntax doc, string workspacePath)
    {
        var expected = new KeySyntax(ProjectsKey, workspacePath).ToString().Trim();
        foreach (var table in doc.Tables)
        {
            if (table is TableSyntax t && t.Name?.ToString().Trim() == expected)
            {
                return t;
            }
        }

        return null;
    }

    private static KeyValueSyntax? FindTrustValue(TableSyntax table)
    {
        foreach (var item in table.Items)
        {
            if (item.Key?.ToString().Trim() == TrustKey)
            {
                return item;
            }
        }

        return null;
    }

    private static bool IsTrusted(KeyValueSyntax trust) =>
        trust.Value is StringValueSyntax s && s.Value == TrustedValue;

    private static string FlipToTrusted(DocumentSyntax doc, TableSyntax table, KeyValueSyntax? trust)
    {
        if (trust is not null)
        {
            trust.Value = new StringValueSyntax(TrustedValue);
        }
        else
        {
            table.Items.Add(new KeyValueSyntax(TrustKey, new StringValueSyntax(TrustedValue)));
        }

        return doc.ToString();
    }

    private static bool HasTopLevelProjectsKey(DocumentSyntax doc)
    {
        foreach (var kv in doc.KeyValues)
        {
            if (kv.Key?.ToString().Trim() == ProjectsKey)
            {
                return true;
            }
        }

        return false;
    }

    private static string Append(string baseText, string workspacePath)
    {
        var block = $"[{ProjectsKey}.{QuoteKey(workspacePath)}]\n{TrustKey} = \"{TrustedValue}\"\n";
        if (baseText.Length == 0)
        {
            return block;
        }

        var newline = baseText.EndsWith('\n') ? string.Empty : "\n";
        return baseText + newline + "\n" + block;
    }

    private static string QuoteKey(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            if (ch is '"' or '\\')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }
        sb.Append('"');
        return sb.ToString();
    }
}
