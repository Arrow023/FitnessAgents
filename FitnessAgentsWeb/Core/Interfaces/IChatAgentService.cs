using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Interfaces
{
    /// <summary>
    /// Service for processing chat messages with tool-calling agent capabilities.
    /// Streams events (thinking, tool calls, results, text) via an async enumerable.
    /// </summary>
    public interface IChatAgentService
    {
        /// <summary>
        /// Processes a user message and streams back events including reasoning, tool calls, and responses.
        /// </summary>
        /// <param name="userId">The authenticated user's ID.</param>
        /// <param name="userMessage">The user's chat message.</param>
        /// <param name="conversationHistory">Previous messages for context continuity.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the stream.</param>
        /// <returns>An async stream of chat events to be sent as SSE.</returns>
        IAsyncEnumerable<ChatStreamEvent> ProcessMessageAsync(
            string userId,
            string userMessage,
            List<ChatHistoryMessage> conversationHistory,
            CancellationToken cancellationToken = default);
    }
}
