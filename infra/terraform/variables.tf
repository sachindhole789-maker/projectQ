variable "aws_region" {
  description = "AWS region for the S3 buckets and optional RDS instance."
  type        = string
  default     = "ap-south-1"
}

variable "project_name" {
  description = "Prefix used for bucket and RDS names. Adjust so bucket names are globally unique."
  type        = string
  default     = "rest-api"
}

variable "environments" {
  description = "Environments that receive an S3 bucket."
  type        = set(string)
  default     = ["development", "testing", "production"]
}

variable "cors_allowed_origins" {
  description = "Browser/Flutter web origins allowed to call S3 directly. Flutter desktop/mobile does not need CORS."
  type        = map(list(string))
  default = {
    development = ["http://localhost:3000", "http://localhost:8080", "http://127.0.0.1:3000"]
    testing     = ["http://localhost:3000", "http://localhost:8080"]
    production  = ["https://app.example.com"]
  }
}

variable "kms_key_arn" {
  description = "Optional customer-managed KMS key. When null, AES-256 (SSE-S3) is used."
  type        = string
  default     = null
}

variable "noncurrent_version_expiration_days" {
  description = "How long old object versions are kept."
  type        = number
  default     = 90
}

variable "create_rds" {
  description = "Create a SQL Server RDS instance. Requires rds_subnet_ids and rds_security_group_ids."
  type        = bool
  default     = false
}

variable "rds_engine" {
  description = "RDS SQL Server engine, e.g. sqlserver-se, sqlserver-ex."
  type        = string
  default     = "sqlserver-se"
}

variable "rds_instance_class" {
  type    = string
  default = "db.t3.small"
}

variable "rds_allocated_storage" {
  type    = number
  default = 100
}

variable "rds_username" {
  type    = string
  default = "app_user"
}

variable "rds_password" {
  description = "RDS master password. Supply through TF_VAR_rds_password or a secret store."
  type        = string
  sensitive   = true
  default     = null
}

variable "rds_multi_az" {
  type    = bool
  default = false
}

variable "rds_deletion_protection" {
  type    = bool
  default = true
}

variable "rds_subnet_ids" {
  description = "Private subnets for the RDS subnet group."
  type        = list(string)
  default     = []
}

variable "rds_security_group_ids" {
  description = "Security groups allowing the API hosts to reach SQL Server (1433)."
  type        = list(string)
  default     = []
}
