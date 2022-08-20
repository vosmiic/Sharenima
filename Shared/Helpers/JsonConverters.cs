using System.Net.Http.Headers;
using System.Text.Json;

namespace Sharenima.Shared.Helpers; 

public class JsonConverters {
    public static ByteArrayContent ConvertObjectToHttpContent(object obj) {
        var content = JsonSerializer.Serialize(obj);
        var buffer = System.Text.Encoding.UTF8.GetBytes(content);
        var byteContent = new ByteArrayContent(buffer);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return byteContent;
    }
}