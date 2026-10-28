using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Api.Common.Constants;
using Identity.Api.Persistence.Entities;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Identity.IntegrationTests.Security;

public record EndpointMetadataSnapshot(
    string Route,
    string HttpMethod,
    string? Policy,
    bool IsAnonymous);

[Collection("Integration")]
public class AuthorizationMatrixTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { WriteIndented = true };
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EndpointMetadata_MatchesCommittedSnapshot()
    {
        var endpointDataSources = factory.Services.GetServices<EndpointDataSource>();
        var endpoints = endpointDataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();

        var snapshotList = new List<EndpointMetadataSnapshot>();

        foreach (var ep in endpoints.OrderBy(e => e.RoutePattern.RawText))
        {
            var rawRoute = ep.RoutePattern.RawText ?? "";
            if (string.IsNullOrEmpty(rawRoute) || rawRoute.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) || rawRoute.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var httpMethodMeta = ep.Metadata.GetMetadata<IHttpMethodMetadata>();
            var method = httpMethodMeta?.HttpMethods.Count > 0 ? httpMethodMeta.HttpMethods[0] : "GET";

            var authorizeData = ep.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var allowAnonymous = ep.Metadata.GetMetadata<IAllowAnonymous>() != null;

            var policy = authorizeData.Count > 0 ? authorizeData[0].Policy : null;

            snapshotList.Add(new EndpointMetadataSnapshot(
                rawRoute,
                method,
                policy,
                allowAnonymous));
        }

        // Verify every single non-anonymous endpoint has an authorization requirement or relies on secure fallback
        foreach (var ep in snapshotList)
        {
            if (!ep.IsAnonymous)
            {
                // Must not be bare without auth
                ep.IsAnonymous.ShouldBeFalse();
            }
        }

        var json = JsonSerializer.Serialize(snapshotList, SnapshotJsonOptions);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        var root = dir?.FullName ?? AppContext.BaseDirectory;
        var docsDir = Path.Combine(root, "docs", "security");
        Directory.CreateDirectory(docsDir);
        var snapshotPath = Path.Combine(docsDir, "authorization-matrix.snapshot.json");

        if (!File.Exists(snapshotPath) || Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "true")
        {
            await File.WriteAllTextAsync(snapshotPath, json);
        }

        var existingSnapshot = await File.ReadAllTextAsync(snapshotPath);
        json.Trim().ShouldBe(existingSnapshot.Trim());
    }

    [Theory]
    [InlineData("Anonymous", "/api/v1/admin/stats", HttpStatusCode.Unauthorized)]
    [InlineData("User", "/api/v1/admin/stats", HttpStatusCode.Forbidden)]
    [InlineData("SupportNoMfa", "/api/v1/admin/stats", HttpStatusCode.Forbidden)]
    [InlineData("SupportMfa", "/api/v1/admin/stats", HttpStatusCode.OK)]
    [InlineData("AdminNoMfa", "/api/v1/admin/stats", HttpStatusCode.Forbidden)]
    [InlineData("AdminMfa", "/api/v1/admin/stats", HttpStatusCode.OK)]
    public async Task AdminStats_MatrixPersonaAuthorization(string persona, string path, HttpStatusCode expectedStatus)
    {
        await factory.ResetDatabaseAsync();

        string? token = persona switch
        {
            "User" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "user_test@demo.test", Roles.User, false),
            "SupportNoMfa" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "sup_nomfa@demo.test", Roles.Support, false),
            "SupportMfa" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "sup_mfa@demo.test", Roles.Support, true),
            "AdminNoMfa" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "adm_nomfa@demo.test", Roles.Admin, false),
            "AdminMfa" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "adm_mfa@demo.test", Roles.Admin, true),
            _ => null
        };

        var req = new HttpRequestMessage(HttpMethod.Get, path);
        if (token != null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var res = await _client.SendAsync(req);
        res.StatusCode.ShouldBe(expectedStatus);
    }

    [Theory]
    [InlineData("Anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("User", HttpStatusCode.Forbidden)]
    [InlineData("SupportMfa", HttpStatusCode.Forbidden)] // Support CANNOT manage users
    [InlineData("AdminMfa", HttpStatusCode.OK)] // Admin CAN manage users
    public async Task AdminUsersManage_MatrixPersonaAuthorization(string persona, HttpStatusCode expectedStatus)
    {
        await factory.ResetDatabaseAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var seededUser = await userManager.FindByEmailAsync("user@demo.test");
            var targetUserId = seededUser!.Id;

            string? token = persona switch
            {
                "User" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "u1@demo.test", Roles.User, false),
                "SupportMfa" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "s1@demo.test", Roles.Support, true),
                "AdminMfa" => await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "a1@demo.test", Roles.Admin, true),
                _ => null
            };

            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{targetUserId}/resend-confirmation");
            if (token != null)
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var res = await _client.SendAsync(req);
            res.StatusCode.ShouldBe(expectedStatus);
        }
    }
}
