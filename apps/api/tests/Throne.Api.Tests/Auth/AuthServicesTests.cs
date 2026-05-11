using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
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

    [Fact(DisplayName = "AddThroneAuth с Auth:Mode=Jwt регистрирует JwtBearer и FallbackPolicy")]
    public async Task Jwt_mode_registers_jwt_bearer_and_fallback_policy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Jwt",
                ["Auth:Authority"] = "https://authority.example.test",
                ["Auth:Issuer"] = "https://authority.example.test",
                ["Auth:Audience"] = "throne",
            })
            .Build();

        services.AddThroneAuth(configuration);

        await using var provider = services.BuildServiceProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var jwt = await schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);
        jwt.Should().NotBeNull();

        var authzOptions = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;
        authzOptions.FallbackPolicy.Should().NotBeNull();
        authzOptions.FallbackPolicy!.Requirements.Should().Contain(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact(DisplayName = "Auth:Mode=Jwt регистрирует HttpContextCurrentUserAccessor — userId читается из claim sub")]
    public void Jwt_mode_resolves_user_id_from_sub_claim()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Jwt",
                ["Auth:Authority"] = "https://authority.example.test",
                ["Auth:Issuer"] = "https://authority.example.test",
                ["Auth:Audience"] = "throne",
            })
            .Build();

        services.AddThroneAuth(configuration);

        using var provider = services.BuildServiceProvider();

        var ctxAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(AuthOptions.UserIdClaim, "user-42"),
                new System.Security.Claims.Claim("email", "ignored@example.test"),
            }, authenticationType: "Test"));
        ctxAccessor.HttpContext = ctx;

        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();

        accessor.UserId.Should().Be("user-42");
    }

    [Fact(DisplayName = "HttpContextCurrentUserAccessor бросает, если claim sub отсутствует")]
    public void Jwt_mode_throws_when_sub_claim_missing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Jwt",
                ["Auth:Authority"] = "https://authority.example.test",
                ["Auth:Issuer"] = "https://authority.example.test",
                ["Auth:Audience"] = "throne",
            })
            .Build();

        services.AddThroneAuth(configuration);

        using var provider = services.BuildServiceProvider();

        var ctxAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("email", "no-sub@example.test"),
            }, authenticationType: "Test"));
        ctxAccessor.HttpContext = ctx;

        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();

        var act = () => accessor.UserId;
        act.Should().Throw<InvalidOperationException>().WithMessage("*sub*");
    }

}
