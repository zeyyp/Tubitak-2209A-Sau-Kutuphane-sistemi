<#
.SYNOPSIS
    Integration Test Runner Script
    TÜBİTAK 2209-A - SAÜ Kütüphane Rezervasyon Sistemi

.DESCRIPTION
    Bu script integration testlerini çalıştırır ve sonuçları raporlar.

.PARAMETER Category
    Çalıştırılacak test kategorisi (Authentication, Reservation, Turnstile, Feedback, All)

.PARAMETER Verbose
    Detaylı çıktı göster

.EXAMPLE
    .\run-tests.ps1 -Category All
    .\run-tests.ps1 -Category Authentication
#>

param(
    [ValidateSet("All", "Authentication", "Reservation", "Turnstile", "Feedback", "Security", "Priority")]
    [string]$Category = "All",
    
    [switch]$VerboseOutput,
    
    [switch]$GenerateReport
)

$ErrorActionPreference = "Continue"
$TestProject = "$PSScriptRoot\IntegrationTests\IntegrationTests.csproj"
$ReportPath = "$PSScriptRoot\TestResults"

function Write-Header {
    param([string]$Text)
    Write-Host "`n" -NoNewline
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Text)
    Write-Host "`n► $Text" -ForegroundColor Yellow
}

function Test-DockerServices {
    Write-Step "Docker servisleri kontrol ediliyor..."
    
    $services = @(
        @{Name="Identity Service"; Url="http://localhost:5001/health"},
        @{Name="Reservation Service"; Url="http://localhost:5002/health"},
        @{Name="Turnstile Service"; Url="http://localhost:5003/health"},
        @{Name="Feedback Service"; Url="http://localhost:5004/health"}
    )
    
    $allRunning = $true
    
    foreach ($service in $services) {
        try {
            $response = Invoke-WebRequest -Uri $service.Url -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            Write-Host "  ✓ $($service.Name): Çalışıyor" -ForegroundColor Green
        }
        catch {
            Write-Host "  ✗ $($service.Name): Erişilemiyor" -ForegroundColor Red
            $allRunning = $false
        }
    }
    
    return $allRunning
}

function Start-Tests {
    Write-Header "Integration Test Runner - SAÜ Kütüphane Sistemi"
    Write-Host "Tarih: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host "Kategori: $Category" -ForegroundColor Gray
    
    # Docker kontrol
    $servicesOk = Test-DockerServices
    
    if (-not $servicesOk) {
        Write-Host "`n⚠ Bazı servisler çalışmıyor. Önce 'docker-compose up -d' çalıştırın." -ForegroundColor Yellow
        Write-Host "Yine de devam etmek istiyor musunuz? (E/H): " -NoNewline -ForegroundColor Yellow
        $continue = Read-Host
        if ($continue -ne "E" -and $continue -ne "e") {
            Write-Host "Test iptal edildi." -ForegroundColor Red
            return
        }
    }
    
    # Test klasörünü oluştur
    if ($GenerateReport) {
        if (-not (Test-Path $ReportPath)) {
            New-Item -ItemType Directory -Path $ReportPath | Out-Null
        }
    }
    
    # Testleri çalıştır
    Write-Step "Testler başlatılıyor..."
    
    $filterArg = ""
    if ($Category -ne "All") {
        $filterArg = "--filter `"Category=$Category`""
    }
    
    $loggerArg = ""
    if ($GenerateReport) {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $loggerArg = "--logger `"trx;LogFileName=TestResults_$timestamp.trx`" --results-directory `"$ReportPath`""
    }
    
    $verboseArg = ""
    if ($VerboseOutput) {
        $verboseArg = "-v detailed"
    }
    
    $command = "dotnet test `"$TestProject`" $filterArg $loggerArg $verboseArg --no-build"
    
    Write-Host "Komut: $command" -ForegroundColor Gray
    Write-Host ""
    
    # Önce projeyi build et
    Write-Step "Proje derleniyor..."
    dotnet build $TestProject --configuration Release
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n✗ Derleme başarısız!" -ForegroundColor Red
        return
    }
    
    # Testleri çalıştır
    Write-Step "Testler çalıştırılıyor..."
    $startTime = Get-Date
    
    if ($Category -eq "All") {
        dotnet test $TestProject --no-build $verboseArg
    }
    else {
        dotnet test $TestProject --no-build --filter "Category=$Category" $verboseArg
    }
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    # Sonuç özeti
    Write-Header "Test Sonuç Özeti"
    Write-Host "Toplam Süre: $($duration.TotalSeconds.ToString('F2')) saniye" -ForegroundColor Cyan
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✓ TÜM TESTLER BAŞARILI!" -ForegroundColor Green
    }
    else {
        Write-Host "`n✗ BAZI TESTLER BAŞARISIZ!" -ForegroundColor Red
    }
    
    if ($GenerateReport) {
        Write-Host "`nTest raporu: $ReportPath" -ForegroundColor Gray
    }
}

# Ana çalıştırma
Start-Tests
