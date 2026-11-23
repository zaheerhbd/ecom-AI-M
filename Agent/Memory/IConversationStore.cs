using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;

namespace Agent.Memory
{
    /// <summary>
    /// One saved conversation: who started it, and its messages.
    /// Keeping the owner lets the runner refuse a session id that belongs to someone else.
    /// </summary>
    public sealed class ConversationSession
    {
        /// <summary>Creates a session record.</summary>
        public ConversationSession(string ownerEmail, List<ChatMessage> messages)
        {
            OwnerEmail = ownerEmail;
            Messages = messages ?? new List<ChatMessage>();
        }

        /// <summary>The email of the user who started the conversation. Null for an anonymous conversation.</summary>
        public string OwnerEmail { get; }

        /// <summary>The messages, oldest first. The first one is always the system prompt.</summary>
        public List<ChatMessage> Messages { get; }
    }

    /// <summary>
    /// Where we keep each conversation's history ("memory"), keyed by a session id.
    ///
    /// LLM APIs are STATELESS: the model remembers nothing between calls.
    /// The "memory" you see in chat apps is just the app re-sending earlier messages every time.
    ///
    /// SCALABILITY: the in-memory store only works with one server. To run several copies of the
    /// API, swap it for a shared one (Redis, SQL...). Only this one class would change.
    /// </summary>
    public interface IConversationStore
    {
        /// <summary>Returns a copy of the saved session, or null for a new session.</summary>
        Task<ConversationSession> GetAsync(string sessionId, CancellationToken cancellationToken);

        /// <summary>Replaces the saved session.</summary>
        Task SaveAsync(string sessionId, ConversationSession session, CancellationToken cancellationToken);
    }
}
