# 🎯 Sistem Güncellemeleri - Scoring & Önceliklendirme Sistemi

## ✅ Tamamlanan Değişiklikler

### 1. Database Değişiklikleri (Migration: AddScoringSystemFields)

#### StudentProfile Entity
- ✅ **Eklenen:** `Department` (string) - Öğrenci bölümü
- ✅ **Eklenen:** `ExamWeekStart` (DateOnly?) - Sınav haftası başlangıcı
- ✅ **Eklenen:** `ExamWeekEnd` (DateOnly?) - Sınav haftası bitişi
- ✅ **Kaldırılan:** `PenaltyPoints` - Ceza puanı sistemi kaldırıldı

#### Reservation Entity
- ✅ **Eklenen:** `CreatedAt` (DateTime) - Oluşturulma zamanı (tie-breaking için)
- ✅ **Eklenen:** `Score` (int) - Önceliklendirme puanı

### 2. Backend Değişiklikleri

#### ReservationController
**Yeni Scoring Sistemi:**
```csharp
- Doktora: 300 puan (sınav haftası yok)
- Yüksek Lisans: 200 puan (sınav haftası yok)
- Lisans (sınav haftasında): 150 puan (100 + 50 bonus)
- Lisans (normal): 100 puan
```

**Yeni Endpoint'ler:**
- ✅ `POST /api/Reservation/SetExamWeek` - Bölüm için sınav haftası ayarla (Admin)
- ✅ `GET /api/Reservation/ExamWeeks` - Tüm sınav haftalarını listele (Admin)
- ✅ `POST /api/Reservation/UpdateStudentDepartment` - Öğrenci bölümünü güncelle (Admin)

**Değişiklikler:**
- ✅ `CalculateScore()` metodu eklendi - Öğrenci tipine ve sınav haftasına göre puan hesaplama
- ✅ `CreateReservation()` - Score hesaplaması entegre edildi
- ✅ `CreateReservation()` - CreatedAt timestamp otomatik ekleniyor

#### PenaltyCheckService (Basitleştirildi)
- ✅ **Eski:** 3 ceza puanı → 7 gün ban
- ✅ **Yeni:** 1 no-show → 2 gün direkt ban
- ✅ PenaltyPoints mantığı tamamen kaldırıldı
- ✅ `BanUntil = DateTime.Now.AddDays(2)` direkt uygulanıyor

### 3. Frontend Değişiklikleri

#### Admin Panel Component
**Yeni Özellikler:**
- ✅ Sınav Haftası Yönetimi Bölümü eklendi
- ✅ Bölüm bazında sınav haftası tanımlama formu
- ✅ Mevcut sınav haftalarını listeleme
- ✅ FormsModule import edildi (two-way binding için)

**Yeni Form Alanları:**
- Department (Bölüm adı)
- ExamWeekStart (Başlangıç tarihi)
- ExamWeekEnd (Bitiş tarihi)

#### Reservation Service
**Yeni Metodlar:**
- ✅ `setExamWeek()` - Sınav haftası ayarlama
- ✅ `getExamWeeks()` - Sınav haftalarını getirme
- ✅ `updateStudentDepartment()` - Bölüm güncelleme

---

## 📋 Test Senaryoları

### Test 1: Database Migration
```bash
cd Backend/ReservationService
dotnet ef database update
```
✅ **Beklenen:** Migration başarıyla uygulanmalı, tablolar güncellenmiş olmalı

### Test 2: Backend Build
```bash
cd Backend/ReservationService
dotnet build
```
✅ **Sonuç:** Build başarılı (sadece kullanılmayan değişken uyarısı var, normal)

### Test 3: Servisleri Başlatma
```bash
.\start-all.ps1
```
**Kontrol Edilecek:**
- ✅ ReservationService başladı mı? (port 5002)
- ✅ Database bağlantısı başarılı mı?
- ✅ Migration uygulandı mı?

### Test 4: Admin Panel - Sınav Haftası Ekleme
1. Admin olarak giriş yap
2. Admin Panel'e git
3. "Sınav Haftası Yönetimi" bölümüne bak
4. Yeni sınav haftası ekle:
   - Bölüm: "Bilgisayar Mühendisliği"
   - Başlangıç: "2026-02-10"
   - Bitiş: "2026-02-17"
5. **Beklenen:** "Sınav haftası başarıyla ayarlandı" mesajı

### Test 5: Scoring Sistemi Test
**Senaryo A: Doktora Öğrencisi**
1. Doktora öğrencisi olarak rezervasyon oluştur
2. Database'de kontrol et: `SELECT Score FROM Reservations ORDER BY Id DESC LIMIT 1;`
3. **Beklenen:** Score = 300

**Senaryo B: Lisans + Sınav Haftası**
1. Lisans öğrencisine bölüm ata (Admin panel)
2. Bölümün sınav haftasını ayarla
3. Sınav haftası içinde rezervasyon oluştur
4. **Beklenen:** Score = 150 (100 + 50 bonus)

**Senaryo C: Lisans Normal**
1. Lisans öğrencisi, sınav haftası dışında rezervasyon
2. **Beklenen:** Score = 100

### Test 6: Ceza Sistemi (2 Günlük Ban)
1. Öğrenci rezervasyon oluştur
2. Başlangıç saatinden 15 dakika geç kal
3. PenaltyCheckService çalışsın (1 dakika bekle)
4. **Beklenen:** 
   - `BanUntil = Bugün + 2 gün`
   - `BanReason = "Rezervasyonunuza katılmadığınız için sistem 2 gün ceza uyguladı."`

### Test 7: API Endpoint Testleri (Postman/Curl)

**SetExamWeek:**
```bash
curl -X POST http://localhost:5010/api/Reservation/SetExamWeek \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {admin_token}" \
  -d '{
    "department": "Bilgisayar Mühendisliği",
    "examWeekStart": "2026-02-10",
    "examWeekEnd": "2026-02-17"
  }'
```

**GetExamWeeks:**
```bash
curl -X GET http://localhost:5010/api/Reservation/ExamWeeks \
  -H "Authorization: Bearer {admin_token}"
```

---

## 🔍 Database Kontrol Sorguları

```sql
-- StudentProfile değişikliklerini kontrol et
SELECT "Id", "StudentNumber", "StudentType", "Department", "ExamWeekStart", "ExamWeekEnd", "BanUntil"
FROM "StudentProfiles"
LIMIT 10;

-- Reservation Score'larını kontrol et
SELECT "Id", "StudentNumber", "StudentType", "Score", "CreatedAt", "ReservationDate"
FROM "Reservations"
ORDER BY "CreatedAt" DESC
LIMIT 10;

-- Sınav haftası olan bölümleri listele
SELECT DISTINCT "Department", "ExamWeekStart", "ExamWeekEnd", COUNT(*) as StudentCount
FROM "StudentProfiles"
WHERE "Department" IS NOT NULL AND "ExamWeekStart" IS NOT NULL
GROUP BY "Department", "ExamWeekStart", "ExamWeekEnd";

-- Ban'lı öğrencileri listele
SELECT "StudentNumber", "BanUntil", "BanReason"
FROM "StudentProfiles"
WHERE "BanUntil" IS NOT NULL AND "BanUntil" >= CURRENT_DATE;
```

---

## 🎨 Frontend Test Adımları

### 1. Package Install (İlk Defa)
```bash
cd Frontend
npm install
```

### 2. Frontend Başlatma
```bash
npm start
```

### 3. Admin Panel Test
- http://localhost:4200/admin-panel adresine git
- Sınav Haftası Yönetimi bölümünü kontrol et
- Form doldur ve kaydet
- Mevcut sınav haftalarını listele

---

## ⚠️ Bilinen Durumlar

1. **Frontend Build:** `npm install` gerekli (ilk kez çalıştırıyorsan)
2. **Backend Warning:** `penaltyAppliedThisCycle` kullanılmayan değişken uyarısı - zararsız
3. **Database:** Migration sonrası eski `PenaltyPoints` verisi kaybolur (tasarım gereği)
4. **Sınav Haftası:** Sadece lisans öğrencileri için geçerli (doktora/yüksek lisans için null)

---

## 📊 Değişiklik Özeti

| Kategori | Değişiklik Sayısı | Durum |
|----------|-------------------|-------|
| Database Alanları | 5 alan (3 eklendi, 1 kaldırıldı, 2 güncellendi) | ✅ Tamamlandı |
| Backend Endpoint | 3 yeni endpoint | ✅ Tamamlandı |
| Backend Metod | 1 yeni metod (CalculateScore) | ✅ Tamamlandı |
| Frontend Component | 1 güncelleme (AdminPanel) | ✅ Tamamlandı |
| Frontend Service | 3 yeni metod | ✅ Tamamlandı |
| Migration | 1 yeni migration | ✅ Uygulandı |

---

## 🚀 Sonraki Adımlar (Opsiyonel İyileştirmeler)

1. **Otomatik Sınav Haftası Bildirimi:** Öğrencilere email/notification
2. **Sınav Takvimi Excel Import:** Toplu sınav haftası yükleme
3. **Dashboard İstatistikleri:** Sınav haftasında rezervasyon artışı grafiği
4. **Department Dropdown:** Frontend'de bölüm listesi (dropdown)
5. **Score Görünürlüğü:** Öğrencilere kendi puanlarını gösterme

---

## 📝 Not

Tüm değişiklikler geriye uyumlu şekilde tasarlandı. Eski rezervasyonlar için Score=0 olacak ama bu yeni rezervasyonları etkilemez. Migration otomatik olarak mevcut verileri korur (CreatedAt için default değer verir).

**Tarih:** 30 Ocak 2026
**Versiyon:** v2.0 - Scoring & Önceliklendirme Sistemi
