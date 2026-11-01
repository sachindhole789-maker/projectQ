#!/usr/bin/env bash
# Creates the three document buckets with public access blocked, SSE and
# versioning enabled. Works against AWS or LocalStack.
#
# Usage:
#   PROJECT_NAME=rest-api AWS_REGION=ap-south-1 \
#   CORS_ORIGINS="http://localhost:3000,https://app.example.com" \
#   ./scripts/create-s3-buckets.sh [development testing production]
#
# LocalStack:
#   AWS_ENDPOINT_URL=http://localhost:4566 AWS_ACCESS_KEY_ID=test \
#   AWS_SECRET_ACCESS_KEY=test PROJECT_NAME=rest-api \
#   CORS_ORIGINS="http://localhost:3000" ./scripts/create-s3-buckets.sh testing

set -euo pipefail

PROJECT_NAME="${PROJECT_NAME:-rest-api}"
AWS_REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-ap-south-1}}"
CORS_ORIGINS="${CORS_ORIGINS:-http://localhost:3000,http://localhost:8080}"
ENDPOINT_ARGS=()

if [[ -n "${AWS_ENDPOINT_URL:-}" ]]; then
  ENDPOINT_ARGS=(--endpoint-url "${AWS_ENDPOINT_URL}")
fi

if [[ $# -gt 0 ]]; then
  ENVIRONMENTS=("$@")
else
  ENVIRONMENTS=(development testing production)
fi

if ! command -v aws >/dev/null 2>&1; then
  echo "AWS CLI not found. Install it or run the Terraform configuration instead." >&2
  exit 1
fi

ORIGINS_JSON="$(printf '%s' "${CORS_ORIGINS}" | awk -F',' '{
  printf "[";
  for (i = 1; i <= NF; i++) {
    if (i > 1) printf ",";
    gsub(/^ +| +$/, "", $i);
    printf "\"%s\"", $i;
  }
  printf "]";
}')"

for environment in "${ENVIRONMENTS[@]}"; do
  bucket="${PROJECT_NAME}-${environment}-documents"

  if aws "${ENDPOINT_ARGS[@]}" s3api head-bucket --bucket "${bucket}" >/dev/null 2>&1; then
    echo "Bucket ${bucket} already exists."
  else
    echo "Creating bucket ${bucket} in ${AWS_REGION}..."
    if [[ "${AWS_REGION}" == "us-east-1" ]]; then
      aws "${ENDPOINT_ARGS[@]}" s3api create-bucket --bucket "${bucket}" --region "${AWS_REGION}" >/dev/null
    else
      aws "${ENDPOINT_ARGS[@]}" s3api create-bucket --bucket "${bucket}" --region "${AWS_REGION}" \
        --create-bucket-configuration "LocationConstraint=${AWS_REGION}" >/dev/null
    fi
  fi

  aws "${ENDPOINT_ARGS[@]}" s3api put-public-access-block --bucket "${bucket}" \
    --public-access-block-configuration \
    "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"

  aws "${ENDPOINT_ARGS[@]}" s3api put-bucket-versioning --bucket "${bucket}" \
    --versioning-configuration Status=Enabled

  aws "${ENDPOINT_ARGS[@]}" s3api put-bucket-encryption --bucket "${bucket}" \
    --server-side-encryption-configuration \
    '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'

  aws "${ENDPOINT_ARGS[@]}" s3api put-bucket-cors --bucket "${bucket}" \
    --cors-configuration "{\"CORSRules\":[{\"AllowedHeaders\":[\"*\"],\"AllowedMethods\":[\"GET\",\"PUT\",\"POST\",\"HEAD\"],\"AllowedOrigins\":${ORIGINS_JSON},\"ExposeHeaders\":[\"ETag\"],\"MaxAgeSeconds\":3000}]}"

  echo "Configured ${bucket}."
done

echo "Done. Point Storage:BucketName at the bucket for each environment."
