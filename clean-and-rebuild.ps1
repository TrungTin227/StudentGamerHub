# Clean & Rebuild Script
# S? d?ng khi c?n clear cache và rebuild toàn b? solution

Write-Host "=== CLEANING CACHE & REBUILDING ===" -ForegroundColor Cyan

# 1. Shutdown build servers
Write-Host "`n[1/6] Shutting down build servers..." -ForegroundColor Yellow
dotnet build-server shutdown

# 2. Clean solution
Write-Host "`n[2/6] Cleaning solution..." -ForegroundColor Yellow
dotnet clean

# 3. Remove bin/obj folders
Write-Host "`n[3/6] Removing bin/obj folders..." -ForegroundColor Yellow
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "? Deleted all bin/obj folders" -ForegroundColor Green

# 4. Restore packages
Write-Host "`n[4/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

# 5. Build solution
Write-Host "`n[5/6] Building solution..." -ForegroundColor Yellow
dotnet build --no-incremental

# 6. Summary
Write-Host "`n[6/6] DONE!" -ForegroundColor Green
Write-Host "? Cache cleared" -ForegroundColor Green
Write-Host "? Solution rebuilt" -ForegroundColor Green
Write-Host "`nBây gi? b?n có th?:" -ForegroundColor Cyan
Write-Host "  1. Nh?n F5 trong Visual Studio ?? ch?y" -ForegroundColor White
Write-Host "  2. M? https://localhost:7227/docs ?? xem Scalar UI" -ForegroundColor White
Write-Host "  3. Ki?m tra section 'Clubs' trong API documentation" -ForegroundColor White
