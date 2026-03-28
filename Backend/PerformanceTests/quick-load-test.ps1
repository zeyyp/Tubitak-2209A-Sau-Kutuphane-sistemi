<#
.SYNOPSIS
    Basit Performans Testi - TUBİTAK 2209-A

.EXAMPLE
    .\quick-load-test.ps1 -RequestCount 500 -ConcurrentBatch 20
#>

param(
    [int]$RequestCount = 500,
    [int]$ConcurrentBatch = 20,
    [string]$TargetUrl = "http://localhost:5002/api/Reservation/Faculties"
)

Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  TUBITAK 2209-A PERFORMANS TESTI" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ayarlar:" -ForegroundColor Yellow
Write-Host "  Toplam Istek: $RequestCount"
Write-Host "  Paralel Batch: $ConcurrentBatch"
Write-Host "  Hedef: $TargetUrl"
Write-Host ""
Write-Host "Test baslatiliyor..." -ForegroundColor Green
Write-Host ""

$successCount = 0
$failCount = 0
$responseTimes = @()
$startTime = Get-Date

# HTTP Client setup
Add-Type -AssemblyName System.Net.Http
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromSeconds(10)

$batches = [math]::Ceiling($RequestCount / $ConcurrentBatch)

for ($b = 0; $b -lt $batches; $b++) {
    $batchStart = $b * $ConcurrentBatch
    $batchEnd = [math]::Min($batchStart + $ConcurrentBatch, $RequestCount)
    $batchSize = $batchEnd - $batchStart
    
    $tasks = @()
    $stopwatches = @()
    
    # Start batch requests
    for ($i = 0; $i -lt $batchSize; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $stopwatches += $sw
        $tasks += $httpClient.GetAsync($TargetUrl)
    }
    
    # Wait for batch completion
    $taskWait = [System.Threading.Tasks.Task]::WaitAll($tasks, 15000)
    
    # Process results
    for ($i = 0; $i -lt $tasks.Count; $i++) {
        $stopwatches[$i].Stop()
        
        if ($tasks[$i].IsFaulted -or $tasks[$i].IsCanceled) {
            $failCount++
        }
        elseif ($tasks[$i].Result.StatusCode -eq [System.Net.HttpStatusCode]::OK) {
            $successCount++
            $responseTimes += $stopwatches[$i].ElapsedMilliseconds
        }
        else {
            $failCount++
        }
    }
    
    # Progress display
    $progress = [math]::Round((($b + 1) / $batches) * 100)
    $total = $successCount + $failCount
    Write-Host "  [$progress%] $total / $RequestCount istek tamamlandi (Basarili: $successCount, Basarisiz: $failCount)" -ForegroundColor Gray
}

$httpClient.Dispose()

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

# Calculate statistics
$totalRequests = $successCount + $failCount
$successRate = if ($totalRequests -gt 0) { [math]::Round(($successCount / $totalRequests) * 100, 2) } else { 0 }
$rps = [math]::Round($totalRequests / $duration, 1)

if ($responseTimes.Count -gt 0) {
    $avgTime = [math]::Round(($responseTimes | Measure-Object -Average).Average, 0)
    $minTime = ($responseTimes | Measure-Object -Minimum).Minimum
    $maxTime = ($responseTimes | Measure-Object -Maximum).Maximum
    $sortedTimes = $responseTimes | Sort-Object
    $p95Index = [math]::Floor($sortedTimes.Count * 0.95)
    $p95Time = if ($p95Index -lt $sortedTimes.Count -and $p95Index -ge 0) { $sortedTimes[$p95Index] } else { $sortedTimes[-1] }
} else {
    $avgTime = 0
    $minTime = 0
    $maxTime = 0
    $p95Time = 0
}

# Print results
Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "                   TEST SONUCLARI" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "GENEL BILGILER" -ForegroundColor Yellow
Write-Host "  Toplam Sure         : $([math]::Round($duration, 1)) saniye"
Write-Host "  Toplam Istek        : $totalRequests"
Write-Host "  Istek/Saniye (RPS)  : $rps"
Write-Host ""
Write-Host "BASARI ORANI" -ForegroundColor Yellow
$successColor = if ($successRate -ge 90) { "Green" } else { "Red" }
$failColor = if ($failCount -gt 0) { "Red" } else { "Green" }
Write-Host "  Basarili            : $successCount ($successRate%)" -ForegroundColor $successColor
Write-Host "  Basarisiz           : $failCount" -ForegroundColor $failColor
Write-Host ""
Write-Host "YANIT SURELERI" -ForegroundColor Yellow
Write-Host "  Ortalama            : ${avgTime}ms"
Write-Host "  Minimum             : ${minTime}ms"
Write-Host "  Maksimum            : ${maxTime}ms"
Write-Host "  P95                 : ${p95Time}ms"
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
Write-Host ""

# Save report
$report = @{
    TestDate = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    Configuration = @{
        TotalRequests = $RequestCount
        ConcurrentBatch = $ConcurrentBatch
        TargetUrl = $TargetUrl
    }
    Results = @{
        TotalRequests = $totalRequests
        SuccessfulRequests = $successCount
        FailedRequests = $failCount
        SuccessRate = $successRate
        RequestsPerSecond = $rps
        DurationSeconds = [math]::Round($duration, 1)
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
$reportPath = Join-Path $PSScriptRoot "perf-report-$timestamp.json"
$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "Rapor: $reportPath" -ForegroundColor Gray
Write-Host ""
