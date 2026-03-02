# Testing Guide - Secure Fintech Banking Backend

## 🚀 Quick Start - Local Development with Aspire

### Step 1: Run with .NET Aspire AppHost

```powershell
# Navigate to AppHost directory
cd AppHost

# Run Aspire orchestration (starts all services)
dotnet run

# Or run the entire solution
dotnet run --project AppHost/PastryManager.AppHost.csproj
```

**This will automatically start:**
- ✅ PostgreSQL (port 5433) with PgAdmin
- ✅ Kafka (port 9092) with Kafka UI
- ✅ LocalStack (port 4566) - S3, KMS, Secrets Manager
- ✅ PastryManager API

**Access Points:**
- API: https://localhost:7xxx (check Aspire dashboard)
- Aspire Dashboard: http://localhost:15xxx (opens automatically)
- PgAdmin: http://localhost:8080
- Kafka UI: http://localhost:8081
- LocalStack: http://localhost:4566

---

## 📋 Step-by-Step Testing

### 1. Run Database Migrations

```powershell
# Navigate to Infrastructure project
cd PastryManager.Infrastructure

# Add initial migration
dotnet ef migrations add InitialBankingSchema --startup-project ../PastryManager

# Update database
dotnet ef database update --startup-project ../PastryManager
```

### 2. Initialize Kafka Topics

The topics will be auto-created on first use, or you can create them manually:

```powershell
# Access Kafka container (via Docker)
docker exec -it <kafka-container-id> /bin/bash

# Create topics
kafka-topics --create --topic account-events --bootstrap-server localhost:9092 --partitions 3 --replication-factor 1
kafka-topics --create --topic transaction-events --bootstrap-server localhost:9092 --partitions 3 --replication-factor 1
kafka-topics --create --topic audit-logs --bootstrap-server localhost:9092 --partitions 2 --replication-factor 1
kafka-topics --create --topic dead-letter-queue --bootstrap-server localhost:9092 --partitions 2 --replication-factor 1

# List topics
kafka-topics --list --bootstrap-server localhost:9092
```

Or use **Kafka UI** at http://localhost:8081 to create topics via the web interface.

### 3. Initialize LocalStack (S3, KMS, Secrets Manager)

```powershell
# Install AWS CLI Local wrapper (if not already installed)
pip install awscli-local

# Create S3 bucket
awslocal s3 mb s3://pastrymanager-uploads-dev

# Create KMS key
awslocal kms create-key --description "Dev encryption key"

# Get the key ID
$KEY_ID = (awslocal kms list-keys --query 'Keys[0].KeyId' --output text)

# Create alias
awslocal kms create-alias --alias-name alias/dev-key --target-key-id $KEY_ID

# Verify KMS setup
awslocal kms describe-key --key-id alias/dev-key
```

---

## 🧪 API Testing

### Option 1: Swagger UI (Recommended for Quick Testing)

1. Run the AppHost
2. Open Swagger UI (displayed in Aspire dashboard)
3. Click **Authorize** button
4. You'll need a JWT token first (see authentication flow below)

### Option 2: REST Client (VS Code Extension)

Create a file `test.http`:

```http
### Variables
@baseUrl = https://localhost:7xxx
@token = your_jwt_token_here

### 1. Register User
POST {{baseUrl}}/api/users
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePass123!",
  "name": "Test User"
}

### 2. Login (Get JWT Token)
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "SecurePass123!"
}

### 3. Create Account
POST {{baseUrl}}/api/accounts
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "accountType": "Checking",
  "currency": "USD",
  "initialBalance": 1000.00
}

### 4. Get Account
GET {{baseUrl}}/api/accounts/{{accountId}}
Authorization: Bearer {{token}}

### 5. Create Transaction
POST {{baseUrl}}/api/transactions
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "fromAccountId": "{{accountId}}",
  "toAccountId": "{{toAccountId}}",
  "amount": 100.00,
  "currency": "USD",
  "transactionType": "Transfer",
  "idempotencyKey": "{{$guid}}",
  "description": "Test transfer"
}

### 6. Check Health
GET {{baseUrl}}/health
```

### Option 3: PowerShell Script

```powershell
# Set base URL
$baseUrl = "https://localhost:7001"

# 1. Register User
$registerBody = @{
    email = "test@example.com"
    password = "SecurePass123!"
    name = "Test User"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "$baseUrl/api/users" -Method Post -Body $registerBody -ContentType "application/json"

# 2. Login and get token
$loginBody = @{
    email = "test@example.com"
    password = "SecurePass123!"
} | ConvertTo-Json

$authResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $authResponse.accessToken

# 3. Create account
$headers = @{
    "Authorization" = "Bearer $token"
}

$accountBody = @{
    accountType = "Checking"
    currency = "USD"
    initialBalance = 1000.00
} | ConvertTo-Json

$account = Invoke-RestMethod -Uri "$baseUrl/api/accounts" -Method Post -Body $accountBody -ContentType "application/json" -Headers $headers

Write-Host "Account created: $($account.id)"
```

---

## 🔍 Testing Scenarios

### Scenario 1: Basic Account Creation

```http
POST /api/accounts
Authorization: Bearer {{token}}

{
  "accountType": "Checking",
  "currency": "USD",
  "initialBalance": 1000.00
}
```

**Expected Result:**
- ✅ Account created in database
- ✅ `AccountCreatedEvent` published to Kafka topic `account-events`
- ✅ Event stored in EventStore table
- ✅ Audit log created
- ✅ PII fields (AccountNumber) encrypted with KMS

**Verify:**
```sql
-- Check database
SELECT * FROM "Accounts" ORDER BY "CreatedAt" DESC LIMIT 1;

-- Check event store
SELECT * FROM "EventStore" WHERE "EventType" = 'AccountCreatedEvent' ORDER BY "Timestamp" DESC LIMIT 1;

-- Check audit logs
SELECT * FROM "AuditLogs" WHERE "Action" LIKE '%Account%' ORDER BY "Timestamp" DESC LIMIT 5;
```

**Verify Kafka** (via Kafka UI at http://localhost:8081):
- Navigate to Topics → `account-events`
- View messages to see the `AccountCreatedEvent`

### Scenario 2: Money Transfer (Saga Pattern)

```http
POST /api/transactions
Authorization: Bearer {{token}}

{
  "fromAccountId": "guid-of-source-account",
  "toAccountId": "guid-of-destination-account",
  "amount": 100.00,
  "currency": "USD",
  "transactionType": "Transfer",
  "idempotencyKey": "unique-key-12345",
  "description": "Test transfer"
}
```

**Expected Result:**
- ✅ `TransactionInitiatedEvent` published
- ✅ Saga orchestrator debits source account
- ✅ Saga orchestrator credits destination account
- ✅ `TransactionCompletedEvent` published
- ✅ Both accounts updated with new balances
- ✅ Optimistic locking prevents race conditions

**Test Failure Scenario:**
```http
# Insufficient funds
POST /api/transactions
{
  "fromAccountId": "guid",
  "toAccountId": "guid",
  "amount": 999999.00,  # More than available
  "currency": "USD",
  "transactionType": "Transfer",
  "idempotencyKey": "unique-key-67890"
}
```

**Expected Result:**
- ✅ Transaction fails
- ✅ `TransactionFailedEvent` published
- ✅ No account balances changed (compensating transaction works)

### Scenario 3: Test Idempotency

```http
# Send the same request twice with same idempotency key
POST /api/transactions
{
  "idempotencyKey": "same-key-123",
  "fromAccountId": "guid",
  "toAccountId": "guid",
  "amount": 50.00,
  "currency": "USD",
  "transactionType": "Transfer"
}
```

**Expected Result:**
- ✅ First request: Transaction processed
- ✅ Second request: Returns existing transaction (no duplicate created)

### Scenario 4: Rate Limiting

```powershell
# Rapid fire 150 requests
1..150 | ForEach-Object {
    Invoke-RestMethod -Uri "$baseUrl/api/health" -Method Get
}
```

**Expected Result:**
- ✅ First 100 requests: 200 OK
- ✅ Requests 101-150: 429 Too Many Requests
- ✅ `Retry-After` header present

### Scenario 5: Security Headers

```powershell
$response = Invoke-WebRequest -Uri "$baseUrl/api/health" -Method Get
$response.Headers
```

**Expected Headers:**
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `X-Content-Type-Options: nosniff`
- `Strict-Transport-Security: max-age=31536000`
- `Content-Security-Policy: ...`

---

## 📊 Monitoring & Verification

### Aspire Dashboard
Open the Aspire dashboard (automatically opens when you run AppHost):
- View all running services
- Check logs in real-time
- View traces across services
- Monitor resource usage

### PgAdmin (Database)
1. Open http://localhost:8080
2. Login: `admin@admin.com` / `admin`
3. Connect to PostgreSQL server:
   - Host: `postgres` (service name)
   - Port: `5432`
   - Username: `postgres`
   - Password: `postgres`

### Kafka UI
1. Open http://localhost:8081
2. View topics, messages, consumer groups
3. Monitor message throughput

### LocalStack (AWS Services)
```powershell
# List S3 buckets
awslocal s3 ls

# List KMS keys
awslocal kms list-keys

# List secrets
awslocal secretsmanager list-secrets

# Get secret value
awslocal secretsmanager get-secret-value --secret-id pastrymanager/jwt/secret
```

---

## 🧪 Unit & Integration Testing

### Create Test Project

```powershell
# Create test project
dotnet new xunit -n PastryManager.Tests
cd PastryManager.Tests

# Add references
dotnet add reference ../PastryManager/PastryManager.Api.csproj
dotnet add reference ../PastryManager.Infrastructure/PastryManager.Infrastructure.csproj

# Add test packages
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Testcontainers
dotnet add package FluentAssertions
```

### Sample Integration Test

```csharp
public class AccountApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public AccountApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task CreateAccount_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = await GetAuthToken(client);
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        var request = new CreateAccountRequest
        {
            AccountType = AccountType.Checking,
            Currency = "USD",
            InitialBalance = 1000m
        };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/accounts", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account.Should().NotBeNull();
        account.Balance.Should().Be(1000m);
    }
}
```

---

## 🎯 Performance Testing

### Using k6 (Load Testing)

Install k6: https://k6.io/docs/get-started/installation/

```javascript
// load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  vus: 100, // 100 virtual users
  duration: '30s',
};

export default function () {
  const url = 'https://localhost:7001/api/health';
  const res = http.get(url);
  
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
  
  sleep(1);
}
```

Run: `k6 run load-test.js`

---

## 🐛 Troubleshooting

### Issue: Kafka connection fails
```
Solution: Ensure Kafka is running and accepting connections on localhost:9092
Check: docker ps | grep kafka
Restart: Stop and restart AppHost
```

### Issue: Database migration fails
```
Solution: Check PostgreSQL is running on port 5433
Fix: dotnet ef database update --startup-project ../PastryManager --verbose
```

### Issue: KMS encryption fails
```
Solution: Verify LocalStack KMS key exists
Check: awslocal kms list-keys
Fix: Run the init-secrets.sh script manually
```

### Issue: JWT token validation fails
```
Solution: Ensure JWT secret key is at least 64 characters
Check: appsettings.Development.json -> Jwt:SecretKey
```

---

## ✅ Testing Checklist

- [ ] AppHost starts all services successfully
- [ ] Database migrations applied
- [ ] Kafka topics created
- [ ] LocalStack initialized (S3, KMS, Secrets)
- [ ] API health endpoint returns 200
- [ ] User registration works
- [ ] JWT authentication works
- [ ] Account creation publishes events to Kafka
- [ ] Money transfer saga completes successfully
- [ ] Idempotency prevents duplicate transactions
- [ ] Rate limiting blocks excessive requests
- [ ] Security headers present in responses
- [ ] PII fields encrypted in database
- [ ] Audit logs created for sensitive operations
- [ ] Circuit breaker activates on failures
- [ ] Dead letter queue captures failed messages

---

## 📈 Next Steps

1. **Add more controllers** for Accounts and Transactions
2. **Implement background consumers** for Kafka events
3. **Add integration tests** with Testcontainers
4. **Configure CI/CD** pipeline with automated tests
5. **Add Prometheus metrics** for monitoring
6. **Set up Grafana** dashboards
7. **Implement health checks** for Kafka and KMS connectivity

---

## 📚 Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Kafka Testing Best Practices](https://kafka.apache.org/documentation/#testing)
- [LocalStack Documentation](https://docs.localstack.cloud/)
- [xUnit Testing](https://xunit.net/)
