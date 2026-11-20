using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Tools
{
    /// <summary>
    /// A TOOL is a normal C# method the model is allowed to ask for.
    /// Tools are how an agent "does things": look up products, read a policy, check an order.
    ///
    /// Golden rules for tools:
    ///  - Treat the arguments as UNTRUSTED input (the model can send anything). Validate them.
    ///  - Return plain text or JSON the model can read. Return errors as text too ("Error: ..."), never crash the agent.
    ///  - Write a clear Description: it is the only thing the model reads to decide when to use the tool.
    ///  - Take the user's identity from AgentRequestContext, never from the arguments.
    /// </summary>
    public interface ITool
    {
        /// <summary>Short snake_case name, e.g. "search_products".</summary>
        string Name { get; }

        /// <summary>Plain-English explanation for the model of WHEN and WHY to use this tool.</summary>
        string Description { get; }

        /// <summary>JSON Schema describing the arguments. Build it with Schema.Object(...).</summary>
        JsonObject ParametersSchema { get; }

        /// <summary>
        /// Runs the tool and returns text for the model to read.
        /// Start the text with "Error" when something went wrong, so the step is marked as an error.
        /// </summary>
        Task<string> ExecuteAsync(JsonElement arguments, AgentRequestContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A "marker" interface with no members. Put it on a tool that needs a signed-in user
    /// (e.g. "get my orders"). The registry then hides the tool from anonymous users AND refuses
    /// to run it for them, as a second line of defence in case the model calls it anyway.
    /// </summary>
    public interface IRequiresSignIn
    {
    }
}
