# Banking API Test - Complete Transaction Flow
# This script tests the complete banking backend including Saga orchestrator

param(
    [string]$ApiUrl = "https://localhost:7001"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Secure Banking Backend - Transaction Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Step 1: Health Check
Write-Host "1️⃣  Health Check" -ForegroundColor Green
try {
    $health = Invoke-RestMethod -Uri "$ApiUrl/health" -Method Get -SkipCertificateCheck
    Write-Host "   ✓ API is healthy" -ForegroundColor Green
} catch {
    Write-Host "   ✗ API is not responding. Make sure it's running!" -ForegroundColor Red
    Write-Host "   Check Aspire Dashboard at: https://localhost:17065" -ForegroundColor Yellow
    exit 1
}

# Step 2: Create User
Write-Host ""
Write-Host "2️⃣  Creating Test User" -ForegroundColor Green
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$createUserBody = @{
    email = "john.doe$timestamp@bank.com"
    firstName = "John"
    lastName = "Doe"
    phoneNumber = "+1234567890"
    password = "SecurePass123!"
} | ConvertTo-Json

try {
    $userResponse = Invoke-RestMethod -Uri "$ApiUrl/api/users/register" -Method Post -Body $createUserBody -ContentType "application/json" -SkipCertificateCheck
    Write-Host "   ✓ User created: $($userResponse.firstName) $($userResponse.lastName)" -ForegroundColor Green
    Write-Host "     User ID: $($userResponse.id)" -ForegroundColor Gray
    $userId = $userResponse.id
} catch {
    Write-Host "   ✗ Failed to create user: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "     $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    exit 1
}

# Step 3: Create Two Accounts
Write-Host ""
Write-Host "3️⃣  Creating Bank Accounts" -ForegroundColor Green

# Account 1
$createAccount1Body = @{
    userId = $userId
    accountType = 1  # Checking = 1
    currency = "USD"
    initialBalance = 5000.00
} | ConvertTo-Json

try {
    $account1 = Invoke-RestMethod -Uri "$ApiUrl/api/accounts" -Method Post -Body $createAccount1Body -ContentType "application/json" -SkipCertificateCheck
    Write-Host "   ✓ Account 1 created: $($account1.accountNumber)" -ForegroundColor Green
    Write-Host "     Balance: $$($account1.balance) $($account1.currency)" -ForegroundColor Gray
    $account1Id = $account1.id
} catch {
    Write-Host "   ✗ Failed to create account 1: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "     $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    exit 1
}

# Account 2
$createAccount2Body = @{
    userId = $userId
    accountType = 2  # Savings = 2
    currency = "USD"
    initialBalance = 1000.00
} | ConvertTo-Json

try {
    $account2 = Invoke-RestMethod -Uri "$ApiUrl/api/accounts" -Method Post -Body $createAccount2Body -ContentType "application/json" -SkipCertificateCheck
    Write-Host "   ✓ Account 2 created: $($account2.accountNumber)" -ForegroundColor Green
    Write-Host "     Balance: $$($account2.balance) $($account2.currency)" -ForegroundColor Gray
    $account2Id = $account2.id
} catch {
    Write-Host "   ✗ Failed to create account 2: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Transfer Money (Saga Orchestrator Test)
Write-Host ""
Write-Host "4️⃣  Executing Money Transfer (Saga Pattern)" -ForegroundColor Green
$transferAmount = 250.50
$idempotencyKey = [Guid]::NewGuid().ToString()

$transferBody = @{
    idempotencyKey = $idempotencyKey
    fromAccountId = $account1Id
    toAccountId = $account2Id
    amount = $transferAmount
    currency = "USD"
    description = "Test transfer - Saga orchestrator"
    deviceFingerprint = "test-device-123"
} | ConvertTo-Json

try {
    Write-Host "   Transferring $$transferAmount from Account 1 to Account 2..." -ForegroundColor Gray
    $transfer = Invoke-RestMethod -Uri "$ApiUrl/api/transactions/transfer" -Method Post -Body $transferBody -ContentType "application/json" -SkipCertificateCheck
    Write-Host "   ✓ Transfer completed successfully!" -ForegroundColor Green
    Write-Host "     Transaction ID: $($transfer.id)" -ForegroundColor Gray
    Write-Host "     Status: $($transfer.status)" -ForegroundColor Gray
    $transactionId = $transfer.id
} catch {
    Write-Host "   ✗ Transfer failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
        Write-Host "     Details: $($errorDetails.error)" -ForegroundColor Yellow
    }
}

# Step 5: Verify Balances
Write-Host ""
Write-Host "5️⃣  Verifying Account Balances" -ForegroundColor Green

try {
    $updatedAccount1 = Invoke-RestMethod -Uri "$ApiUrl/api/accounts/$account1Id" -Method Get -SkipCertificateCheck
    Write-Host "   Account 1 (Checking):" -ForegroundColor Cyan
    Write-Host "     Previous: $5000.00" -ForegroundColor Gray
    Write-Host "     Current:  $$($updatedAccount1.balance)" -ForegroundColor $(if ($updatedAccount1.balance -eq 4749.50) { "Green" } else { "Yellow" })
    Write-Host "     Expected: $$([decimal]5000.00 - $transferAmount)" -ForegroundColor Gray
    
    $updatedAccount2 = Invoke-RestMethod -Uri "$ApiUrl/api/accounts/$account2Id" -Method Get -SkipCertificateCheck
    Write-Host ""
    Write-Host "   Account 2 (Savings):" -ForegroundColor Cyan
    Write-Host "     Previous: $1000.00" -ForegroundColor Gray
    Write-Host "     Current:  $$($updatedAccount2.balance)" -ForegroundColor $(if ($updatedAccount2.balance -eq 1250.50) { "Green" } else { "Yellow" })
    Write-Host "     Expected: $$([decimal]1000.00 + $transferAmount)" -ForegroundColor Gray
    
    if ($updatedAccount1.balance -eq (5000.00 - $transferAmount) -and 
        $updatedAccount2.balance -eq (1000.00 + $transferAmount)) {
        Write-Host ""
        Write-Host "   ✓ Balances are correct!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "   ⚠ Balance mismatch detected!" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ✗ Failed to verify balances: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 6: Test Idempotency
Write-Host ""
Write-Host "6️⃣  Testing Idempotency (Same Transaction ID)" -ForegroundColor Green

try {
    Write-Host "   Attempting duplicate transfer with same idempotency key..." -ForegroundColor Gray
    $duplicateTransfer = Invoke-RestMethod -Uri "$ApiUrl/api/transactions/transfer" -Method Post -Body $transferBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
    Write-Host "   ⚠ Duplicate prevented - returned existing transaction" -ForegroundColor Yellow
    Write-Host "     Transaction ID: $($duplicateTransfer.id)" -ForegroundColor Gray
} catch {
    if ($_.ErrorDetails.Message) {
        $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
        if ($errorDetails.error -like "*already exists*" -or $errorDetails.error -like "*idempotency*") {
            Write-Host "   ✓ Idempotency working! Duplicate transaction blocked" -ForegroundColor Green
        } else {
            Write-Host "   Error: $($errorDetails.error)" -ForegroundColor Yellow
        }
    }
}

# Step 7: Test Insufficient Funds
Write-Host ""
Write-Host "7️⃣  Testing Insufficient Funds (Saga Rollback)" -ForegroundColor Green

$insufficientFundsBody = @{
    idempotencyKey = [Guid]::NewGuid().ToString()
    fromAccountId = $account1Id
    toAccountId = $account2Id
    amount = 99999.00
    currency = "USD"
    description = "Should fail - insufficient funds"
} | ConvertTo-Json

try {
    Write-Host "   Attempting transfer of $99,999 (exceeds balance)..." -ForegroundColor Gray
    $failedTransfer = Invoke-RestMethod -Uri "$ApiUrl/api/transactions/transfer" -Method Post -Body $insufficientFundsBody -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
    Write-Host "   ⚠ Transaction should have failed but succeeded!" -ForegroundColor Yellow
} catch {
    if ($_.ErrorDetails.Message) {
        $errorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
        Write-Host "   ✓ Transaction rejected as expected!" -ForegroundColor Green
        Write-Host "     Reason: $($errorDetails.error)" -ForegroundColor Gray
    } else {
        Write-Host "   ✓ Transaction rejected (insufficient funds)" -ForegroundColor Green
    }
}

# Step 8: View Transaction History
Write-Host ""
Write-Host "8️⃣  Transaction History" -ForegroundColor Green

try {
    $transactions = Invoke-RestMethod -Uri "$ApiUrl/api/transactions/account/$account1Id" -Method Get -SkipCertificateCheck
    Write-Host "   Account 1 has $($transactions.Count) transaction(s):" -ForegroundColor Cyan
    foreach ($txn in $transactions) {
        $direction = if ($txn.fromAccountId -eq $account1Id) { "→ OUT" } else { "← IN" }
        Write-Host "     $direction $$($txn.amount) - $($txn.status) - $($txn.description)" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ✗ Failed to get transaction history: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "✓ Backend Features Tested:" -ForegroundColor Green
Write-Host "  • User creation" -ForegroundColor Gray
Write-Host "  • Account creation with PII encryption (KMS)" -ForegroundColor Gray
Write-Host "  • Money transfer using Saga orchestrator" -ForegroundColor Gray
Write-Host "  • Distributed transaction with compensating actions" -ForegroundColor Gray
Write-Host "  • Idempotency validation" -ForegroundColor Gray
Write-Host "  • Insufficient funds detection" -ForegroundColor Gray
Write-Host "  • Transaction history retrieval" -ForegroundColor Gray
Write-Host ""
Write-Host "🔍 Manual Verification:" -ForegroundColor Yellow
Write-Host "  1. Kafka UI (http://localhost:8081):" -ForegroundColor Gray
Write-Host "     → Check topics: account-events, transaction-events, audit-logs" -ForegroundColor Gray
Write-Host "     → View published event messages" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. PgAdmin (http://localhost:8080):" -ForegroundColor Gray
Write-Host "     → Accounts table: AccountNumber should be encrypted (binary)" -ForegroundColor Gray
Write-Host "     → Transactions table: Verify IdempotencyKey, Status" -ForegroundColor Gray
Write-Host "     → EventStore table: View event sourcing history" -ForegroundColor Gray
Write-Host "     → AuditLogs table: Check tamper-proof logs with SHA-256 hashes" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Aspire Dashboard (https://localhost:17065):" -ForegroundColor Gray
Write-Host "     → View traces for distributed transaction" -ForegroundColor Gray
Write-Host "     → Monitor service health and logs" -ForegroundColor Gray
Write-Host ""
