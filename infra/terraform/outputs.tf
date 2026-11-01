output "bucket_names" {
  description = "S3 bucket name per environment."
  value       = { for environment, bucket in aws_s3_bucket.documents : environment => bucket.bucket }
}

output "bucket_arns" {
  description = "S3 bucket ARN per environment."
  value       = { for environment, bucket in aws_s3_bucket.documents : environment => bucket.arn }
}

output "rds_endpoint" {
  description = "RDS SQL Server endpoint when create_rds is enabled."
  value       = var.create_rds ? aws_db_instance.app[0].address : null
}

output "rds_port" {
  description = "RDS SQL Server port when create_rds is enabled."
  value       = var.create_rds ? aws_db_instance.app[0].port : null
}
