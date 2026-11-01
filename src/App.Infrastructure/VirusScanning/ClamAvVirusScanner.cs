using System.Net.Sockets;
using System.Text;
using App.Application.Common.Exceptions;
using App.Application.Common.Options;
using App.Application.Common.Responses;
using App.Domain.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Infrastructure.VirusScanning;

/// <summary>
/// Minimal ClamAV INSTREAM client. A scan failure is treated as a rejection
/// (fail closed) so a ClamAV outage cannot be used to bypass scanning.
/// </summary>
public sealed class ClamAvVirusScanner(
    IOptions<VirusScanOptions> options,
    ILogger<ClamAvVirusScanner> logger) : IVirusScanner
{
    private readonly VirusScanOptions _options = options.Value;

    public async Task<VirusScanResult> ScanAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!content.CanSeek)
        {
            throw new AppException("Upload stream must be seekable for scanning.", AppStatusCodes.ValidationError);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
            await using var stream = client.GetStream();

            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);

            var buffer = new byte[64 * 1024];
            var lengthBytes = new byte[4];
            int read;

            content.Position = 0;
            while ((read = await content.ReadAsync(buffer, timeout.Token)) > 0)
            {
                lengthBytes[0] = (byte)(read >> 24);
                lengthBytes[1] = (byte)(read >> 16);
                lengthBytes[2] = (byte)(read >> 8);
                lengthBytes[3] = (byte)read;

                await stream.WriteAsync(lengthBytes, timeout.Token);
                await stream.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }

            await stream.WriteAsync(new byte[4], timeout.Token);

            var response = await ReadNullTerminatedAsync(stream, timeout.Token);
            content.Position = 0;

            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                var threat = response.Replace("stream:", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("FOUND", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();

                logger.LogWarning("ClamAV detected {Threat} in {FileName}", threat, fileName);
                return VirusScanResult.Infected(string.IsNullOrWhiteSpace(threat) ? "Unknown malware" : threat);
            }

            if (!response.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("Malware scan returned an unexpected response.", AppStatusCodes.ServerError);
            }

            return VirusScanResult.Clean;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AppException("Malware scan timed out.", AppStatusCodes.ServerError);
        }
        catch (SocketException ex)
        {
            logger.LogError(ex, "ClamAV is unreachable at {Host}:{Port}", _options.Host, _options.Port);
            throw new AppException("Malware scan service is unavailable.", AppStatusCodes.ServerError, ex);
        }
    }

    private static async Task<string> ReadNullTerminatedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>(64);
        var one = new byte[1];

        while (await stream.ReadAsync(one, cancellationToken) == 1)
        {
            if (one[0] == 0)
            {
                break;
            }

            bytes.Add(one[0]);
            if (bytes.Count > 4096)
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
