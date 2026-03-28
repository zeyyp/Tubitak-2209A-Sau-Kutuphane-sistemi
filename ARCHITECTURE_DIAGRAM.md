# Kütüphane Rezervasyon Sistemi - Mimari Diyagram

## Sistem Mimarisi

```mermaid
graph TB
    subgraph "Frontend Layer"
        UI[Angular UI<br/>Port: 4200<br/>• Kullanıcı Arayüzü<br/>• Masa Seçimi<br/>• Ceza Görüntüleme]
    end

    subgraph "API Gateway"
        Gateway[Ocelot API Gateway<br/>Port: 5010<br/>• İstek Yönlendirme<br/>• Kimlik Doğrulama Kontrolü]
    end

    subgraph "Microservices"
        Identity[Identity Service<br/>Port: 5001<br/>• JWT Token Üretimi<br/>• Kullanıcı Kayıt/Giriş]
        
        Reservation[Reservation Service<br/>Port: 5002<br/>• Rezervasyon Kuralları<br/>• Ceza Algoritması<br/>• Masa Yönetimi]
        
        Turnstile[Turnstile Service<br/>Port: 5003<br/>• Kart Okuyucu Simülasyonu<br/>• Giriş Kontrolü<br/>• In-Memory Log]
        
        Feedback[Feedback Service<br/>Port: 5004<br/>• Geri Bildirim Yönetimi<br/>• File System Storage]
    end

    subgraph "Background Services"
        Penalty[PenaltyCheckService<br/>• Her 1 dakikada çalışır<br/>• 15 dk kontrol<br/>• 3 ceza = 7 gün ban]
        
        Consumer[StudentEntryEventConsumer<br/>• RabbitMQ dinler<br/>• Giriş eventlerini işler]
    end

    subgraph "Data Layer"
        DB[(PostgreSQL Database<br/>Port: 5432<br/>• Identity Tables<br/>• Reservation Tables<br/>• Student Profiles<br/>• Tables & Floors)]
    end

    subgraph "Message Broker"
        RabbitMQ[RabbitMQ<br/>Port: 5672/15672<br/>Event Bus]
    end

    subgraph "RabbitMQ Events"
        Event1[student.entered<br/>✅ Consumed by Reservation]
        Event2[reservation.created<br/>⚠️ No Consumer]
        Event3[reservation.cancelled<br/>⚠️ No Consumer]
        Event4[student.profile.updated<br/>⚠️ No Consumer]
    end

    %% Frontend to Gateway
    UI -->|HTTP Requests| Gateway

    %% Gateway to Services
    Gateway -->|/api/Auth/*| Identity
    Gateway -->|/api/Reservation/*| Reservation
    Gateway -->|/api/Turnstile/*| Turnstile
    Gateway -->|/api/Feedback/*| Feedback

    %% Database Connections
    Identity -.->|Read/Write| DB
    Reservation -.->|Read/Write| DB

    %% RabbitMQ Publishers
    Turnstile -->|Publish| Event1
    Reservation -->|Publish| Event2
    Reservation -->|Publish| Event3
    Reservation -->|Publish| Event4

    %% RabbitMQ Consumers
    Event1 -->|Consume| Consumer

    %% Events to RabbitMQ
    Event1 -.-> RabbitMQ
    Event2 -.-> RabbitMQ
    Event3 -.-> RabbitMQ
    Event4 -.-> RabbitMQ

    %% Background Services Connection
    Consumer -.->|Inside| Reservation
    Penalty -.->|Inside| Reservation

    %% HTTP Communication
    Turnstile -.->|HTTP Check Access| Reservation

    %% Styling
    classDef frontend fill:#4A90E2,stroke:#2E5C8A,color:#fff
    classDef gateway fill:#F5A623,stroke:#C47F1A,color:#fff
    classDef service fill:#7ED321,stroke:#5FA319,color:#000
    classDef database fill:#D0D0D0,stroke:#8B8B8B,color:#000
    classDef message fill:#BD10E0,stroke:#8B0AA8,color:#fff
    classDef background fill:#F8E71C,stroke:#B8A815,color:#000
    classDef event fill:#FFA07A,stroke:#CC7F61,color:#000

    class UI frontend
    class Gateway gateway
    class Identity,Reservation,Turnstile,Feedback service
    class DB database
    class RabbitMQ message
    class Penalty,Consumer background
    class Event1,Event2,Event3,Event4 event
```

## Detaylı Akış Diyagramı

```mermaid
sequenceDiagram
    participant U as Angular UI
    participant G as API Gateway
    participant I as Identity Service
    participant R as Reservation Service
    participant T as Turnstile Service
    participant MQ as RabbitMQ
    participant DB as PostgreSQL
    participant BG as Background Services

    Note over U,BG: Kullanıcı Giriş Akışı
    U->>G: POST /api/Auth/login
    G->>I: Forward to Identity
    I->>DB: Validate User
    DB-->>I: User Data
    I-->>G: JWT Token
    G-->>U: Token Response

    Note over U,BG: Rezervasyon Oluşturma
    U->>G: POST /api/Reservation/Create
    G->>R: Forward Request (+ JWT)
    R->>DB: Check Student Profile
    R->>DB: Check Table Availability
    R->>DB: Create Reservation
    DB-->>R: Reservation Created
    R->>MQ: Publish ReservationCreatedEvent
    R-->>G: Success Response
    G-->>U: Reservation Confirmed

    Note over U,BG: Turnstile Giriş Akışı
    U->>G: POST /api/Turnstile/enter
    G->>T: Forward Request
    T->>R: HTTP: Check Access Permission
    R->>DB: Validate Reservation + Profile
    DB-->>R: Access Decision
    R-->>T: Allow/Deny
    alt Access Allowed
        T->>MQ: Publish StudentEnteredEvent
        MQ->>BG: Consume Event
        BG->>R: Process Entry (StudentEntryEventConsumer)
        T-->>G: Door Open = true
    else Access Denied
        T-->>G: Door Open = false
    end
    G-->>U: Entry Result

    Note over U,BG: Otomatik Ceza Kontrolü (Background)
    loop Her 1 Dakika
        BG->>DB: Check Overdue Reservations
        alt 15dk geçmiş + gelmeyen
            BG->>DB: Apply Penalty (+1 point)
            BG->>MQ: Publish StudentProfileUpdatedEvent
            alt 3 Ceza Puanı
                BG->>DB: Ban User (7 days)
            end
        end
    end

    Note over U,BG: Feedback Gönderme
    U->>G: POST /api/Feedback/Submit
    G->>Feedback: Forward Request
    Feedback->>Feedback: Save to File System
    Feedback-->>G: Success
    G-->>U: Feedback Saved
```

## Sistem Bileşenleri

### 1. Frontend Layer
- **Angular UI (Port 4200)**
  - Kullanıcı arayüzü
  - Masa seçimi ve rezervasyon oluşturma
  - Ceza puanı görüntüleme
  - API Gateway ile iletişim

### 2. API Gateway
- **Ocelot (Port 5010)**
  - Tüm istekleri merkezi olarak yönlendirir
  - CORS politikalarını yönetir
  - Route yapılandırması:
    - `/api/Auth/*` → Identity Service (5001)
    - `/api/Reservation/*` → Reservation Service (5002)
    - `/api/Turnstile/*` → Turnstile Service (5003)
    - `/api/Feedback/*` → Feedback Service (5004)

### 3. Microservices

#### Identity Service (Port 5001)
- ✅ Kullanıcı kayıt ve giriş
- ✅ JWT token üretimi ve doğrulama
- ✅ PostgreSQL veritabanı kullanır
- ❌ RabbitMQ kullanmaz

#### Reservation Service (Port 5002)
- ✅ Rezervasyon oluşturma ve iptal
- ✅ Masa ve kat yönetimi
- ✅ Öğrenci profili ve ceza yönetimi
- ✅ Rezervasyon kuralları (StudentType bazlı)
- ✅ PostgreSQL veritabanı kullanır
- ✅ RabbitMQ'ya event publish eder
- ✅ RabbitMQ'dan event consume eder
- **Background Services:**
  - `PenaltyCheckService`: Her 1 dakikada otomatik ceza kontrolü
  - `StudentEntryEventConsumer`: Giriş eventlerini dinler

#### Turnstile Service (Port 5003)
- ✅ Kart okuyucu simülasyonu
- ✅ Giriş izni kontrolü (Reservation Service'e HTTP call)
- ✅ RabbitMQ'ya StudentEnteredEvent publish eder
- ❌ Veritabanı kullanmaz (In-memory log)

#### Feedback Service (Port 5004)
- ✅ Geri bildirim yönetimi
- ✅ File system storage (App_Data)
- ❌ Veritabanı kullanmaz
- ❌ RabbitMQ kullanmaz

### 4. Data Layer
- **PostgreSQL (Port 5432)**
  - Tek merkezi veritabanı
  - Identity Service tabloları (AspNetUsers, AspNetRoles)
  - Reservation Service tabloları (Reservations, Tables, Floors, StudentProfiles)

### 5. Message Broker
- **RabbitMQ (Port 5672/15672)**
  - Event-driven architecture için message broker
  - Management UI: http://localhost:15672

## RabbitMQ Event'leri

| Event | Publisher | Consumer | Routing Key | Durum |
|-------|-----------|----------|-------------|-------|
| **StudentEnteredEvent** | Turnstile Service | Reservation Service | `student.entered` | ✅ Çalışıyor |
| **ReservationCreatedEvent** | Reservation Service | - | `reservation.created` | ⚠️ Consumer yok |
| **ReservationCancelledEvent** | Reservation Service | - | `reservation.cancelled` | ⚠️ Consumer yok |
| **StudentProfileUpdatedEvent** | Reservation Service | - | `student.profile.updated` | ⚠️ Consumer yok |

### Event Detayları

#### 1. StudentEnteredEvent ✅
**Publisher:** Turnstile Service  
**Consumer:** Reservation Service (StudentEntryEventConsumer)  
**Ne zaman:** Öğrenci kartını okutup giriş yaptığında  
**İçerik:**
```json
{
  "studentNumber": "202012345",
  "entryTime": "2025-12-21T10:30:00Z",
  "turnstileId": "turnstile-1"
}
```

#### 2. ReservationCreatedEvent ⚠️
**Publisher:** Reservation Service  
**Consumer:** Yok (mimari eksiklik)  
**Ne zaman:** Yeni rezervasyon oluşturulduğunda  
**Potansiyel Kullanım:** Notification service, analytics, audit log

#### 3. ReservationCancelledEvent ⚠️
**Publisher:** Reservation Service  
**Consumer:** Yok (mimari eksiklik)  
**Ne zaman:** Rezervasyon iptal edildiğinde  
**Potansiyel Kullanım:** Notification service, capacity management

#### 4. StudentProfileUpdatedEvent ⚠️
**Publisher:** Reservation Service (PenaltyCheckService)  
**Consumer:** Yok (mimari eksiklik)  
**Ne zaman:** Ceza verildiğinde veya ban uygulandığında  
**Potansiyel Kullanım:** Notification service, email alerts

## Ceza Sistemi Algoritması

```mermaid
flowchart TD
    Start[PenaltyCheckService Başla] --> Wait[1 Dakika Bekle]
    Wait --> Check[Veritabanını Kontrol Et]
    Check --> Query{Süresi geçmiş<br/>rezervasyon var mı?}
    
    Query -->|Hayır| Wait
    Query -->|Evet| Loop[Her rezervasyon için]
    
    Loop --> Grace{15 dakika<br/>geçmiş mi?}
    Grace -->|Hayır| Loop
    Grace -->|Evet| NotAttended{IsAttended<br/>= false?}
    
    NotAttended -->|Hayır| Loop
    NotAttended -->|Evet| AddPenalty[Ceza Puanı +1]
    
    AddPenalty --> Processed[PenaltyProcessed = true]
    Processed --> CheckPoints{Ceza Puanı<br/>= 3?}
    
    CheckPoints -->|Hayır| SavePoints[Profili Güncelle]
    CheckPoints -->|Evet| ApplyBan[7 Gün Ban Uygula<br/>Ceza Puanı = 0]
    
    SavePoints --> PublishEvent[StudentProfileUpdatedEvent<br/>Publish]
    ApplyBan --> PublishEvent
    
    PublishEvent --> Loop
    Loop --> Wait
    
    style Start fill:#4A90E2
    style AddPenalty fill:#FF6B6B
    style ApplyBan fill:#C92A2A
    style PublishEvent fill:#BD10E0
```

## Rezervasyon Kuralları (Student Type)

| Student Type | Max Active Reservations | Max Advance Days | Daily Limit |
|--------------|------------------------|------------------|-------------|
| **Undergraduate** | 2 | 3 gün | 1 |
| **Graduate** | 3 | 7 gün | 2 |
| **PhD** | 5 | 14 gün | 3 |

## Port Listesi

| Servis | Port | Protokol |
|--------|------|----------|
| Angular UI | 4200 | HTTP |
| API Gateway | 5010 | HTTP |
| Identity Service | 5001 | HTTP |
| Reservation Service | 5002 | HTTP |
| Turnstile Service | 5003 | HTTP |
| Feedback Service | 5004 | HTTP |
| PostgreSQL | 5432 | TCP |
| RabbitMQ (AMQP) | 5672 | AMQP |
| RabbitMQ (Management) | 15672 | HTTP |

## Teknoloji Stack

### Backend
- **.NET 8.0** - Tüm mikroservisler
- **ASP.NET Core** - Web API framework
- **Entity Framework Core** - ORM
- **Ocelot** - API Gateway
- **RabbitMQ.Client** - Message broker client
- **PostgreSQL** - İlişkisel veritabanı

### Frontend
- **Angular 18** - SPA framework
- **TypeScript** - Programlama dili
- **RxJS** - Reactive programming

### Infrastructure
- **Docker & Docker Compose** - Container orchestration
- **PostgreSQL 15** - Database
- **RabbitMQ 3.13** - Message broker

## Mimari Kararlar ve Öneriler

### ✅ İyi Yönler
1. Mikroservis mimarisi doğru uygulanmış
2. API Gateway merkezi yönetim sağlıyor
3. Event-driven architecture RabbitMQ ile kurulmuş
4. Background services ile asenkron işlemler
5. JWT tabanlı güvenlik
6. Docker ile kolay deployment

### ⚠️ İyileştirme Önerileri
1. **Consumer Eksikliği**: 3 event publish ediliyor ama consume edilmiyor
   - Notification service eklenebilir
   - Analytics service eklenebilir
   
2. **Turnstile Service Database**: 
   - In-memory yerine Redis kullanılabilir (persistent log için)
   
3. **Feedback Service**: 
   - File system yerine veritabanı kullanılabilir
   
4. **Health Checks**: 
   - Tüm servislere health check endpoint'leri eklenebilir
   
5. **Monitoring**: 
   - Prometheus + Grafana eklenebilir
   - Serilog ile merkezi log toplama
   
6. **API Gateway**: 
   - Rate limiting eklenebilir
   - Request/Response caching

## Güvenlik

- ✅ JWT token tabanlı authentication
- ✅ API Gateway üzerinden merkezi CORS yönetimi
- ⚠️ HTTPS kullanımı (production için gerekli)
- ⚠️ API rate limiting (DDoS koruması)
- ⚠️ Input validation ve sanitization

## Ölçeklenebilirlik

Mevcut mimari horizontal scaling için uygun:
- Mikroservisler bağımsız scale edilebilir
- RabbitMQ ile asenkron iletişim
- Stateless servisler (Turnstile hariç)

**Öneriler:**
- Load balancer eklenebilir
- Redis cache layer eklenebilir
- Database read replicas kullanılabilir

---

**Son Güncelleme:** 21 Aralık 2025  
**Versiyon:** 1.0  
**Proje:** TÜBİTAK 2209-A SAÜ Kütüphane Rezervasyon Sistemi
