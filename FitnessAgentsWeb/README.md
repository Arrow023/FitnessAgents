# FitnessAgentsWeb — Technical Reference

> This is the detailed technical documentation for the ASP.NET Core backend. For a high-level overview, features, and setup instructions, see the [root README](../README.md).

---

## Table of Contents

- [Service Architecture](#service-architecture)
- [AI Pipeline](#ai-pipeline)
- [Health Data Processing](#health-data-processing)
- [Scoring Engine](#scoring-engine)
- [InBody OCR Vision](#inbody-ocr-vision)
- [Email Notification System](#email-notification-system)
- [Background Scheduler](#background-scheduler)
- [Interfaces Reference](#interfaces-reference)
- [Models Reference](#models-reference)
- [Webhook Payload Format](#webhook-payload-format)
- [Controllers Reference](#controllers-reference)
- [Configuration Providers](#configuration-providers)
- [Factory System](#factory-system)
- [Directory Structure](#directory-structure)

---

## Service Architecture

The application uses ASP.NET Core 8 with cookie-based authentication, factory-pattern DI, and a hosted background service.

### Dependency Injection

```
Singleton:
  ├── ConfigurationProviderFactory    → Resolves Firebase or local config
  ├── StorageRepositoryFactory        → Creates FirebaseStorageRepository
  ├── HealthDataProcessorFactory      → Creates HealthConnectDataProcessor
  ├── AiAgentServiceFactory           → Creates NvidiaNimAgentService
  ├── NotificationServiceFactory      → Creates SmtpEmailNotificationService
  ├── InBodyOcrService                → Vision OCR (single instance)
  └── AiOrchestratorService           → Central AI workflow coordinator

Scoped (per-request):
  ├── IStorageRepository              → via StorageRepositoryFactory
  ├── IHealthDataProcessor            → via HealthDataProcessorFactory
  └── INotificationService            → via NotificationServiceFactory

Hosted Service:
  └── WorkoutEmailSchedulerService    → Background daily scheduler
```

### Authentication

- **Scheme:** Cookie-based (`CookieAuthenticationDefaults`)
- **Login path:** `/Auth/Login`
- **Expiration:** 7 days (persistent)
- **Roles:** `Admin`, `User`
- **Password hashing:** PBKDF2-HMACSHA256 (100,000 iterations)

### Middleware Pipeline

```
Static Files → Routing → Authentication → Authorization → MVC
Default route: {controller=Overview}/{action=Index}/{id?}
```

---

## AI Pipeline

The `AiOrchestratorService` coordinates the full AI workflow:

```
ProcessAndGenerateAsync(userId)
  │
  ├── 1. Fetch or merge today's HealthExportPayload
  ├── 2. Load into UserHealthContext (~70 computed properties)
  ├── 3. Call IAiAgentService.GenerateWorkoutAsync(context)
  │      └── Streams prompt → LLM → JSON (warmup/main/cooldown)
  ├── 4. Call IAiAgentService.GenerateRecoveryDietJsonAsync(workout, context)
  │      └── Streams prompt → LLM → JSON (meals + macros)
  ├── 5. Parse responses → format as Markdown
  ├── 6. Save to WeeklyWorkoutHistory + DietPlan (Firebase)
  └── 7. Send HTML emails via INotificationService
```

### AI Agent Details (`NvidiaNimAgentService`)

Uses `Microsoft.Agents.AI` multi-agent framework with OpenAI-compatible endpoints.

**Workout Agent:**
- Persona: "World's best personal trainer"
- Input context: Name, weight, body fat, readiness brief, weekly history, conditions, InBody analysis, workout schedule
- Output schema: `{ plan_date, session_title, personalized_introduction, warmup[], main_workout[], cooldown[], coach_notes }`

**Diet Agent:**
- Persona: "Sports nutritionist"
- Input context: Workout plan, weight, BMR, active burn, food preferences, diet history
- Output schema: `{ plan_date, total_calories_target, meals[], ai_summary }`

### AI Tool Calling (`HealthDataTools`)

The AI agents can call these tools for real-time context:

| Tool | Returns |
|------|---------|
| `GetDailyReadiness()` | Recovery/Sleep/Active scores + vitals brief |
| `GetInBodyBaseline()` | Body composition summary |
| `GetUserConditions()` | Medical conditions and injury notes |
| `GetWorkoutSchedule()` | Today's target muscle group |
| `GetWeeklyWorkoutHistory()` | Summary of this week's completed workouts |

---

## Health Data Processing

`HealthConnectDataProcessor` manages the raw health data lifecycle.

### Merge Strategy

```
ProcessAndMergeHealthDataAsync(userId, newPayload)
  │
  ├── Fetch existing data from Firebase
  ├── For each data type (19 channels):
  │   ├── Concatenate new + existing records
  │   ├── Deduplicate by timestamp
  │   └── Retain only last 15 days (sliding window)
  ├── Special case: VO2max — always retains latest record
  └── Save merged payload back to Firebase
```

### Context Loading

```
LoadHealthStateToRAMAsync(userId, payload)
  │
  ├── Extract today's vitals from raw records
  │   ├── Sleep: total duration, deep sleep %, efficiency
  │   ├── Cardiac: RHR, HRV (RMSSD), heart rate
  │   ├── Activity: steps, distance, active + total calories
  │   ├── Body: weight, height, blood pressure, SpO2, temperature
  │   ├── Metabolic: blood glucose, respiratory rate, hydration, nutrition
  │   └── Fitness: VO2max (device or Uth-estimated)
  │
  ├── Compute composite scores (0–100)
  ├── Calculate 15-day rolling averages
  ├── Build 7-day trend arrays (JSON for sparklines)
  ├── Load weekly workout + diet history
  ├── Load InBody data
  └── Generate AI-ready text briefs
```

---

## Scoring Engine

Three composite scores (0–100) are computed from raw biometrics:

### Recovery Score
Weighted combination of:
- HRV (heart rate variability — RMSSD)
- Resting heart rate (lower = better recovery)
- Sleep quality metrics
- Blood oxygen saturation (SpO2)

### Sleep Score
Weighted combination of:
- Total sleep duration
- Deep sleep percentage
- Sleep efficiency (time asleep / time in bed)

### Active Score
Weighted combination of:
- Step count
- Active calories burned
- Exercise session minutes

### VO2max Estimation
When device-reported VO2max is unavailable, the system estimates it using the Uth formula:
```
VO2max = 15.3 × (HRmax / RHR)
HRmax = 220 - Age
```

---

## InBody OCR Vision

`InBodyOcrService` extracts structured data from body composition scan images.

### Pipeline
1. Image → Base64 encoding
2. Send to vision model endpoint with structured extraction prompt
3. Parse JSON response, clean markdown wrapping
4. Return structured `InBodyExport` object

### Extracted Data Schema

```json
{
  "scan_date": "YYYY-MM-DD",
  "demographics": { "gender", "age", "height_cm" },
  "core_composition": {
    "weight_kg", "skeletal_muscle_mass_kg", "body_fat_mass_kg",
    "percent_body_fat", "bmi"
  },
  "segmental_lean_analysis": {
    "right_arm", "left_arm", "right_leg", "left_leg", "trunk"
  },
  "inbody_targets": {
    "target_weight_kg", "fat_control_kg", "muscle_control_kg", "inbody_score"
  },
  "metabolic_health": {
    "basal_metabolic_rate_kcal", "visceral_fat_level", "waist_hip_ratio"
  }
}
```

---

## Email Notification System

`SmtpEmailNotificationService` delivers two types of HTML emails:

### Workout Email
- **Template:** `Templates/EmailTemplate.html`
- **Subject:** `🏋️‍♂️ {FirstName}'s Daily Workout - {date}`
- **Sender Name:** "AI Strength Coach"
- **Content:**
  - Header panel with InBody stats (visceral fat, BMI, weight, BF%, muscle mass, training focus)
  - 24-hour vitals panel (HRV, sleep, RHR, steps, calorie burn)
  - Full workout plan (Markdown → styled HTML via `MarkdownStylingHelper`)

### Diet Email
- **Template:** `Templates/DietEmailTemplate.html`
- **Subject:** `🥗 {FirstName}'s Nutrition Plan - {date}`
- **Sender Name:** "AI Nutritionist"
- **Content:**
  - Daily calorie target + AI summary
  - Meals grouped by type (Morning, Lunch, Evening, Dinner)

### Markdown Rendering
`MarkdownStylingHelper` converts Markdown to HTML with inline CSS ("Elite Coach" theme) for email client compatibility. Styles applied to headings, tables, lists, and paragraphs.

---

## Background Scheduler

`WorkoutEmailSchedulerService` runs as an ASP.NET Core `BackgroundService`:

```
Loop (every 30 seconds):
  For each active user:
    ├── Parse user's NotificationTime (HH:mm)
    ├── Check: current time ≥ scheduled time?
    ├── Check: not already triggered today?
    │   └── Tracked via ConcurrentDictionary<string, DateOnly>
    ├── If both: fire-and-forget ProcessAndGenerateAsync(userId)
    └── Mark as triggered for today (exactly-once guarantee)
```

---

## Interfaces Reference

### IAiOrchestratorService
```csharp
Task<bool> AppendHealthDataAsync(string userId, HealthExportPayload newPayload);
Task<bool> ProcessAndGenerateAsync(string userId, HealthExportPayload? newPayload = null, bool sendEmail = true);
Task<bool> EmailStoreDietPlanAsync(string userId, DietPlan diet);
```

### IAiAgentService
```csharp
Task<string> GenerateWorkoutAsync(UserHealthContext context);
Task<string> GenerateRecoveryDietJsonAsync(string upcomingWorkoutPlan, UserHealthContext context);
```

### IStorageRepository
```csharp
// Health Data
Task<HealthExportPayload?> GetTodayHealthDataAsync(string userId);
Task SaveTodayHealthDataAsync(string userId, string jsonPayload);

// Weekly History
Task<WeeklyWorkoutHistory?> GetWeeklyHistoryAsync(string userId);
Task SaveWeeklyHistoryAsync(string userId, WeeklyWorkoutHistory history);
Task<WeeklyDietHistory?> GetWeeklyDietHistoryAsync(string userId);
Task SaveWeeklyDietHistoryAsync(string userId, WeeklyDietHistory history);

// InBody
Task<InBodyExport?> GetLatestInBodyDataAsync(string userId);
Task SaveLatestInBodyDataAsync(string userId, string jsonPayload);

// User Profiles
Task<Dictionary<string, UserProfile>> GetAllUserProfilesAsync();
Task<UserProfile?> GetUserProfileAsync(string userId);
Task SaveUserProfileAsync(string userId, UserProfile profile);

// Diet
Task<DietPlan?> GetLatestDietAsync(string userId);
Task SaveLatestDietAsync(string userId, DietPlan diet);
```

### IHealthDataProcessor
```csharp
Task<HealthExportPayload> ProcessAndMergeHealthDataAsync(string userId, HealthExportPayload newPayload);
Task<UserHealthContext> LoadHealthStateToRAMAsync(string userId, HealthExportPayload payload);
```

### INotificationService
```csharp
Task SendWorkoutNotificationAsync(string toEmail, string markdownWorkout, UserHealthContext context);
Task SendDietNotificationAsync(string toEmail, DietPlan diet, UserHealthContext context);
```

---

## Models Reference

### HealthExportPayload
19 data channels from Health Connect, each as a `List<T>` of timestamped records:
- Steps, Sleep (with stages), Heart Rate, Resting Heart Rate, HRV
- Active Calories, Total Calories, Distance, Exercise Sessions
- Weight, Height, Blood Pressure, Blood Glucose, SpO2
- Body Temperature, Respiratory Rate, Hydration, Nutrition, VO2max

### UserHealthContext (~70 properties)
Comprehensive computed health state used as AI context:
- **Vitals:** Sleep, RHR, HRV, steps, distance, calories, BP, SpO2, VO2max, etc.
- **Scores:** RecoveryScore, SleepScore, ActiveScore (0–100)
- **Trends:** 7-day JSON arrays for sparkline rendering
- **Averages:** 15-day rolling averages (RHR, HRV, Steps, Sleep)
- **InBody:** Weight, BF%, SMM, BMR, segmental analysis, targets
- **Briefs:** Pre-computed text summaries for AI prompt injection
- **Schedule:** User's workout schedule (Monday–Sunday)

### UserProfile
```csharp
string FirstName, LastName, Email, PasswordHash
string NotificationTime       // "HH:mm" — default "08:00"
string Preferences            // Free-text conditions/notes
string FoodPreferences        // Dietary restrictions
bool IsActive                 // Account active flag
int Age                       // Used for HRmax (220 - Age)
string? WebhookHeaderKey      // Optional webhook security
string? WebhookHeaderValue
Dictionary<string, string> WorkoutSchedule  // Day → muscle group
```

### DietPlan
```csharp
DateTime PlanDate
int TotalCaloriesTarget
List<DietMeal> Meals          // { MealType, FoodName, QuantityDescription, Calories }
string AiSummary
```

### InBodyExport
```csharp
string ScanDate
CoreComposition Core          // Weight, SMM, PBF, BMI
SegmentalLean LeanBalance     // Right/Left Arm/Leg, Trunk
InBodyTargets Targets         // Fat/Muscle control goals
MetabolicHealth Metabolism    // BMR, Visceral Fat Level
```

---

## Webhook Payload Format

```json
{
  "timestamp": "2026-03-20T10:30:00Z",
  "app_version": "1.0",
  "steps": [{ "count": 5432, "start_time": "...", "end_time": "..." }],
  "heart_rate": [{ "bpm": 72, "time": "..." }],
  "sleep": [{
    "session_end_time": "...",
    "duration_seconds": 28800,
    "stages": [{ "stage": "deep", "start_time": "...", "end_time": "..." }]
  }],
  "exercise": [{ "type": "79", "start_time": "...", "end_time": "...", "duration_seconds": 1800 }],
  "weight": [{ "kilograms": 75.5, "time": "..." }]
}
```

> Exercise type codes are numeric constants from Android `ExerciseSessionRecord` (e.g., `79` = Walking, `56` = Running, `8` = Biking, `70` = Strength Training). Mapped automatically by `ExerciseTypeHelper` (50+ types supported with FontAwesome icons).

---

## Controllers Reference

| Controller | Auth | Route | Key Endpoints |
|-----------|------|-------|---------------|
| **SetupController** | None | `/Setup` | `GET /` — First-run wizard; `POST /` — Save initial config |
| **AuthController** | None | `/Auth` | `GET /Login`; `POST /Login`; `POST /Logout` |
| **WebhooksController** | Header | `/api/webhooks` | `POST /{userId}/generate-workout` — HC data ingest |
| **OverviewController** | User | `/Overview` | `GET /` — Main dashboard (vitals + InBody) |
| **WorkoutController** | User | `/Workout` | `GET /` — Weekly plans; `GET /Detail`; `POST /Generate`; `POST /ResendEmail` |
| **DietController** | User | `/Diet` | `GET /` — Latest diet + history; `GET /Detail`; `POST /ResendEmail` |
| **ExerciseController** | User | `/Exercise` | `GET /` — Exercise sessions grouped by day |
| **ProfileController** | User | `/Profile` | `GET /` — Settings; `POST /UpdatePreferences`; `POST /UploadInBody` |
| **AdminController** | Admin | `/Admin` | `GET /Settings`; `POST /UpdateSettings`; `GET /Users`; `POST /AddUser`; `POST /ToggleUser`; `GET /Logs`; `GET /LogFile` |
| **DashboardController** | User | `/Dashboard` | `GET /` — Redirects to Overview (legacy) |

---

## Configuration Providers

### Priority Order

1. **Environment Variables** — `FIREBASE_DATABASE_URL`, `FIREBASE_DATABASE_SECRET` (highest priority)
2. **LocalSettingsProvider** — Reads from `appsettings.json` → `FirebaseSettings:DatabaseUrl` / `FirebaseSettings:DatabaseSecret`
3. **FirebaseSettingsProvider** — Reads from Firebase `config/app_settings` node (primary for all other config)

### Firebase Database Secret

The `FIREBASE_DATABASE_SECRET` is a **legacy auth token** that grants full admin access to the Realtime Database. Both `FirebaseSettingsProvider` and `FirebaseStorageRepository` pass it via `AuthTokenAsyncFactory` to authenticate all read/write operations.

**How to obtain it:**

1. Open the [Firebase Console](https://console.firebase.google.com/) → select your project
2. Click the **gear icon** (⚙️) → **Project settings**
3. Go to **Service accounts** tab
4. Click **Database secrets** (bottom of the page, under Firebase Admin SDK)
5. Click **Show** to reveal the secret, then **copy** it
6. Provide it via environment variable or config:
   ```bash
   # Environment variable (preferred)
   export FIREBASE_DATABASE_SECRET="your-secret-here"
   ```
   ```json
   // Or in appsettings.json
   {
     "FirebaseSettings": {
       "DatabaseUrl": "https://<your-project>-default-rtdb.asia-southeast1.firebasedatabase.app/",
       "DatabaseSecret": "your-secret-here"
     }
   }
   ```

> **Security note:** Database secrets grant unrestricted access. Never commit them to source control. Use environment variables in production.

### Firebase Security Rules

The repository includes [`firebase-rules.json`](../firebase-rules.json) which enforces `auth != null` on all nodes. Apply these rules in **Firebase Console → Realtime Database → Rules → Publish** to block unauthenticated access.

### Firebase Settings Keys (stored in `config/app_settings`)

```
AiModel, AiEndpoint, AiKey           → LLM configuration
OcrModel, OcrEndpoint, OcrKey        → Vision model configuration
SmtpHost, SmtpPort, FromEmail, SmtpPassword  → Email delivery
AdminEmail, AdminPassword            → Master admin credentials
AppTimezone                          → Global timezone (e.g., "India Standard Time")
FirebaseDatabaseSecret               → Database auth token (also stored for backup)
```

---

## Factory System

Factories enable pluggable providers via configuration:

| Factory | Config Key | Current Default |
|---------|-----------|-----------------|
| `AiAgentServiceFactory` | `FactorySettings:AiType` | `"NVIDIA"` → `NvidiaNimAgentService` |
| `NotificationServiceFactory` | `FactorySettings:NotificationType` | `"SMTP"` → `SmtpEmailNotificationService` |
| `StorageRepositoryFactory` | — | `FirebaseStorageRepository` |
| `ConfigurationProviderFactory` | — | Local → Firebase fallback |

---

## Directory Structure

```
FitnessAgentsWeb/
├── Controllers/
│   ├── AdminController.cs           # User management, global settings, logs
│   ├── AuthController.cs            # Cookie-based login/logout
│   ├── DashboardController.cs       # Legacy redirect to Overview
│   ├── DietController.cs            # AI diet plans (view, resend email)
│   ├── ExerciseController.cs        # Exercise session history
│   ├── OverviewController.cs        # Main dashboard (vitals + InBody)
│   ├── ProfileController.cs         # User preferences, schedule, InBody upload
│   ├── SetupController.cs           # First-run configuration wizard
│   ├── WebhooksController.cs        # Health Connect webhook endpoint
│   └── WorkoutController.cs         # AI workout plans (view, generate, resend)
├── Core/
│   ├── Configuration/
│   │   ├── FirebaseSettingsProvider.cs   # Firebase Realtime DB config
│   │   ├── LocalSettingsProvider.cs      # appsettings.json fallback
│   │   ├── IAppConfigurationManager.cs  # Config read + write interface
│   │   ├── IAppConfigurationProvider.cs # Config read-only interface
│   │   └── PasswordHasher.cs            # PBKDF2-HMACSHA256 (100K iterations)
│   ├── Factories/
│   │   ├── AiAgentServiceFactory.cs
│   │   ├── ConfigurationProviderFactory.cs
│   │   ├── HealthDataProcessorFactory.cs
│   │   ├── NotificationServiceFactory.cs
│   │   └── StorageRepositoryFactory.cs
│   ├── Helpers/
│   │   ├── ExerciseTypeHelper.cs        # 50+ exercise type codes → name + icon
│   │   ├── MarkdownStylingHelper.cs     # Markdown → styled HTML for emails
│   │   └── TimezoneHelper.cs            # App timezone utilities
│   ├── Interfaces/
│   │   ├── IAiAgentService.cs
│   │   ├── IAiOrchestratorService.cs
│   │   ├── IHealthDataProcessor.cs
│   │   ├── INotificationService.cs
│   │   └── IStorageRepository.cs
│   ├── Logging/
│   │   └── TimezoneTimestampEnricher.cs # Serilog timezone-aware timestamps
│   └── Services/
│       ├── AiOrchestratorService.cs         # Central AI pipeline coordinator
│       ├── BaseAiAgentService.cs            # Shared LLM client utilities
│       ├── FirebaseStorageRepository.cs     # Firebase Realtime DB persistence
│       ├── HealthConnectDataProcessor.cs    # 15-day merge + scoring engine
│       ├── InBodyOcrService.cs              # Vision-based body comp extraction
│       ├── LocalFileStorageRepository.cs    # Local file fallback (dev only)
│       ├── NvidiaNimAgentService.cs         # NVIDIA NIM / OpenAI LLM client
│       ├── SmtpEmailNotificationService.cs  # HTML email delivery
│       └── WorkoutEmailSchedulerService.cs  # Daily background scheduler
├── Models/
│   ├── DailyHealthMetrics.cs
│   ├── DietPlan.cs
│   ├── HealthExportPayload.cs       # 19 Health Connect data types
│   ├── InBodyExport.cs
│   ├── InBodyMetrics.cs
│   ├── UserHealthContext.cs         # ~70 computed properties for AI
│   ├── UserProfile.cs
│   └── ViewModels/
├── Views/
│   ├── Admin/      (Settings, Users, Logs)
│   ├── Auth/       (Login)
│   ├── Dashboard/  (Legacy redirect)
│   ├── Diet/       (Index, Detail)
│   ├── Exercise/   (Index)
│   ├── Overview/   (Index — main dashboard)
│   ├── Profile/    (Index)
│   ├── Setup/      (Index — first-run wizard)
│   ├── Shared/     (_Layout)
│   └── Workout/    (Index, Detail)
├── Templates/
│   ├── EmailTemplate.html           # Workout + vitals email
│   └── DietEmailTemplate.html       # Nutrition plan email
├── Tools/
│   └── HealthDataTools.cs           # AI function-calling definitions
├── Properties/
│   └── launchSettings.json
└── wwwroot/
    └── css/
        ├── site.css
        ├── layout.css
        ├── components.css
        └── theme.css
```

---

*For setup instructions and feature overview, see the [root README](../README.md).*