# ⚡ AI Strength Coach Webhook (Project "Apex")

An autonomous, AI-driven biomechanics specialist and strength coach built with C# .NET. This backend application acts as a webhook receiver that ingests real-time physiological telemetry from Google Health Connect, combines it with body composition data, and uses a dual-agent AI architecture to generate and email a highly personalized, physiologically-aware daily workout routine.

## 🧠 Architecture Overview

1. **The Telemetry Stream**: An Android device uses the [mcnaveen Health Connect Webhook](https://github.com/mcnaveen/health-connect-webhook) app to push daily vitals (HRV, Sleep, Steps, Calories) to this API.
2. **The Cloud State**: Body composition data (InBody scan) and user feedback/conditions are pulled live from GitHub Gists.
3. **The Analyst Agent**: An AI sports scientist analyzes the raw data, calculating CNS fatigue, readiness, and metabolic load.
4. **The Coach Agent (Apex)**: An AI biomechanics specialist reads the Analyst's brief, checks the week's workout history to avoid repetition, adapts to user feedback, and generates the day's programming.
5. **The Dashboard**: The C# server compiles the AI's markdown into a premium, Apple Health-style HTML email dashboard and delivers it to the user.

## ✨ "God Mode" Features

* **Advanced Sleep Math (Noon-to-Noon)**: Fixes the "Midnight Sleep Split" by calculating sleep from 12:00 PM yesterday to 12:00 PM today, explicitly filtering out "awake" stages for 100% accuracy against premium trackers like Gabit and Oura.
* **HRV & CNS Tracking**: Integrates Heart Rate Variability (RMSSD) to dynamically adapt workout intensity based on nervous system recovery.
* **Strict Timezone Handling**: Forcefully anchors all UTC timestamps to India Standard Time (IST) to ensure daily aggregation perfectly matches the user's calendar day, regardless of server location.
* **Micro-Cycle Memory**: Maintains a `weekly_history.json` file that resets every Sunday. Apex reviews this memory before programming to ensure balanced muscle targeting and zero redundant exercises.
* **Live Gist Polling**: Uses cache-busting logic to instantly fetch the user's latest physical conditions (e.g., "my piriformis hurts") and body fat targets from GitHub Gists.
* **Premium UI Injection**: Uses `Markdig` to convert the AI's markdown into a custom-styled HTML email, featuring clinical "Lab Report" metric cards, a highlighted HRV recovery gauge, and Caloric Gap fractionals.

## 🛠️ Dependencies & Tech Stack

* **Framework**: C# .NET (Minimal API)
* **AI Integration**: Microsoft Semantic Kernel / Azure OpenAI SDK (for `AsAIAgent` and Tool calling)
* **Markdown Parser**: [Markdig](https://github.com/xoofx/markdig) (For converting AI markdown to HTML)
* **Mobile Bridge**: [Health Connect Webhook by mcnaveen](https://github.com/mcnaveen/health-connect-webhook) (Android 13+ compatible)
* **Email**: `System.Net.Mail` (SMTP client for delivery)

## 📱 Mobile App Setup (Android 13/14)

This project requires your Android phone to push Health Connect data to the endpoint.
1. Install [Health Connect](https://play.google.com/store/apps/details?id=com.google.android.apps.healthdata) (if not natively integrated into your OS).
2. Install the open-source **Health Connect Webhook** app.
3. In Android Settings > Apps > Health Connect Webhook > Battery, set to **Unrestricted** (Prevents Android from killing background syncs).
4. Grant the app permissions to read: Steps, Sleep, Heart Rate, Heart Rate Variability, Distance, and Active/Total Calories.
5. Set the webhook URL to your hosted C# endpoint (e.g., `https://your-server.com/webhook`).

## ⚙️ Configuration (`appsettings.json`)

You must configure your external data sources and API keys in your `appsettings.json` file. Ensure your GitHub Gist URLs point to the **raw** file and do *not* include the commit hash (this ensures you always pull the latest version).

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ExternalData": {
    "InBodyUrl": "[https://gist.githubusercontent.com/USERNAME/GIST_ID/raw/latest_inbody.json](https://gist.githubusercontent.com/USERNAME/GIST_ID/raw/latest_inbody.json)",
    "ConditionsUrl": "[https://gist.githubusercontent.com/USERNAME/GIST_ID/raw/user_conditions.txt](https://gist.githubusercontent.com/USERNAME/GIST_ID/raw/user_conditions.txt)"
  },
  "AI": {
    "Endpoint": "YOUR_AZURE_OR_OPENAI_ENDPOINT",
    "ApiKey": "YOUR_API_KEY",
    "Model": "gpt-4o"
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Sender": "your-bot-email@gmail.com",
    "Password": "your-app-password",
    "Recipient": "your-personal-email@gmail.com"
  }
}