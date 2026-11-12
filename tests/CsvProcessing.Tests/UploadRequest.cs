using System.Net.Http.Headers;
using System.Text;

namespace CsvProcessing.Tests;

internal static class UploadRequest
{
    public static MultipartFormDataContent Create(string fileName, string content) =>
        Create(fileName, Encoding.UTF8.GetBytes(content));

    public static MultipartFormDataContent Create(string fileName, byte[] content, string contentType = "text/csv")
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        form.Add(fileContent, "file", fileName);

        return form;
    }
}
