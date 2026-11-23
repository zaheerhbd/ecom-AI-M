using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Threading.RateLimiting;
using Agent;
using Infrastructure.AgentTools;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace API.Extensions
{
    /// <summary>
    /// Everything the shop needs to run the agent (Phase 3), in one place:
    /// the engine, the 7 shop tools, the database stores, the admin rule, the rate limit
    /// and the 30-day cleanup. Startup.cs calls services.AddShopAgent(...) and app.UseRateLimiter().
    /// </summary>
    public static class AgentServiceExtensions
    {
        /// <summary>Name of the rate-limit rule used by POST api/agent/chat.</summary>
        public const string ChatRateLimitPolicy = "agent-chat";

        /// <summary>Name of the rule that guards the runs and evals endpoints.</summary>
        public const string AdminPolicy = "AgentAdmin";

        public static IServiceCollection AddShopAgent(this IServiceCollection services, IConfiguration config)
        {
            // ---- Shop defaults ----
            // Registered BEFORE AddAgent, so any value in the "Agent" config section still wins.
            // appsettings.json is not in git, so these keep the agent sensible on a fresh machine.
            bool usesGemini = string.Equals(config["Agent:Llm:Provider"], "Gemini", StringComparison.OrdinalIgnoreCase);
            services.Configure<AgentOptions>(options =>
            {
                options.SystemPromptPath = "Agent/system-prompt.md";
                // Prices per million tokens, used for the ESTIMATED cost of each answer.
                // Gemini free tier: $0. gpt-5.4-mini list prices (checked 2026-11-21): $0.75 / $4.50.
                // Set Agent:Pricing to override (e.g. Gemini on a paid plan).
                options.Pricing = usesGemini
                    ? new AgentPricing { InputPerMillionUsd = 0m, OutputPerMillionUsd = 0m }
                    : new AgentPricing { InputPerMillionUsd = 0.75m, OutputPerMillionUsd = 4.50m };
            });

            // ---- The engine and the shop's tools (all read-only) ----
            // The LLM provider comes from Agent:Llm:Provider: "Gemini" (free tier, the shop's choice)
            // or "OpenAI". Without it the engine uses its free offline Fake model.
            services.AddAgent(config)
                .AddTool<SearchProductsTool>()
                .AddTool<GetProductDetailsTool>()
                .AddTool<ListCatalogFiltersTool>()
                .AddTool<SearchKnowledgeTool>()
                .AddTool<GetDeliveryOptionsTool>()
                .AddTool<GetMyOrdersTool>()   // sign-in only (IRequiresSignIn)
                .AddTool<GetOrderTool>()      // sign-in only (IRequiresSignIn)
                .UseRunStore<EfAgentRunStore>()
                .UseEvalRunStore<EfEvalRunStore>();

            // ---- Delete runs older than 30 days (D3) ----
            services.AddHostedService<AgentRunCleanupService>();

            // ---- Admin only in production, open in development (D4) ----
            services.AddSingleton<IAuthorizationHandler, AgentAdminHandler>();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AdminPolicy, policy => policy.Requirements.Add(new AgentAdminRequirement()));
            });

            // ---- Rate limit: N chat messages per minute per user, or per IP when anonymous (D2) ----
            int perMinute = config.GetValue("Agent:RateLimit:PermitPerMinute", 10);
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        "{\"statusCode\":429,\"message\":\"Too many messages. Please wait a minute and try again.\"}", token);
                };

                options.AddPolicy(ChatRateLimitPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        PartitionKey(httpContext),
                        key => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = perMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0 // over the limit = answer 429 straight away, don't wait
                        }));
            });

            return services;
        }

        /// <summary>
        /// Who a request counts against: the signed-in user's email, or else the caller's IP address.
        /// Note: behind a proxy (Azure Container Apps) every anonymous caller can share the proxy's IP
        /// until forwarded headers are configured (Phase 6).
        /// </summary>
        private static string PartitionKey(HttpContext httpContext)
        {
            string email = httpContext.User?.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrWhiteSpace(email))
                return "user:" + email.ToLowerInvariant();

            return "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
    }

    /// <summary>The "agent admin" rule: see AgentAdminHandler.</summary>
    public class AgentAdminRequirement : IAuthorizationRequirement
    {
    }

    /// <summary>
    /// Allows everyone in Development, so the pages are easy to try on the laptop.
    /// Anywhere else, only users with the "Admin" role (decision D4).
    /// </summary>
    public class AgentAdminHandler : AuthorizationHandler<AgentAdminRequirement>
    {
        private readonly IHostEnvironment _environment;

        public AgentAdminHandler(IHostEnvironment environment)
        {
            _environment = environment;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AgentAdminRequirement requirement)
        {
            if (_environment.IsDevelopment() || context.User.IsInRole(AppIdentityDbContextSeed.AdminRole))
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
