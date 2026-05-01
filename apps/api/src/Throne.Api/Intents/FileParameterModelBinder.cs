using Microsoft.AspNetCore.Mvc.ModelBinding;
using Throne.Api.Generated;

namespace Throne.Api.Intents;

/// <summary>
/// Satisfies binding for NSwag-generated <see cref="FileParameter"/> without using the default complex-type binder
/// (which cannot instantiate that type). Actual bytes are read from <see cref="Microsoft.AspNetCore.Http.HttpRequest.Form"/> in the action.
/// </summary>
internal sealed class FileParameterModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ModelType != typeof(FileParameter))
        {
            return Task.CompletedTask;
        }

        context.Result = ModelBindingResult.Success(new FileParameter(Stream.Null, null, null));
        return Task.CompletedTask;
    }
}

internal sealed class FileParameterModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Metadata.ModelType == typeof(FileParameter)
            ? new FileParameterModelBinder()
            : null;
    }
}
