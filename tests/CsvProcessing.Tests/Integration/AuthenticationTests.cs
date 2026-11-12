using System.Net;
using System.Net.Http.Json;

namespace CsvProcessing.Tests.Integration;

public class AuthenticationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_WithoutApiKey_ReturnsUnauthorized()
    {
        var content = UploadRequest.Create("data.csv", "name,amount\r\nalpha,10\r\n");

        var response = await _client.PostAsync("/api/uploads", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("ApiKey", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Upload_WithInvalidApiKey_ReturnsUnauthorized()
    {
        var content = UploadRequest.Create("data.csv", "name,amount\r\nalpha,10\r\n");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/uploads") { Content = content };
        request.Headers.Add("X-Api-Key", "definitely-not-the-key");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("ApiKey", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Upload_WithEmptyApiKey_ReturnsUnauthorized()
    {
        var content = UploadRequest.Create("data.csv", "name,amount\r\nalpha,10\r\n");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/uploads") { Content = content };
        request.Headers.Add("X-Api-Key", string.Empty);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithValidApiKey_IsAccepted()
    {
        var client = CreateAuthenticatedClient();

        var content = UploadRequest.Create("data.csv", "name,amount\r\nalpha,10\r\n");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/uploads") { Content = content };
        request.Headers.Add("X-Api-Key", _factory.ApiKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/history")]
    [InlineData("/api/reports/summary")]
    public async Task ProtectedEndpoints_WithoutApiKey_ReturnUnauthorized(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsPubliclyAccessible()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload!["status"]);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.ApiKey);

        return client;
    }
}
