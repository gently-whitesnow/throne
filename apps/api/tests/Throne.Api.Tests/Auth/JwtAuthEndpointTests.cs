using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Auth;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class JwtAuthEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private const string Issuer = "https://issuer.test/";
    private const string Audience = "throne";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RSA _otherRsa = RSA.Create(2048);

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private RsaSecurityKey _signingKey = null!;

    public Task InitializeAsync()
    {
        var connectionString = mongo.ConnectionString;
        var dbName = $"throne_jwt_{Guid.NewGuid():N}";

        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "test-key" };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateScopes = false;
                o.ValidateOnBuild = false;
            });
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mongo:ConnectionString"] = connectionString,
                    ["Mongo:Database"] = dbName,
                    ["Auth:Mode"] = "Jwt",
                    ["Auth:Issuer"] = Issuer,
                    ["Auth:Audience"] = Audience,
                    ["Auth:RequireHttpsMetadata"] = "false",
                });
            });
            builder.ConfigureServices(services =>
            {
                // Подменяем JWKS-провайдер: используем in-memory ключ, без обращения к внешнему AS.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    o.Authority = null!;
                    o.MetadataAddress = null!;
                    o.RequireHttpsMetadata = false;
                    o.TokenValidationParameters.IssuerSigningKey = _signingKey;
                    o.TokenValidationParameters.ValidateIssuerSigningKey = true;
                });
            });
        });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        _rsa.Dispose();
        _otherRsa.Dispose();
    }

    [Fact(DisplayName = "Auth:Mode=Jwt: запрос без токена возвращает 401")]
    public async Task Request_without_token_returns_401()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/intents", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Auth:Mode=Jwt: запрос с валидным JWT попадает в endpoint и возвращает 200")]
    public async Task Request_with_valid_token_passes_auth()
    {
        var token = IssueToken("user-42", _rsa, lifetime: TimeSpan.FromMinutes(5));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/intents", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "Auth:Mode=Jwt: запрос с поддельной подписью возвращает 401")]
    public async Task Request_with_forged_signature_returns_401()
    {
        var token = IssueToken("user-42", _otherRsa, lifetime: TimeSpan.FromMinutes(5));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/intents", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Auth:Mode=Jwt: expired JWT возвращает 401")]
    public async Task Expired_token_returns_401()
    {
        var token = IssueToken("user-42", _rsa, lifetime: TimeSpan.FromMinutes(-5), notBeforeOffset: TimeSpan.FromMinutes(-10));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/intents", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Auth:Mode=Jwt: /health открыт (AllowAnonymous), /mcp требует PAT")]
    public async Task Health_is_anonymous_and_mcp_requires_pat()
    {
        var health = await _client.GetAsync(new Uri("/health", UriKind.Relative));
        health.StatusCode.Should().Be(HttpStatusCode.OK);

        // /mcp под Mode=Jwt требует Personal Access Token. Без него — 401
        // (см. ADR-0016 — PAT).
        using var mcp = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative))
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        var mcpResponse = await _client.SendAsync(mcp);
        mcpResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string IssueToken(
        string userId,
        RSA signingRsa,
        TimeSpan lifetime,
        TimeSpan? notBeforeOffset = null)
    {
        var now = DateTime.UtcNow;
        var key = new RsaSecurityKey(signingRsa) { KeyId = "test-key" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[]
            {
                new Claim("sub", userId),
            },
            notBefore: now.Add(notBeforeOffset ?? TimeSpan.FromMinutes(-1)),
            expires: now.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
