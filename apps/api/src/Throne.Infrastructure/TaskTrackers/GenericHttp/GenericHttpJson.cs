using System.Text.Json;

namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal static class GenericHttpJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
