using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IB.WatchCluster.Abstract.Services;

public static class HealthcheckStatic
{
    public static async Task HealthResultResponseJsonFull(HttpListenerContext context, HealthReport result)
    {
        var jsonStream = FormatJsonOutput(result);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.Add(HttpResponseHeader.CacheControl, "no-store, no-cache");
        context.Response.StatusCode = result.Status == HealthStatus.Healthy 
            ? (int)HttpStatusCode.OK 
            : (int)HttpStatusCode.ServiceUnavailable;
        var buffer = jsonStream.ToArray();
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
        context.Response.Close();
    }
    /// <summary>
    /// Custom JSON output for HealthReport
    /// </summary>
    public static Task HealthResultResponseJsonFull(HttpContext context, HealthReport result)
    {
        var jsonStream = FormatJsonOutput(result);
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(Encoding.UTF8.GetString(jsonStream.ToArray()));
    }

    private static MemoryStream FormatJsonOutput(HealthReport result)
    {
        var options = new JsonWriterOptions
        {
            Indented = true
        };

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, options);
        writer.WriteStartObject();
        writer.WriteString("serverVersion", SolutionInfo.Version);
        writer.WriteString("status", result.Status.ToString());
        writer.WriteString("totalDuration", result.TotalDuration.ToString());
        writer.WriteStartObject("results");
        foreach (var entry in result.Entries)
        {
            writer.WriteStartObject(entry.Key);
            writer.WriteString("status", entry.Value.Status.ToString());
            writer.WriteString("description", entry.Value.Description);
            writer.WriteStartObject("data");
            foreach (var item in entry.Value.Data)
            {
                writer.WritePropertyName(item.Key);
                JsonSerializer.Serialize(writer, item.Value, item.Value.GetType());
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        writer.WriteEndObject();

        return stream;
    }
}