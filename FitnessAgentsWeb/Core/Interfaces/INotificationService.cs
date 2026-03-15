using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface INotificationService
    {
        Task SendWorkoutNotificationAsync(string toEmail, string markdownWorkout, UserHealthContext context);
    }
}
