using Microsoft.AspNetCore.Mvc.ModelBinding;
using IntentsFileParameter = Throne.Api.Generated.FileParameter;

namespace Throne.Api.Intents;

/// <summary>
/// Satisfies binding for NSwag-generated <c>FileParameter</c> placeholders
/// without going through the default complex-type binder (which cannot
/// instantiate that type). Actual bytes are read from
/// <see cref="Microsoft.AspNetCore.Http.HttpRequest.Form"/> inside each action.
/// </summary>
internal sealed class FileParameterModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ModelType == typeof(IntentsFileParameter))
        {
            context.Result = ModelBindingResult.Success(new IntentsFileParameter(Stream.Null, null, null));
        }
        return Task.CompletedTask;
    }
}

internal sealed class FileParameterModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var t = context.Metadata.ModelType;
        return t == typeof(IntentsFileParameter)
            ? new FileParameterModelBinder()
            : null;
    }
}
