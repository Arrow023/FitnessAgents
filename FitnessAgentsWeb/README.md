# HealthAssistant AI - Personalized Fitness Orchestrator

HealthAssistant AI is a sophisticated, AI-driven fitness and nutrition ecosystem designed to provide hyper-personalized coaching by integrating biological data, daily activity metrics, and user preferences. It leverages state-of-the-art Large Language Models (LLMs) and Computer Vision to act as a private Strength Coach and Sports Nutritionist.

---

## 📑 Table of Contents
- [Introduction](#introduction)
- [✨ Core Features](#-core-features)
  - [AI Strength Coach](#ai-strength-coach)
  - [AI Sports Nutritionist](#ai-sports-nutritionist)
  - [InBody OCR Vision](#inbody-ocr-vision)
- [🧩 Technical Architecture](#-technical-architecture)
- [🚀 Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Initial Setup](#initial-setup)
  - [Running the Application](#running-the-application)
- [⚙️ Global Configuration](#-global-configuration)
- [📦 Dependencies](#-dependencies)
- [📂 Directory Structure](#-directory-structure)

---

## ✨ Core Features

### AI Strength Coach
The AI Strength Coach orchestrates your weekly workout plan based on real-time data from your smart ring (Sleep, HRV, Steps, Active Burn).
- **Personalized Readiness**: Adjusts workout intensity based on your recovery metrics.
- **Configurable Schedule**: Users can define their own target muscle groups for Monday through Sunday.
- **Progressive Narrative**: The AI maintains a "persona" to encourage and guide you like a real-time coach.

### AI Sports Nutritionist
A dedicated diet agent that drafts daily macro-optimized recovery plans.
- **Food Preferences**: Respects dietary restrictions (e.g., Vegetarian, specific dislikes).
- **History Aware**: Tracks your diet history for the week to ensure variety and smart recommendations.
- **Beautiful UI**: Visualizes meal plans with macro breakdowns and beautiful CSS cards.

### InBody OCR Vision
Automatically extracts biological data from InBody scan images.
- **Vision Mapping**: Uses high-end OCR models to translate image data into structured JSON.
- **Metric Insights**: Tracks Weight, Body Fat %, Muscle Mass (SMM), BMR, and detect muscular imbalances.

---

## 🧩 Technical Architecture

The application is built on **ASP.NET Core 8 MVC** with a modular service-oriented architecture:

- **AI Orchestration**: The `AiOrchestratorService` coordinates between multiple AI agents and the data layer.
- **Storage Layer**: Uses **Firebase Realtime Database** for global settings and user-specific profiles/history.
- **Prompt Engineering**: Dynamic prompt generation utilizing `HealthDataTools` to inject real-time context into LLM calls.
- **Notification System**: Integrated SMTP service for delivering daily plans via high-quality HTML emails.
- **Logging**: Advanced **Serilog** integration with custom timezone enrichment for accurate server-side tracking.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Firebase Project (for Realtime Database)
- API Keys for:
  - NVIDIA NIM or OpenAI (LLM)
  - AI Vision/OCR Provider

### Initial Setup
1. **Clone the repository**:
   ```bash
   git clone <repo-url>
   cd FitnessAgentsWeb
   ```
2. **First Run Configuration**:
   When you first run the application, navigate to `/Setup`. You will be prompted to:
   - Configure Master Admin credentials.
   - Set up AI Model Endpoints and API Keys.
   - Configure SMTP credentials for email delivery.
   - Select your Global Application Timezone.

### Running the Application
```bash
dotnet run
```
Access the application at `https://localhost:5001` or `http://localhost:5000`.

---

## ⚙️ Global Configuration

Administrators can update global system settings via the **Global App Configuration** menu:
- **Regional Settings**: Update the application-wide timezone (e.g., IST, EST, UTC).
- **AI Orchestration**: Update model names (e.g., `meta/llama-3.1-70b-instruct`) and endpoint URLs.
- **Webhook Security**: Users can configure custom header keys/values in their **Preferences** tab to secure their data ingest API.

---

## 📦 Dependencies

Major packages utilized in this project:

| Package | Version | Purpose |
| :--- | :--- | :--- |
| `FirebaseDatabase.net` | 5.0.0 | Realtime database interaction |
| `FirebaseAdmin` | 3.4.0 | Firebase authentication and admin SDK |
| `Microsoft.Extensions.AI` | 10.3.0 | Unified AI service abstractions |
| `Serilog.AspNetCore` | 10.0.0 | Structured logging |
| `Markdig` | 1.1.1 | Markdown to HTML rendering for AI outputs |
| `Swashbuckle` | 6.6.2 | API Documentation/Swagger |

---

## 📂 Directory Structure

```text
FitnessAgentsWeb/
├── Controllers/         # MVC Controllers (Dashboard, Admin, Webhooks)
├── Core/
│   ├── Configuration/   # IAppConfiguration and Firebase providers
│   ├── Helpers/         # Timezone and Markdown utilities
│   ├── Services/        # AI orchestration, storage repos, and email
├── Models/              # Health records, User profiles, and AI payloads
├── Views/               # Razor templates (Premium Glassmorphism UI)
├── Tools/               # Helper classes for AI tool-calling
└── Templates/           # HTML Email templates for plans
```

---

*Designed with ❤️ for elite performance tracking.*