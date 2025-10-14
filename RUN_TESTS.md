# Quick Start - Run Quest Tests

## Prerequisites ?
- [x] .NET 9 SDK installed
- [x] Docker Desktop installed
- [ ] Docker Desktop running ??

## Step 1: Start Docker

### Windows
```powershell
# Option 1: Start Docker Desktop application
Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"

# Option 2: Start service
Start-Service -Name "com.docker.service"

# Verify
docker ps
```

### Linux
```bash
sudo systemctl start docker
docker ps
```

### macOS
```bash
open -a Docker
docker ps
```

## Step 2: Run Tests

```bash
# Navigate to solution root
cd C:\Users\trung\Downloads\StudentGamerHub

# Run all quest tests
dotnet test Tests/Services.Quests.Tests/Services.Quests.Tests.csproj

# With detailed output
dotnet test Tests/Services.Quests.Tests/Services.Quests.Tests.csproj --logger "console;verbosity=detailed"

# Single test
dotnet test Tests/Services.Quests.Tests/Services.Quests.Tests.csproj --filter "CheckIn_FirstTime_ShouldSucceed"
```

## Expected Output

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

  Starting Redis container...
  Redis container started: redis:7-alpine
  Connecting to Redis...
  Connected to Redis at localhost:xxxxx
  
Passed CheckIn_FirstTime_ShouldSucceed [10ms]
Passed CheckIn_SecondTimeSameDay_ShouldFail_Idempotent [8ms]
Passed CheckIn_ShouldCreateRedisFlag_WithCorrectTTL [12ms]
Passed CompleteMultipleQuests_ShouldIncrementAnalyticsCounter [15ms]
Passed JoinRoom_ShouldComplete_Idempotent [9ms]
Passed InviteAccepted_ShouldComplete_Idempotent [9ms]
Passed AttendEvent_ShouldComplete_Idempotent [9ms]
Passed CompleteCheckIn_Parallel_ShouldOnlySucceedOnce [20ms]
Passed GetTodayAsync_ShouldReturnCorrectQuests_WithDoneStatus [8ms]
Passed GetTodayAsync_WithNoCompletedQuests_ShouldReturnAllFalse [7ms]
Passed CompleteQuest_WithInvalidUserId_ShouldReturnNotFound [6ms]
Passed CompleteQuest_WithEmptyUserId_ShouldReturnValidationError [5ms]

  Stopping Redis container...
  Redis container stopped

Test Run Successful.
Total tests: 12
     Passed: 12
     Failed: 0
     Skipped: 0
 Total time: ~10 seconds
```

## Troubleshooting

### Error: "Failed to connect to Docker endpoint"
**Problem**: Docker not running  
**Solution**: Start Docker Desktop (see Step 1)

### Error: "Port already in use"
**Problem**: Previous test container not cleaned up  
**Solution**:
```bash
docker ps -a | grep redis
docker rm -f <container_id>
```

### Slow First Run
**Normal**: First run downloads `redis:7-alpine` image (~10MB)  
**Expected**: Subsequent runs are faster (~3-5s startup)

## CI/CD Integration

### GitHub Actions
```yaml
name: Quest Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'
    
    - name: Start Docker
      run: |
        docker --version
        docker ps
    
    - name: Run Quest Tests
      run: |
        dotnet test Tests/Services.Quests.Tests/Services.Quests.Tests.csproj \
          --logger "trx;LogFileName=quest-tests.trx"
    
    - name: Publish Test Results
      if: always()
      uses: dorny/test-reporter@v1
      with:
        name: Quest System Tests
        path: '**/*.trx'
        reporter: dotnet-trx
```

## Alternative: Without Docker (Mock Redis)

If Docker not available, can mock Redis (less accurate):

```csharp
// In test constructor
var redisMock = Substitute.For<IConnectionMultiplexer>();
var dbMock = Substitute.For<IDatabase>();

dbMock.StringSetAsync(
    Arg.Any<RedisKey>(),
    Arg.Any<RedisValue>(),
    Arg.Any<TimeSpan?>(),
    When.NotExists,
    Arg.Any<CommandFlags>())
  .Returns(Task.FromResult(true)); // First call
  
// Use redisMock instead of Testcontainer
```

**Trade-off**: Fast but less accurate (no real TTL, no real SET NX atomicity)

## Quick Check

```bash
# 1. Check Docker
docker --version

# 2. Check .NET
dotnet --version

# 3. Build tests
dotnet build Tests/Services.Quests.Tests/Services.Quests.Tests.csproj

# 4. Run tests
dotnet test Tests/Services.Quests.Tests/Services.Quests.Tests.csproj

# Expected: ? 12 passed
```

## What Tests Verify

? Idempotency (SET NX atomic)  
? TTL calculation (00:00 VN timezone)  
? Transaction safety (points awarded in DB)  
? Analytics counter (INCR operations)  
? Parallelism (race conditions)  
? Error handling (404, 400 codes)  
? All quest types (4 quests)  
? GET API (done flags from Redis)  

---

**Status**: Tests ready, Docker required ?  
**Next**: Start Docker ? Run tests ? Verify 12/12 pass ?
