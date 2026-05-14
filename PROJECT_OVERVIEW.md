# SAÜ Library Reservation System — Comprehensive Project Documentation

> **Purpose of this document:** Complete developer-grade reference for the Sakarya University Library Seat Reservation & Turnstile Management System (TÜBİTAK 2209-A research project). Intended to be fed to an AI assistant or onboarding engineer as a single, authoritative source of truth.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [System Architecture](#2-system-architecture)
3. [Technology Stack](#3-technology-stack)
4. [Backend Services — Deep Dive](#4-backend-services--deep-dive)
   - 4.1 [Identity Service](#41-identity-service-port-5001)
   - 4.2 [Reservation Service](#42-reservation-service-port-5002)
   - 4.3 [Turnstile Service](#43-turnstile-service-port-5003)
   - 4.4 [Feedback Service](#44-feedback-service-port-5004)
   - 4.5 [API Gateway](#45-api-gateway-port-5010)
   - 4.6 [Shared.Events Library](#46-sharedevents-library)
5. [Frontend Application](#5-frontend-application)
6. [Data Models & Database Schema](#6-data-models--database-schema)
7. [Key Business Logic & Workflows](#7-key-business-logic--workflows)
   - 7.1 [Priority Scoring & Time-Windowed Access](#71-priority-scoring--time-windowed-access)
   - 7.2 [Reservation Creation Flow](#72-reservation-creation-flow)
   - 7.3 [Turnstile Entry Flow](#73-turnstile-entry-flow)
   - 7.4 [No-Show Penalty System](#74-no-show-penalty-system)
   - 7.5 [AI Feedback Analysis](#75-ai-feedback-analysis)
8. [Event-Driven Communication (RabbitMQ)](#8-event-driven-communication-rabbitmq)
9. [Authentication & Security](#9-authentication--security)
10. [API Reference](#10-api-reference)
11. [Infrastructure & Deployment](#11-infrastructure--deployment)
12. [Setup & Installation](#12-setup--installation)
13. [Testing](#13-testing)
14. [Folder Structure](#14-folder-structure)
15. [Existing Markdown Files Audit](#15-existing-markdown-files-audit)
16. [Known Issues & Technical Debt](#16-known-issues--technical-debt)

---

## 1. Project Overview

### What This Project Is

The **SAÜ Library Reservation System** is a web-based platform that allows Sakarya University students to reserve seats at the university's central library study hall. It was developed as part of a **TÜBİTAK 2209-A** (Undergraduate Research Projects) grant.

### Problem It Solves

| Problem | Solution |
|---|---|
| Students cannot find seats during peak hours | Online seat reservation with floor & table selection |
| "Ghost reservations" (reserved but unused seats) | Turnstile entry validation: attendee marked only if physically present |
| Unfair access (seniors lose to undergrads who click first) | Priority scoring with time-windowed reservation opening |
| No visibility into reservation history or penalties | Student profile page with history and ban status |
| No structured feedback mechanism | Feedback portal with AI-powered sentiment analysis |

### Target Users

- **Students** (~30,000 at SAÜ): reserve seats, manage reservations, see ban status
- **Administrators**: manage exam schedules, view all reservations, lift bans, manage tables
- **Turnstile hardware** (service account): call the Turnstile API to validate physical entry

### Scale Targets (from project brief)

- 500+ study desks
- ~2,000 reservations/day
- 100+ concurrent users

---

## 2. System Architecture

The system follows a **microservices architecture** with an Ocelot API Gateway as the single entry point for the Angular frontend.

```
┌─────────────────────────────────────────────┐
│          Angular 20 SPA (SSR enabled)        │
│               Port 4200                      │
└─────────────────┬───────────────────────────┘
                  │ HTTP
                  ▼
┌─────────────────────────────────────────────┐
│         Ocelot API Gateway                   │
│               Port 5010                      │
│   Routing only — no JWT verification here    │
└────┬──────────┬──────────┬──────────┬───────┘
     │          │          │          │
     ▼          ▼          ▼          ▼
 :5001      :5002      :5003      :5004
Identity  Reserva-  Turnstile  Feedback
Service   tion Svc   Service   Service
     │          │          │
     └──────────┼──────────┘
                │ (PostgreSQL)
                ▼
        ┌──────────────┐
        │  PostgreSQL   │
        │  Port 5432    │
        │ (shared DB)   │
        └──────────────┘

ReservationService ◄──────── RabbitMQ ◄──── TurnstileService
(consumer)               Port 5672/15672    (publisher)
```

### Key Architectural Decisions

1. **Single shared PostgreSQL database** — Identity and Reservation services share one PostgreSQL instance (`LibraryReservation` database) but use separate schema tables. This simplifies deployment for a research project at the cost of service isolation.

2. **Ocelot Gateway does not validate JWT** — JWT is issued by IdentityService and forwarded by the gateway as a raw header. Each downstream service is responsible for its own authorization, though in the current codebase authorization on most ReservationController endpoints is effectively bypassed (see [Technical Debt](#16-known-issues--technical-debt)).

3. **FeedbackService uses PostgreSQL** — Feedback records are persisted in the shared `LibraryReservation` PostgreSQL database via EF Core (`FeedbackDbContext`). An earlier file-based implementation (`FileFeedbackRepository`) has been replaced by `EfFeedbackRepository`.

4. **RabbitMQ is used for decoupled entry notification** — When a student passes through the turnstile, `TurnstileService` publishes a `student.entered` event. `ReservationService` consumes it and sets `IsAttended = true`. Other events (`reservation.created`, `reservation.cancelled`, `student.profile.updated`) are published but have no consumers yet (marked with `TODO` comments).

---

## 3. Technology Stack

### Backend

| Component | Technology | Version |
|---|---|---|
| Runtime | .NET / ASP.NET Core | 8.0 |
| ORM | Entity Framework Core | 8.x |
| Database | PostgreSQL | 15 |
| API Gateway | Ocelot | latest |
| Message Broker | RabbitMQ | 3.13 |
| Auth Framework | ASP.NET Core Identity | 8.x |
| Token Standard | JWT (HS256) | — |
| AI Integration | OpenAI GPT-3.5-turbo API | — |

### Frontend

| Component | Technology | Version |
|---|---|---|
| Framework | Angular | 20.1 |
| Rendering | SSR (`@angular/ssr`) | 20.1 |
| HTTP | Angular `HttpClient` | — |
| Auth Storage | `localStorage` | — |

### Infrastructure

| Component | Technology |
|---|---|
| Containerization | Docker + Docker Compose |
| Orchestration | `docker-compose.yml` (single-node) |
| Reverse Proxy | None (gateway handles routing) |
| CI/CD | None configured |

---

## 4. Backend Services — Deep Dive

### 4.1 Identity Service (Port 5001)

**Purpose:** Authentication, user registration, JWT token issuance, and refresh token management.

**Framework:** ASP.NET Core + ASP.NET Core Identity + EF Core (PostgreSQL)

**Key Responsibilities:**
- User registration (student number, full name, academic level, email, password)
- Login with student number + password
- JWT Access Token generation (HS256, configurable TTL, default 60 min)
- Refresh Token generation (7-day TTL, stored in DB)
- Token revocation (logout endpoint)
- Account lockout after 5 failed login attempts (15-minute lockout)
- Role seeding on startup: `admin`, `student`, `service`
- Default user seeding: `admin / Admin123!`, `123456 / Password123!`, `turnstile-bot / Turnstile123!`

**JWT Claims issued:**
- `sub` — user ID
- `studentNumber` — student number (used as identity across services)
- `role` — user role (`admin`, `student`, `service`)
- `academicLevel` — e.g., `Lisans`, `YüksekLisans`, `Doktora`

**Configuration (`appsettings.json`):**
```json
{
  "Jwt": {
    "Issuer": "Library.Identity",
    "Audience": "Library.Api",
    "Key": "ChangeThisDevelopmentKey123!",
    "AccessTokenMinutes": 60,
    "RefreshTokenDays": 7
  }
}
```

> **Security note:** The JWT signing key is committed in plain text in `appsettings.json`. In production this must be an environment secret.

**API Endpoints (under `/api/Auth`):**

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/login` | Anonymous | Login, returns access + refresh tokens |
| POST | `/register` | Anonymous | Register new student |
| POST | `/refresh` | Anonymous | Exchange refresh token for new access token |
| POST | `/revoke` | Anonymous | Revoke a refresh token (logout) |
| GET | `/profile/{studentNumber}` | — | Get user profile |
| PUT | `/profile/{studentNumber}` | — | Update user profile |

---

### 4.2 Reservation Service (Port 5002)

**Purpose:** Core business logic — seat reservation, priority scoring, ban management, admin operations.

**Framework:** ASP.NET Core + EF Core (PostgreSQL) + RabbitMQ (publisher + consumer) + hosted background services

**Key Responsibilities:**
- Manage library `Tables` (linked to floors by `FloorId`)
- Create, list, and cancel `Reservations`
- Enforce business rules (duration limits, overlap checks, advance-booking limits)
- Calculate student `Score` at reservation time
- Enforce time-windowed access for next-day reservations based on score
- Manage `StudentProfile` records (auto-created on first reservation)
- Apply and lift no-show penalties
- Manage `Faculty` and `ExamSchedule` records (admin)
- Expose an internal `/CheckAccess` endpoint used by TurnstileService

**Background Services:**
- `PenaltyCheckService` — runs every 1 minute; scans unattended past reservations; applies 2-day ban per no-show
- `StudentEntryEventConsumer` — consumes `student.entered` events from RabbitMQ; creates a DI scope and sets `IsAttended = true` on the matching reservation

**Student Type Rules:**

| Academic Level | Priority Score | Max Advance Days | Max Active Reservations |
|---|---|---|---|
| Doktora | 300 | 1 (today + tomorrow) | 2 |
| YüksekLisans | 200 | 1 | 2 |
| Lisans | 100 (+50 exam bonus) | 1 | 2 |
| Admin | 99 | 30 | 10 |

**Reservation duration limits:** minimum 1 hour, maximum 4 hours.

**API Endpoints (under `/api/Reservation`):**

| Method | Path | Description |
|---|---|---|
| GET | `/Tables?date=&start=&end=&floorId=` | List tables with availability for a time slot |
| POST | `/Create` | Create a new reservation |
| GET | `/MyReservations?studentNumber=` | List a student's reservations |
| DELETE | `/Cancel/{id}` | Cancel a reservation |
| GET | `/Stats` | Aggregated usage statistics: total, attendance rate, no-show rate, breakdown by student type and hour (admin) |
| GET | `/All` | List all reservations (admin) |
| GET | `/Profile/{studentNumber}` | Get student profile (ban status, type, etc.) |
| PUT | `/Profile/{studentNumber}` | Update student profile |
| GET | `/Penalties` | List students currently banned |
| GET | `/CheckAccess?studentNumber=` | Used by TurnstileService to check if student can enter |
| POST | `/CheckAccess` | Score-based access check for tomorrow's reservations |
| POST | `/SetExamWeek` | Admin: define exam week for a faculty |
| GET | `/ExamWeeks` | Admin: list all exam schedules |
| GET | `/Faculties` | List faculties |
| POST | `/UpdateStudentDepartment` | Admin: assign student to faculty/department |

---

### 4.3 Turnstile Service (Port 5003)

**Purpose:** Simulate a physical library turnstile. Accepts a student number, calls ReservationService to check if entry is permitted, logs the result, and publishes a `student.entered` event.

**Framework:** ASP.NET Core + RabbitMQ publisher + PostgreSQL entry log

**Key Responsibilities:**
- Accept `POST /api/Turnstile/enter` requests with a student number
- Authenticate against Identity Service using the `turnstile-bot` service account (JWT obtained on first request, cached until `401` forces a refresh)
- Forward access check to `ReservationService /api/Reservation/CheckAccess`
- Publish `StudentEnteredEvent` to RabbitMQ on successful entry
- Persist every entry attempt to PostgreSQL via `PostgresTurnstileEntryLog`
- Return `{ doorOpen: true/false, message: "..." }` to the caller

**Entry log:** Persisted to the shared PostgreSQL database (`EntryLogs` table via `TurnstileDbContext`). `PostgresTurnstileEntryLog` implements the sync `ITurnstileEntryLog` interface using fire-and-forget `Task.Run` for writes and `.GetAwaiter().GetResult()` for reads. The old `InMemoryTurnstileEntryLog` is no longer registered.

**API Endpoints:**

| Method | Path | Description |
|---|---|---|
| POST | `/api/Turnstile/enter` | Check + record entry attempt |
| GET | `/api/Turnstile/logs?take=20` | Retrieve recent entry log |

---

### 4.4 Feedback Service (Port 5004)

**Purpose:** Collect student text feedback and provide AI-powered analysis via OpenAI.

**Framework:** ASP.NET Core + EF Core (PostgreSQL)

**Storage:** PostgreSQL via `FeedbackDbContext`. `EfFeedbackRepository` (implements `IFeedbackRepository`) stores feedback in the shared `LibraryReservation` database. The old file-based `FileFeedbackRepository` and `App_Data/` volume have been removed.

**AI Integration:** `OpenAIAnalysisService` calls the OpenAI Chat Completions API (`gpt-3.5-turbo`) to:
- Summarize all feedback into a paragraph
- Run sentiment analysis (Positive / Negative / Neutral)
- Extract the top 5 issues and top 5 suggestions
- Produce topic frequency counts

If the OpenAI API key is missing or a call fails, the service falls back to a static summary derived from the raw data.

**API Endpoints:**

| Method | Path | Description |
|---|---|---|
| POST | `/api/Feedback/Submit` | Submit a feedback record |
| GET | `/api/Feedback?studentNumber=` | List feedback (optionally filtered) |
| GET | `/api/Feedback/Analysis` | Full AI analysis (requires OpenAI key) |
| GET | `/api/Feedback/Summary` | Short AI summary paragraph |

**Configuration required:**
```json
{ "OpenAI": { "ApiKey": "<your-key>" } }
```

In Docker, pass as environment variable `OPENAI_API_KEY`.

---

### 4.5 API Gateway (Port 5010)

**Purpose:** Single HTTP entry point for the frontend. Routes requests to the appropriate microservice using Ocelot.

**Route table (`ocelot.json`):**

| Upstream Path | Downstream Service | Port |
|---|---|---|
| `/api/Auth/{everything}` | IdentityService | 5001 |
| `/api/Reservation/{everything}` | ReservationService | 5002 |
| `/api/Turnstile/{everything}` | TurnstileService | 5003 |
| `/api/Feedback` | FeedbackService | 5004 |
| `/api/Feedback/{everything}` | FeedbackService | 5004 |

The gateway does **not** perform JWT verification. It passes `Authorization` headers through unmodified.

A `ocelot.docker.json` variant exists for Docker Compose where hostnames resolve to container names (e.g., `identity-service` instead of `localhost`).

---

### 4.6 Shared.Events Library

A .NET class library (`Shared.Events.csproj`) shared among backend services. It contains:

**Event DTOs:**
- `ReservationCreatedEvent` — published when a reservation is created
- `ReservationCancelledEvent` — published when a reservation is cancelled
- `StudentEnteredEvent` — published when a student enters via turnstile
- `StudentProfileUpdatedEvent` — published when a student profile changes

**Infrastructure classes:**
- `RabbitMQPublisher` — wraps RabbitMQ `IModel`, serializes events as JSON to a topic exchange named `library_events`
- `RabbitMQConsumer` — wraps RabbitMQ consumer, deserializes JSON and dispatches to typed handler callbacks

---

## 5. Frontend Application

**Framework:** Angular 20 with SSR (`@angular/ssr`, Express 5)

**Port:** 4200 (development), also containerized in Docker

**API Base URL:** All calls go through the API Gateway at `http://localhost:5010`

### Routes

| Path | Component | Guard |
|---|---|---|
| `/` | `HomeComponent` | — |
| `/reservation` | `ReservationFilterComponent` | — |
| `/turnstile` | `TurnstileComponent` | — |
| `/login` | `LoginComponent` | — |
| `/signup` | `SignupComponent` | — |
| `/profile` | `ProfileComponent` | — |
| `/feedback` | `FeedbackComponent` | — |
| `/contact` | `ContactComponent` | — |
| `/about` | `AboutComponent` | — |
| `/faq` | `FaqComponent` | — |
| `/admin` | `AdminPanelComponent` | `AdminGuard` |

### Key Services

**`AuthService`**
- Manages login/logout, JWT token storage in `localStorage`
- Implements proactive token refresh: checks token expiry 1 minute before it expires and automatically calls the `/refresh` endpoint
- Exposes: `login()`, `register()`, `logout()`, `isLoggedIn()`, `isAdmin()`, `getToken()`, `refreshToken()`

**`AuthInterceptor`**
- Angular HTTP interceptor that attaches the JWT `Authorization: Bearer <token>` header to all outgoing requests
- On `401` response, attempts a token refresh and retries the original request once

**`ReservationService`**
- Wraps all Reservation and Turnstile API calls
- Key methods: `getTables()`, `createReservation()`, `getMyReservations()`, `cancelReservation()`, `checkAccess()`, `setExamWeek()`, `getFaculties()`, `updateStudentDepartment()`

**`FeedbackService`**
- Wraps `/api/Feedback` endpoints: `submitFeedback()`, `getFeedbacks()`, `getAnalysis()`, `getSummary()`

### Auth Flow (Frontend)

```
1. User enters student number + password on /login
2. POST /api/Auth/login → receives { accessToken, refreshToken, role, academicLevel }
3. Tokens stored in localStorage
4. AuthInterceptor attaches Bearer token to all requests
5. On 401: interceptor calls /api/Auth/refresh with refresh token
6. On logout: POST /api/Auth/revoke, then clear localStorage
```

---

## 6. Data Models & Database Schema

Both IdentityService and ReservationService share one PostgreSQL database (`LibraryReservation`).

### Identity Tables (managed by ASP.NET Core Identity)

- `AspNetUsers` — extended with `FullName`, `StudentNumber`, `AcademicLevel`
- `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`
- `RefreshTokens` — custom table for refresh token storage

### Reservation Tables

**`Reservations`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `TableId` | int FK | References `Tables.Id` |
| `StudentNumber` | string | Denormalized from Identity |
| `ReservationDate` | DateOnly | |
| `StartTime` | TimeOnly | |
| `EndTime` | TimeOnly | |
| `IsAttended` | bool | Set to true when student checks in |
| `PenaltyProcessed` | bool | True after no-show penalty applied |
| `StudentType` | string | Denormalized from profile at time of reservation |
| `Score` | int | Calculated at reservation time |
| `CreatedAt` | DateTime | UTC, used as tiebreaker |

**`Tables`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `TableNumber` | string | Displayed to users |
| `FloorId` | int | Logical floor grouping |

**`StudentProfiles`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `StudentNumber` | string UNIQUE | |
| `StudentType` | string | Lisans / YüksekLisans / Doktora |
| `FacultyId` | int? FK | References `Faculties.Id` |
| `Department` | string? | |
| `BanUntil` | DateOnly? | Active ban expiry |
| `BanReason` | string? | Human-readable ban explanation |
| `LastNoShowProcessedAt` | DateTime? | |

**`Faculties`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `Name` | string UNIQUE | e.g., "Mühendislik Fakültesi" |

**`ExamSchedules`**

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `FacultyId` | int FK | References `Faculties.Id` (UNIQUE) |
| `ExamWeekStart` | DateOnly | |
| `ExamWeekEnd` | DateOnly | |
| `CreatedAt` | DateTime | |
| `UpdatedAt` | DateTime? | |

### Composite Index

`Reservations(TableId, ReservationDate, StartTime, EndTime)` — supports fast overlap queries.

---

## 7. Key Business Logic & Workflows

### 7.1 Priority Scoring & Time-Windowed Access

**Why it exists:** To give higher-degree students a fair head start when booking the next day's seats. Doctoral students get first access, then master's students, then undergraduates.

**Score calculation:**

```
Base Score:
  Doktora           → 300
  YüksekLisans      → 200
  Lisans            → 100

Exam Bonus (Lisans only):
  If today falls within an active ExamSchedule for the student's faculty → +50
  (Doktora and YüksekLisans never receive this bonus)
```

**Time-windowed access for next-day reservations:**

```
Score ≥ 300  → Reservation opens at 08:00  (Doktora)
Score ≥ 200  → Reservation opens at 10:00  (YüksekLisans)
Score ≥ 150  → Reservation opens at 12:00  (Lisans + exam bonus)
Score ≥ 100  → Reservation opens at 14:00  (Normal Lisans)
```

Today's reservations are always immediately available at any score.

---

### 7.2 Reservation Creation Flow

```
POST /api/Reservation/Create
│
├── Parse date/time fields
├── GetOrCreate StudentProfile (lazy creation on first reservation)
├── Apply any un-processed no-show penalties from past reservations
├── Check active ban — if BanUntil ≥ rDate → reject 400
├── Resolve student type rules (max advance days, max active)
├── If rDate == tomorrow → check PriorityService.CheckAccessAsync()
│     └── If before allowed time → reject 400 with time & score info
├── Count active reservations — if ≥ MaxActiveReservations → reject 400
├── Check same-student overlap (another reservation at same time) → reject 400
├── Check consecutive table block (same student + same table + same date) → reject 400
├── Check table overlap (table already booked in slot) → reject 400
├── CalculateScoreAsync(profile, rDate) → store on Reservation.Score
├── INSERT Reservation
├── Publish ReservationCreatedEvent to RabbitMQ
└── 200 OK { message, reservationId }
```

---

### 7.3 Turnstile Entry Flow

```
POST /api/Turnstile/enter { studentNumber }
│
├── TurnstileService authenticates with IdentityService as turnstile-bot
├── GET /api/Reservation/CheckAccess?studentNumber=<N>
│     └── ReservationService logic:
│           • Find reservation for today, now within window
│             (StartTime - 5min  ≤  now  ≤  StartTime + 15min)
│           • If found and not attended → Allowed = true
│           • Otherwise → Allowed = false with reason
│
├── Record entry in PostgresTurnstileEntryLog (fire-and-forget DB write)
├── If Allowed:
│     • Publish StudentEnteredEvent to RabbitMQ (routing key: "student.entered")
│     • Return { doorOpen: true }
└── Else:
      • Return { doorOpen: false, message }
```

`StudentEntryEventConsumer` receives the `student.entered` event and sets `IsAttended = true` on the matching open reservation (today, within ±5 min of the slot). Only reservations not yet marked attended are updated, preventing double-processing.

---

### 7.4 No-Show Penalty System

`PenaltyCheckService` runs as a hosted background service, polling every 1 minute.

**Logic per run:**

```
1. Clear expired bans (BanUntil < today)
2. Find all Reservations where:
     IsAttended == false AND PenaltyProcessed == false
3. For each:
     reservationStart = ReservationDate + StartTime
     entryDeadline   = reservationStart + 15 minutes
     If now > entryDeadline:
       • Mark reservation.PenaltyProcessed = true
       • Set StudentProfile.BanUntil = now + 2 days
       • Set StudentProfile.BanReason = "Rezervasyonunuza katılmadığınız için sistem 2 gün ceza uyguladı."
4. SaveChanges
```

When `IsAttended = true` is successfully written by `StudentEntryEventConsumer`, `PenaltyCheckService` will skip that reservation — students who physically attended are not penalized.

---

### 7.5 AI Feedback Analysis

`FeedbackService` stores feedback records in the shared PostgreSQL database via `FeedbackDbContext`. When `/api/Feedback/Analysis` is called:

1. Load all feedback from `EfFeedbackRepository` (ordered by date descending)
2. Concatenate messages into a numbered list
3. Send to OpenAI `gpt-3.5-turbo` with a Turkish prompt requesting:
   - 2-3 sentence summary
   - Sentiment (Pozitif / Negatif / Nötr)
   - Up to 5 key issues
   - Up to 5 suggestions
   - Topic frequency map
4. Parse JSON response into `FeedbackAnalysisResult`
5. On failure: fall back to a simple statistical analysis without AI

---

## 8. Event-Driven Communication (RabbitMQ)

**Exchange:** `library_events` (type: `topic`, durable)

| Routing Key | Publisher | Consumer | Status |
|---|---|---|---|
| `student.entered` | TurnstileService | ReservationService `StudentEntryEventConsumer` | Active — consumer sets `IsAttended = true` |
| `reservation.created` | ReservationService | None | Yayınlanıyor — Tüketici yok. Gelecekteki bildirim/analitik servisi için tasarlanmış (kod içinde TODO yorumu mevcut) |
| `reservation.cancelled` | ReservationService | None | Yayınlanıyor — Tüketici yok. Gelecekteki bildirim servisi için tasarlanmış (kod içinde TODO yorumu mevcut) |
| `student.profile.updated` | ReservationService `PenaltyCheckService` | None | Yayınlanıyor — Tüketici yok. Gelecekteki profil senkronizasyon servisi için tasarlanmış (kod içinde TODO yorumu mevcut) |

> **Tasarım Notu:** Bu üç event'in yayınlanıyor ama tüketilmiyor olması sistemin *genişletilebilirlik mimarisinin* bir parçasıdır. RabbitMQ topic exchange `library_events` üzerine yeni bir consumer servis (bildirim, istatistik veya profil senkronizasyon servisi) eklendiğinde bu event'ler hazır olacaktır. Araştırma prototipinin sınırları dışında değerlendirilmelidir.

RabbitMQ credentials (Docker): `library / library123`
Management UI: `http://localhost:15672`

---

## 9. Authentication & Security

### JWT Configuration

- Algorithm: HMAC-SHA256 (HS256)
- Issuer: `Library.Identity`
- Audience: `Library.Api`
- Access token TTL: 60 minutes (configurable)
- Refresh token TTL: 7 days (stored in DB, revocable)
- **Signing key in plain-text config** — must be moved to secrets/environment variables before production

### Security Headers (applied by all services)

```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
```

### CORS Policy

All services read allowed origins from `Cors:AllowedOrigins` in `appsettings.json`. The default fallback is `["http://localhost:4200"]`. To allow additional origins (e.g., ngrok), add them to the array in the relevant `appsettings.json` or pass the env var `Cors__AllowedOrigins__0=https://xxxx.ngrok.io` in Docker.

### HTTPS

Bu proje bir **akademik araştırma prototipidir**. HTTP üzerinden çalışmaktadır. Üretim ortamına taşınma senaryosunda şu yöntemlerden biri uygulanmalıdır:
- Docker Compose önüne bir `nginx` reverse proxy eklenerek Let's Encrypt / self-signed TLS sertifikası yapılandırılmalı,
- veya her serviste `app.UseHttpsRedirection()` ve HTTPS endpoint'i aktif edilmeli, sertifikalar secret olarak yönetilmelidir.

Araştırma kapsamında bu yapılandırma kasitlı olarak dışarıda bırakılmıştır.

### Account Lockout

Configured on IdentityService:
- 5 failed attempts → 15-minute lockout
- Applies to newly registered users

### Authorization State on Endpoints

`ReservationController` uses claim-based properties to determine the caller's role:

```csharp
private bool IsAdmin => User.FindFirst("role")?.Value == "admin";
private bool IsService => User.FindFirst("role")?.Value is "service" or "admin";
```

JWT middleware (`AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`) is configured in `ReservationService/Program.cs`. Requests without a valid token will have no role claims and will be correctly rejected by admin/service-only endpoints.

### Roles

| Role | Description |
|---|---|
| `admin` | Full access: all reservations, penalties, exam weeks |
| `student` | Own reservations only |
| `service` | Used by `turnstile-bot` service account |

---

## 10. API Reference

All endpoints are accessed through the API Gateway at `http://localhost:5010`.

### Authentication

```http
POST /api/Auth/login
Content-Type: application/json

{ "studentNumber": "123456", "password": "Password123!" }

Response 200:
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "accessTokenExpiresAt": "2026-05-02T18:00:00Z",
  "refreshTokenExpiresAt": "2026-05-09T17:00:00Z",
  "studentNumber": "123456",
  "role": "student",
  "academicLevel": "Lisans",
  "fullName": "Test Öğrenci"
}
```

```http
POST /api/Auth/refresh
{ "refreshToken": "..." }
```

```http
POST /api/Auth/revoke
{ "refreshToken": "..." }
```

### Reservations

```http
GET /api/Reservation/Tables?date=2026-05-03&start=10:00&end=12:00&floorId=1
```

```http
POST /api/Reservation/Create
{
  "studentNumber": "123456",
  "tableId": 5,
  "reservationDate": "2026-05-03",
  "startTime": "10:00",
  "endTime": "12:00",
  "studentType": "Lisans"
}
```

```http
GET /api/Reservation/MyReservations?studentNumber=123456
DELETE /api/Reservation/Cancel/{id}
GET /api/Reservation/Profile/{studentNumber}
GET /api/Reservation/CheckAccess?studentNumber=123456
```

### Admin

```http
POST /api/Reservation/SetExamWeek
{ "facultyId": 1, "examWeekStart": "2026-06-02", "examWeekEnd": "2026-06-09" }

POST /api/Reservation/UpdateStudentDepartment
{ "studentNumber": "123456", "facultyId": 2, "department": "Bilgisayar Mühendisliği" }

GET /api/Reservation/ExamWeeks
GET /api/Reservation/Penalties
GET /api/Reservation/All
GET /api/Reservation/Stats
```

### Turnstile

```http
POST /api/Turnstile/enter
{ "studentNumber": "123456" }

Response: { "doorOpen": true, "message": "Giriş başarılı." }

GET /api/Turnstile/logs?take=20
```

### Feedback

```http
POST /api/Feedback/Submit
{ "studentNumber": "123456", "message": "Sistemden çok memnunum." }

GET /api/Feedback
GET /api/Feedback/Analysis
GET /api/Feedback/Summary
```

---

## 11. Infrastructure & Deployment

### Docker Services

| Container | Image | Port(s) |
|---|---|---|
| `library_postgres` | postgres:15-alpine | 5433→5432 |
| `library_rabbitmq` | rabbitmq:3.13-management-alpine | 5672, 15672 |
| `identity_service` | custom build | 5001 |
| `reservation_service` | custom build | 5002 |
| `turnstile_service` | custom build | 5003 |
| `feedback_service` | custom build | 5004 |
| `api_gateway` | custom build | 5010 |
| `library_frontend` | custom build | 4200 |
| `library_db_init` | postgres:15-alpine | — (init container) |

### Startup Order

```
postgres (healthy) → identity-service, reservation-service
rabbitmq (healthy) → reservation-service, turnstile-service
identity + reservation + turnstile + feedback → api-gateway
api-gateway → frontend
identity + reservation → db-init (data restore scripts)
```

`reservation-service` adds an explicit `sleep 15` before starting to give RabbitMQ time to be fully ready.

### Volumes

- `postgres_data` — PostgreSQL data directory

> The `FeedbackService/App_Data` bind-mount has been removed. Feedback is now stored in PostgreSQL.

---

## 12. Setup & Installation

### Prerequisites

- Docker Desktop (Windows / macOS / Linux)
- Git

### Quick Start

**Windows (recommended):**
```powershell
git clone <repository-url>
cd Tubitak-2209A-Sau-Kutuphane-sistemi
.\start.ps1
```

**Linux / macOS:**
```bash
chmod +x start.sh
./start.sh
```

**Manual:**
```bash
docker-compose up -d
docker-compose logs -f db-init
# Wait for: "Database initialization completed successfully!"
```

### Access Points

| Service | URL |
|---|---|
| Frontend | http://localhost:4200 |
| API Gateway | http://localhost:5010 |
| RabbitMQ Management | http://localhost:15672 |
| Identity Service (direct) | http://localhost:5001 |
| Reservation Service (direct) | http://localhost:5002 |
| Turnstile Service (direct) | http://localhost:5003 |
| Feedback Service (direct) | http://localhost:5004 |

### Default Credentials

| Student Number | Password | Role | Notes |
|---|---|---|---|
| `admin` | `Admin123!` | admin | Full admin access |
| `123456` | `Password123!` | student | Test undergraduate |
| `turnstile-bot` | `Turnstile123!` | service | Used by TurnstileService internally |

### OpenAI Configuration (optional)

For AI feedback analysis, set the API key before starting:

**Docker:**
```bash
export OPENAI_API_KEY=sk-...
docker-compose up -d
```

**Local development:**
Add to `Backend/FeedbackService/appsettings.Development.json`:
```json
{ "OpenAI": { "ApiKey": "sk-..." } }
```

### Local Development (without Docker)

Start each service individually:

```powershell
# Terminal 1
dotnet run --project Backend/IdentityService/IdentityService.csproj --urls http://localhost:5001

# Terminal 2
dotnet run --project Backend/ReservationService/ReservationService.csproj --urls http://localhost:5002

# Terminal 3
dotnet run --project Backend/TurnstileService/TurnstileService.csproj --urls http://localhost:5003

# Terminal 4
dotnet run --project Backend/FeedbackService/FeedbackService.csproj --urls http://localhost:5004

# Terminal 5
dotnet run --project Backend/ApiGateway/ApiGateway.csproj --urls http://localhost:5010

# Terminal 6
cd Frontend ; npm start
```

PostgreSQL and RabbitMQ still need to be running. Use Docker for just those:
```bash
docker-compose up -d postgres rabbitmq
```

---

## 13. Testing

### Integration Tests

Located in `Backend/IntegrationTests/`. Test files:

| File | Coverage |
|---|---|
| `AuthenticationTests.cs` | Login, register, refresh token flows |
| `ReservationTests.cs` | Create, list, cancel reservations; overlap logic |
| `TurnstileTests.cs` | Entry allow/deny scenarios |
| `FeedbackTests.cs` | Submit and retrieve feedback |

Run:
```powershell
cd Backend
dotnet test IntegrationTests/IntegrationTests.csproj
# or use the helper script:
.\run-tests.ps1
```

### Performance Tests

Located in `Backend/PerformanceTests/`. Uses k6 (`load-test.js`, `smoke-test.js`).

Helper scripts:
- `simple-load-test.ps1` — basic load test
- `realistic-load-test.ps1` — user-journey simulation
- `quick-load-test.ps1` — smoke test wrapper

Pre-run results stored as JSON (`perf-report-*.json`, `realistic-report-*.json`) for comparison.

### Frontend Unit Tests

```bash
cd Frontend
npm test
```

---

## 14. Folder Structure

```
/
├── docker-compose.yml               # Full stack orchestration
├── start.ps1 / start.sh             # One-command startup scripts
├── LibaryRezervation.sln            # .NET solution file
│
├── Backend/
│   ├── ApiGateway/                  # Ocelot gateway (port 5010)
│   │   ├── ocelot.json              # Local routing config
│   │   └── ocelot.docker.json       # Docker routing config
│   │
│   ├── IdentityService/             # Auth service (port 5001)
│   │   ├── Controllers/AuthController.cs
│   │   ├── Entities/AppUser.cs, RefreshToken.cs
│   │   ├── Data/AppDbContext.cs, DbInitializer.cs
│   │   ├── Options/JwtOptions.cs
│   │   └── Migrations/
│   │
│   ├── ReservationService/          # Core service (port 5002)
│   │   ├── Controllers/ReservationController.cs   # GET /Stats endpoint eklendi
│   │   ├── Data/ReservationDbContext.cs
│   │   ├── Services/
│   │   │   ├── PenaltyCheckService.cs   # Background: no-show detection
│   │   │   ├── PriorityService.cs       # Score & access-time calculation
│   │   │   └── StudentEntryEventConsumer.cs  # RabbitMQ consumer
│   │   ├── Converters/                  # DateOnly/TimeOnly JSON converters
│   │   └── Migrations/
│   │
│   ├── TurnstileService/            # Turnstile simulator (port 5003)
│   │   ├── Controllers/TurnstileController.cs
│   │   ├── Data/TurnstileDbContext.cs      # EF Core context (EntryLogs tablosu)
│   │   ├── Services/
│   │   │   ├── ReservationAccessClient.cs  # HTTP client to ReservationService
│   │   │   ├── TurnstileAuthProvider.cs    # JWT cache for service account
│   │   │   ├── TurnstileEntryLog.cs        # ITurnstileEntryLog interface + InMemoryTurnstileEntryLog (legacy, kayıtlı değil)
│   │   │   └── PostgresTurnstileEntryLog.cs # Aktif PostgreSQL log deposu
│   │   └── Models/
│   │       └── TurnstileEntryLogEntity.cs  # DB entity
│   │
│   ├── FeedbackService/             # Feedback + AI (port 5004)
│   │   ├── Controllers/FeedbackController.cs
│   │   ├── Data/FeedbackDbContext.cs        # EF Core context (Feedbacks tablosu)
│   │   ├── Services/
│   │   │   ├── OpenAIAnalysisService.cs
│   │   │   ├── FileFeedbackRepository.cs    # Legacy — DI'a kayıtlı değil, EfFeedbackRepository aktif
│   │   │   ├── EfFeedbackRepository.cs      # Aktif PostgreSQL deposu
│   │   │   └── IAIAnalysisService.cs
│   │   └── Models/Feedback.cs
│   │
│   ├── Shared.Events/               # Shared event DTOs + RabbitMQ wrappers
│   │   ├── RabbitMQPublisher.cs
│   │   ├── RabbitMQConsumer.cs
│   │   └── *Event.cs
│   │
│   ├── IntegrationTests/
│   └── PerformanceTests/
│
├── Frontend/
│   └── src/app/
│       ├── services/                # AuthService (`getStats()` ekli), ReservationService, FeedbackService
│       ├── guards/AdminGuard.ts
│       ├── models/
│       ├── home/
│       ├── login/, signup/
│       ├── reservation-filter/      # Main seat booking UI
│       ├── profile/                 # Student profile + reservation history
│       ├── admin-panel/             # Admin operations (Rezervasyon İstatistikleri bölümü eklendi)
│       ├── turnstile/               # Turnstile simulation UI
│       ├── feedback/
│       ├── about/, contact/, faq/
│       └── config/                  # App configuration
│
└── init-db/                         # Database initialization scripts
    ├── 01-wait-for-migrations.sh
    ├── 02-restore-identity.sql
    └── 03-restore-data.sql
```

---

## 15. Existing Markdown Files Audit

| File | Classification | Status |
|---|---|---|
| `README.md` | Useful — setup guide | Keep |
| `ARCHITECTURE_DIAGRAM.md` | Useful — Mermaid diagram | Keep |
| `AI_INTEGRATION_GUIDE.md` | Useful — OpenAI setup guide | Keep |
| `PUANLAMA_TEST_SENARYOSU.md` | Useful — scoring test scenarios | Keep |
| `DOCKER_KURULUM.md` | Partially redundant with README | Keep |
| `TUBITAK_2209A_SONUC_RAPORU.md` | Academic result report | Keep |
| `Backend/PerformanceTests/README.md` | k6 instructions | Keep |
| `PROJECT_OVERVIEW.md` | This file — primary dev reference | Keep |
| ~~`GUNCELLEMELER.md`~~ | Stale changelog | **Deleted** |
| ~~`proje_analiz_dokumani.md`~~ | Outdated analysis doc | **Deleted** |
| ~~`Frontend/README.md`~~ | Default Angular boilerplate | **Deleted** |

---

## 16. Known Issues & Technical Debt

### Critical

| # | Issue | Location | Status |
|---|---|---|---|
| 1 | **`IsAdmin` and `IsService` hardcoded to `true`** | `ReservationController.cs` | ✅ **Fixed** — now reads `"role"` claim from JWT |
| 2 | **`IsAttended` never set to `true`** | `StudentEntryEventConsumer.cs` | ✅ **Fixed** — consumer now writes `IsAttended = true` via EF Core scope |
| 3 | **JWT signing key in plain-text config** | `appsettings.json` in all services | ⚠️ Open — must be moved to secrets/environment variables before production |
| 4 | **CORS allows all origins** | All services | ✅ **Fixed** — all services now use `WithOrigins(allowedOrigins)` from config |

### High

| # | Issue | Location | Status |
|---|---|---|---|
| 5 | **No Ocelot JWT verification** | `ApiGateway` | ⚠️ Open — gateway still passes tokens through without validating them |
| 6 | **Turnstile entry log is in-memory** | `InMemoryTurnstileEntryLog` | ✅ **Fixed** — `PostgresTurnstileEntryLog` persists all entries to DB |
| 7 | **No HTTPS in development or Docker** | All services | ⚠️ Open — traffic is unencrypted end-to-end |
| 8 | **Priority time windows are test values (17:00–17:15)** | `PriorityService.cs` | ✅ **Fixed** — production windows: 08:00 / 10:00 / 12:00 / 14:00 |

### Medium

| # | Issue | Location | Status |
|---|---|---|---|
| 9 | **FeedbackService has no persistence layer** | `FileFeedbackRepository` | ✅ **Fixed** — replaced with `EfFeedbackRepository` + `FeedbackDbContext` (PostgreSQL) |
| 10 | **Dead event publishers have no consumers** | `ReservationController`, `PenaltyCheckService` | ⚠️ Open — publishers are kept and annotated with `TODO` comments for future consumer services |
| 11 | **No rate limiting on the API Gateway** | `ApiGateway` | ⚠️ Open — no protection against brute force or DoS |
| 12 | **Student type is denormalized into each Reservation row** | `ReservationDbContext` | ⚠️ Open — type changes on `StudentProfile` do not retroactively update reservations |
| 13 | **`admin.component.ts` is deprecated stub** | `Frontend/src/app/admin/` | ✅ **Fixed** — file deleted |

### Low / Design Notes

- `CheckAccess` endpoint hem `GET` hem `POST` olarak tanımlanmış — `GET /CheckAccess?studentNumber=X` turnstile için, `POST /CheckAccess` frontend skor kontrolü için kullanılıyor. İki ayrı endpoint'e ayrılması önerilir.
- Angular `@angular/common` version is `^20.1.0` (Angular 20, pre-release as of project date) — production use requires a stable release.
- `PostgresTurnstileEntryLog.Record()` fire-and-forget `Task.Run` kullanıyor (sync interface kısıtı nedeniyle). Yazma hatalarını yalnızca logger yakalar; `GetLatest()` direkt EF Core sync `.ToList()` kullandığından deadlock riski bulunmuyor.

### Araştırma Çıktıları (TÜBİTAK Sunumu)

| # | Konu | Sonuç |
|---|---|---|
| R-1 | **Kullanım istatistikleri** — Rezervasyon sayısı, öğrenci tipi dağılımı, no-show oranı | ✅ **Tamamlandı** — `GET /api/Reservation/Stats` endpoint eklendi; toplam rezervasyon, katılım oranı, no-show oranı, öğrenci tipine ve saate göre dağılımı döndürüyor. Admin panelinde görselleştiriliyor. |
| R-2 | **Önceliklendirme etkinlik verisi** — Sınav dönemi önceliği memnuniyeti | ✅ **Tamamlandı** — 21 kullanıcı anketi: Sınav dönemi önceliği (Q4) ortalama **4.62/5** (%92.4 memnuniyet). Araştırma sorusu 2 kullanıcı verisiyle desteklendi. |
| R-3 | **GPT doğruluk ölçümü** — %90 başarı kriteri | ✅ **Tamamlandı** — 10 etiketlenmiş test vakasıyla (`gpt_accuracy_test.py`) yapılan ölçüm: **10/10 = %100 doğruluk** (Pozitif/Negatif/Nötr sınıflandırması). Sonuç `gpt_accuracy_report.json` dosyasında kayıtlı. IP-5 başarı ölçütü (%90) aşıldı. |
| R-4 | **Kullanıcı testi (IP-7)** — %90 pozitif değerlendirme kriteri | ✅ **Tamamlandı** — 21 katılımcı (16 Lisans, 3 YL, 2 Doktora), 29.03.2026. Sonuçlar aşağıdadır. |

### IP-7 Kullanıcı Değerlendirme Anketi Sonuçları (n=21)

**Tarih:** 29 Mart 2026 | **Katılımcı Profili:** 16 Lisans, 3 Yüksek Lisans, 2 Doktora

| Soru | Konu | Ort. (1-5) |
|---|---|---|
| S2 | Rezervasyon yapma süreci kolaylığı | **4.71** |
| S3 | Akademik kademeye göre önceliklendirme adaleti | **4.38** |
| S4 | Sınav döneminde ek öncelik verilmesi memnuniyeti | **4.62** |
| S5 | No-show ceza (2 gün ban) adaleti | **4.62** |

| Genel Değerlendirme (S6) | Sayı | Oran |
|---|---|---|
| Çok Memnun Kaldım | 17 | %81 |
| Memnun Kaldım | 4 | %19 |
| Kararsız / Memnun Kalmadım | 0 | %0 |
| **Toplam Pozitif** | **21/21** | **%100** |

| Kullanım İsteği (S7) | Sayı | Oran |
|---|---|---|
| Kesinlikle Evet | 18 | %86 |
| Evet | 3 | %14 |
| **Toplam** | **21/21** | **%100** |

> **IP-7 Başarı Ölçütü:** "%90 oranında pozitif değerlendirme" → **%100 ile karşılandı. ✅**

---

*Belge son güncelleme: Mayıs 2026 — Tüm teknik düzeltmeler, kullanıcı anketi sonuçları ve araştırma çıktılarının eklenmesi sonrası nihai hali.*
