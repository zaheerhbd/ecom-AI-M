using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.Tools;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.AgentTools
{
    /// <summary>
    /// search_knowledge: finds the shop policy sections (returns, warranty, shipping, payments,
    /// discounts, support) that answer a question.
    ///
    /// Two ways to search, and the result says which one was used ("source"):
    ///  - "azure": the existing vector search on Azure AI Search (rag-index). Only when
    ///    Agent:Knowledge:UseAzureSearch is true AND Azure Search is configured. Off by default,
    ///    because the laptop must never touch the live index (decision D7).
    ///  - "local": a keyword search over the policy files that ship with the app.
    /// If Azure is switched on but returns nothing (e.g. not set up yet), the local search is used.
    /// </summary>
    public class SearchKnowledgeTool : ITool
    {
        private const int MaxResults = 3;
        private const int MaxChunkLength = 700;

        private readonly IAiRetrieverService _retriever;
        private readonly IConfiguration _config;

        public SearchKnowledgeTool(IAiRetrieverService retriever, IConfiguration config)
        {
            _retriever = retriever;
            _config = config;
        }

        public string Name => "search_knowledge";

        public string Description =>
            "Search the shop's policies: returns and refunds, warranty, shipping and international delivery, " +
            "payment methods, discounts and promo codes, customer support and store locations. " +
            "Use it for any 'can I...', 'do you...', 'how long...' question about how the shop works. " +
            "If no section answers the question, say you don't know; never invent a policy.";

        public JsonObject ParametersSchema => Schema.Object(
            new ToolParameter("question", "The user's question, in plain words, e.g. 'return after 30 days'.", true));

        public async Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken)
        {
            string question = arguments.GetString("question");
            if (string.IsNullOrWhiteSpace(question))
                return "Error: 'question' is required, e.g. 'how long do refunds take'.";

            var results = new List<object>();
            string source = "local";

            if (UseAzure())
            {
                IReadOnlyList<AiSearchMatch> matches = await _retriever.SearchAsync(question, MaxResults, "policy");
                foreach (AiSearchMatch match in matches.Where(m => m.Chunk != null))
                {
                    context.AddSource("policy", match.Chunk.DocumentId ?? match.Chunk.Id, match.Chunk.Title);
                    results.Add(new
                    {
                        title = match.Chunk.Title,
                        text = ToolHelpers.Shorten(match.Chunk.Text, MaxChunkLength),
                        score = System.Math.Round(match.Score, 3)
                    });
                }
                if (results.Count > 0)
                    source = "azure";
            }

            if (results.Count == 0)
            {
                foreach (PolicyMatch match in LocalPolicySearch.Search(question, MaxResults))
                {
                    context.AddSource("policy", match.Document.Id, match.Document.Title);
                    results.Add(new
                    {
                        title = match.Document.Title,
                        text = ToolHelpers.Shorten(match.Document.Text, MaxChunkLength),
                        score = match.Score
                    });
                }
            }

            if (results.Count == 0)
                return ToolHelpers.ToJson(new { source, results, note = "No policy section matches this question." });

            return ToolHelpers.ToJson(new { source, results });
        }

        private bool UseAzure()
        {
            return string.Equals(_config["Agent:Knowledge:UseAzureSearch"], "true", System.StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_config["AzureSearch:Endpoint"]);
        }
    }
}
