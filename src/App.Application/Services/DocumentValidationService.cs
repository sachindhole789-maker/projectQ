using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using App.Application.Common.Exceptions;
using App.Application.Common.Options;
using App.Application.Common.Responses;
using Microsoft.Extensions.Options;

namespace App.Application.Services;

public sealed record ValidatedDocument(
    string FileName,
    string Extension,
    string DetectedContentType,
    string Sha256Hash,
    long SizeBytes);

public interface IDocumentValidationService
{
    Task<ValidatedDocument> ValidateAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates documents by content signature and structure, not just the file
/// extension. Extension, magic bytes, OOXML package layout, size and name are
/// all checked before anything is written to storage.
/// </summary>
public sealed class DocumentValidationService(
    IOptions<StorageOptions> storageOptions) : IDocumentValidationService
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public async Task<ValidatedDocument> ValidateAsync(
        Stream content,
        string fileName,
        string? declaredContentType,
        CancellationToken cancellationToken = default)
    {
        var options = storageOptions.Value;

        if (!content.CanSeek)
        {
            throw new AppException("Upload stream must be seekable.", AppStatusCodes.ValidationError);
        }

        var safeName = SanitizeFileName(fileName);
        var extension = Path.GetExtension(safeName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension)
            || !options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new AppException(
                $"File type '{extension}' is not allowed.",
                AppStatusCodes.ValidationError);
        }

        if (content.Length <= 0 || content.Length > options.MaxFileSizeBytes)
        {
            throw new AppException(
                $"File size must be between 1 and {options.MaxFileSizeBytes} bytes.",
                AppStatusCodes.ValidationError);
        }

        var detectedContentType = await DetectContentTypeAsync(content, extension, cancellationToken);
        if (detectedContentType is null)
        {
            throw new AppException(
                "File content does not match an allowed document type.",
                AppStatusCodes.ValidationError);
        }

        content.Position = 0;
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(await sha.ComputeHashAsync(content, cancellationToken));
        content.Position = 0;

        _ = declaredContentType; // client-provided MIME is informational only

        return new ValidatedDocument(safeName, extension, detectedContentType, hash, content.Length);
    }

    private static async Task<string?> DetectContentTypeAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[8];
        content.Position = 0;
        var read = await content.ReadAsync(header.AsMemory(), cancellationToken);
        content.Position = 0;

        if (read == 0)
        {
            return null;
        }

        var isPdf = read >= PdfSignature.Length && header.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature);
        var isZip = read >= ZipSignature.Length && header.AsSpan(0, ZipSignature.Length).SequenceEqual(ZipSignature);
        var isOle = read >= OleSignature.Length && header.AsSpan(0, OleSignature.Length).SequenceEqual(OleSignature);

        return extension switch
        {
            ".pdf" when isPdf => "application/pdf",
            ".docx" or ".xlsx" or ".pptx" when isZip => await DetectOpenXmlAsync(content, extension, cancellationToken),
            ".doc" when isOle => "application/msword",
            ".xls" when isOle => "application/vnd.ms-excel",
            ".ppt" when isOle => "application/vnd.ms-powerpoint",
            ".txt" or ".csv" => await ValidateTextAsync(content, extension, cancellationToken),
            _ => null,
        };
    }

    private static async Task<string?> DetectOpenXmlAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken)
    {
        try
        {
            content.Position = 0;
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var names = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            content.Position = 0;

            if (!names.Contains("[Content_Types].xml"))
            {
                return null;
            }

            return extension switch
            {
                ".docx" when names.Contains("word/document.xml") => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" when names.Contains("xl/workbook.xml") => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" when names.Contains("ppt/presentation.xml") => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => null,
            };
        }
        catch (InvalidDataException)
        {
            return null;
        }
        finally
        {
            content.Position = 0;
        }
    }

    private static async Task<string?> ValidateTextAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken)
    {
        try
        {
            content.Position = 0;
            using var reader = new StreamReader(
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);

            var text = await reader.ReadToEndAsync(cancellationToken);
            content.Position = 0;

            if (text.Contains('\0'))
            {
                return null;
            }

            return extension == ".csv" ? "text/csv" : "text/plain";
        }
        catch (DecoderFallbackException)
        {
            content.Position = 0;
            return null;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AppException("Invalid file name.", AppStatusCodes.ValidationError);
        }

        return name.Length > 180 ? name[..180] : name;
    }
}
