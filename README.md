# PastryManager - Task Request Management System

A scalable, production-ready task management system built with Clean Architecture, .NET 8, EF Core, PostgreSQL, and Aspire.

## 🏗️ Architecture

This solution follows **Clean Architecture** principles with clear separation of concerns:

```
PastryManager/
├── PastryManager.Domain          # Core business entities
├── PastryManager.Application     # Business logic (CQRS + MediatR)
├── PastryManager.Infrastructure  # Data access (EF Core + PostgreSQL)
├── PastryManager.Api            # REST API endpoints
├── AppHost                       # Aspire orchestration
└── ServiceDefaults              # Shared Aspire configurations
```

### Projects Overview

#### **Domain Layer** - Core entities with zero dependencies
- Entities: `User`, `TaskRequest`, `TaskComment`, `FileAttachment`
- Enums: `UserRole`, `TaskStatus`, `TaskPriority`, `EntityType`
- Soft delete support

#### **Application Layer** - Business logic (CQRS + MediatR 13.0.0)
- Commands: RegisterUser, CreateTaskRequest, UpdateTaskStatus, UploadFile, DeleteFile
- Queries: GetUserById, GetTasksByAssignedUser, GetFilesByEntity, GetFile
- FluentValidation 11.11.0 for validation
- Repository interfaces
- File storage abstraction (IFileStorageService)

#### **Infrastructure Layer** - Data access (EF Core 8.0.11 + PostgreSQL 8.0.11)
- Repository implementations
- Entity configurations
- PBKDF2 password hashing (100k iterations)
- AWS S3 integration for file storage
- Database migrations

#### **API Layer** - REST API with Swagger
- Controllers for Users and TaskRequests
- Auto-migration in development
- CORS enabled

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- Docker Desktop (for PostgreSQL, LocalStack via Aspire)

### Local Development Setup

**1. Start the application with Aspire (Recommended):**
```bash
dotnet run --project AppHost/PastryManager.AppHost.csproj
```

This single command starts everything you need:
- PostgreSQL database (port 5433)
- LocalStack with S3 service (port 4566)
- PastryManager API
- Aspire Dashboard for monitoring

**2. Access the services:**
- **Aspire Dashboard**: Check terminal output for URL (e.g., `https://localhost:17065`)
- **API Swagger**: Available through Aspire dashboard → pastrymanager-api → View resource
- **LocalStack S3**: http://localhost:4566

**Note:** The LocalStack S3 bucket (`pastrymanager-uploads-dev`) is automatically created on startup.

## 🔧 Database Migrations

**Install EF Core Tools (one-time):**
```bash
dotnet tool install --global dotnet-ef
```

**Generate Migration:**
```bash
dotnet ef migrations add MigrationName --project PastryManager.Infrastructure --startup-project PastryManager
```

**Apply Migration Manually (optional - auto-applies on startup in development):**
```bash
dotnet ef database update --project PastryManager.Infrastructure --startup-project PastryManager
```

**Generate SQL Script (for production deployments):**
```bash
dotnet ef migrations script --project PastryManager.Infrastructure --startup-project PastryManager --output migration.sql
```

**Remove Last Migration (if not applied):**
```bash
dotnet ef migrations remove --project PastryManager.Infrastructure --startup-project PastryManager
```

> **Note:** Migrations auto-apply on startup in development. For production, consider using manual migrations or SQL scripts.

## 📋 API Endpoints

### Users
- `POST /api/users/register` - Register new user
- `GET /api/users/{id}` - Get user by ID

### Task Requests
- `POST /api/taskrequests` - Create task (requires X-User-Id header)
- `GET /api/taskrequests/assigned/{userId}` - Get tasks by assigned user
- `PATCH /api/taskrequests/{id}/status` - Update task status

### File Uploads (NEW)
- `POST /api/files/{entityType}/{entityId}` - Upload file for an entity
- `GET /api/files/{entityType}/{entityId}` - List all files for an entity
- `GET /api/files/{fileId}` - Get file metadata with presigned download URL
- `DELETE /api/files/{fileId}` - Delete file (soft delete)

**Supported Entity Types:**
- `taskrequest` - Attach files to task requests
- `user` - Attach files to user profiles

**File Upload Example:**
```bash
curl -X POST "http://localhost:5000/api/files/taskrequest/123e4567-e89b-12d3-a456-426614174000" \
  -H "X-User-Id: 123e4567-e89b-12d3-a456-426614174001" \
  -F "file=@document.pdf"
```

**File Constraints:**
- Max file size: 10 MB
- Allowed extensions: `.pdf`, `.docx`, `.xlsx`, `.png`, `.jpg`, `.jpeg`, `.txt`, `.csv`
- Files stored with S3 key pattern: `uploads/{entityType}/{entityId}/{fileId}-{filename}`

## 📦 Key Technologies

- .NET 8 / ASP.NET Core
- MediatR 13.0.0 (CQRS)
- FluentValidation 11.11.0
- EF Core 8.0.11
- PostgreSQL via Aspire
- AWS SDK for S3 (file storage)
- LocalStack (local S3 emulation)
- Swagger/OpenAPI

## 🎯 Features

✅ User registration with secure password hashing  
✅ Task request management with status tracking  
✅ Priority levels (Low, Medium, High, Critical)  
✅ **File upload & storage with AWS S3**  
✅ **Presigned URLs for secure file downloads**  
✅ **LocalStack integration for local S3 development**  
✅ **File validation (size, type)**  
✅ **Entity-based file organization**  
✅ Soft delete for all entities  
✅ Clean Architecture for scalability  
✅ CQRS pattern with MediatR  
✅ Comprehensive validation  
✅ Auto-migration in development  

## 📄 Database Schema

**Users**: Email (unique), Name, Role, Password (hashed)  
**TaskRequests**: Title, Description, Priority, Status, DueDate, Assignments  
**TaskComments**: Content, linked to Task and User  
**FileAttachments**: FileName, S3Key, ContentType, FileSize, EntityType, EntityId, UploadedBy  

All tables include audit fields (CreatedAt, UpdatedAt) and soft delete support.

## 🗄️ AWS S3 Configuration

### Local Development (LocalStack via Aspire)

Files are stored in LocalStack S3 emulator that's automatically started by Aspire on `localhost:4566`. 

**Aspire Integration:**
- LocalStack container is defined in [AppHost/AppHost.cs](AppHost/AppHost.cs)
- Initialization script in [AppHost/localstack-init/init-s3.sh](AppHost/localstack-init/init-s3.sh) auto-creates the bucket
- Environment variables automatically configured by Aspire

**Configuration in `appsettings.Development.json`:**
```json
{
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "BucketName": "pastrymanager-uploads-dev",
      "ServiceURL": "http://localhost:4566",
      "ForcePathStyle": true
    }
  }
}
```

**Verify bucket (once Aspire is running):**
```bash
aws --endpoint-url=http://localhost:4566 s3 ls
```

### Production (AWS S3)

For production deployment, update `appsettings.json` with your AWS credentials or use IAM roles:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "S3": {
      "BucketName": "pastrymanager-uploads"
    }
  }
}
```

**AWS Credentials:**
- Use IAM roles on AWS App Runner/ECS
- Or configure AWS credentials via environment variables
- Or use AWS CLI profile configuration

**Create S3 Bucket:**
```bash
aws s3 mb s3://pastrymanager-uploads --region us-east-1
```

**Configure CORS (if needed for web uploads):**
```bash
aws s3api put-bucket-cors --bucket pastrymanager-uploads --cors-configuration file://cors.json
```

### File Storage Structure

Files are organized by entity type for better management:
```
s3://pastrymanager-uploads-dev/
└── uploads/
    ├── taskrequest/
    │   └── {taskRequestId}/
    │       ├── {fileId1}-document.pdf
    │       └── {fileId2}-image.png
    └── user/
        └── {userId}/
            └── {fileId3}-profile.jpg
```

## 🔒 Security Considerations

- **File Validation**: Only allowed file types and sizes (configurable in `appsettings.json`)
- **Presigned URLs**: Generated with 60-minute expiration for secure downloads
- **Soft Delete**: Files are soft-deleted in database, S3 cleanup happens asynchronously
- **Entity Verification**: Files can only be uploaded to existing entities
- **Authentication**: Currently uses `X-User-Id` header (consider adding JWT authentication for production)

## 🐳 Docker & LocalStack

LocalStack S3 emulation is managed by Aspire for seamless local development.

**Aspire Configuration:**
- LocalStack defined as container resource in [AppHost/AppHost.cs](AppHost/AppHost.cs)
- Auto-starts with `dotnet run --project AppHost/PastryManager.AppHost.csproj`
- Persistent container lifetime for data retention between restarts
- Initialization script creates bucket on first run

**Useful Commands:**
```bash
# List S3 buckets in LocalStack (requires Aspire to be running)
aws --endpoint-url=http://localhost:4566 s3 ls

# List files in bucket
aws --endpoint-url=http://localhost:4566 s3 ls s3://pastrymanager-uploads-dev/uploads/ --recursive

# View Aspire dashboard (check terminal output for URL)
# From dashboard, you can monitor all containers including LocalStack
```

**Architecture Benefits:**
- ✅ Single command startup (`dotnet run`)
- ✅ Automatic service orchestration
- ✅ Integrated monitoring via Aspire dashboard
- ✅ No manual Docker Compose management
- ✅ Environment variables automatically injected