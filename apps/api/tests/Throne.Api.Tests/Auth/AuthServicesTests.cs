using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Auth;
using Throne.Application.Auth;

namespace Throne.Api.Tests.Auth;

public class AuthServicesTests
{
    [Fact(DisplayName = "AddThroneAuth по умолчанию регистрирует LocalDevCurrentUserAccessor с userId=local-dev")]
    public void Default_mode_is_disabled_with_local_dev_user()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddThroneAuth(configuration);

        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<ICurrentUserAccessor>();

        accessor.UserId.Should().Be(CurrentUserIds.LocalDev);
    }

    [Fact(DisplayName = "AddThroneAuth с Auth:Mode=Disabled подставляет local-dev")]
    public void Explicit_disabled_uses_local_dev()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Disabled",
            })
            .Build();

        services.AddThroneAuth(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICurrentUserAccessor>().UserId.Should().Be(CurrentUserIds.LocalDev);
    }

    [Fact(DisplayName = "AddThroneAuth с Auth:Mode=Jwt бросает NotSupportedException — JWT не подключен")]
    public void Jwt_mode_not_supported_yet()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Jwt",
            })
            .Build();

        var act = () => services.AddThroneAuth(configuration);

        act.Should().Throw<NotSupportedException>();
    }
}
