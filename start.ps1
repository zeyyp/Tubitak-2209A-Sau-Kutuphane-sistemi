# SAÜ Kütüphane Rezervasyon Sistemi - Başlatma Script'i
# Windows PowerShell

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  SAÜ Kütüphane Rezervasyon Sistemi" -ForegroundColor Cyan
Write-Host "  Başlatılıyor..." -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

# Docker kontrolü
try {
    docker info | Out-Null
    Write-Host "✓ Docker çalışıyor" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker çalışmıyor! Lütfen Docker Desktop'ı başlatın." -ForegroundColor Red
    exit 1
}

Write-Host ""

# Servisleri başlat
Write-Host "🚀 Docker servisleri başlatılıyor..." -ForegroundColor Yellow
docker-compose up -d

Write-Host ""
Write-Host "⏳ Servislerin başlaması bekleniyor (60 saniye)..." -ForegroundColor Yellow
Start-Sleep -Seconds 60

Write-Host ""
Write-Host "📊 Servis durumları:" -ForegroundColor Cyan
docker-compose ps

Write-Host ""
Write-Host "🔍 Database initialization kontrol ediliyor..." -ForegroundColor Yellow
$dbInitLog = docker-compose logs db-init --tail=5 | Select-String "successfully"
if ($dbInitLog) {
    Write-Host "✅ Database başarıyla initialize edildi!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Database initialization henüz tamamlanmadı." -ForegroundColor Yellow
    Write-Host "   'docker-compose logs -f db-init' ile ilerlemeyi izleyebilirsiniz" -ForegroundColor Gray
}

Write-Host ""
Write-Host "🔍 Reservation service kontrol ediliyor..." -ForegroundColor Yellow
$resServiceLog = docker-compose logs reservation-service --tail=5 | Select-String "StudentEntryEventConsumer started"
if ($resServiceLog) {
    Write-Host "✅ Reservation service başarıyla başladı!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Reservation service henüz hazır değil." -ForegroundColor Yellow
    Write-Host "   'docker-compose logs -f reservation-service' ile ilerlemeyi izleyebilirsiniz" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  🎉 Sistem Hazır!" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📱 Erişim Adresleri:" -ForegroundColor Cyan
Write-Host "   Frontend:    http://localhost:4200" -ForegroundColor White
Write-Host "   API Gateway: http://localhost:5010" -ForegroundColor White
Write-Host "   RabbitMQ:    http://localhost:15672 (user: library, pass: library123)" -ForegroundColor White
Write-Host ""
Write-Host "👤 Test Kullanıcıları:" -ForegroundColor Cyan
Write-Host "   Admin:   admin / Admin123!" -ForegroundColor White
Write-Host "   Öğrenci: 123456 / Student123!" -ForegroundColor White
Write-Host ""
Write-Host "📚 Detaylı bilgi için README.md dosyasını okuyun" -ForegroundColor Yellow
Write-Host "=================================================" -ForegroundColor Cyan
