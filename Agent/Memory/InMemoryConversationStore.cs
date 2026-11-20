using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;

namespace Agent.Memory
{
    /// <summary>
    /// Keeps conversations in a thread-safe dictionary in this process's memory.
    /// Fine for one server. Lost on restart (which is fine for a shop chat), and NOT shared between servers.
    /// </summary>
    public sealed class InMemoryConversationStore : IConversationStore
    {
        // ConcurrentDictionary is safe when many requests read and write at the same time.
        private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new ConcurrentDictionary<string, ConversationSession>();

        /// <summary>Returns a copy of the saved session, or null for a new session.</summary>
        public Task<ConversationSession> GetAsync(string sessionId, CancellationToken cancellationToken)
        {
            ConversationSession saved;
            if (!_sessions.TryGetValue(sessionId, out saved))
                return Task.FromResult<ConversationSession>(null);

            // Return a COPY so the caller can't accidentally change our stored list.
            return Task.FromResult(new ConversationSession(saved.OwnerEmail, new List<ChatMessage>(saved.Messages)));
        }

        /// <summary>Stores a copy of the session.</summary>
        public Task SaveAsync(string sessionId, ConversationSession session, CancellationToken cancellationToken)
        {
            _sessions[sessionId] = new ConversationSession(session.OwnerEmail, new List<ChatMessage>(session.Messages));
            return Task.CompletedTask;
        }
    }
}
