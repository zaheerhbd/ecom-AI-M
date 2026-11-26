using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Llm;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentChatMessage = Agent.Llm.ChatMessage;
using AgentChatRole = Agent.Llm.ChatRole;
using FrameworkChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Framework
{
    /// <summary>
    /// THE AGENT FRAMEWORK ENGINE (Phase 7). Used when Agent:Engine = "AgentFramework".
    ///
    /// AgentRunner still does everything around the loop (guardrails, memory, run record...). This
    /// class only replaces the loop in the middle:
    ///
    ///     our history --MessageMapper--> Microsoft messages
    ///         |
    ///     ChatClientAgent (Microsoft)          the agent: holds the tools and options
    ///         |
    ///     FunctionInvokingChatClient           Microsoft's loop: model asks for a tool -> run it -> ask again
    ///         |                                (at most MaxSteps model calls in total)
    ///     LlmChatClient (ours)                 one model call through OUR ILlmClient; records the step
    ///         |
    ///     ShopToolFunction (ours)              each of our 7 tools, run through OUR ToolRegistry
    ///         |
    ///     new Microsoft messages --MessageMapper--> added to our history
    ///
    /// Compare with AgentRunner.RunAgentLoopAsync: same job, but Microsoft decides the order of work.
    /// </summary>
    public sealed class FrameworkAgentLoop : IAgentLoop
    {
        private readonly ILoggerFactory _loggerFactory;

        public FrameworkAgentLoop(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public string EngineName
        {
            get { return AgentEngines.AgentFramework; }
        }

        public async Task<string> RunAsync(AgentLoopRun run, CancellationToken cancellationToken)
        {
            FrameworkTurn turn = new FrameworkTurn();

            // 1. TOOLS: our tools for this user, wrapped as Microsoft AIFunctions.
            List<AITool> tools = new List<AITool>();
            foreach (ToolDefinition definition in run.Tools)
                tools.Add(new ShopToolFunction(definition, run, turn));

            // 2. MODEL + LOOP: our model client, wrapped in Microsoft's function-calling loop.
            //    MaxSteps = how many model calls one answer may make (same meaning as in the custom loop).
            //    The framework counts ROUNDS OF TOOL USE, then makes one more call with the tools removed so
            //    the model must answer. So MaxSteps - 1 rounds + that final call = MaxSteps calls (min 2).
            //    Difference from the custom loop: on that last call the model gives its best answer,
            //    where the custom loop apologises. Both record stop reason "max-steps".
            //    Tools run one after another, never in parallel: they share one database context.
            LlmChatClient modelClient = new LlmChatClient(run, turn);

            int toolRounds = run.Options.MaxSteps - 1;
            if (toolRounds < 1)
                toolRounds = 1; // the framework needs at least 1

            FunctionInvokingChatClient chatClient = new FunctionInvokingChatClient(modelClient, _loggerFactory, null);
            chatClient.MaximumIterationsPerRequest = toolRounds;
            chatClient.AllowConcurrentInvocation = false;
            chatClient.IncludeDetailedErrors = false;

            // 3. THE AGENT. UseProvidedChatClientAsIs: we already added the function-calling loop above,
            //    with our limits, so the agent must not add a second one.
            ChatClientAgentOptions agentOptions = new ChatClientAgentOptions();
            agentOptions.Name = run.Options.AppName;
            agentOptions.UseProvidedChatClientAsIs = true;
            agentOptions.ChatOptions = new ChatOptions();
            agentOptions.ChatOptions.Tools = tools;
            if (run.Options.Temperature.HasValue)
                agentOptions.ChatOptions.Temperature = (float)run.Options.Temperature.Value;

            ChatClientAgent agent = new ChatClientAgent(chatClient, agentOptions, _loggerFactory, null);

            // 4. RUN. Memory stays in OUR store (one format for both engines), so we pass the whole
            //    conversation in and use no framework session.
            List<FrameworkChatMessage> conversation = MessageMapper.ToFramework(run.History);
            AgentSession noSession = null;
            ChatClientAgentRunOptions noExtraOptions = null;
            AgentResponse response = await agent.RunAsync(conversation, noSession, noExtraOptions, cancellationToken);

            // 5. Add the new messages (tool requests, tool results, answer) to our history.
            List<AgentChatMessage> added = MessageMapper.ToAgent(response.Messages);
            run.History.AddRange(added);

            // 6. SAFETY NET: if the reply still ends with tool requests the framework did not run (it hands
            //    those back to the caller), answer each one (a tool request without a result breaks the
            //    next call) and stop politely, exactly like the custom loop.
            if (CloseUnansweredToolCalls(run.History))
            {
                run.Result.StopReason = AgentStopReasons.MaxSteps;
                return MaxStepsAnswer;
            }

            // The final answer is the last assistant message without tool requests.
            for (int i = added.Count - 1; i >= 0; i--)
            {
                AgentChatMessage message = added[i];
                bool isAssistant = message.Role == AgentChatRole.Assistant;
                bool asksForTools = message.ToolCalls != null && message.ToolCalls.Count > 0;

                if (isAssistant && !asksForTools)
                {
                    if (message.Content == null)
                        return "";
                    return message.Content;
                }
            }

            return "";
        }

        /// <summary>The same answer the custom loop gives when it runs out of steps.</summary>
        internal const string MaxStepsAnswer = "Sorry, I couldn't finish that. Could you rephrase or break it into smaller questions?";

        /// <summary>
        /// If the LAST message is the assistant asking for tools, adds a "not run" result for each request
        /// and returns true. Otherwise changes nothing and returns false.
        /// </summary>
        internal static bool CloseUnansweredToolCalls(List<AgentChatMessage> history)
        {
            if (history.Count == 0)
                return false;

            AgentChatMessage last = history[history.Count - 1];
            if (last.Role != AgentChatRole.Assistant)
                return false;
            if (last.ToolCalls == null || last.ToolCalls.Count == 0)
                return false;

            foreach (ToolCall call in last.ToolCalls)
                history.Add(AgentChatMessage.ToolResult(call.Id, "Not run: the step limit for one answer was reached."));

            return true;
        }
    }
}
