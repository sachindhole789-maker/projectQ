resource "aws_s3_bucket" "documents" {
  for_each = var.environments

  bucket        = "${var.project_name}-${each.key}-documents"
  force_destroy = false

  tags = {
    Environment = each.key
    Purpose     = "document-storage"
  }
}

resource "aws_s3_bucket_ownership_controls" "documents" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_public_access_block" "documents" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "documents" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "documents" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm     = var.kms_key_arn == null ? "AES256" : "aws:kms"
      kms_master_key_id = var.kms_key_arn
    }

    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "documents" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  rule {
    id     = "abort-incomplete-multipart-uploads"
    status = "Enabled"

    filter {}

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }

  rule {
    id     = "expire-noncurrent-versions"
    status = "Enabled"

    filter {}

    noncurrent_version_expiration {
      noncurrent_days = var.noncurrent_version_expiration_days
    }
  }
}

resource "aws_s3_bucket_cors_configuration" "documents" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET", "PUT", "POST", "HEAD"]
    allowed_origins = lookup(var.cors_allowed_origins, each.key, [])
    expose_headers  = ["ETag"]
    max_age_seconds = 3000
  }
}

resource "aws_s3_bucket_policy" "deny_insecure_transport" {
  for_each = aws_s3_bucket.documents

  bucket = each.value.id

  depends_on = [aws_s3_bucket_public_access_block.documents]

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "DenyInsecureTransport"
        Effect    = "Deny"
        Principal = "*"
        Action    = "s3:*"
        Resource = [
          each.value.arn,
          "${each.value.arn}/*",
        ]
        Condition = {
          Bool = {
            "aws:SecureTransport" = "false"
          }
        }
      },
    ]
  })
}

resource "aws_db_subnet_group" "app" {
  count = var.create_rds ? 1 : 0

  name       = "${var.project_name}-db-subnets"
  subnet_ids = var.rds_subnet_ids

  tags = {
    Environment = "shared"
  }
}

resource "aws_db_instance" "app" {
  count = var.create_rds ? 1 : 0

  identifier        = "${var.project_name}-db"
  engine            = var.rds_engine
  instance_class    = var.rds_instance_class
  allocated_storage = var.rds_allocated_storage

  username = var.rds_username
  password = var.rds_password

  storage_encrypted      = true
  multi_az               = var.rds_multi_az
  publicly_accessible    = false
  db_subnet_group_name   = aws_db_subnet_group.app[0].name
  vpc_security_group_ids = var.rds_security_group_ids

  backup_retention_period = 7
  deletion_protection     = var.rds_deletion_protection
  skip_final_snapshot     = false
  final_snapshot_identifier = "${var.project_name}-final-${formatdate("YYYYMMDDhhmmss", timestamp())}"

  apply_immediately = false

  tags = {
    Environment = "shared"
  }

  lifecycle {
    precondition {
      condition     = length(var.rds_subnet_ids) > 0 && length(var.rds_security_group_ids) > 0
      error_message = "When create_rds is true, provide rds_subnet_ids and rds_security_group_ids."
    }
  }
}
