using System;
using System.Collections.Generic;

namespace Agent
{
    /// <summary>
    /// Facts about the CURRENT request that the model must never be allowed to choose:
    /// who the user is, where the request came from, and which sources the tools used.
    ///
    /// One of these is created per request (it is "scoped"). The API fills it from the
    /// signed-in user's token (JWT). Tools read the user's email from HERE, never from the
    /// model's arguments, because the model can be tricked into sending someone else's email.
    /// </summary>
    public sealed class AgentRequestContext
    {
        /// <summary>The value of Channel for normal chat requests.</summary>
        public const string ChatChannel = "chat";

        /// <summary>The value of Channel for requests made by the eval runner.</summary>
        public const string EvalChannel = "eval";

        private readonly List<AgentSource> _sources = new List<AgentSource>();

        /// <summary>Starts as an anonymous chat request.</summary>
        public AgentRequestContext()
        {
            Channel = ChatChannel;
        }

        /// <summary>The signed-in user's email, taken from the JWT. Null when nobody is signed in.</summary>
        public string UserEmail { get; set; }

        /// <summary>True when a user is signed in.</summary>
        public bool IsSignedIn
        {
            get { return !string.IsNullOrWhiteSpace(UserEmail); }
        }

        /// <summary>Where the request came from: "chat" or "eval".</summary>
        public string Channel { get; set; }

        /// <summary>Set when the request is part of an eval run, so the run record can link back to it.</summary>
        public Guid? EvalRunId { get; set; }

        /// <summary>The products, policies or orders the tools looked at. The UI shows these under the answer.</summary>
        public IReadOnlyList<AgentSource> Sources
        {
            get { return _sources; }
        }

        /// <summary>
        /// Records that the answer used this source. Adding the same source twice is ignored,
        /// so tools don't need to check first.
        /// </summary>
        public void AddSource(string type, string id, string title)
        {
            foreach (AgentSource existing in _sources)
            {
                if (string.Equals(existing.Type, type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            _sources.Add(new AgentSource(type, id, title));
        }
    }

    /// <summary>One thing the answer is based on, e.g. { type: "product", id: "12", title: "Blue Hat" }.</summary>
    public sealed class AgentSource
    {
        /// <summary>Empty constructor, needed when a saved run is read back from JSON.</summary>
        public AgentSource()
        {
        }

        /// <summary>Creates a source.</summary>
        public AgentSource(string type, string id, string title)
        {
            Type = type;
            Id = id;
            Title = title;
        }

        /// <summary>What kind of thing it is: "product", "policy", "order"...</summary>
        public string Type { get; set; }

        /// <summary>Its id, as text.</summary>
        public string Id { get; set; }

        /// <summary>A human-readable name.</summary>
        public string Title { get; set; }
    }
}
