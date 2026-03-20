<div align="center">

# 🏋️ HealthAssistant AI

### Your Private AI Strength Coach & Sports Nutritionist

**A self-hosted, AI-powered fitness and nutrition platform that turns your real-time Health Connect data into hyper-personalized workout programs and macro-optimized meal plans — delivered daily to your inbox.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![NVIDIA NIM](https://img.shields.io/badge/NVIDIA-NIM-76B900?logo=nvidia&logoColor=white)](https://build.nvidia.com/)
[![OpenAI Compatible](https://img.shields.io/badge/OpenAI-Compatible-412991?logo=openai&logoColor=white)](https://openai.com/)
[![Firebase](https://img.shields.io/badge/Firebase-Realtime_DB-FFCA28?logo=firebase&logoColor=black)](https://firebase.google.com/)
[![Health Connect](https://img.shields.io/badge/Health_Connect-Android-3DDC84?logo=android&logoColor=white)](https://developer.android.com/health-and-fitness/health-connect)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[**Getting Started**](#-getting-started) · [**Features**](#-core-features) · [**Architecture**](#-architecture) · [**API Reference**](#-api-reference) · [**Contributing**](#-contributing)

</div>

---

## Why HealthAssistant?

Most fitness apps give everyone the same cookie-cutter plan. HealthAssistant is different — it reads your **real biometrics** (HRV, resting heart rate, sleep stages, VO2max, body composition) and generates a plan that adapts to *you* every single day.

- **Bad sleep last night?** The AI dials back intensity and prescribes active recovery.
- **Crushing your step goals?** It pushes progressive overload on your target muscle groups.
- **InBody scan shows muscle imbalance?** The plan compensates with unilateral work.

No cloud lock-in. No subscriptions. Self-host it, own your data, and get elite-level coaching for free.

---

## ✨ Core Features

### 🏋️ AI Strength Coach
Generates a structured daily workout (warm-up → main workout → cooldown) based on real-time recovery data from your smart ring or fitness tracker.

- **Readiness-Aware** — Adjusts intensity using composite Recovery, Sleep, and Active scores (0–100)
- **15-Day Trend Intelligence** — Detects deviations from your personal baseline (RHR, HRV, Steps, Sleep)
- **VO2max Tracking** — Ingests device-reported VO2max or estimates via the [Uth formula](https://en.wikipedia.org/wiki/VO2_max#Estimation) from resting heart rate
- **Configurable Schedule** — Define your target muscle groups for each day of the week
- **Progressive Narrative** — The AI maintains a coaching persona that evolves with your progress
- **Weekly History Context** — Knows what you trained this week to avoid redundancy and ensure balance

### 🥗 AI Sports Nutritionist
Drafts daily macro-optimized meal plans tailored to your workout, recovery needs, and body composition.

- **Calorie Targeting** — Calculated from your BMR, active burn, and training goals
- **Food Preference Aware** — Respects dietary restrictions (vegetarian, allergies, specific dislikes)
- **Diet History Tracking** — Ensures daily meal variety across the week
- **Grouped Meal Cards** — Morning, Lunch, Evening, and Dinner with per-item macros

### 🔬 InBody OCR Vision
Upload a photo of your InBody body composition scan and the AI vision model extracts structured data automatically.

- **Metrics Extracted** — Weight, Body Fat %, SMM, BMR, BMI, Visceral Fat, Waist-Hip Ratio
- **Segmental Lean Analysis** — Detects muscular imbalances (left/right arms, legs, trunk)
- **Target Tracking** — Fat control and muscle control goals from the scan
- **AI Context Integration** — Extracted data feeds directly into workout and diet generation

### 📧 Automated Daily Emails
Beautiful HTML emails delivered at your preferred time with your full daily briefing.

- **Workout Email** — Includes a 24-hour vitals snapshot + InBody panel alongside the training plan
- **Nutrition Email** — Grouped meals with calorie totals and an AI-generated summary
- **Scheduled Delivery** — Per-user notification times with exactly-once daily triggering

### 📱 19 Health Data Types
Ingests a comprehensive set of biometric data from 50+ Android health apps via Health Connect:

Steps · Sleep (with stages) · Heart Rate · Resting Heart Rate · HRV · Active Calories · Total Calories · Distance · Exercise Sessions · Weight · Height · Blood Pressure · Blood Glucose · SpO2 · Body Temperature · Respiratory Rate · Hydration · Nutrition · VO2max

---

## 📱 Health Connect Integration

HealthAssistant receives physiological data from Android devices via [**Health Connect to Webhook**](https://github.com/mcnaveen/health-connect-webhook) by [@mcnaveen](https://github.com/mcnaveen). This open-source Android app bridges **Google Fit, Samsung Health, Fitbit, Garmin Connect, Oura, and 50+ other health apps** into a unified webhook pipeline.

### Quick Setup

1. Install [HC Webhook](https://github.com/mcnaveen/health-connect-webhook/releases) on your Android device (Android 8.0+)
2. Grant Health Connect permissions for desired data types
3. Set webhook URL to `https://<your-host>/api/webhooks/<userId>/generate-workout`
4. Set sync interval (recommended: 30–60 minutes)
5. *(Optional)* Add a custom security header (configured in Profile → Preferences)

### Data Pipeline

```
┌─────────────┐     ┌──────────────┐     ┌───────────────────┐     ┌──────────────┐     ┌────────────┐
│ Smart Ring / │     │ HC Webhook   │     │ WebhooksController│     │ HealthConnect│     │ AI         │
│ Fitness App  │────▶│ Android App  │────▶│ POST /api/webhook │────▶│ DataProcessor│────▶│ Orchestrator│
│ (via Health  │     │              │     │                   │     │ (15-day      │     │ (Workout + │
│  Connect)    │     └──────────────┘     └───────────────────┘     │  merge)      │     │  Diet Gen) │
└─────────────┘                                                     └──────────────┘     └────────────┘
                                                                                               │
                                                                          ┌────────────────────┤
                                                                          ▼                    ▼
                                                                    ┌──────────┐      ┌──────────────┐
                                                                    │ Firebase │      │ Email Service│
                                                                    │ Storage  │      │ (SMTP/Gmail) │
                                                                    └──────────┘      └──────────────┘
```

---

## 🧩 Architecture

Built on **ASP.NET Core 8 MVC** with a clean, service-oriented architecture:

```
FitnessAgentsWeb/
├── Controllers/            # MVC controllers (10 controllers, REST + views)
│   ├── WebhooksController  #   Health Connect data ingest (POST)
│   ├── OverviewController  #   Main dashboard — today's vitals + InBody
│   ├── WorkoutController   #   AI workout plans (view, generate, resend)
│   ├── DietController      #   AI diet plans (view, resend)
│   ├── ExerciseController  #   Exercise session history
│   ├── ProfileController   #   User prefs, schedule, InBody upload
│   ├── AdminController     #   User management, global settings, logs
│   ├── AuthController      #   Cookie-based login/logout
│   └── SetupController     #   First-run configuration wizard
│
├── Core/
│   ├── Interfaces/         # Service abstractions
│   │   ├── IAiAgentService          — LLM workout + diet generation
│   │   ├── IAiOrchestratorService   — Central AI workflow coordinator
│   │   ├── IStorageRepository       — Data persistence (CRUD)
│   │   ├── IHealthDataProcessor     — Merge, deduplicate, compute scores
│   │   └── INotificationService     — Email delivery
│   │
│   ├── Services/           # Implementations
│   │   ├── AiOrchestratorService         — Multi-step AI pipeline
│   │   ├── NvidiaNimAgentService         — NVIDIA NIM / OpenAI LLM client
│   │   ├── HealthConnectDataProcessor    — 15-day sliding window + scoring
│   │   ├── FirebaseStorageRepository     — Firebase Realtime DB persistence
│   │   ├── InBodyOcrService              — Vision-based body comp extraction
│   │   ├── SmtpEmailNotificationService  — HTML email delivery
│   │   └── WorkoutEmailSchedulerService  — Background scheduler (daily)
│   │
│   ├── Factories/          # DI factory pattern for pluggable providers
│   ├── Configuration/      # Firebase + local config providers
│   ├── Helpers/            # Timezone, exercise types, markdown rendering
│   └── Logging/            # Serilog timezone enricher
│
├── Models/                 # Domain models
│   ├── HealthExportPayload — 19 Health Connect data types
│   ├── UserHealthContext   — ~70 computed properties for AI context
│   ├── UserProfile         — User preferences + workout schedule
│   ├── DietPlan            — Structured meal plan with macros
│   └── InBodyMetrics       — Body composition scan data
│
├── Views/                  # Razor views (MVC)
├── Templates/              # HTML email templates (Workout + Diet)
├── Tools/                  # AI function-calling tool definitions
└── wwwroot/                # Static assets (CSS)
```

### Key Design Patterns

| Pattern | Implementation |
|---------|---------------|
| **Factory Pattern** | Pluggable AI, Storage, Notification, and Config providers |
| **Strategy Pattern** | `IAiAgentService` allows swapping LLM backends |
| **Repository Pattern** | `IStorageRepository` abstracts Firebase vs local storage |
| **Background Service** | `WorkoutEmailSchedulerService` runs as a hosted service |
| **Orchestrator** | `AiOrchestratorService` coordinates data → AI → storage → email |

### Scoring Engine

The `HealthConnectDataProcessor` computes three composite scores (0–100) from raw biometrics:

| Score | Inputs | Purpose |
|-------|--------|---------|
| **Recovery Score** | HRV, Resting HR, Sleep Quality, SpO2 | Should you train hard today? |
| **Sleep Score** | Duration, Deep Sleep %, Sleep Efficiency | How well did you recover? |
| **Active Score** | Steps, Active Calories, Exercise Minutes | How active were you today? |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A [Firebase project](https://console.firebase.google.com/) with Realtime Database enabled
- API key for one of:
  - [NVIDIA NIM](https://build.nvidia.com/) (recommended) or any OpenAI-compatible endpoint
  - A separate Vision/OCR model endpoint (for InBody scans)
- SMTP credentials for email delivery (e.g., Gmail App Password)
- *(Optional)* An Android device with [HC Webhook](https://github.com/mcnaveen/health-connect-webhook/releases) for live health data

### Installation

```bash
# Clone the repository
git clone https://github.com/<your-username>/HealthAssistant.git
cd HealthAssistant

# Set required environment variables
export FIREBASE_DATABASE_URL="https://<your-project>-default-rtdb.asia-southeast1.firebasedatabase.app/"
export FIREBASE_DATABASE_SECRET="<your-database-secret>"

# Build and run
dotnet run --project FitnessAgentsWeb
```

The app starts at `https://localhost:7278` (HTTPS) or `http://localhost:5094` (HTTP).

### First-Run Setup

On first launch, navigate to `/Setup` to configure:

1. **Admin Credentials** — Master admin email and password
2. **AI Model** — Model name, endpoint URL, and API key (e.g., `meta/llama-3.1-70b-instruct`)
3. **OCR Vision Model** — For InBody scan extraction
4. **SMTP** — Host, port, sender email, and app password
5. **Timezone** — Application-wide timezone for scheduling and display

After setup, log in at `/Auth/Login` and start adding users from the Admin panel.

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `FIREBASE_DATABASE_URL` | Yes | Firebase Realtime Database URL |
| `FIREBASE_DATABASE_SECRET` | Yes | Firebase database auth secret (legacy token) |

> All other configuration (AI keys, SMTP, timezone) is stored in Firebase and managed through the web UI.

### Obtaining the Firebase Database Secret

The app authenticates with Firebase Realtime Database using a **Database Secret** (legacy auth token). Here's how to get it:

1. Go to the [Firebase Console](https://console.firebase.google.com/) and select your project
2. Click the **gear icon** (⚙️) next to "Project Overview" → **Project settings**
3. Navigate to the **Service accounts** tab
4. Scroll down and click **Database secrets** (under the Firebase Admin SDK section)
5. If no secret exists, one will be auto-generated. Click **Show** to reveal it, then click the **copy** icon
6. Set the secret as an environment variable:
   ```bash
   # Linux / macOS
   export FIREBASE_DATABASE_SECRET="your-secret-here"

   # Windows (PowerShell)
   $env:FIREBASE_DATABASE_SECRET = "your-secret-here"
   ```
   Or add it to `appsettings.json`:
   ```json
   {
     "FirebaseSettings": {
       "DatabaseUrl": "https://<your-project>-default-rtdb.asia-southeast1.firebasedatabase.app/",
       "DatabaseSecret": "your-secret-here"
     }
   }
   ```

> **Note:** Database secrets are a legacy Firebase feature. They grant full read/write access equivalent to an admin. Keep them out of source control — use environment variables for production.

### Securing the Realtime Database

By default, Firebase Realtime Database may have open rules. Apply the security rules from [`firebase-rules.json`](firebase-rules.json) to restrict access to authenticated requests only:

1. In the Firebase Console, go to **Realtime Database** → **Rules** tab
2. Replace the existing rules with the contents of `firebase-rules.json`
3. Click **Publish**

With these rules, only requests that include a valid Database Secret (via the app) will be able to read/write data. Unauthenticated requests from browsers or other clients will be denied.

---

## 📡 API Reference

### Webhook Endpoint

```
POST /api/webhooks/{userId}/generate-workout
```

Receives Health Connect data and appends it to the user's daily health record.

**Headers** *(optional — configured per-user in Profile → Preferences)*:
```
X-Custom-Header-Key: <value>
```

**Request Body** — JSON object with arrays for each enabled data type:

```json
{
  "timestamp": "2026-03-20T10:30:00Z",
  "app_version": "1.0",
  "steps": [{ "count": 5432, "start_time": "...", "end_time": "..." }],
  "heart_rate": [{ "bpm": 72, "time": "..." }],
  "sleep": [{ "duration_seconds": 28800, "session_end_time": "...", "stages": [...] }],
  "exercise": [{ "type": "79", "start_time": "...", "end_time": "...", "duration_seconds": 1800 }],
  "weight": [{ "kilograms": 75.5, "time": "..." }]
}
```

**Responses:**

| Status | Description |
|--------|-------------|
| `200 OK` | Data accepted and merged |
| `401 Unauthorized` | Webhook security header mismatch |

> Exercise type codes follow Android's `ExerciseSessionRecord` constants (e.g., `79` = Walking, `56` = Running, `8` = Biking). The server maps these automatically to human-readable names.

### Supported Data Types

| # | Type | JSON Key | Key Fields |
|---|------|----------|------------|
| 1 | Steps | `steps` | `count`, `start_time`, `end_time` |
| 2 | Sleep | `sleep` | `duration_seconds`, `session_end_time`, `stages[]` |
| 3 | Heart Rate | `heart_rate` | `bpm`, `time` |
| 4 | Resting Heart Rate | `resting_heart_rate` | `bpm`, `time` |
| 5 | HRV | `heart_rate_variability` | `rmssd_millis`, `time` |
| 6 | Active Calories | `active_calories` | `calories`, `start_time`, `end_time` |
| 7 | Total Calories | `total_calories` | `calories`, `start_time`, `end_time` |
| 8 | Distance | `distance` | `meters`, `start_time`, `end_time` |
| 9 | Exercise Sessions | `exercise` | `type`, `start_time`, `end_time`, `duration_seconds` |
| 10 | Weight | `weight` | `kilograms`, `time` |
| 11 | Height | `height` | `meters`, `time` |
| 12 | Blood Pressure | `blood_pressure` | `systolic`, `diastolic`, `time` |
| 13 | Blood Glucose | `blood_glucose` | `mmol_per_liter`, `time` |
| 14 | SpO2 | `oxygen_saturation` | `percentage`, `time` |
| 15 | Body Temperature | `body_temperature` | `celsius`, `time` |
| 16 | Respiratory Rate | `respiratory_rate` | `rate`, `time` |
| 17 | Hydration | `hydration` | `liters`, `start_time`, `end_time` |
| 18 | Nutrition | `nutrition` | `calories`, `protein_grams`, `carbs_grams`, `fat_grams` |
| 19 | VO2max | `vo2max` | `vo2_ml_per_min_kg`, `time` |

---

## 📦 Tech Stack

### Backend

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.AI` | 10.3.0 | Unified AI service abstractions |
| `Microsoft.Extensions.AI.OpenAI` | 10.3.0 | OpenAI-compatible LLM integration |
| `Microsoft.Agents.AI` | 1.0.0-rc4 | Multi-agent AI framework |
| `OpenAI` | 2.9.1 | OpenAI C# client SDK |
| `FirebaseDatabase.net` | 5.0.0 | Firebase Realtime Database client |
| `FirebaseAdmin` | 3.4.0 | Firebase Admin SDK |
| `Google.Apis.Auth` | 1.73.0 | Google authentication |
| `Serilog.AspNetCore` | 10.0.0 | Structured logging |
| `Markdig` | 1.1.1 | Markdown → HTML rendering |
| `Swashbuckle.AspNetCore` | 6.6.2 | Swagger/OpenAPI |

### External Dependencies

| Component | Description |
|-----------|-------------|
| [HC Webhook](https://github.com/mcnaveen/health-connect-webhook) | Android app that bridges Health Connect → webhook. 18+ data types, configurable sync. |
| [Google Health Connect](https://developer.android.com/health-and-fitness/health-connect) | Android API aggregating 50+ health apps (Google Fit, Samsung Health, Fitbit, Garmin, Oura) |
| NVIDIA NIM / OpenAI | LLM provider for AI generation (any OpenAI-compatible endpoint works) |
| Firebase Realtime Database | Cloud persistence for profiles, health data, and AI-generated plans |

---

## ⚙️ Configuration

### Firebase Data Structure

```
/config
  /app_settings          → Global app configuration (AI keys, SMTP, timezone)
/users
  /{userId}
    /profile             → UserProfile (name, email, schedule, preferences)
    /health_connect      → HealthExportPayload (raw + merged biometrics)
    /weekly_history      → WeeklyWorkoutHistory (7-day AI workout plans)
    /weekly_diet_history → WeeklyDietHistory (7-day AI diet plans)
    /inbody              → InBodyExport (latest body composition scan)
    /diet                → DietPlan (latest generated diet)
```

### Admin Settings (Web UI)

Access via **Admin → Settings** after login:

- **AI Orchestration** — Model name, endpoint URL, API key
- **OCR Vision** — Model name, endpoint URL, API key (for InBody scans)
- **SMTP** — Host, port, sender email, app password
- **Timezone** — Application-wide timezone (IST, EST, UTC, etc.)

### Per-User Settings (Profile)

Each user can configure:

- **Notification Time** — When to receive daily AI-generated plans (HH:mm)
- **Workout Schedule** — Target muscle groups for Monday–Sunday
- **Food Preferences** — Dietary restrictions and dislikes
- **Webhook Security** — Custom header key/value for securing data ingest
- **InBody Upload** — Upload body composition scan images for OCR

---

## 🤝 Contributing

Contributions are welcome! Here's how to get started:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Areas for Contribution

- Additional AI provider integrations (Anthropic, Gemini, local models)
- iOS Health data bridge (HealthKit → webhook)
- Data visualization and trend charts
- Docker containerization
- Unit and integration tests
- Localization / multi-language support

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [**@mcnaveen**](https://github.com/mcnaveen) — [Health Connect to Webhook](https://github.com/mcnaveen/health-connect-webhook) Android app
- [**NVIDIA NIM**](https://build.nvidia.com/) — LLM inference platform
- [**Google Health Connect**](https://developer.android.com/health-and-fitness/health-connect) — Android health data aggregation
- [**Firebase**](https://firebase.google.com/) — Realtime Database

---

<div align="center">

**Built with ❤️ for anyone who takes their health seriously.**

*Star this repo if you find it useful!*

</div>