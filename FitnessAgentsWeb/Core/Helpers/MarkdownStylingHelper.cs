using Markdig;
using System.Text.RegularExpressions;

namespace FitnessAgentsWeb.Core.Helpers
{
    public static class MarkdownStylingHelper
    {
        /// <summary>
        /// Renders markdown to HTML for web views. Uses CSS classes instead of inline
        /// styles so that dark mode theming works correctly via the design-token system.
        /// </summary>
        public static string RenderToWebHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            return Markdown.ToHtml(markdown, pipeline);
        }

        /// <summary>
        /// Renders markdown to HTML for email delivery. Uses hardcoded inline styles
        /// because email clients do not support external CSS or CSS variables.
        /// </summary>
        public static string RenderToEmailHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            string html = Markdown.ToHtml(markdown, pipeline);

            // Apply light-theme inline styles for email rendering
            html = html.Replace("<h2>", "<h2 style='color: #047857; font-family: DM Sans, -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, sans-serif; font-size: 20px; font-weight: 700; border-bottom: 2px solid #e8e4dc; padding-bottom: 10px; margin-top: 36px; margin-bottom: 16px;'>");
            html = html.Replace("<h3>", "<h3 style='color: #1a1a1a; font-family: DM Sans, -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, sans-serif; font-size: 16px; font-weight: 700; margin-top: 24px; margin-bottom: 12px; text-transform: uppercase; letter-spacing: 0.5px;'>");
            html = html.Replace("<strong>", "<strong style='color: #1a1a1a; font-weight: 700;'>");
            html = html.Replace("<ul>", "<ul style='padding-left: 20px; color: #44403c; margin-bottom: 28px;'>");
            html = html.Replace("<li>", "<li style='margin-bottom: 12px; line-height: 1.6; color: #44403c; font-family: DM Sans, -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, sans-serif; font-size: 15px;'>");
            html = html.Replace("<p>", "<p style='color: #44403c; font-family: DM Sans, -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, sans-serif; line-height: 1.7; font-size: 15px; margin-bottom: 20px;'>");
            html = html.Replace("<table>", "<table width='100%' style='border-collapse: collapse; margin-bottom: 30px; font-size: 14px; background-color: #ffffff; border-radius: 8px; overflow: hidden; border: 1px solid #e8e4dc;'>");
            html = html.Replace("<thead>", "<thead style='background-color: #faf8f5; border-bottom: 2px solid #e8e4dc;'>");
            html = html.Replace("<th>", "<th style='padding: 12px 15px; text-align: left; color: #1a1a1a; font-family: DM Sans, -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, sans-serif; font-weight: 700; text-transform: uppercase; font-size: 12px; letter-spacing: 0.5px;'>");
            html = html.Replace("<tbody>", "<tbody>");
            html = html.Replace("<tr>", "<tr style='border-bottom: 1px solid #f0ece4;'>");
            html = html.Replace("<td>", "<td style='padding: 12px 15px; color: #44403c; font-family: DM Sans, -apple-system, BlinkMacSystemFont, Segoe UI, Helvetica, sans-serif; vertical-align: top;'>");

            return html;
        }
    }
}
