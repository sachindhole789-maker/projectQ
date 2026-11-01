namespace App.Application.Common.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>S3 | Local</summary>
    public string Provider { get; set; } = "Local";

    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "ap-south-1";

    /// <summary>Set to http://localhost:4566 for LocalStack in Testing.</summary>
    public string? ServiceUrl { get; set; }

    public bool ForcePathStyle { get; set; }

    /// <summary>Optional static credentials. Prefer IAM roles / instance profiles in AWS.</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }
    public string Prefix { get; set; } = "documents";
    public bool UseServerSideEncryption { get; set; } = true;
    public string? KmsKeyId { get; set; }
    public int PresignedUrlMinutes { get; set; } = 15;

    public string LocalRootPath { get; set; } = "storage";
    public string LocalDownloadBasePath { get; set; } = "/api/v1/files";

    public long MaxFileSizeBytes { get; set; } = 26_214_400;
    public string[] AllowedExtensions { get; set; } =
    [
        ".pdf", ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt", ".txt", ".csv",
    ];
}
