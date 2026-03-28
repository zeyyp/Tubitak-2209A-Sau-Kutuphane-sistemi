# Puanlama Sistemi Test Senaryosu

## Puanlama Kuralları
- **Doktora**: 300 puan (sabit)
- **Yüksek Lisans**: 200 puan (sabit)
- **Lisans (Sınav Haftası)**: 150 puan (100 + 50 bonus)
- **Lisans (Normal)**: 100 puan

## Test Adımları

### 1. Farklı Öğrenci Tipleri Oluştur

#### Öğrenci 1: Lisans (Sınav haftası olmayan fakülte)
```bash
POST http://localhost:5002/api/Reservation/UpdateStudentDepartment
{
  "studentNumber": "1001",
  "facultyId": 1,
  "department": "Bilgisayar Mühendisliği",
  "studentType": "Lisans"
}
```
Beklenen Puan: **100**

#### Öğrenci 2: Lisans (Sınav haftası olan fakülte)
Önce admin panelden Mühendislik Fakültesi (FacultyId=1) için sınav haftası belirle:
- Başlangıç: 2026-02-01
- Bitiş: 2026-02-07

Sonra rezervasyon yap:
```bash
POST http://localhost:5002/api/Reservation/Create
{
  "studentNumber": "1002",
  "facultyId": 1,
  "tableId": 1,
  "reservationDate": "2026-02-02",
  "startTime": "10:00",
  "endTime": "12:00",
  "studentType": "Lisans"
}
```
Beklenen Puan: **150** (100 + 50 sınav bonusu)

#### Öğrenci 3: Yüksek Lisans
```bash
POST http://localhost:5002/api/Reservation/UpdateStudentDepartment
{
  "studentNumber": "2001",
  "facultyId": 1,
  "department": "Bilgisayar Mühendisliği",
  "studentType": "YüksekLisans"
}
```
Rezervasyon yap (sınav haftasında bile):
Beklenen Puan: **200** (sınav bonusu YOK)

#### Öğrenci 4: Doktora
```bash
POST http://localhost:5002/api/Reservation/UpdateStudentDepartment
{
  "studentNumber": "3001",
  "facultyId": 1,
  "department": "Bilgisayar Mühendisliği",
  "studentType": "Doktora"
}
```
Rezervasyon yap:
Beklenen Puan: **300** (sınav bonusu YOK)

### 2. Veritabanında Puanları Kontrol Et

PowerShell ile:
```powershell
# PostgreSQL'e bağlan
docker exec -it library_postgres psql -U postgres -d LibraryReservation

# Rezervasyonları puanlarıyla listele
SELECT 
    "StudentNumber", 
    "StudentType", 
    "ReservationDate", 
    "Score",
    "CreatedAt"
FROM "Reservations"
ORDER BY "Score" DESC, "CreatedAt" ASC;
```

### 3. Frontend'de Profil Sayfasında Kontrol Et

1. Her öğrenci numarasıyla giriş yap
2. Profil sayfasına git
3. Rezervasyon listesinde **Puan** kolonunu kontrol et
4. Renk kodları:
   - 🔴 Kırmızı badge: 300 puan (Doktora)
   - 🟡 Sarı badge: 200 puan (Yüksek Lisans)
   - 🔵 Mavi badge: 150 puan (Lisans - Sınav)
   - ⚪ Gri badge: 100 puan (Lisans)

### 4. Öncelik Testi: Aynı Masa için Yarış

**Senaryo**: 4 farklı öğrenci aynı masa için aynı saatte rezervasyon yapmaya çalışsın.

```bash
# İlk gelen kazanır sistemi - sırayla dene:

# 1. Lisans öğrencisi (100 puan)
curl -X POST http://localhost:5002/api/Reservation/Create \
  -H "Content-Type: application/json" \
  -d '{"studentNumber":"1001","tableId":1,"reservationDate":"2026-02-10","startTime":"14:00","endTime":"16:00","studentType":"Lisans"}'

# 2. Doktora öğrencisi (300 puan) - İKİNCİ GELEN
curl -X POST http://localhost:5002/api/Reservation/Create \
  -H "Content-Type: application/json" \
  -d '{"studentNumber":"3001","tableId":1,"reservationDate":"2026-02-10","startTime":"14:00","endTime":"16:00","studentType":"Doktora"}'
# Sonuç: "Bu saat aralığında masa dolu." mesajı alacak
```

**Beklenen Sonuç**: 
- İlk rezervasyonu yapan (Lisans-100 puan) masayı alır
- İkinci gelen (Doktora-300 puan) bile olsa reddedilir
- Bu "ilk basan alır" prensibidir

### 5. Gerçek Öncelik Sistemi İçin Öneriler

Eğer yüksek puanlının öncelikli olmasını istiyorsan:
- **Bekleme Listesi Sistemi** ekle
- Rezervasyon isteği geldiğinde:
  1. Masayı geçici olarak tut
  2. 10 dakika bekle
  3. Bu sürede gelen tüm istekleri topla
  4. En yüksek puanlıya ver
  5. Diğerlerine bildirim gönder

Veya:

- **Dinamik Rezervasyon Süresi**:
  - Doktora: 7 gün önceden rezervasyon yapabilir
  - Yüksek Lisans: 5 gün önceden
  - Lisans (Sınav): 4 gün önceden
  - Lisans: 3 gün önceden
  
Bu şekilde yüksek puanlılar zaten önce rezervasyon yapar.

## Sonuç

✅ Puanlama sistemi çalışıyor
✅ Sınav haftası bonusu sadece Lisans'ta aktif
✅ Profil sayfasında puanlar görünüyor
✅ İlk rezervasyon alan masayı alıyor (eşitlikte ilk basan kazanır)
