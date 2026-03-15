# Fitness Agents AI - Single-Tenant Orchestration Platform

A powerful, single-tenant, multi-user web application and AI orchestrator designed to track biological baselines, daily health metrics, and autonomously generate and email highly personalized workout and diet plans. Built on C# .NET 8 MVC and Firebase Realtime Database.

## Core Features

- **Single-Tenant Architecture**: A unified global configuration for the application environment, allowing the primary administrator to effortlessly provision and monitor multiple user accounts.
- **Background AI Scheduler (`WorkoutEmailSchedulerService`)**: AI generation is entirely decoupled from the webhook ingestion. Users can configure their absolute preferred "Daily Notification Time". The system's background worker wakes up, cross-references profiles, executes the deep AI reasoning phase asynchronously, and fires the email without blocking front-end operations.
- **Firebase Realtime Sync**: All state is exclusively saved to Firebase. The `/config/app_settings` node manages global SMTP/AI keys. The `/users/{userId}/` node isolates HealthConnect streams, InBody data, weekly workout history, and user profile schedules. Absolutely no user data is saved to local disks.
- **Premium Fitness Dashboard**: A vibrant, high-energy UI built with modern CSS variables, utilizing `Chart.js` for beautiful interactive visualizations of Sleep tracking and Body Composition (Skeletal Muscle Mass vs. Body Fat percentage).
- **InBody OCR Vision Agent**: Upload physical InBody fat/muscle composition scan printouts directly to the Dashboard. The application leverages an OpenAI-compatible Vision Model to extract detailed metrics (SMM, PBF, Visceral Fat) into a unified JSON structure, overwriting the user's biological baseline.
- **Dietician Agent**: A secondary AI agent that works in tandem with the primary coach. It analyzes the newly generated workout, the user's total burned calories, and physiological metrics to spit out a robust, science-backed recovery diet plan appended to the daily email.

## Setup & Execution

### 1. Environmental Configuration
The application relies strictly on environment variables for database connections, ensuring it is 100% Docker-ready and secure.
```bash
# Windows PowerShell
$env:FIREBASE_DATABASE_URL="https://your-project-url.firebasedatabase.app/"

# Linux/macOS
export FIREBASE_DATABASE_URL="https://your-project-url.firebasedatabase.app/"
```

### 2. Run the Application
```bash
dotnet build
dotnet run
```
On first launch, if the global configurations (AI, SMTP, Admin user) are not found in the Firebase `/config/app_settings` node, the application will lock down and automatically redirect you to the `/Setup` onboarding screen.

## Webhooks

Each user possesses a unique webhook endpoint exposed via:
`POST /api/webhooks/{userId}/generate-workout`

This endpoint operates instantaneously, cleanly merging the standard `HealthConnect` payload into Firebase and returning `HTTP 200 OK`. The Background Scheduler will automatically aggregate this data when it's time to generate the user's plan.

## Manual Overrides
Administrators and users can manually trigger the immediate generation and dispatch of emails (bypassing the Background Scheduler) directly from the Dashboard utilizing the **"Email Diet & Workout Plan"** action button.