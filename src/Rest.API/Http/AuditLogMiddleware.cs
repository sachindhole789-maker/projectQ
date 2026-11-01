using System.Diagnostics;
using System.Text;
using App.Application.Abstractions;
using App.Application.Common.Options;
using App.Domain.Entities;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Options;

namespace Rest.API.Http;

/// <summary>
/// Captures every request/response pair, masks sensitive JSON keys and enqueues an
/// audit row. Persistence happens asynchronously in a background writer so request
/// latency is not tied to the audit table.
/// </summary>
public sealed class AuditLogMiddleware(
    RequestDelegate next,
    IAuditLogQueue queue,
    IRequestMetrics metrics,
    IOptions<AuditLogOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditLogMiddleware> logger)
{
    private readonly AuditLogOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context, IUserInfoProvider userInfo)
    {
        var stopwatch = Stopwatch.StartNew();
        var auditEnabled = ShouldAudit(context, _options);
        var captureResponse = auditEnabled
            && _options.CaptureResponseBody
            && !IsBinaryStream(context.Request.Path);

        string? requestBody = null;
        if (auditEnabled && _options.CaptureRequestBody && IsTextContent(context.Request.ContentType))
        {
            requestBody = await ReadBodyAsync(context, _options.MaxBodyCharacters);
            requestBody = SensitiveDataMasker.MaskJson(requestBody, _options.SensitiveJsonKeys);
        }

        var originalBody = context.Response.Body;
        var responseBuffer = captureResponse ? new MemoryStream() : null;

        if (responseBuffer is not null)
        {
            context.Response.Body = responseBuffer;
        }

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            context.Response.Body = originalBody;

            string? responseBody = null;
            if (responseBuffer is not null)
            {
                if (responseBuffer.Length > 0)
                {
                    try
                    {
                        if (IsTextContent(context.Response.ContentType))
                        {
                            responseBody = ReadBufferedBody(responseBuffer, _options.MaxBodyCharacters);
                            responseBody = SensitiveDataMasker.MaskJson(responseBody, _options.SensitiveJsonKeys);
                        }

                        responseBuffer.Position = 0;
                        await responseBuffer.CopyToAsync(originalBody, CancellationToken.None);
                    }
                    catch (Exception copyException)
                    {
                        logger.LogDebug(copyException, "Could not copy the buffered response body");
                    }
                }

                await responseBuffer.DisposeAsync();
            }

            metrics.Record(context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);

            if (auditEnabled)
            {
                var auditLog = new AuditLog
                {
                    RequestId = context.TraceIdentifier,
                    CorrelationId = context.Items[RequestContext.CorrelationIdItemKey] as string,
                    UserId = userInfo.UserId,
                    UserName = userInfo.Email ?? userInfo.UserName,
                    Method = context.Request.Method,
                    Path = context.Request.Path,
                    QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                    IpAddress = userInfo.IpAddress,
                    UserAgent = context.Request.Headers["User-Agent"].FirstOrDefault(),
                    HttpStatusCode = context.Response.StatusCode,
                    DurationMs = (long)stopwatch.Elapsed.TotalMilliseconds,
                    RequestBody = requestBody,
                    ResponseBody = responseBody,
                    CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                };

                if (!queue.TryEnqueue(auditLog))
                {
                    logger.LogWarning(
                        "Audit queue is full; dropped audit row for {Method} {Path}",
                        auditLog.Method,
                        auditLog.Path);
                }
            }
        }
    }

    private static bool ShouldAudit(HttpContext context, AuditLogOptions options)
    {
        if (!options.Enabled)
        {
            return false;
        }

        var path = context.Request.Path;
        return !options.ExcludedPaths.Any(excluded =>
            path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBinaryStream(PathString path) =>
        path.Value?.EndsWith("/content", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsTextContent(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReadBodyAsync(HttpContext context, int maxCharacters)
    {
        context.Request.EnableBuffering();

        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await ReadLimitedAsync(reader, maxCharacters, context.RequestAborted);
        context.Request.Body.Position = 0;
        return body;
    }

    private static string ReadBufferedBody(MemoryStream buffer, int maxCharacters)
    {
        buffer.Position = 0;

        using var reader = new StreamReader(
            buffer,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        using var writer = new StringWriter();
        var chunk = new char[Math.Min(maxCharacters, 2048)];
        var remaining = maxCharacters;

        while (remaining > 0)
        {
            var read = reader.Read(chunk, 0, Math.Min(chunk.Length, remaining));
            if (read == 0)
            {
                break;
            }

            writer.Write(chunk, 0, read);
            remaining -= read;
        }

        if (remaining == 0 && reader.Peek() >= 0)
        {
            writer.Write("...[truncated]");
        }

        buffer.Position = 0;
        return writer.ToString();
    }

    private static async Task<string> ReadLimitedAsync(TextReader reader, int maxCharacters, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(Math.Min(maxCharacters, 4096));
        var chunk = new char[Math.Min(maxCharacters, 2048)];

        while (builder.Length < maxCharacters)
        {
            var toRead = Math.Min(chunk.Length, maxCharacters - builder.Length);
            var read = await reader.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                return builder.ToString();
            }

            builder.Append(chunk, 0, read);
        }

        if (await reader.ReadAsync(chunk.AsMemory(0, 1), cancellationToken) > 0)
        {
            builder.Append("...[truncated]");
        }

        return builder.ToString();
    }
}
