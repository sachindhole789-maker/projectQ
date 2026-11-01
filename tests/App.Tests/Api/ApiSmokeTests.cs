using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace App.Tests.Api;

public sealed class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Security:MasterKey"] = Convert.ToBase64String(new byte[32]),
                        ["Jwt:SigningKey"] = "testing-only-signing-key-not-used-outside-tests-0123456789",
                        ["RateLimiting:Enabled"] = "false",
                        ["AuditLog:Enabled"] = "false",
                        ["BackgroundJobs:RunAuditWriter"] = "false",
                        ["BackgroundJobs:RunDashboardBroadcast"] = "false",
                        ["Notifications:Enabled"] = "false",
                    });
                });
            })
            .CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Protected_endpoint_returns_http_200_with_401_envelope()
    {
        var response = await _client.GetAsync("/api/v1/students/CUST-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        Assert.Equal("401", root.GetProperty("StatusCode").GetString());
        Assert.Equal("FAILURE", root.GetProperty("Status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("RequestId").GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Data").ValueKind);
    }

    [Fact]
    public async Task Invalid_login_returns_http_200_with_422_envelope()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/login", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await ReadJsonAsync(response);
        Assert.Equal("422", json.RootElement.GetProperty("StatusCode").GetString());
        Assert.Equal("FAILURE", json.RootElement.GetProperty("Status").GetString());
    }

    [Fact]
    public async Task Root_endpoint_reports_the_environment()
    {
        var payload = await _client.GetFromJsonAsync<JsonElement>("/");

        Assert.Equal("Rest.API", payload.GetProperty("service").GetString());
        Assert.Equal("Testing", payload.GetProperty("environment").GetString());
    }

    [Fact]
    public async Task Response_uses_pascal_case_contract_fields()
    {
        var response = await _client.GetAsync("/api/v1/students/CUST-1");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"RequestId\"", raw);
        Assert.Contains("\"StatusCode\"", raw);
        Assert.Contains("\"Message\"", raw);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
