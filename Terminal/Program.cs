using System;
using System.ComponentModel;
using System.ClientModel;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Terminal;

// 1. Setup the Chat Client using NVIDIA's OpenAI-compatible endpoint
var openAiClient = new OpenAIClient(
    new ApiKeyCredential("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"),
    new OpenAIClientOptions { Endpoint = new Uri("https://integrate.api.nvidia.com/v1") }
);

// Wrap it in Microsoft.Extensions.AI's standard IChatClient interface
IChatClient chatClient = openAiClient.GetChatClient("qwen/qwen3.5-122b-a10b").AsIChatClient();

// 2. Define your Data Tools (The MCP / Local Data Layer)
// The [Description] tags are automatically parsed into tool schemas for the LLM



// 3. Create the Specialized Agents
// The Analyst gets the tools to fetch data
AIAgent analystAgent = chatClient.AsAIAgent(
    name: "Physiological_Analyst",
    instructions: @"You are an elite sports scientist. Your job is to gather the user's biological data and scheduled goals to create a comprehensive brief for the Strength Coach.

    Execute your tools to gather:
    1. Today's intended Workout Schedule.
    2. Current Physical Conditions/Injuries.
    3. Biological Readiness (Sleep, RHR, Strain).
    4. InBody Baseline (Muscle mass, body fat).

    Output a structured, clinical summary analyzing if the user is capable of performing their scheduled workout. Highlight any critical red flags (like injuries or high CNS fatigue) that the Coach must work around. Do NOT suggest specific exercises.",
    tools: [
        AIFunctionFactory.Create(HealthDataTools.GetDailyReadiness),
        AIFunctionFactory.Create(HealthDataTools.GetInBodyBaseline),
        AIFunctionFactory.Create(HealthDataTools.GetUserConditions),
        AIFunctionFactory.Create(HealthDataTools.GetWorkoutSchedule)
    ]
);

// The Coach gets NO tools. It strictly relies on the Analyst's output.
AIAgent coachAgent = chatClient.AsAIAgent(
    name: "Strength_Coach",
    instructions: @"You are a world-class personal trainer specializing in biomechanics and adaptive programming. You will receive a physiological brief from the Analyst.

    Your task is to design today's exact workout plan based on the Analyst's report. 

    FOLLOW THESE STRICT RULES:
    1. Acknowledge their Intended Schedule, but ADAPT if necessary. 
    2. If there is an injury/pain (e.g., piriformis pain), completely remove exercises that aggravate that area. Pivot to isolation movements, mobility, or a different muscle group if needed.
    3. If CNS fatigue is high, reduce total sets and reps by 20% and avoid heavy compound max-outs.
    4. Provide a structured routine: Warm-up, Main Working Sets (with sets/reps), and a Cooldown.
    5. Output in clean Markdown format."
);

// 4. Orchestrate the Sequential Workflow
// This guarantees the Analyst runs first, and its output is passed to the Coach
var workflow = AgentWorkflowBuilder.BuildSequential([analystAgent, coachAgent]);

// Convert the entire multi-agent workflow into a single, callable agent entity
AIAgent workflowAgent = workflow.AsAIAgent(name: "DailyWorkoutEngine");

// 5. Execute the Pipeline
Console.WriteLine("System: Initializing Daily Workout Engine...\n");

// Create a session to maintain state during the run
AgentSession session = await workflowAgent.CreateSessionAsync();

// Stream the output in real-time as the agents deliberate and respond
await foreach (AgentResponseUpdate update in workflowAgent.RunStreamingAsync("Generate today's workout plan based on my health data.", session))
{
    if (update.Text != null)
    {
        Console.Write(update.Text);
    }
}