using Agent.Evals;
using Agent.Memory;
using Agent.Runs;
using Agent.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agent
{
    /// <summary>
    /// Returned by services.AddAgent(...), so the host can plug its own parts in, one per line:
    ///
    ///   services.AddAgent(Configuration)
    ///           .AddTool&lt;SearchProductsTool&gt;()
    ///           .AddTool&lt;GetMyOrdersTool&gt;()
    ///           .UseRunStore&lt;EfAgentRunStore&gt;();
    ///
    /// This is how the engine stays free of shop code: the shop hands its tools and stores in from outside.
    /// </summary>
    public sealed class AgentBuilder
    {
        /// <summary>Wraps the app's service collection.</summary>
        public AgentBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>The app's service collection, for anything the builder methods don't cover.</summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Adds a tool. Tools are SCOPED (one per request), because shop tools use the database
        /// context, which must never be shared between requests.
        /// </summary>
        public AgentBuilder AddTool<TTool>() where TTool : class, ITool
        {
            Services.AddScoped<ITool, TTool>();
            return this;
        }

        /// <summary>Replaces the in-memory run store, e.g. with a database one. Scoped, like a DbContext.</summary>
        public AgentBuilder UseRunStore<TStore>() where TStore : class, IAgentRunStore
        {
            Services.Replace(ServiceDescriptor.Scoped<IAgentRunStore, TStore>());
            return this;
        }

        /// <summary>Replaces the in-memory eval run store, e.g. with a database one. Scoped, like a DbContext.</summary>
        public AgentBuilder UseEvalRunStore<TStore>() where TStore : class, IEvalRunStore
        {
            Services.Replace(ServiceDescriptor.Scoped<IEvalRunStore, TStore>());
            return this;
        }

        /// <summary>Replaces the in-memory conversation store. Singleton, because it outlives single requests.</summary>
        public AgentBuilder UseConversationStore<TStore>() where TStore : class, IConversationStore
        {
            Services.Replace(ServiceDescriptor.Singleton<IConversationStore, TStore>());
            return this;
        }
    }
}
