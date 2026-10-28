using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Identity.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace Identity.IntegrationTests.Security;

[Collection("Integration")]
public class OpenApiSnapshotTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenApiDocument_MatchesCommittedSnapshot()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jsonString = await response.Content.ReadAsStringAsync();
        jsonString.ShouldNotBeNullOrWhiteSpace();

        // Format JSON cleanly
        var node = JsonNode.Parse(jsonString);
        var formatted = node!.ToJsonString(IndentedJsonOptions);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        var root = dir?.FullName ?? AppContext.BaseDirectory;
        var openApiDir = Path.Combine(root, "docs", "openapi");
        Directory.CreateDirectory(openApiDir);
        var snapshotPath = Path.Combine(openApiDir, "identity.v1.json");

        if (!File.Exists(snapshotPath) || Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "true")
        {
            await File.WriteAllTextAsync(snapshotPath, formatted);
        }

        var committed = await File.ReadAllTextAsync(snapshotPath);
        formatted.Trim().ShouldBe(committed.Trim());
    }
}
