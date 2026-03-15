using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface INotificationService
    {
        Task SendWorkoutNotificationAsync(string toEmail, string markdownWorkout, UserHealthContext context);
        Task SendDietNotificationAsync(string toEmail, DietPlan diet, UserHealthContext context);
    }
}
