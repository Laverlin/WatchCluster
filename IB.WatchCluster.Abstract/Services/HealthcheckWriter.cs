using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IB.WatchCluster.Abstract.Services;

public static class HealthcheckWriter
{
    public static async Task HealthResultResponseJsonFull(HttpListenerResponse response, HealthReport result)
    {
        var jsonStream = FormatJsonOutput(result);
        response.ContentType = "application/json; charset=utf-8";
        response.Headers.Add(HttpResponseHeader.CacheControl, "no-store, no-cache");
        response.Headers.Add(HttpResponseHeader.Connection, "close");
        response.StatusCode = result.Status == HealthStatus.Healthy 
            ? (int)HttpStatusCode.OK 
            : (int)HttpStatusCode.ServiceUnavailable;
        await response.OutputStream.WriteAsync(jsonStream.ToArray());
    }
    
    public static void HealthResultResponseStatus(HttpListenerResponse response, HealthReport result)
    {
        response.Headers.Add(HttpResponseHeader.CacheControl, "no-store, no-cache");
        response.Headers.Add(HttpResponseHeader.Connection, "close");
        response.StatusCode = result.Status == HealthStatus.Healthy 
            ? (int)HttpStatusCode.OK 
            : (int)HttpStatusCode.ServiceUnavailable;
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