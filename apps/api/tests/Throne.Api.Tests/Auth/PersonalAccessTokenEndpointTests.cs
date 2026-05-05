using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MongoDb;
using Throne.Me.Contracts.Generated;

namespace Throne.Api.Tests.Auth;

public sealed class PersonalAccessTokenEndpointTests : IAsyncLifetime
{
    private const string Issuer = "https://auth-gate.test/";
    private const string Audience = "throne";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithReplicaSet().Build();
    private readonly RSA _rsa = RSA.Create(2048);

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private RsaSecurityKey _signingKey = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        var raw = _mongo.GetConnectionString();
        var separator = raw.Contains('?') ? '&' : '?';
        var connectionString = $"{raw}{separator}directConnection=true";
        var dbName = $"throne_pat_{Guid.NewGuid():N}";

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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _mongo.DisposeAsync();
        _rsa.Dispose();
    }

    [Fact(DisplayName = "POST /v1/me/mcp-token без JWT возвращает 401")]
    public async Task Post_without_jwt_returns_401()
    {
        var response = await _client.PostAsync(new Uri("/api/v1/me/mcp-token", UriKind.Relative), content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /v1/me/mcp-token c JWT возвращает plaintext-секрет один раз")]
    public async Task Post_with_jwt_issues_secret_once()
    {
        using var post = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/me/mcp-token", UriKind.Relative));
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", IssueJwt("user-pat-1"));
        var response = await _client.SendAsync(post);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<McpTokenIssuedDto>();
        dto.Should().NotBeNull();
        dto!.Token.Should().StartWith("tpat_");
        dto.Last_four.Should().HaveLength(4);
        dto.Token.Should().EndWith(dto.Last_four);
    }

    [Fact(DisplayName = "GET /v1/me/mcp-token возвращает meta без plaintext, has_token=true после POST")]
    public async Task Get_returns_meta_after_post()
    {
        var jwt = IssueJwt("user-pat-2");

        var issued = await IssueTokenAsync(jwt);

        using var get = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/me/mcp-token", UriKind.Relative));
        get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await _client.SendAsync(get);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var meta = await response.Content.ReadFromJsonAsync<McpTokenMetaDto>();
        meta.Should().NotBeNull();
        meta!.Has_token.Should().BeTrue();
        meta.Last_four.Should().Be(issued.Last_four);
    }

    [Fact(DisplayName = "POST /v1/me/mcp-token второй раз инвалидирует старый секрет")]
    public async Task Regeneration_invalidates_previous_secret()
    {
        var jwt = IssueJwt("user-pat-3");

        var first = await IssueTokenAsync(jwt);
        var second = await IssueTokenAsync(jwt);

        first.Token.Should().NotBe(second.Token);

        using var oldRequest = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative));
        oldRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        oldRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
        var oldResponse = await _client.SendAsync(oldRequest);
        oldResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /mcp под Mode=Jwt без PAT возвращает 401")]
    public async Task Mcp_without_pat_is_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /mcp с валидным PAT не возвращает 401 (auth прошёл)")]
    public async Task Mcp_with_valid_pat_passes_auth()
    {
        var jwt = IssueJwt("user-pat-4");
        var issued = await IssueTokenAsync(jwt);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    private async Task<McpTokenIssuedDto> IssueTokenAsync(string jwt)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/me/mcp-token", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<McpTokenIssuedDto>();
        return dto!;
    }

    private string IssueJwt(string userId)
    {
        var now = DateTime.UtcNow;
        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[]
            {
                new Claim("user_id", userId),
                new Claim("sub", "ignored-sub"),
            },
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
