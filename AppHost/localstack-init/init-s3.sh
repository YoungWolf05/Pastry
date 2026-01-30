#!/bin/bash

echo "Initializing LocalStack S3..."

# Create S3 bucket
awslocal s3 mb s3://pastrymanager-uploads-dev 2>/dev/null || true

# Configure bucket CORS
awslocal s3api put-bucket-cors --bucket pastrymanager-uploads-dev --cors-configuration '{
  "CORSRules": [
    {
      "AllowedOrigins": ["*"],
      "AllowedMethods": ["GET", "PUT", "POST", "DELETE", "HEAD"],
      "AllowedHeaders": ["*"],
      "ExposeHeaders": ["ETag"],
      "MaxAgeSeconds": 3000
    }
  ]
}' 2>/dev/null || true

echo "S3 bucket 'pastrymanager-uploads-dev' created and configured"

# List buckets to confirm
awslocal s3 ls

echo "LocalStack initialization complete!"
