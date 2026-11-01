using System.Text;
using App.Application.Common.Exceptions;
using App.Application.Common.Options;
using App.Application.Services;
using Microsoft.Extensions.Options;

namespace App.Tests.Unit;

public sealed class DocumentValidationTests
{
    private static DocumentValidationService CreateService() =>
        new(Options.Create(new StorageOptions()));

    [Fact]
    public async Task Pdf_is_accepted_by_content_signature()
    {
        var service = CreateService();
        await using var stream = new MemoryStream("%PDF-1.7\nsample"u8.ToArray());

        var validated = await service.ValidateAsync(stream, "report.pdf", "application/pdf");

        Assert.Equal("application/pdf", validated.DetectedContentType);
        Assert.Equal(".pdf", validated.Extension);
        Assert.Equal(64, validated.Sha256Hash.Length);
        Assert.True(validated.SizeBytes > 0);
    }

    [Fact]
    public async Task Extension_mismatch_is_rejected_even_when_content_is_known()
    {
        var service = CreateService();
        await using var stream = new MemoryStream("GIF89a-not-a-pdf"u8.ToArray());

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ValidateAsync(stream, "fake.pdf", "application/pdf"));

        Assert.Equal("422", exception.StatusCode);
    }

    [Fact]
    public async Task Disallowed_extension_is_rejected_before_reading_content()
    {
        var service = CreateService();
        await using var stream = new MemoryStream("MZ executable"u8.ToArray());

        await Assert.ThrowsAsync<AppException>(
            () => service.ValidateAsync(stream, "payload.exe", "application/octet-stream"));
    }

    [Fact]
    public async Task Csv_text_is_detected_by_content()
    {
        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("name,email\nAda,ada@example.com"));

        var validated = await service.ValidateAsync(stream, "students.csv", "text/plain");

        Assert.Equal("text/csv", validated.DetectedContentType);
    }

    [Fact]
    public async Task Binary_content_disguised_as_text_is_rejected()
    {
        var service = CreateService();
        await using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03, 0x00]);

        await Assert.ThrowsAsync<AppException>(
            () => service.ValidateAsync(stream, "notes.txt", "text/plain"));
    }
}
