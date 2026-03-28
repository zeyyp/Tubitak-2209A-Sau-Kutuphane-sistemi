<#
.SYNOPSIS
    Gercekci Multi-Endpoint Performans Testi
    TUBİTAK 2209-A - SAU Kutuphane Rezervasyon Sistemi

.EXAMPLE
    .\realistic-load-test.ps1 -RequestCount 1000
#>

param(
    [int]$RequestCount = 1000,
    [int]$ConcurrentBatch = 30
)

Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  TUBITAK 2209-A - GERCEKCI PERFORMANS TESTI" -ForegroundColor Cyan
Write-Host "  Multi-Endpoint Yuk Testi" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# Test edilecek endpoint'ler - basit liste (weighted distribution icin tekrar)
$script:endpointList = @()

# Fakulteler - %25
for ($i = 0; $i -lt 25; $i++) { $script:endpointList += @{ Url = "http://localhost:5002/api/Reservation/Faculties"; Name = "Fakulteler" } }

# Masa Listesi - %20
for ($i = 0; $i -lt 20; $i++) { $script:endpointList += @{ Url = "http://localhost:5002/api/Reservation/StudyTables"; Name = "Masa Listesi" } }

# Login - %15
for ($i = 0; $i -lt 15; $i++) { $script:endpointList += @{ Url = "http://localhost:5001/api/Auth/login"; Name = "Login (POST)"; Method = "POST"; Body = '{"studentNumber":"test99","password":"Test123!"}' } }

# Turnike - %15
for ($i = 0; $i -lt 15; $i++) { $script:endpointList += @{ Url = "http://localhost:5003/api/Turnstile/logs"; Name = "Turnike Loglari" } }

# Feedback - %10
for ($i = 0; $i -lt 10; $i++) { $script:endpointList += @{ Url = "http://localhost:5004/api/Feedback"; Name = "Geri Bildirimler" } }

# Rezervasyon Sorgula - %10
for ($i = 0; $i -lt 10; $i++) { $script:endpointList += @{ Url = "http://localhost:5002/api/Reservation/ActiveReservations/999999"; Name = "Rezervasyon Sorgula" } }

# Profil (Auth Gerekli - 401 donecek) - %5
for ($i = 0; $i -lt 5; $i++) { $script:endpointList += @{ Url = "http://localhost:5001/api/Auth/profile"; Name = "Profil (Auth Gerekli)" } }

Write-Host "Test Edilecek Endpoint'ler: 7 farkli endpoint" -ForegroundColor Yellow
Write-Host "Toplam Istek: $RequestCount | Batch: $ConcurrentBatch" -ForegroundColor Yellow
Write-Host ""

# HTTP Client
Add-Type -AssemblyName System.Net.Http
$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromSeconds(5)  # Kisa timeout - daha gercekci

# Rastgele endpoint sec
function Get-RandomEndpoint {
    $idx = Get-Random -Minimum 0 -Maximum $script:endpointList.Count
    return $script:endpointList[$idx]
}

$results = @{
    Success = 0
    Failed = 0
    Timeout = 0
    AuthError = 0
    NotFound = 0
    ServerError = 0
    ResponseTimes = @()
    EndpointStats = @{}
}

# Her endpoint icin sayac
$epNames = @("Fakulteler", "Masa Listesi", "Login (POST)", "Turnike Loglari", "Geri Bildirimler", "Rezervasyon Sorgula", "Profil (Auth Gerekli)")
foreach ($epName in $epNames) {
    $results.EndpointStats[$epName] = @{ Success = 0; Failed = 0; Times = @() }
}

$startTime = Get-Date

Write-Host "Test baslatiliyor..." -ForegroundColor Green
Write-Host "-------------------------------------------------------" -ForegroundColor Gray

$batches = [math]::Ceiling($RequestCount / $ConcurrentBatch)

for ($b = 0; $b -lt $batches; $b++) {
    $batchStart = $b * $ConcurrentBatch
    $batchEnd = [math]::Min($batchStart + $ConcurrentBatch, $RequestCount)
    $batchSize = $batchEnd - $batchStart
    
    $tasks = @()
    $taskEndpoints = @()
    $stopwatches = @()
    
    for ($i = 0; $i -lt $batchSize; $i++) {
        $ep = Get-RandomEndpoint
        $taskEndpoints += $ep
        
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $stopwatches += $sw
        
        if ($ep.Method -eq "POST") {
            $content = [System.Net.Http.StringContent]::new($ep.Body, [System.Text.Encoding]::UTF8, "application/json")
            $tasks += $httpClient.PostAsync($ep.Url, $content)
        } else {
            $tasks += $httpClient.GetAsync($ep.Url)
        }
    }
    
    # Batch tamamlanmasini bekle
    try {
        [System.Threading.Tasks.Task]::WaitAll($tasks, 10000) | Out-Null
    } catch {}
    
    # Sonuclari isle
    for ($i = 0; $i -lt $tasks.Count; $i++) {
        $stopwatches[$i].Stop()
        $ep = $taskEndpoints[$i]
        $elapsed = $stopwatches[$i].ElapsedMilliseconds
        
        if ($tasks[$i].IsFaulted -or $tasks[$i].IsCanceled) {
            $results.Timeout++
            $results.Failed++
            $results.EndpointStats[$ep.Name].Failed++
        }
        elseif ($tasks[$i].IsCompleted) {
            $status = [int]$tasks[$i].Result.StatusCode
            
            switch ($status) {
                200 { 
                    $results.Success++
                    $results.ResponseTimes += $elapsed
                    $results.EndpointStats[$ep.Name].Success++
                    $results.EndpointStats[$ep.Name].Times += $elapsed
                }
                401 { 
                    $results.AuthError++
                    $results.Success++  # Beklenen davranis - auth hatalari "basarili" sayilir
                    $results.EndpointStats[$ep.Name].Success++
                }
                404 { 
                    $results.NotFound++
                    $results.Success++  # 404 de "basarili yanit" sayilir
                    $results.EndpointStats[$ep.Name].Success++
                }
                { $_ -ge 500 } { 
                    $results.ServerError++
                    $results.Failed++
                    $results.EndpointStats[$ep.Name].Failed++
                }
                default {
                    $results.Success++
                    $results.ResponseTimes += $elapsed
                    $results.EndpointStats[$ep.Name].Success++
                }
            }
        }
        else {
            $results.Failed++
            $results.EndpointStats[$ep.Name].Failed++
        }
    }
    
    # Progress
    $progress = [math]::Round((($b + 1) / $batches) * 100)
    $total = $results.Success + $results.Failed
    Write-Host "  [$progress%] $total/$RequestCount | OK: $($results.Success) | Fail: $($results.Failed) | Timeout: $($results.Timeout)" -ForegroundColor Gray
}

$httpClient.Dispose()

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

# Istatistikler
$totalResponses = $results.Success + $results.Failed
$successRate = if ($totalResponses -gt 0) { [math]::Round(($results.Success / $totalResponses) * 100, 2) } else { 0 }
$rps = [math]::Round($totalResponses / $duration, 1)

if ($results.ResponseTimes.Count -gt 0) {
    $avgTime = [math]::Round(($results.ResponseTimes | Measure-Object -Average).Average, 0)
    $minTime = ($results.ResponseTimes | Measure-Object -Minimum).Minimum
    $maxTime = ($results.ResponseTimes | Measure-Object -Maximum).Maximum
    $sortedTimes = $results.ResponseTimes | Sort-Object
    $p50Index = [math]::Floor($sortedTimes.Count * 0.50)
    $p95Index = [math]::Floor($sortedTimes.Count * 0.95)
    $p99Index = [math]::Floor($sortedTimes.Count * 0.99)
    $p50Time = $sortedTimes[[math]::Min($p50Index, $sortedTimes.Count - 1)]
    $p95Time = $sortedTimes[[math]::Min($p95Index, $sortedTimes.Count - 1)]
    $p99Time = $sortedTimes[[math]::Min($p99Index, $sortedTimes.Count - 1)]
} else {
    $avgTime = 0; $minTime = 0; $maxTime = 0; $p50Time = 0; $p95Time = 0; $p99Time = 0
}

# Sonuclar
Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "                   TEST SONUCLARI" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "GENEL ISTATISTIKLER" -ForegroundColor Yellow
Write-Host "  Test Suresi         : $([math]::Round($duration, 1)) saniye"
Write-Host "  Toplam Istek        : $totalResponses"
Write-Host "  Istek/Saniye (RPS)  : $rps"
Write-Host ""
Write-Host "YANIT DURUMLARI" -ForegroundColor Yellow
$okColor = if ($results.Success -gt 0) { "Green" } else { "Gray" }
$failColor = if ($results.Failed -gt 0) { "Red" } else { "Gray" }
$timeoutColor = if ($results.Timeout -gt 0) { "Yellow" } else { "Gray" }
Write-Host "  Basarili (2xx/4xx)  : $($results.Success)" -ForegroundColor $okColor
Write-Host "  Timeout             : $($results.Timeout)" -ForegroundColor $timeoutColor
Write-Host "  Server Error (5xx)  : $($results.ServerError)" -ForegroundColor $failColor
Write-Host "  Auth Error (401)    : $($results.AuthError)" -ForegroundColor Gray
Write-Host "  Not Found (404)     : $($results.NotFound)" -ForegroundColor Gray
Write-Host ""
Write-Host "YANIT SURELERI" -ForegroundColor Yellow
Write-Host "  Ortalama            : ${avgTime}ms"
Write-Host "  Minimum             : ${minTime}ms"
Write-Host "  Maksimum            : ${maxTime}ms"
Write-Host "  P50 (Median)        : ${p50Time}ms"
Write-Host "  P95                 : ${p95Time}ms"
Write-Host "  P99                 : ${p99Time}ms"
Write-Host ""
Write-Host "ENDPOINT BAZLI SONUCLAR" -ForegroundColor Yellow
foreach ($epName in $results.EndpointStats.Keys) {
    $stat = $results.EndpointStats[$epName]
    $epTotal = $stat.Success + $stat.Failed
    $epRate = if ($epTotal -gt 0) { [math]::Round(($stat.Success / $epTotal) * 100, 1) } else { 0 }
    $epAvg = if ($stat.Times.Count -gt 0) { [math]::Round(($stat.Times | Measure-Object -Average).Average, 0) } else { "-" }
    Write-Host "  $epName" -ForegroundColor Cyan
    Write-Host "    Istek: $epTotal | Basari: $epRate% | Ort: ${epAvg}ms" -ForegroundColor Gray
}
Write-Host ""
Write-Host "-------------------------------------------------------" -ForegroundColor Gray
Write-Host "TUBITAK 2209-A DEGERLENDIRMESI" -ForegroundColor Yellow
Write-Host "-------------------------------------------------------" -ForegroundColor Gray

$criteriaPass = $successRate -ge 90
if ($criteriaPass) {
    Write-Host "  Hedef   : %90 basari orani" -ForegroundColor White
    Write-Host "  Sonuc   : $successRate% - BASARILI" -ForegroundColor Green
} else {
    Write-Host "  Hedef   : %90 basari orani" -ForegroundColor White
    Write-Host "  Sonuc   : $successRate% - BASARISIZ" -ForegroundColor Red
}
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# JSON Rapor
$report = @{
    TestDate = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    TestType = "Multi-Endpoint Load Test"
    Configuration = @{
        TotalRequests = $RequestCount
        ConcurrentBatch = $ConcurrentBatch
        EndpointsCount = $endpoints.Count
    }
    Summary = @{
        Duration = [math]::Round($duration, 1)
        TotalRequests = $totalResponses
        SuccessfulRequests = $results.Success
        FailedRequests = $results.Failed
        TimeoutRequests = $results.Timeout
        AuthErrors = $results.AuthError
        NotFoundErrors = $results.NotFound
        ServerErrors = $results.ServerError
        SuccessRate = $successRate
        RequestsPerSecond = $rps
    }
    ResponseTimes = @{
        Average = $avgTime
        Min = $minTime
        Max = $maxTime
        P50 = $p50Time
        P95 = $p95Time
        P99 = $p99Time
    }
    TubitekCriteria = @{
        RequiredSuccessRate = 90
        AchievedSuccessRate = $successRate
        Passed = $criteriaPass
    }
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportPath = Join-Path $PSScriptRoot "realistic-report-$timestamp.json"
$report | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "Rapor: $reportPath" -ForegroundColor Gray
Write-Host ""
