using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agent.Framework
{
    /// <summary>Registers the Agent Framework engine.</summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Makes the "AgentFramework" engine available. AgentRunner uses it when
        /// Agent:Engine = "AgentFramework". Call it after AddAgent(). Safe to call more than once.
        /// The loop holds no per-request data (that lives in AgentLoopRun), so one instance is enough.
        /// </summary>
        public static IServiceCollection AddAgentFrameworkEngine(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentLoop, FrameworkAgentLoop>());
            return services;
        }
    }
}
