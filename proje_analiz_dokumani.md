# SAÜ Kütüphane Akıllı Rezervasyon Sistemi - Proje Analiz ve Teknik Detay Dokümanı

Bu doküman, TÜBİTAK 2209-A kapsamında geliştirmiş olduğum Kütüphane Rezervasyon Sistemi'nin tüm teknik kararlarını, mimari detaylarını ve iş kurallarını derinlemesine analiz etmek amacıyla hazırlanmıştır. Geliştirme sürecinde bizzat uyguladığım teknikleri ve çözdüğüm problemleri kapsamlı bir şekilde listeler.

---

## 1. PROJE GENEL TANIMI
**Projenin Amacı:** Sakarya Üniversitesi (SAÜ) kütüphanesindeki çalışma masalarının adil, verimli ve dijital bir sistem üzerinden rezerve edilebilmesini sağlamak; akademik kademelendirmeye dayalı bir önceliklendirme sistemi kurmaktır.

**Çözülen Problemler:**
- Öğrencilerin sabahtan masalara eşya koyarak saatlerce kullanmamaları (haksız işgal).
- Sınav haftalarındaki yoğunlukta gerçekten ihtiyacı olan lisans öğrencileri veya tez yazan doktora öğrencilerinin yer bulamaması.
- Fiziksel takip ve kontrolün insan gücüyle yapılmasının imkansızlığı.

**Hedef Kullanıcı Kitlesi:**
- SAÜ Lisans, Yüksek Lisans ve Doktora öğrencileri.
- Sistemi ve istatistikleri yönetecek olan kütüphane görevlileri (Admin).

---

## 2. KULLANILAN TEKNOLOJİLER
Bu projede dağıtık ve modern bir altyapı kurmak gayesiyle aşağıdaki teknolojileri tercih ettim:

- **Backend:** `ASP.NET Core 6.0` (C# 10). Mikroservis mimarisine tam uyumu, yüksek performansı ve DI (Dependency Injection) yapısının güçlü olması sebebiyle temel framework olarak seçtim.
- **Frontend:** `Angular 17` ve `TypeScript 5`. Component bazlı modüler yapı ve RxJS ile güçlü state/event yönetimi sağladığı için tercih ettim.
- **Veritabanı:** `PostgreSQL 15` (Entity Framework Core 6.x Provider ile). Açık kaynak olması ve ilişkisel veri bütünlüğünü mikroservis ortamında stabil şekilde sağlaması nedeniyle kullandım.
- **Mesajlaşma Kuyruğu (Message Broker):** `RabbitMQ 3.13`. Servisler arası asenkron haberleşmeyi (örneğin turnikeden geçiş anında hedef servisi kilitlenmeden uyarmak) sağlamak için entegre ettim.
- **API Gateway:** `Ocelot`. İstemciden (Angular) gelen isteklerin tek bir noktadan alınarak mikroservislere güvenli bir şekilde yönlendirilmesi için kurdum.
- **Kimlik Doğrulama:** `ASP.NET Identity` ve `JWT (JSON Web Token)`. Stateless, session bağımsız ve güvenli rol yönetimi (Admin/Student) için tercih ettim.
- **Yapay Zeka (AI):** `OpenAI GPT-3.5-turbo API`. Öğrenci geri bildirimlerinin (feedback) metin madenciliğini duygu analiziyle otomatize etmek için projeye dahil ettim.
- **Altyapı:** `Docker` & `Docker Compose`. Projedeki tüm servislerin, veritabanının ve RabbitMQ'nun çevre bağımsız (environment agnostic) tek komutla ayağa kalkabilmesi için konteyner mimarisini kullandım.

---

## 3. SİSTEM MİMARİSİ
Projeyi monolith (tek parça) yerine **Mikroservis Mimarisi** standartlarında tasarladım. Servisler arası izolasyonu sağlarken veri doğruluğunu RabbitMQ event'leri ile asenkron yönetiyorum.

**Katmanlar ve Görevleri:**
1. **API Gateway (Port 5010):** Frontend'den gelen HTTP isteklerini alır, `ocelot.json` konfigürasyonuna göre arka plandaki mikroservis portlarına dağıtır. Dış dünyaya açık tek kapıdır.
2. **Identity Service (Port 5001):** Tüm kullanıcı kayıt, giriş (login) operasyonlarını ve JWT/Refresh Token üretimini üstlenir.
3. **Reservation Service (Port 5002):** Sistemin ana beynidir. Masaların listelenmesi, rezervasyon saat çakışma kontrolleri, akademik puan hesaplamaları, otonom ceza (ban) background servisleri bu modülde çalışır.
4. **Turnstile Service (Port 5003):** Fiziksel kütüphane turnikelerini simüle eden servistir. Öğrenci numarasını okur, içeride aktif rezervasyonu var mı diye HTTP üzerinden Reservation Service'a sorar, onay alırsa geçişi loglar ve RabbitMQ üzerinden `student.entered` event'i fırlatır.
5. **Feedback Service (Port 5004):** Sistemden bağımsız çalışarak öğrencilerin şikayet/önerilerini JSON formatında kalıcı olarak saklar; admin panel istendiğinde OpenAI API'sine gidip bu metinleri toplu analiz ettirir.

**Veri Akışı Örneği:** Angular İstemcisi -> HTTP POST -> API Gateway (Ocelot) -> İlgili Mikroservis (örneğin Auth) -> PostgreSQL DB şeklinde akar. Servisler içi asenkron güncellemeler RabbitMQ Publisher/Consumer mantığıyla gerçekleşir.

---

## 4. VERİTABANI TASARIMI
Sistemde EF Core Code-First yaklaşımıyla tasarladığım PostgreSQL veritabanı bulunmaktadır. Servisler mantıksal olarak izole edilse de operasyonel kolaylık için aynı veritabanı instance'ı üzerinde çalışmaktadır. Şemadaki ana tablolarım ve görevleri:

- **`AspNetUsers` / `AspNetRoles` vs.:** ASP.NET Identity'nin yönettiği tablolar. Öğrencinin `StudentNumber`, `AcademicLevel` (Doktora/Lisans vb.) ve `FullName` spesifik alanlarını buraya entegre ettim.
- **`StudentProfiles`:** `AppUser` ile 1:1 ilişkili ana profil tablomdur. Öğrencinin `FacultyId`, `BanUntil` (Ceza Bitiş Tarihi), `BanReason` gibi kütüphaneye özel durumlarını burada tutuyorum.
- **`Faculties` & `ExamSchedules`:** Fakülteler ve onların ilgili dönemdeki sınav takvimlerini tutan tablolardır (1:N ilişki).
- **`Tables`:** Kütüphanedeki gerçek masaları temsil eder (`TableNumber: M1`, `FloorId: 1` vb.).
- **`Reservations`:** Sistemin transactional yükünü çeken tablo. Hangi `TableId`'ye, hangi `StudentNumber`'ın, `StartTime` ve `EndTime` aralıklarında rezerve ettiğini tutar. Öğrencinin turnikeden geçip geçmediğini doğrulamak için `IsAttended` ve ceza işlenip işlenmediğini tutmak için `PenaltyProcessed` field'larını barındırır. (Table ile 1:N ilişkili).

---

## 5. UYGULAMA ÖZELLİKLERİ
Projeyi geliştirirken kullanıcı ve yönetici bazında iş kısıtlamalarını net kurallara bağladım:

**Öğrenci Fonksiyonları:**
- **Yetkilendirme:** Sistemde hesap oluşturabilir, JWT tabanlı güvenli giriş yapabilirler.
- **Dinamik Rezervasyon:** Öğrenci, masanın müsaitlik durumunu takvim üzerinde görür. Yalnızca 1 saat ile 4 saat aralığında seçim yapabilir. Maksimum 2 aktif rezervasyon hakkı vardır.
- **Gelişmiş Puan ve Erişim Kademesi:** Yarın için yapılacak rezervasyonları sıradan bir kullanıcı gece 00:00'da yapamaz. Geliştirdiğim algoritmaya göre Doktora öğrencisi 300 puanla saat **17:00**'de, Yüksek Lisans 200 puanla **17:05**'te sistemi kullanmaya başlarken; sıradan bir lisans öğrencisi **17:15**'te kalan masaları seçebilir.
- **Sıfır Temaslı Geçiş:** Turnike sayfasından öğrenci numarasını girip sanal giriş yapabilir. Erken tolerans 5 dakika, geç kalma toleransı 15 dakikadır.
- **Feedback Gönderimi:** Sistem/kütüphane hakkındaki deneyimini serbest metin olarak iletebilir.

**Admin Paneli Fonksiyonları:**
- **Sınav Haftası Yönetimi:** Yöneticiler fakülte bazlı sınav takvimi girebilirler. Bu takvimi gören sistem işlemi dinamikleştirir (İş kurallarında detaylıdır).
- **Genel Kontrol:** Tüm rezervasyon hareketlerini anlık listeleyebilir ve aktif cezalı (Ban yemiş) öğrenciler listesini takip edebilirler.
- **AI Tabanlı Müşteri Hizmetleri Memuru:** Yüzlerce feedback tek tek okunamaz diyerek OpenAI entegrasyonunu kodladım. Admin butona bastığında GPT-3.5-turbo API tüm veriyi okur ve 3 cümlelik genel bir duygu/memnuniyet analiz raporu (Örn: "Öğrenciler masa temizliğinden memnun değil") çıkartır.

---

## 6. İŞ AKIŞLARI (FLOW)
Gerçekleştirdiğim en kritik iş akışı: **Rezervasyon, Puanlama ve Otomatik Ceza Akışı**. Adım adım kurgusu şu şekildedir:

1. **Rezervasyon İsteği:** Öğrenci, `POST /api/Reservation/Create` endpoint'i üzerinden yarın saat 10:00 - 12:00 arası için M1 masasını talep eder.
2. **Kural Denetimi:** Servis önce öğrencinin aktif bir cezası (BanUntil >= Bugün) var mı bakar. Sonra çakışan (overlap) bir zaman diliminde masa boş mu diye `Reservations` tablosuna SQL atar.
3. **Puan Hesaplaması:** Sistem, Identity servisinden öğrenci kademesini çeker. Lisans öğrencisi ise (100 puan), `ExamSchedules` tablosuna bakılır. O fakültenin belirtilen tarihte sınavı varsa +50 bonus puan yüklenir ve öğrencinin puanı 150 olur.
4. **Erişim Zamanı Kilidi:** 150 puana karşılık gelen erişim saati algoritmadan kontrol edilir (Örn: 17:10). Mevcut saat henüz 17:08 ise sistem `HTTP 400: Yarın için saat 17:10'da erişim açılacak` diyerek işlemi engeller.
5. **Onay ve Event Yayını:** Her şey uygunsa DB'ye kaydedilir ve RabbitMQ `reservation.created` kuyruğuna event fırlatılır.
6. **Fiziksel Giriş:** Öğrenci rezervasyon saati geldiğinde (tolerans +15dk dahilinde) Turnike servisine İstek atar. Turnike, Reservation'a HTTP call yapar, onaylanırsa RabbitMQ üzerinden `student.entered` publish eder. Reservation servisi bunu yakalayıp DB'de `IsAttended = true` yapar.
7. **Otomatik Ceza Mekanizması (Background Worker):** Geliştirdiğim `PenaltyCheckService` adındaki arka plan HostedService her 1 dakikada uyanır. `StartTime + 15 dakika` geçmiş ve `IsAttended = false` kalmış kayıtları bulur. Kütüphaneye gelmeyip başkasının hakkını gasp eden bu öğrencinin profilindeki `BanUntil` değerini "Bugün + 2 gün" yapar ve `PenaltyProcessed = true` olarak flagler.

---

## 7. GELİŞTİRME SÜRECİ
Projeyi geliştirirken tek başıma bir takımın rolünü üstlenerek A'dan Z'ye uçtan uca çalışır bir ürün çıkarma hedefi güttüm.

- **Analiz ve Planlama:** Kütüphanedeki en büyük kaos noktasının herkesin aynı anda yer kapması olduğunu fark ettim. Sabit bir önceliklendirme yerine (Doktora > YL > Lisans) sınav haftası konseptiyle dinamik (+50 puan bonusu şansı) bir mimari çizdim. Dockerization kararı da dağıtım sorunlarını önlemek için başta alındı.
- **Uygulama Geliştirme (Backend):** 
  - Monolitik bir API yazmak yerine Ocelot Gateway ile mikroservis şablonlarını oluşturdum.
  - C# 10 ve EF Core kullanarak repository / service paternlerini uyguladım. 
  - Event-Driven yaklaşım için RabbitMQ entegrasyonunu yaptım. Özellikle asenkron worker thread'leri (HostedService) yazarak ceza denetimini otomatikleştirdim; bu sayede dışarıdan bir cronjob çalıştırmaya gerek kalmadı.
- **Yapay Zeka Entegrasyonu:** OpenAI dökümanlarını inceleyerek, öğrencilerin text verilerini Prompt Engineering ile işleyip mantıklı JSON çıktıları haline (Duygu, KeyIssues, Suggestions) parse edebilen bir AI Service modülü geliştirdim. Fallback mekanizması yazarak, API limitine takıldığımda standart kelime frekans analizi yapan C# mantığını yedek olarak koydum.
- **Karşılaştığım Problemler ve Çözümler:**
  - *Concurrency (Eşzamanlılık) Problemi:* İki öğrencinin milisaniyeler farkla aynı masayı rezerve etme ihtimalini engellemek için kod aşamasında tarih aralıklarına tam net Intersection Query'leri yazdım. EF Core üzerinde bunu stabilize ettim.
  - *RabbitMQ Cold Start:* Docker compose up yapıldığında .NET Servislerinin RabbitMQ hazır olmadan erişmeye çalışıp çökmesi sorunu oldu. `Task.Delay` ile basit bi retry logic and startup lock mekanizması kurdum.

---

## 8. TESTLER
Yazılımın güvenilirliğini ispatlamak için çeşitli aşamalarda testler yürüttüm:

- **Birim ve Mantık Kurgularının Manuel Testleri (Postman/Swagger):** Öğrenci rezervasyon süresi kurallarını zorladım. (Örn: Geçmiş saate istek atmak, 4 saati aşan rezervasyon denemek, tam 15. dakikada turnikeden geçiş logu yollamak). Sistem tüm exceptional senaryolarda doğru Bad Request yanıtları verdi.
- **Otomatize/Entegrasyon Simülasyonları:** Docker üzerinde farklı portlardan CLI üzerinden istek atarak, API gateway yönlendirmelerinin doğruluğunu ve Header pass edilmesini (Authentication Jwt bearer token'larının alt servislere kesilmeden akmasını) doğrulattım.
- **Kritik Senaryo Doğrulaması:** +50 Sınav Bonusu testi. Bir fakülte için sınav periyodu eklendi; aynı fakülteden bir lisans öğrencisi login edildi, profilde bakıldı ve sistemin saate kilit koymadan "17:10" bandına dinamik switch yaptığı görüldü.
- **RabbitMQ Publish/Consume Testleri:** Turnikeden atılan bir isteğin mesaj broker üzerinden geçip Reservation DB'sini ortalama 20-30 ms içinde güncellemesi başarılı şekilde ölçümlendi.

---

## 9. PERFORMANS VE SINIRLAR
Geliştirdiğim yapının bilincinde olarak çizdiğim sınırlar:

- **Sistem Sınırları:** Tüm mikroservisler aynı PostrgeSQL instance'ını kullanıyor (database-per-resource pattern yerine shared-db). Dağıtık veri yönetimi (Saga) maliyeti sebebiyle bu yolu seçtim. Bu durum 10K+ anlık aktif transaction'da DB pool'unda daralma yapabilir.
- **Yavaşlayan Noktalar:** OpenAI entegrasyonuna bağladığım Feedback/Analysis endpoint'i dış API çağrısı olduğundan 3-5 saniye sürebilmektedir. UI bu anlarda bekleme (spinner) moduna geçmektedir.
- **Bilinen Eksiklikler:** JWT Refresh Token yapısı backend'de kurulu ancak Angular tarafındaki `HttpInterceptor` üzerinde token düşme yaşandığı an otomatik retry akışı (Silent Refresh) mükemmelleştirilmediği için bazen kullanıcıların login sayfasına düşmesi olasılığı bulunuyor. Optimistic Concurrency yönetimi (RowVersion tracking) şimdilik tam olarak aktif değil.

---

## 10. EK NOTLAR VE TEKNİK KARARLAR
- **Puan / Saat Korelasyonu:** Öğrenci önceliklerini puanlarla yönetirken, sıralamayı veritabanı kilitleri veya karmaşık eşleşme kuyruklarıyla yapmak yerine, işlemi *Erişim Saati* metriğine bağladım. Bu, sunucu tarafındaki CPU yükünü muazzam ölçüde düşüren; kuralları "Saat gelmediyse istek atılamaz" gibi çok statik, maliyetsiz bir IF kontrolüne indirgeyen en zekice mimari kararımdı.
- **Neden RabbitMQ?** Turnike sistemi fiziki bir alet olduğu için, HTTP iletişiminin timeout'a düşmesi ihtimalini elemek zorundaydım. Turnike sadece RabbitMQ'ya "okuttu ve geçti" diye publish eder; rezervasyon hizmeti çökmüş bile olsa (veya yoğun yük altında olsa), sistem açıldığında o event'leri okur ve veriyi senkronize eder. Böylece veri kaybını önledim.

---

## 11. KOD SEVİYESİ OPTİMİZASYONLAR VE GÜVENLİK ÖNLEMLERİ
Projenin analizinden sonra production ortamına (canlıya) geçiş hazırlığı kapsamında sistemde aşağıdaki kritik düzeltmeleri bizzat uyguladım:
- **RabbitMQ Exception Handling:** `RabbitMQConsumer` yapısında `autoAck: false` kullanılarak try-catch içerisinde hata anında `BasicNack(requeue: true)` mekanizması doğrulandı. Böylece servis hata fırlatsa bile turnike giriş mesajları kaybolmayacak ve yeniden işlenecek.
- **Güvenli Background Service:** Ceza işleyen `PenaltyCheckService` içinde veritabanı context'inin birikme (memory leak) yapmaması için `using var scope = _serviceProvider.CreateScope();` ile her dakika başında temiz bir bağlantı scope'u oluşturulduğu doğrulandı.
- **HTTP Timeout Senkronizasyonu:** `TurnstileService`'in `ReservationService`'e attığı kilitlenme yaratabilecek HTTP çağrısının default timeout süresi 10 saniyeden **5 saniyeye** düşürüldü. Turnike önünde bekleme riskleri minimize edildi.
- **Overlap (Çakışma) Sorgusu Performansı:** Eşzamanlı rezervasyon isteklerindeki okuma yükünü hafifletmek için veritabanında `Reservation` tablosuna performans indexlemesi (B-Tree Multi-column) eklendi (`TableId`, `ReservationDate`, `StartTime`, `EndTime`).
- **API Anahtarı Gizliliği (Environment Config):** Hardcoded şekilde yazılan OpenAI (GPT) API anahtarı `appsettings.json` içerisinden temizlendi. Sırlar tamamen izole edilerek `docker-compose.yml` üzerinden ortam değişkeni (`OpenAI__ApiKey=${OPENAI_API_KEY}`) olarak enjekte edilmeye başlandı.
