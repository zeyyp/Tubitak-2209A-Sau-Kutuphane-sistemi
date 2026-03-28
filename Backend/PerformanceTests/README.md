# Performans Testleri - TÜBİTAK 2209-A

## 📊 Ne Test Ediyoruz?

TÜBİTAK kriteri: **"Sistemin yüksek kullanıcı trafiğine %90 üzerinde cevap verebilmesi"**

Bu testler şunları ölçer:
- Aynı anda kaç kullanıcıya hizmet verilebiliyor
- Yanıt süreleri ne kadar
- Sistemin kapasitesi nedir

## 🛠️ Test Araçları

### Seçenek 1: k6 (Önerilen)

Modern, güçlü load testing aracı.

**Kurulum:**
```powershell
# Windows
winget install k6 --source winget

# veya Chocolatey ile
choco install k6

# Mac
brew install k6

# Linux
sudo apt install k6
```

**Çalıştırma:**
```powershell
# Basit smoke test
k6 run smoke-test.js

# Full load test
k6 run load-test.js

# Özel ayarlarla
k6 run --vus 100 --duration 60s load-test.js
```

### Seçenek 2: PowerShell Script (k6 gerekmez)

k6 yükleyemiyorsanız PowerShell ile test yapabilirsiniz.

**Çalıştırma:**
```powershell
# 50 kullanıcı, 60 saniye (varsayılan)
.\simple-load-test.ps1

# 100 kullanıcı, 30 saniye
.\simple-load-test.ps1 -ConcurrentUsers 100 -Duration 30

# Farklı endpoint test et
.\simple-load-test.ps1 -TargetUrl "http://localhost:5001/api/Auth/login"
```

## 📋 Test Senaryoları

### 1. Smoke Test (smoke-test.js)
- **Amaç:** Sistemin çalıştığını doğrulama
- **Kullanıcı:** 10
- **Süre:** 30 saniye
- **Beklenen:** Tüm istekler başarılı

### 2. Load Test (load-test.js)
- **Amaç:** Normal yük performansı
- **Kullanıcı:** 20 → 50 → 100
- **Süre:** ~4 dakika
- **Beklenen:** %90+ başarı

### 3. Stress Test
```powershell
k6 run --vus 200 --duration 2m load-test.js
```
- **Amaç:** Sistemin limitini bulmak
- **Kullanıcı:** 200
- **Süre:** 2 dakika

## 📈 Sonuç Okuma

### k6 Çıktısı
```
     checks................: 95.00% ✓ 1900 ✗ 100
     http_req_duration.....: avg=245ms min=12ms max=2.1s p(95)=890ms
     http_req_failed.......: 5.00%  ✓ 100  ✗ 1900
     http_reqs.............: 2000   33.33/s
```

| Metrik | Açıklama | TÜBİTAK Kriteri |
|--------|----------|-----------------|
| `http_req_failed` | Başarısız istek oranı | < %10 |
| `http_req_duration p(95)` | %95 istek süresi | < 2000ms |
| `checks` | Test assertion başarısı | > %90 |

### PowerShell Çıktısı
```
┌─────────────────────────────────────────────────────────────┐
│ BAŞARI ORANI                                                │
├─────────────────────────────────────────────────────────────┤
│   ✅ Başarılı         : 950 (95.00%)
│   ❌ Başarısız        : 50 (5.00%)
└─────────────────────────────────────────────────────────────┘
```

## ✅ TÜBİTAK Başarı Kriteri

| Metrik | Gerekli | Örnek Sonuç |
|--------|---------|-------------|
| Başarı Oranı | ≥ %90 | %95 ✅ |
| P95 Yanıt | < 2000ms | 890ms ✅ |
| Concurrent Users | ≥ 100 | 100 ✅ |

## 🔧 Önkoşullar

1. Docker servisleri çalışıyor olmalı:
```powershell
docker-compose up -d
```

2. Servislerin hazır olduğunu kontrol edin:
```powershell
# Identity Service
curl http://localhost:5001/api/Auth/login

# Reservation Service
curl http://localhost:5002/api/Reservation/Faculties
```

## 📊 Örnek Test Çalıştırma

```powershell
cd Backend/PerformanceTests

# 1. Önce smoke test ile kontrol
.\simple-load-test.ps1 -ConcurrentUsers 10 -Duration 10

# 2. Sonra gerçek load test
.\simple-load-test.ps1 -ConcurrentUsers 50 -Duration 60

# 3. Stress test
.\simple-load-test.ps1 -ConcurrentUsers 100 -Duration 60
```

## 📝 Rapor Örneği

Her test sonunda JSON rapor oluşturulur:
```
performance-report-20260206_143052.json
```

Bu raporu TÜBİTAK dosyasına ekleyebilirsiniz.
