<#
.SYNOPSIS
    PowerShell Performans Test Script
    TUBİTAK 2209-A - SAU Kutuphane Rezervasyon Sistemi

.PARAMETER ConcurrentUsers
    Ayni anda istek atacak kullanici sayisi (varsayilan: 50)

.PARAMETER Duration
    Test suresi saniye cinsinden (varsayilan: 60)

.EXAMPLE
    .\simple-load-test.ps1 -ConcurrentUsers 100 -Duration 30
#>

param(
    [int]$ConcurrentUsers = 50,
    [int]$Duration = 60,
    [string]$TargetUrl = "http://localhost:5002/api/Reservation/Faculties"
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  TUBITAK 2209-A PERFORMANS TESTI" -ForegroundColor Cyan
Write-Host "  SAU Kutuphane Rezervasyon Sistemi" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ayarlar:" -ForegroundColor Yellow
Write-Host "  Eszamanli Kullanici: $ConcurrentUsers"
Write-Host "  Test Suresi: $Duration saniye"
Write-Host "  Hedef URL: $TargetUrl"
Write-Host ""

$startTime = Get-Date

Write-Host "Test baslatiliyor..." -ForegroundColor Green
Write-Host "-------------------------------------------------------" -ForegroundColor Gray

# Worker function
$workerScript = {
    param($UserId, $TargetUrl, $Duration)
    
    $results = @{
        Success = 0
        Failed = 0
        Times = @()
    }
    
    $endTime = (Get-Date).AddSeconds($Duration)
    
    while ((Get-Date) -lt $endTime) {
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $response = Invoke-WebRequest -Uri $TargetUrl -UseBasicParsing -TimeoutSec 10
            $sw.Stop()
            
            if ($response.StatusCode -eq 200) {
                $results.Success++
                $results.Times += $sw.ElapsedMilliseconds
            } else {
                $results.Failed++
            }
        }
        catch {
            $results.Failed++
        }
        
        Start-Sleep -Milliseconds (Get-Random -Minimum 100 -Maximum 500)
    }
    
    return $results
}

# Start worker jobs
$jobs = @()
for ($i = 0; $i -lt $ConcurrentUsers; $i++) {
    $jobs += Start-Job -ScriptBlock $workerScript -ArgumentList $i, $TargetUrl, $Duration
}

# Show progress
$elapsed = 0
while ($elapsed -lt $Duration) {
    Start-Sleep -Seconds 5
    $elapsed = [math]::Min($elapsed + 5, $Duration)
    $percent = [math]::Round(($elapsed / $Duration) * 100)
    $completed = ($jobs | Where-Object { $_.State -eq "Completed" }).Count
    Write-Host "  [$percent%] Gecen sure: ${elapsed}s / ${Duration}s | Tamamlanan: $completed/$ConcurrentUsers" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Sonuclar toplaniyor..." -ForegroundColor Yellow

# Collect results
$allResults = @()
foreach ($job in $jobs) {
    $result = Receive-Job -Job $job -Wait
    if ($result) {
        $allResults += $result
    }
    Remove-Job -Job $job -Force
}

# Calculate statistics
$totalSuccess = ($allResults | Measure-Object -Property Success -Sum).Sum
$totalFailed = ($allResults | Measure-Object -Property Failed -Sum).Sum
$totalRequests = $totalSuccess + $totalFailed
$allTimes = $allResults | ForEach-Object { $_.Times } | Where-Object { $_ }

if ($allTimes.Count -gt 0) {
    $avgTime = [math]::Round(($allTimes | Measure-Object -Average).Average, 0)
    $minTime = ($allTimes | Measure-Object -Minimum).Minimum
    $maxTime = ($allTimes | Measure-Object -Maximum).Maximum
    $sortedTimes = $allTimes | Sort-Object
    $p95Index = [math]::Floor($sortedTimes.Count * 0.95)
    $p95Time = if ($p95Index -lt $sortedTimes.Count) { $sortedTimes[$p95Index] } else { $sortedTimes[-1] }
} else {
    $avgTime = 0
    $minTime = 0
    $maxTime = 0
    $p95Time = 0
}

$successRate = if ($totalRequests -gt 0) { [math]::Round(($totalSuccess / $totalRequests) * 100, 2) } else { 0 }
$failRate = if ($totalRequests -gt 0) { [math]::Round(($totalFailed / $totalRequests) * 100, 2) } else { 0 }
$rps = [math]::Round($totalRequests / $Duration, 1)

$endTime = Get-Date
$actualDuration = ($endTime - $startTime).TotalSeconds

# Print results
Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "                   TEST SONUCLARI" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "GENEL BILGILER" -ForegroundColor Yellow
Write-Host "  Toplam Sure         : $([math]::Round($actualDuration, 1)) saniye"
Write-Host "  Eszamanli Kullanici : $ConcurrentUsers"
Write-Host "  Toplam Istek        : $totalRequests"
Write-Host "  Istek/Saniye (RPS)  : $rps"
Write-Host ""
Write-Host "BASARI ORANI" -ForegroundColor Yellow
$successColor = "Green"
$failColor = if ($totalFailed -gt 0) { "Red" } else { "Green" }
Write-Host "  Basarili            : $totalSuccess ($successRate%)" -ForegroundColor $successColor
Write-Host "  Basarisiz           : $totalFailed ($failRate%)" -ForegroundColor $failColor
Write-Host ""
Write-Host "YANIT SURELERI" -ForegroundColor Yellow
Write-Host "  Ortalama            : ${avgTime}ms"
Write-Host "  Minimum             : ${minTime}ms"
Write-Host "  Maksimum            : ${maxTime}ms"
Write-Host "  P95 (95. yuzdelik)  : ${p95Time}ms"
Write-Host ""
Write-Host "-------------------------------------------------------" -ForegroundColor Gray
Write-Host "TUBITAK 2209-A KRITER DEGERLENDIRMESI" -ForegroundColor Yellow
Write-Host "-------------------------------------------------------" -ForegroundColor Gray

$criteriaPass = $successRate -ge 90
if ($criteriaPass) {
    Write-Host "  Kriter: %90 Basari Orani" -ForegroundColor Green
    Write-Host "  Sonuc:  $successRate% - BASARILI!" -ForegroundColor Green
} else {
    Write-Host "  Kriter: %90 Basari Orani" -ForegroundColor Red
    Write-Host "  Sonuc:  $successRate% - BASARISIZ!" -ForegroundColor Red
}
Write-Host "=======================================================" -ForegroundColor Cyan

# Save JSON report
$report = @{
    TestDate = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    Configuration = @{
        ConcurrentUsers = $ConcurrentUsers
        Duration = $Duration
        TargetUrl = $TargetUrl
    }
    Results = @{
        TotalRequests = $totalRequests
        SuccessfulRequests = $totalSuccess
        FailedRequests = $totalFailed
        SuccessRate = $successRate
        RequestsPerSecond = $rps
        ResponseTimes = @{
            Average = $avgTime
            Min = $minTime
            Max = $maxTime
            P95 = $p95Time
        }
    }
    TubitekCriteria = @{
        Required = 90
        Achieved = $successRate
        Passed = $criteriaPass
    }
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportPath = Join-Path $PSScriptRoot "performance-report-$timestamp.json"
$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "Rapor kaydedildi: $reportPath" -ForegroundColor Gray
Write-Host ""
