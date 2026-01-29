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
- Entities: `User`, `TaskRequest`, `TaskComment`
- Enums: `UserRole`, `TaskStatus`, `TaskPriority`
- Soft delete support

#### **Application Layer** - Business logic (CQRS + MediatR 13.0.0)
- Commands: RegisterUser, CreateTaskRequest, UpdateTaskStatus
- Queries: GetUserById, GetTasksByAssignedUser
- FluentValidation 11.11.0 for validation
- Repository interfaces

#### **Infrastructure Layer** - Data access (EF Core 8.0.11 + PostgreSQL 8.0.11)
- Repository implementations
- Entity configurations
- PBKDF2 password hashing (100k iterations)
- Database migrations

#### **API Layer** - REST API with Swagger
- Controllers for Users and TaskRequests
- Auto-migration in development
- CORS enabled

## 🚀 Quick Start

**Start the application:**
```bash
dotnet run --project AppHost/PastryManager.AppHost.csproj
```

**Access:**
- Aspire Dashboard: Check terminal output for URL (e.g., `https://localhost:17065`)
- API Swagger: Available through Aspire dashboard links

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

**Users:**
- `POST /api/users/register` - Register new user
- `GET /api/users/{id}` - Get user by ID

**Task Requests:**
- `POST /api/taskrequests` - Create task (requires X-User-Id header)
- `GET /api/taskrequests/assigned/{userId}` - Get tasks by assigned user
- `PATCH /api/taskrequests/{id}/status` - Update task status

## 📦 Key Technologies

- .NET 8 / ASP.NET Core
- MediatR 13.0.0 (CQRS)
- FluentValidation 11.11.0
- EF Core 8.0.11
- PostgreSQL via Aspire
- Swagger/OpenAPI

## 🎯 Features

✅ User registration with secure password hashing  
✅ Task request management with status tracking  
✅ Priority levels (Low, Medium, High, Critical)  
✅ Soft delete for all entities  
✅ Clean Architecture for scalability  
✅ CQRS pattern with MediatR  
✅ Comprehensive validation  
✅ Auto-migration in development  

## 📄 Database Schema

**Users**: Email (unique), Name, Role, Password (hashed)  
**TaskRequests**: Title, Description, Priority, Status, DueDate, Assignments  
**TaskComments**: Content, linked to Task and User  

All tables include audit fields (CreatedAt, UpdatedAt) and soft delete support.