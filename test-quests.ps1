# Quest System - Quick Test Script
# Prerequisites: Redis running, API running on localhost:5276

param(
    [Parameter(Mandatory=$true)]
    [string]$Token,
    
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:5276"
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Quest System - Acceptance Test" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Helper function
function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Endpoint,
        [string]$Description
    )
    
    Write-Host "TEST: $Description" -ForegroundColor Yellow
    Write-Host "  ? $Method $Endpoint" -ForegroundColor Gray
    
    try {
        $headers = @{
            "Authorization" = "Bearer $Token"
            "Content-Type" = "application/json"
        }
        
        $response = Invoke-WebRequest -Uri "$BaseUrl$Endpoint" `
            -Method $Method `
            -Headers $headers `
            -SkipHttpErrorCheck
        
        $statusCode = $response.StatusCode
        $statusColor = if ($statusCode -lt 300) { "Green" } elseif ($statusCode -lt 500) { "Yellow" } else { "Red" }
        
        Write-Host "  ? HTTP $statusCode" -ForegroundColor $statusColor
        
        if ($response.Content) {
            $content = $response.Content | ConvertFrom-Json -ErrorAction SilentlyContinue
            if ($content) {
                Write-Host "  Response:" -ForegroundColor Gray
                Write-Host ($content | ConvertTo-Json -Depth 5 | Out-String) -ForegroundColor Gray
            }
        }
        
        Write-Host ""
        return @{
            Success = ($statusCode -lt 400)
            StatusCode = $statusCode
            Content = $content
        }
    }
    catch {
        Write-Host "  ? ERROR: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        return @{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

# Test 1: View Today's Quests (Before check-in)
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "TEST 1: View Today's Quests (Initial State)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$result1 = Invoke-ApiRequest -Method "GET" -Endpoint "/quests/today" -Description "Get today's quests"

if ($result1.Success) {
    $points = $result1.Content.Points
    Write-Host "Current Points: $points" -ForegroundColor Green
    Write-Host ""
}

# Test 2: First Check-in (Should succeed)
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "TEST 2: First Check-in (Should succeed)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$result2 = Invoke-ApiRequest -Method "POST" -Endpoint "/quests/check-in" -Description "First check-in"

if ($result2.StatusCode -eq 204) {
    Write-Host "? PASS: First check-in succeeded (204 No Content)" -ForegroundColor Green
} else {
    Write-Host "? FAIL: Expected 204, got $($result2.StatusCode)" -ForegroundColor Red
}
Write-Host ""

# Test 3: View Quests After Check-in
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "TEST 3: View Quests After Check-in" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$result3 = Invoke-ApiRequest -Method "GET" -Endpoint "/quests/today" -Description "Get today's quests after check-in"

if ($result3.Success) {
    $newPoints = $result3.Content.Points
    $checkInQuest = $result3.Content.Quests | Where-Object { $_.Code -eq "CHECK_IN_DAILY" }
    
    if ($checkInQuest.Done -eq $true) {
        Write-Host "? PASS: CHECK_IN_DAILY marked as Done" -ForegroundColor Green
    } else {
        Write-Host "? FAIL: CHECK_IN_DAILY not marked as Done" -ForegroundColor Red
    }
    
    if ($newPoints -eq ($points + 5)) {
        Write-Host "? PASS: Points increased by 5 ($points ? $newPoints)" -ForegroundColor Green
    } else {
        Write-Host "? FAIL: Points did not increase correctly ($points ? $newPoints)" -ForegroundColor Red
    }
}
Write-Host ""

# Test 4: Second Check-in (Should fail - idempotent)
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "TEST 4: Second Check-in (Idempotency Test)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$result4 = Invoke-ApiRequest -Method "POST" -Endpoint "/quests/check-in" -Description "Second check-in (should fail)"

if ($result4.StatusCode -eq 400) {
    Write-Host "? PASS: Idempotency check succeeded (400 Bad Request)" -ForegroundColor Green
    
    if ($result4.Content.detail -match "already completed") {
        Write-Host "? PASS: Correct error message" -ForegroundColor Green
    } else {
        Write-Host "? FAIL: Incorrect error message" -ForegroundColor Red
    }
} else {
    Write-Host "? FAIL: Expected 400, got $($result4.StatusCode)" -ForegroundColor Red
}
Write-Host ""

# Test 5: Verify Points Unchanged
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "TEST 5: Verify Points Unchanged (Idempotency)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$result5 = Invoke-ApiRequest -Method "GET" -Endpoint "/quests/today" -Description "Get today's quests after second check-in"

if ($result5.Success) {
    $finalPoints = $result5.Content.Points
    
    if ($finalPoints -eq $newPoints) {
        Write-Host "? PASS: Points unchanged after idempotent request ($finalPoints)" -ForegroundColor Green
    } else {
        Write-Host "? FAIL: Points changed unexpectedly ($newPoints ? $finalPoints)" -ForegroundColor Red
    }
}
Write-Host ""

# Test Summary
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$passedTests = 0
$totalTests = 5

if ($result1.Success) { $passedTests++ }
if ($result2.StatusCode -eq 204) { $passedTests++ }
if ($result3.Success -and $checkInQuest.Done -eq $true) { $passedTests++ }
if ($result4.StatusCode -eq 400) { $passedTests++ }
if ($result5.Success -and $finalPoints -eq $newPoints) { $passedTests++ }

Write-Host "Passed: $passedTests / $totalTests" -ForegroundColor $(if ($passedTests -eq $totalTests) { "Green" } else { "Yellow" })
Write-Host ""

# Redis Verification
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "REDIS VERIFICATION (Manual)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$dateVn = (Get-Date).ToUniversalTime().AddHours(7).ToString("yyyyMMdd")
Write-Host "VN Date: $dateVn" -ForegroundColor Gray
Write-Host ""
Write-Host "Run these Redis commands to verify:" -ForegroundColor Yellow
Write-Host "  redis-cli GET `"q:${dateVn}:{YOUR_USER_ID}:CHECK_IN_DAILY`"" -ForegroundColor Gray
Write-Host "  redis-cli TTL `"q:${dateVn}:{YOUR_USER_ID}:CHECK_IN_DAILY`"" -ForegroundColor Gray
Write-Host ""

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Test completed!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
