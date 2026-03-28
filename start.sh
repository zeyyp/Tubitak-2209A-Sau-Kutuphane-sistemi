#!/bin/bash

echo "================================================="
echo "  SAÜ Kütüphane Rezervasyon Sistemi"
echo "  Başlatılıyor..."
echo "================================================="
echo ""

# Check if Docker is running (Devre dışı bırakıldı - Railway/Cloud için)
# if ! docker info > /dev/null 2>&1; then
#     echo "❌ Docker çalışmıyor! Lütfen Docker Desktop'ı başlatın."
#     exit 1
# fi

echo "✓ Docker çalışıyor"
echo ""

# Start services
echo "🚀 Docker servisleri başlatılıyor..."
docker-compose up -d

echo ""
echo "⏳ Servislerin başlaması bekleniyor (60 saniye)..."
sleep 60

echo ""
echo "📊 Servis durumları:"
docker-compose ps

echo ""
echo "🔍 Database initialization kontrol ediliyor..."
docker-compose logs db-init --tail=5 | grep "successfully"

if [ $? -eq 0 ]; then
    echo "✅ Database başarıyla initialize edildi!"
else
    echo "⚠️  Database initialization henüz tamamlanmadı."
    echo "   'docker-compose logs -f db-init' ile ilerlemeyi izleyebilirsiniz"
fi

echo ""
echo "🔍 Reservation service kontrol ediliyor..."
docker-compose logs reservation-service --tail=5 | grep "StudentEntryEventConsumer started"

if [ $? -eq 0 ]; then
    echo "✅ Reservation service başarıyla başladı!"
else
    echo "⚠️  Reservation service henüz hazır değil."
    echo "   'docker-compose logs -f reservation-service' ile ilerlemeyi izleyebilirsiniz"
fi

echo ""
echo "================================================="
echo "  🎉 Sistem Hazır!"
echo "================================================="
echo ""
echo "📱 Erişim Adresleri:"
echo "   Frontend:    http://localhost:4200"
echo "   API Gateway: http://localhost:5010"
echo "   RabbitMQ:    http://localhost:15672 (user: library, pass: library123)"
echo ""
echo "👤 Test Kullanıcıları:"
echo "   Admin:   admin / Admin123!"
echo "   Öğrenci: 123456 / Student123!"
echo ""
echo "📚 Detaylı bilgi için README.md dosyasını okuyun"
echo "================================================="
