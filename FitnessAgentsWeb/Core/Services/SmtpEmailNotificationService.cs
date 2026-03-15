using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Tools;
using FitnessAgentsWeb.Core.Helpers;
using Markdig;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class SmtpEmailNotificationService : INotificationService
    {
        private readonly IAppConfigurationProvider _configProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<SmtpEmailNotificationService> _logger;

        public SmtpEmailNotificationService(IAppConfigurationProvider configProvider, Microsoft.Extensions.Logging.ILogger<SmtpEmailNotificationService> logger)
        {
            _configProvider = configProvider;
            _logger = logger;
        }

        public async Task SendWorkoutNotificationAsync(string toEmail, string markdownWorkout, UserHealthContext context)
        {
            string fromEmail = _configProvider.GetFromEmail();
            string appPassword = _configProvider.GetSmtpPassword();

            if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(toEmail) || string.IsNullOrEmpty(appPassword))
            {
                _logger.LogError("[Error] SMTP Config missing. Cannot send email.");
                return;
            }

            string aiHtmlContent = FitnessAgentsWeb.Core.Helpers.MarkdownStylingHelper.RenderToEmailHtml(markdownWorkout);

            string templatePath = Path.Combine(AppContext.BaseDirectory, @"Templates\EmailTemplate.html");
            string finalHtmlBody = "";

            try
            {
                finalHtmlBody = await File.ReadAllTextAsync(templatePath);

                string tzId = _configProvider.GetAppTimezone();
                var todayApp = TimezoneHelper.GetAppNow(tzId);

                finalHtmlBody = finalHtmlBody.Replace("{{DATE}}", todayApp.ToString("dddd, MMM d"));
                finalHtmlBody = finalHtmlBody.Replace("{{WORKOUT_CONTENT}}", aiHtmlContent);

                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_WEIGHT}}", context.InBodyWeight);
                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BF}}", context.InBodyBf);
                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_SMM}}", context.InBodySmm);
                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BMR}}", context.InBodyBmr);
                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_WEAK}}", context.InBodyImbalances);
                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_VISCERAL}}", context.InBodyVisceral);
                finalHtmlBody = finalHtmlBody.Replace("{{INBODY_BMI}}", context.InBodyBmi);

                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_SLEEP}}", context.VitalsSleepTotal);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_DEEP}}", context.VitalsSleepDeep);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_RHR}}", context.VitalsRhr);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_STEPS}}", context.VitalsSteps);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_DIST}}", context.VitalsDistance);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_CALS}}", context.VitalsCalories);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_HRV}}", context.VitalsHrv);
                finalHtmlBody = finalHtmlBody.Replace("{{VITALS_TOTAL_CALS}}", context.VitalsTotalCalories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Error] Could not load EmailTemplate.html");
                return;
            }

            using var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, appPassword),
                EnableSsl = true,
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "AI Strength Coach"),
                Subject = $"🏋️‍♂️ {context.FirstName}'s Daily Workout - {TimezoneHelper.GetAppNow(_configProvider.GetAppTimezone()):dddd, MMM d}",
                Body = finalHtmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"[System] Professional HTML Workout successfully emailed to {context.FirstName}!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Error] Failed to send email");
            }
        }

        public async Task SendDietNotificationAsync(string toEmail, DietPlan diet, UserHealthContext context)
        {
            string fromEmail = _configProvider.GetFromEmail();
            string appPassword = _configProvider.GetSmtpPassword();

            if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(toEmail) || string.IsNullOrEmpty(appPassword))
            {
                _logger.LogError("[Error] SMTP Config missing. Cannot send email.");
                return;
            }

            // Group meals by type for the email content
            var groupedMeals = "";
            var prevType = "";
            foreach (var meal in diet.Meals)
            {
                if (meal.MealType != prevType)
                {
                    groupedMeals += $"<h3 style='color: #ec4899; font-size: 14px; font-weight: 800; text-transform: uppercase; margin-top: 20px; margin-bottom: 10px;'>{meal.MealType}</h3>";
                    prevType = meal.MealType;
                }
                groupedMeals += $"<p style='margin: 0 0 8px 0; color: #374151; font-size: 15px;'>• <strong>{meal.FoodName}</strong> ({meal.QuantityDescription}) <span style='color: #6b7280; float: right;'>{meal.Calories} kcal</span></p>";
            }

            string templatePath = Path.Combine(AppContext.BaseDirectory, @"Templates\DietEmailTemplate.html");
            string finalHtmlBody = "";

            try
            {
                finalHtmlBody = await File.ReadAllTextAsync(templatePath);

                finalHtmlBody = finalHtmlBody.Replace("{{DATE}}", diet.PlanDate.ToString("dddd, MMM d"));
                finalHtmlBody = finalHtmlBody.Replace("{{TOTAL_CALORIES}}", diet.TotalCaloriesTarget.ToString());
                finalHtmlBody = finalHtmlBody.Replace("{{AI_SUMMARY}}", diet.AiSummary);
                finalHtmlBody = finalHtmlBody.Replace("{{DIET_CONTENT}}", groupedMeals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Error] Could not load DietEmailTemplate.html");
                return;
            }

            using var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromEmail, appPassword),
                EnableSsl = true,
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, "AI Nutritionist"),
                Subject = $"🥗 {context.FirstName}'s Nutrition Plan - {diet.PlanDate:dddd, MMM d}",
                Body = finalHtmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"[System] Nutrition Plan successfully emailed to {context.FirstName}!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Error] Failed to send diet email");
            }
        }
    }
}
