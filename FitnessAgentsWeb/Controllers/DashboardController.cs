using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers
{
    /// <summary>
    /// Legacy controller kept for backward compatibility.
    /// All functionality has been moved to Overview, Workout, Diet, and Profile controllers.
    /// </summary>
    [Authorize]
    public class DashboardController : Controller
    {
        public IActionResult Index(string? userId = null)
        {
            if (!string.IsNullOrEmpty(userId))
                return RedirectToAction("Index", "Overview", new { userId });

            return RedirectToAction("Index", "Overview");
        }
    }
}
