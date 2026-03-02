# Backend API Testing Script
# This script tests the secure fintech banking backend

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Banking Backend API Test Suite" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get API URL from Aspire dashboard (check the dashboard for actual URL)
Write-Host "Please enter the API URL from Aspire Dashboard (e.g., https://localhost:7001):" -ForegroundColor Yellow
$baseUrl = Read-Host
if ([string]::IsNullOrWhiteSpace($baseUrl)) {
    $baseUrl = "https://localhost:7001"
    Write-Host "Using default: $baseUrl" -ForegroundColor Gray
}

# Ignore SSL certificate errors for local testing
Add-Type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host ""
Write-Host "Test 1: Health Check" -ForegroundColor Green
Write-Host "--------------------"
try {
    $healthResponse = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get -SkipCertificateCheck
    Write-Host "✓ Health endpoint: " -NoNewline -ForegroundColor Green
    Write-Host "HEALTHY" -ForegroundColor Green
    $healthResponse | ConvertTo-Json -Depth 3
} catch {
    Write-Host "✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure the API is running. Check Aspire Dashboard at https://localhost:17065" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Test 2: Security Headers" -ForegroundColor Green
Write-Host "--------------------"
try {
    $headers = Invoke-WebRequest -Uri "$baseUrl/health" -Method Get -SkipCertificateCheck
    
    $securityHeaders = @(
        "X-Frame-Options",
        "X-Content-Type-Options",
        "Strict-Transport-Security",
        "Content-Security-Policy",
        "Referrer-Policy"
    )
    
    foreach ($header in $securityHeaders) {
        if ($headers.Headers[$header]) {
            Write-Host "✓ $header`: $($headers.Headers[$header])" -ForegroundColor Green
        } else {
            Write-Host "✗ Missing: $header" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "✗ Security headers check failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 3: Rate Limiting (100 requests/min)" -ForegroundColor Green
Write-Host "--------------------"
try {
    Write-Host "Sending 105 rapid requests to test rate limiter..." -ForegroundColor Gray
    $successCount = 0
    $rateLimitedCount = 0
    
    for ($i = 1; $i -le 105; $i++) {
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/health" -Method Get -SkipCertificateCheck -ErrorAction Stop
            $successCount++
        } catch {
            if ($_.Exception.Response.StatusCode -eq 429) {
                $rateLimitedCount++
            }
        }
    }
    
    Write-Host "✓ Successful requests: $successCount" -ForegroundColor Green
    Write-Host "✓ Rate limited (429): $rateLimitedCount" -ForegroundColor Green
    
    if ($rateLimitedCount -gt 0) {
        Write-Host "✓ Rate limiting is working!" -ForegroundColor Green
    } else {
        Write-Host "⚠ Rate limiting may not be configured correctly" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Rate limiting test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 4: Input Validation (SQL Injection Prevention)" -ForegroundColor Green
Write-Host "--------------------"
try {
    $maliciousInputs = @(
        "$baseUrl/api/users?id=1' OR '1'='1",
        "$baseUrl/api/users?search=<script>alert('xss')</script>",
        "$baseUrl/api/users?path=../../etc/passwd"
    )
    
    foreach ($input in $maliciousInputs) {
        try {
            $response = Invoke-WebRequest -Uri $input -Method Get -SkipCertificateCheck -ErrorAction Stop
            Write-Host "✗ Malicious input not blocked: $input" -ForegroundColor Red
        } catch {
            if ($_.Exception.Response.StatusCode -eq 400) {
                Write-Host "✓ Blocked malicious input: $($input.Split('?')[1])" -ForegroundColor Green
            } else {
                Write-Host "⚠ Unexpected response for: $($input.Split('?')[1])" -ForegroundColor Yellow
            }
        }
    }
} catch {
    Write-Host "✗ Input validation test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 5: Create User (Registration)" -ForegroundColor Green
Write-Host "--------------------"
try {
    $timestamp = Get-Date -Format "yyyyMMddHHmmss"
    $registerBody = @{
        name = "Test User $timestamp"
        email = "test$timestamp@example.com"
        phoneNumber = "+1234567890"
    } | ConvertTo-Json
    
    $registerResponse = Invoke-RestMethod -Uri "$baseUrl/api/users" -Method Post -Body $registerBody -ContentType "application/json" -SkipCertificateCheck
    Write-Host "✓ User created successfully!" -ForegroundColor Green
    Write-Host "  User ID: $($registerResponse.id)" -ForegroundColor Gray
    Write-Host "  Name: $($registerResponse.name)" -ForegroundColor Gray
    Write-Host "  Email: $($registerResponse.email)" -ForegroundColor Gray
    
    $global:testUserId = $registerResponse.id
} catch {
    Write-Host "✗ User creation failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "  Details: $($_.ErrorDetails.Message)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Test 6: Database Encryption Verification" -ForegroundColor Green
Write-Host "--------------------"
Write-Host "⚠ To verify encryption, connect to PostgreSQL and check if PII fields are encrypted:" -ForegroundColor Yellow
Write-Host "  1. Open PgAdmin at http://localhost:8080" -ForegroundColor Gray
Write-Host "  2. Connect to postgres server (password: postgres)" -ForegroundColor Gray
Write-Host "  3. Query: SELECT * FROM ""Accounts"" LIMIT 1;" -ForegroundColor Gray
Write-Host "  4. AccountNumber should be encrypted (binary data)" -ForegroundColor Gray

Write-Host ""
Write-Host "Test 7: Kafka Event Publishing" -ForegroundColor Green
Write-Host "--------------------"
Write-Host "⚠ To verify Kafka messages:" -ForegroundColor Yellow
Write-Host "  1. Open Kafka UI at http://localhost:8081" -ForegroundColor Gray
Write-Host "  2. Check topics: account-events, transaction-events, audit-logs" -ForegroundColor Gray
Write-Host "  3. View messages to see published events" -ForegroundColor Gray

Write-Host ""
Write-Host "Test 8: Audit Logging" -ForegroundColor Green
Write-Host "--------------------"
Write-Host "⚠ To verify audit logs:" -ForegroundColor Yellow
Write-Host "  1. Connect to PostgreSQL via PgAdmin" -ForegroundColor Gray
Write-Host "  2. Query: SELECT * FROM ""AuditLogs"" ORDER BY ""Timestamp"" DESC LIMIT 10;" -ForegroundColor Gray
Write-Host "  3. Verify Hash field contains SHA-256 hash for tamper detection" -ForegroundColor Gray

Write-Host ""
Write-Host "Test 9: LocalStack (AWS Services)" -ForegroundColor Green
Write-Host "--------------------"
Write-Host "Testing LocalStack connectivity..." -ForegroundColor Gray
try {
    $localstackHealth = Invoke-RestMethod -Uri "http://localhost:4566/_localstack/health" -Method Get
    Write-Host "✓ LocalStack services:" -ForegroundColor Green
    $localstackHealth.services.PSObject.Properties | ForEach-Object {
        $status = if ($_.Value -eq "running" -or $_.Value -eq "available") { "✓" } else { "✗" }
        $color = if ($_.Value -eq "running" -or $_.Value -eq "available") { "Green" } else { "Yellow" }
        Write-Host "  $status $($_.Name): $($_.Value)" -ForegroundColor $color
    }
} catch {
    Write-Host "✗ LocalStack not accessible: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "✓ Basic Tests Completed" -ForegroundColor Green
Write-Host ""
Write-Host "Manual Verification Required:" -ForegroundColor Yellow
Write-Host "1. Check Aspire Dashboard: https://localhost:17065" -ForegroundColor Gray
Write-Host "2. Check Kafka UI: http://localhost:8081" -ForegroundColor Gray
Write-Host "3. Check PgAdmin: http://localhost:8080" -ForegroundColor Gray
Write-Host "4. Check LocalStack: http://localhost:4566" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "- Create Account and Transaction controllers to test banking operations" -ForegroundColor Gray
Write-Host "- Test Saga orchestrator with money transfers" -ForegroundColor Gray
Write-Host "- Verify event sourcing in EventStore table" -ForegroundColor Gray
Write-Host "- Test JWT authentication when auth endpoints are created" -ForegroundColor Gray
Write-Host ""
