#!/bin/bash

echo "Initializing AWS resources in LocalStack..."

# Set AWS credentials for LocalStack
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_DEFAULT_REGION=us-east-1

# Create S3 bucket
echo "Creating S3 bucket: pastrymanager-uploads-dev"
awslocal s3 mb s3://pastrymanager-uploads-dev

# Configure bucket CORS for web uploads
echo "Configuring CORS for S3 bucket"
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
}'

# List buckets to verify
echo "S3 buckets created:"
awslocal s3 ls

echo "LocalStack initialization complete!"
