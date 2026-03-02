#!/bin/bash
# LocalStack initialization script for KMS and Secrets Manager

# Wait for LocalStack to be ready
echo "Waiting for LocalStack services..."
sleep 5

# Create KMS key for encryption
echo "Creating KMS key..."
awslocal kms create-key --description "Dev encryption key" --key-usage ENCRYPT_DECRYPT
KEY_ID=$(awslocal kms list-keys --query 'Keys[0].KeyId' --output text)

# Create alias for the key
awslocal kms create-alias --alias-name alias/dev-key --target-key-id $KEY_ID

# Create secrets in Secrets Manager
echo "Creating secrets..."

# JWT Secret
awslocal secretsmanager create-secret \
  --name pastrymanager/jwt/secret \
  --secret-string "DEV_SECRET_KEY_FOR_JWT_TOKENS_MINIMUM_64_CHARS_LONG_CHANGE_IN_PROD"

# Kafka credentials (for local dev, we use plaintext)
awslocal secretsmanager create-secret \
  --name pastrymanager/kafka/credentials \
  --secret-string '{"username":"dev-user","password":"dev-password"}'

# Database connection (handled by Aspire, but create for completeness)
awslocal secretsmanager create-secret \
  --name pastrymanager/db/connection \
  --secret-string "Host=localhost;Port=5433;Database=pastrydb;Username=postgres;Password=postgres"

echo "LocalStack initialization complete!"
