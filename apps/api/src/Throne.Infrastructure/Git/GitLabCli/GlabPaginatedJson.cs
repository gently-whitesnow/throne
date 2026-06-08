using System.Text;
using System.Text.Json;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Parses the output of <c>glab api … --paginate</c>, which concatenates each
/// page's JSON array back-to-back with no delimiter: <c>[…][…][…]</c>.
/// </summary>
/// <remarks>
/// A naïve <c>"][" → ","</c> string replace corrupts any payload whose own
/// string content contains the substring <c>][</c> (e.g. a comment body or a
/// repository description), producing invalid JSON. Instead we walk the token
/// stream with a forward-only <see cref="Utf8JsonReader"/> — which, unlike
/// <see cref="JsonDocument.Parse(string,JsonDocumentOptions)"/>, does not
/// enforce a single top-level value — and project every element of every
/// top-level array via the caller's projector, fully inside the
/// <see cref="JsonDocument"/> scope.
/// </remarks>
internal static class GlabPaginatedJson
{
    /// <summary>
    /// Invokes <paramref name="onElement"/> for each element of every top-level
    /// array in <paramref name="json"/>. Throws <see cref="FormatException"/>
    /// when the payload contains a top-level value that is not an array
    /// (mirrors the per-parser "non-array JSON payload" guard).
    /// </summary>
    public static void ForEachElement(string json, Action<JsonElement> onElement)
    {
        ArgumentNullException.ThrowIfNull(onElement);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        // AllowMultipleValues lets the reader walk past the first top-level
        // array into the next one (glab concatenates pages as [..][..]); without
        // it the reader rejects anything after the first complete JSON value.
        var reader = new Utf8JsonReader(
            Encoding.UTF8.GetBytes(json),
            new JsonReaderOptions { AllowMultipleValues = true });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new FormatException("glab api returned non-array JSON payload.");
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                onElement(element);
            }
        }
    }
}
