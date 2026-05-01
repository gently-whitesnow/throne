using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Instructions;
using Throne.Application.Intents;

namespace Throne.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CreateIntentHandler>();
        services.AddSingleton<GetIntentHandler>();
        services.AddSingleton<ReadIntentTextHandler>();
        services.AddSingleton<ReplaceIntentTextHandler>();
        services.AddSingleton<SearchIntentTextHandler>();
        services.AddSingleton<InsertIntentTextAfterLineHandler>();
        services.AddSingleton<AddIntentQaHandler>();
        services.AddSingleton<AddIntentReviewHandler>();
        services.AddSingleton<ListIntentsHandler>();
        services.AddSingleton<GetInstructionBundleHandler>();
        services.AddSingleton<EnsureSeedInstructionsHandler>();
        services.AddSingleton<ListInstructionsHandler>();
        services.AddSingleton<GetInstructionHandler>();
        return services;
    }
}
