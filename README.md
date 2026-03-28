# Sakarya Üniversitesi Kütüphane Rezervasyon Sistemi

## 🚀 Hızlı Başlangıç

### Ön Gereksinimler
- Docker Desktop (Windows/Mac/Linux)
- Git

### Kurulum Adımları

1. **Projeyi klonlayın**
```bash
git clone <repository-url>
cd Tubitak-2209A-Sau-Kutuphane-sistemi
```

2. **Otomatik başlatma script'i ile başlatın** (Önerilen)

**Windows için:**
```powershell
.\start.ps1
```

**Linux/Mac için:**
```bash
chmod +x start.sh
./start.sh
```

Script otomatik olarak:
- ✅ Docker'ın çalıştığını kontrol eder
- ✅ Servisleri başlatır
- ✅ Veritabanı ve servislerin hazır olmasını bekler
- ✅ Durumu raporlar

**Manuel başlatma:**
```bash
docker-compose up -d

# Logları izleyerek ilerlemeyi görebilirsiniz:
docker-compose logs -f db-init

# Servisler otomatik olarak RabbitMQ ve PostgreSQL'in hazır olmasını bekleyecektir
# Eğer bir servis hata verirse otomatik olarak tekrar başlayacaktır (restart: on-failure)
```

"Database initialization completed successfully!" mesajını gördüğünüzde sistem hazır!

**Not:** Reservation ve Turnstile servisleri RabbitMQ bağlantısı için 15 saniye bekler ve gerekirse otomatik olarak yeniden başlar.

4. **Uygulamaya erişin**
- Frontend: http://localhost:4200
- API Gateway: http://localhost:5010
- RabbitMQ Management: http://localhost:15672 (user: library, pass: library123)

### 🔐 Test Kullanıcıları

| Kullanıcı | Şifre | Rol | Açıklama |
|-----------|-------|-----|----------|
| admin | Admin123! | Admin | Yönetici paneline erişim |
| 123456 | Student123! | Öğrenci | Normal öğrenci |
| 12345 | Student123! | Öğrenci | Normal öğrenci |
| 777 | Student123! | Öğrenci | Doktora öğrencisi |
| 1111 | Student123! | Öğrenci | Cezalı öğrenci (test için) |

### 📱 Özellikler

- ✅ Masa rezervasyon sistemi
- ✅ Turnike giriş kontrolü
- ✅ Ceza ve yasak sistemi
- ✅ Admin paneli
- ✅ Geri bildirim sistemi
- ✅ İletişim, Hakkımızda ve SSS sayfaları

### 🔧 Geliştirme Modu

Frontend'de değişiklik yapmak için development modunda çalıştırın:

```bash
cd Frontend
npm install
npm start
```

Frontend http://localhost:4200 adresinde hot-reload ile çalışacaktır.

### 🐛 Sorun Giderme

**Rezervasyonlar görünmüyor:**
```bash
# 1. Önce db-init servisinin başarıyla çalıştığını kontrol edin
docker-compose logs db-init
# "Database initialization completed successfully!" mesajını görmeli

# 2. Reservation service'in çalıştığını kontrol edin
docker-compose logs reservation-service --tail=20
# "Application started" ve "StudentEntryEventConsumer started" görmelisiniz

# 3. Eğer RabbitMQ connection hatası görürseniz, servisi yeniden başlatın
docker-compose restart reservation-service

# 4. Hala sorun varsa tüm servisleri yeniden başlatın
docker-compose restart
```

**Servisleri tamamen sıfırlamak:**
```bash
docker-compose down -v  # Dikkat: Tüm veriler silinir!
docker-compose up -d
```

**Sadece frontend'i yeniden build etmek:**
```bash
docker-compose build frontend
docker-compose up -d frontend
```

### 📊 Servis Portları

| Servis | Port | Açıklama |
|--------|------|----------|
| Frontend | 4200 | Angular SSR uygulaması |
| API Gateway | 5010 | Ocelot API Gateway |
| Identity Service | 5001 | Kimlik doğrulama |
| Reservation Service | 5002 | Rezervasyon işlemleri |
| Turnstile Service | 5003 | Turnike kontrolü |
| Feedback Service | 5004 | Geri bildirim |
| PostgreSQL | 5432 | Veritabanı |
| RabbitMQ | 5672 | Message broker |
| RabbitMQ Management | 15672 | RabbitMQ web arayüzü |

### 🏗️ Mimari

- **Frontend**: Angular 18 with SSR
- **Backend**: .NET 8 Microservices
- **Database**: PostgreSQL 15
- **Message Broker**: RabbitMQ 3.13
- **API Gateway**: Ocelot

### 📝 Önemli Notlar

1. İlk çalıştırmada database initialization'ın tamamlanmasını bekleyin
2. Docker volume'lar kullanıldığı için veriler kalıcıdır
3. Verileri sıfırlamak için `docker-compose down -v` kullanın
4. Production ortamında şifreleri ve connection string'leri değiştirin

---

## 🌍 ngrok ile Demo/Test

Arkadaşlarınıza sistemi test ettirmek için ngrok kullanabilirsiniz.

### ⚡ Hızlı Başlangıç

```powershell
.\setup-ngrok.ps1
```

**Detaylı bilgi için:** [NGROK\_SETUP.md](NGROK_SETUP.md) dosyasına bakın.

### Manuel Kurulum

1. **ngrok'u yükleyin:**
   ```powershell
   choco install ngrok -y
   ```

2. **İki terminal açın:**
   
   **Terminal 1 (Backend):**
   ```powershell
   ngrok http 5010 --region eu
   ```
   
   **Terminal 2 (Frontend):**
   ```powershell
   ngrok http 4200 --region eu
   ```

3. **URL'leri not edin ve Frontend'i güncelleyin:**
   
   `Frontend/src/app/config/api.config.ts`:
   ```typescript
   NGROK_BACKEND_URL: 'https://YOUR-BACKEND-URL.ngrok-free.app'
   ```

4. **Arkadaşlarına frontend ngrok URL'ini paylaş!**

**Not:** CORS ayarları ngrok için hazır durumdadır.

---

### 🤝 Katkıda Bulunma

1. Fork yapın
2. Feature branch oluşturun (`git checkout -b feature/amazing-feature`)
3. Commit yapın (`git commit -m 'feat: Add amazing feature'`)
4. Push yapın (`git push origin feature/amazing-feature`)
5. Pull Request açın

### 📄 Lisans

Bu proje TÜBİTAK 2209-A projesi kapsamında geliştirilmiştir.
