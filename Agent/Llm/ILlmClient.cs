using System.Threading;
using System.Threading.Tasks;

namespace Agent.Llm
{
    /// <summary>
    /// The ONE thing the agent needs from a language model: "here are the messages, give me a reply".
    ///
    /// Because the agent only knows about this interface, we can swap providers without touching
    /// the agent: a real model (OpenAI / Azure OpenAI / Ollama) or a fake one for free offline testing.
    /// This is the classic Dependency Inversion idea from Clean Architecture.
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>Friendly name, shown in logs, e.g. "OpenAI:gpt-5.4-mini".</summary>
        string Name { get; }

        /// <summary>The model name on its own, e.g. "gpt-5.4-mini". Saved with every run.</summary>
        string Model { get; }

        /// <summary>Sends the conversation (and the list of tools) to the model and returns its reply.</summary>
        Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default(CancellationToken));
    }
}
