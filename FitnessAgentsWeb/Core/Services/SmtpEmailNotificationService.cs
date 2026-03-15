using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Tools;
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

        public SmtpEmailNotificationService(IAppConfigurationProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public async Task SendWorkoutNotificationAsync(string toEmail, string markdownWorkout, UserHealthContext context)
        {
            string fromEmail = _configProvider.GetFromEmail();
            string appPassword = _configProvider.GetSmtpPassword();

            if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(toEmail) || string.IsNullOrEmpty(appPassword))
            {
                Console.WriteLine("[Error] SMTP Config missing. Cannot send email.");
                return;
            }

            string aiHtmlContent = FitnessAgentsWeb.Core.Helpers.MarkdownStylingHelper.RenderToEmailHtml(markdownWorkout);

            string templatePath = Path.Combine(AppContext.BaseDirectory, @"Templates\EmailTemplate.html");
            string finalHtmlBody = "";

            try
            {
                finalHtmlBody = await File.ReadAllTextAsync(templatePath);

                finalHtmlBody = finalHtmlBody.Replace("{{DATE}}", DateTime.Now.ToString("dddd, MMM d"));
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
                Console.WriteLine($"[Error] Could not load EmailTemplate.html: {ex.Message}");
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
                Subject = $"🏋️‍♂️ {context.FirstName}'s Daily Workout - {DateTime.Now:dddd, MMM d}",
                Body = finalHtmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine($"[System] Professional HTML Workout successfully emailed to {context.FirstName}!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to send email: {ex.Message}");
            }
        }
    }
}
