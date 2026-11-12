using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace CsvProcessing.Tests.Integration;

public class SwaggerDocumentTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public SwaggerDocumentTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void SwaggerDocument_GeneratesWithoutErrors()
    {
        var provider = _factory.Services.GetRequiredService<ISwaggerProvider>();

        var document = provider.GetSwagger("v1");

        Assert.NotNull(document);
        Assert.NotEmpty(document.Paths);
    }

    [Fact]
    public async Task SwaggerJson_IsServedAndDocumentsEveryEndpoint()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("/api/uploads", json);
        Assert.Contains("/api/history", json);
        Assert.Contains("/api/reports/aggregates/{runId}", json);
        Assert.Contains("/api/reports/summary", json);
        Assert.Contains("/health", json);
        Assert.Contains("X-Api-Key", json);
    }
}
