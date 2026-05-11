namespace Throne.Application.Errors;

public sealed class ApiException : Exception
{
    public ApiException(string code, string detail, IReadOnlyDictionary<string, object?>? extensions = null)
        : base(detail)
    {
        Code = code;
        Detail = detail;
        Extensions = extensions ?? new Dictionary<string, object?>();
    }

    public string Code { get; }
    public string Detail { get; }
    public IReadOnlyDictionary<string, object?> Extensions { get; }
}
