using System.Net;
using System.Net.Http.Json;
using CsvProcessing.Api.Contracts;

namespace CsvProcessing.Tests.Integration;

public class UploadEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public UploadEndpointTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.ApiKey);
    }

    [Fact]
    public async Task Upload_ValidCsv_ReturnsCreatedWithAggregates()
    {
        var csv = "name,amount,ratio\r\nalpha,10,0.5\r\nbeta,20,1.5\r\ngamma,,2.5\r\n";

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("data.csv", csv));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CsvUploadResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Completed", payload!.Status);
        Assert.Equal(3, payload.RowCount);
        Assert.Equal(3, payload.ColumnCount);
        Assert.Equal(2, payload.NumericColumnCount);
        Assert.Equal($"data.csv", payload.FileName);

        var amount = payload.Aggregates.Single(item => item.ColumnName == "amount");
        Assert.Equal(2, amount.Count);
        Assert.Equal(30m, amount.Sum);
        Assert.Equal(15m, amount.Average);
        Assert.Equal(10m, amount.Minimum);
        Assert.Equal(20m, amount.Maximum);

        var ratio = payload.Aggregates.Single(item => item.ColumnName == "ratio");
        Assert.Equal(3, ratio.Count);
        Assert.Equal(4.5m, ratio.Sum);
        Assert.Equal(1.5m, ratio.Average);
    }

    [Fact]
    public async Task Upload_MalformedCsv_ReturnsUnprocessableEntity()
    {
        var csv = "name,amount\r\n\"unterminated,10\r\n";

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("data.csv", csv));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CsvUploadErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Failed", payload!.Status);
        Assert.DoesNotContain("unterminated", payload.Error);
    }

    [Fact]
    public async Task Upload_WrongExtension_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("data.txt", "name\r\nalpha\r\n"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_OversizedFile_ReturnsPayloadTooLarge()
    {
        var oversized = new byte[(5 * 1024 * 1024) + 1];
        Array.Fill(oversized, (byte)'a');

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("data.csv", oversized));

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutFile_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/uploads", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_NonNumericColumn_IsNotAggregated()
    {
        var csv = "name,amount\r\nalpha,10\r\nbeta,twenty\r\n";

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("data.csv", csv));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CsvUploadResponse>();

        Assert.NotNull(payload);
        Assert.Equal(0, payload!.NumericColumnCount);
        Assert.Empty(payload.Aggregates);
    }

    [Fact]
    public async Task Upload_FileNameWithPathTraversal_IsStoredWithoutPathSegments()
    {
        var csv = "name,amount\r\nalpha,10\r\n";

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("../../../etc/passwd.csv", csv));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CsvUploadResponse>();

        Assert.NotNull(payload);
        Assert.Equal("passwd.csv", payload!.FileName);
        Assert.DoesNotContain("/", payload.FileName);
        Assert.DoesNotContain("\\", payload.FileName);
    }

    [Fact]
    public async Task Upload_FileNameWithWindowsStylePathTraversal_IsStoredWithoutPathSegments()
    {
        var csv = "name,amount\r\nalpha,10\r\n";

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create(@"..\..\..\windows\system32\evil.csv", csv));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CsvUploadResponse>();

        Assert.NotNull(payload);
        Assert.Equal("evil.csv", payload!.FileName);
        Assert.DoesNotContain("/", payload.FileName);
        Assert.DoesNotContain("\\", payload.FileName);
    }

    [Fact]
    public async Task Upload_FileNameWithControlCharacters_IsStoredWithoutControlCharacters()
    {
        var csv = "name,amount\r\nalpha,10\r\n";

        var response = await _client.PostAsync("/api/uploads", UploadRequest.Create("re\u0001port.csv", csv));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CsvUploadResponse>();

        Assert.NotNull(payload);
        Assert.DoesNotContain(payload!.FileName, character => char.IsControl(character));
        Assert.Equal("report.csv", payload.FileName);
    }
}
