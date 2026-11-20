using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Agent.Guardrails
{
    /// <summary>What happened when the text went through every guardrail of one stage.</summary>
    public sealed class GuardrailPipelineResult
    {
        /// <summary>Creates the combined result of a pipeline run.</summary>
        public GuardrailPipelineResult(bool allowed, string text, string blockedBy, string reason, IReadOnlyList<GuardrailCheck> checks)
        {
            Allowed = allowed;
            Text = text;
            BlockedBy = blockedBy;
            Reason = reason;
            Checks = checks;
        }

        /// <summary>True if every guardrail let the text through.</summary>
        public bool Allowed { get; }

        /// <summary>The text after all the cleaning steps.</summary>
        public string Text { get; }

        /// <summary>Name of the guardrail that blocked the text (null if none did).</summary>
        public string BlockedBy { get; }

        /// <summary>Why it was blocked (null if it was not).</summary>
        public string Reason { get; }

        /// <summary>One record per guardrail in this stage, including the ones that were skipped.</summary>
        public IReadOnlyList<GuardrailCheck> Checks { get; }
    }

    /// <summary>
    /// Runs guardrails one after another, like airport security lanes.
    /// Each guardrail gets the (possibly cleaned) text from the previous one.
    /// The first one that blocks stops the pipeline; the rest are recorded as Skipped.
    /// </summary>
    public sealed class GuardrailPipeline
    {
        private const string SkippedAfterBlock = "An earlier check already blocked the text.";

        private readonly List<IGuardrail> _guardrails;

        /// <summary>Creates a pipeline for one stage ("input" or "output"). The guardrails run in the order given.</summary>
        public GuardrailPipeline(string stage, IEnumerable<IGuardrail> guardrails)
        {
            Stage = stage;
            _guardrails = new List<IGuardrail>(guardrails);
        }

        /// <summary>"input" or "output".</summary>
        public string Stage { get; }

        /// <summary>Sends the text through every guardrail in turn.</summary>
        public GuardrailPipelineResult Run(string text)
        {
            List<GuardrailCheck> checks = new List<GuardrailCheck>();
            string current = text ?? "";

            for (int i = 0; i < _guardrails.Count; i++)
            {
                IGuardrail guardrail = _guardrails[i];

                // Time each check, so the UI can show that guardrails are cheap (usually well under 1 ms).
                Stopwatch timer = Stopwatch.StartNew();
                GuardrailResult result = guardrail.Check(current);
                double durationMs = Math.Round(timer.Elapsed.TotalMilliseconds, 3);

                // A block ends the pipeline straight away: later guardrails never see the text.
                if (!result.Allowed)
                {
                    checks.Add(new GuardrailCheck(Stage, guardrail.Name, GuardrailOutcome.Blocked, result.Reason, durationMs));
                    AddSkipped(checks, i + 1, SkippedAfterBlock);
                    return new GuardrailPipelineResult(false, current, guardrail.Name, result.Reason, checks);
                }

                // Note whether this guardrail changed the text, then pass the new text along.
                GuardrailOutcome outcome = result.Text == current ? GuardrailOutcome.Passed : GuardrailOutcome.Modified;
                checks.Add(new GuardrailCheck(Stage, guardrail.Name, outcome, null, durationMs));
                current = result.Text;
            }

            return new GuardrailPipelineResult(true, current, null, null, checks);
        }

        /// <summary>
        /// Records every guardrail of this stage as Skipped, without running them.
        /// Used for the output stage when the input was blocked, so the UI still shows the full picture.
        /// </summary>
        public List<GuardrailCheck> Skip(string reason)
        {
            List<GuardrailCheck> checks = new List<GuardrailCheck>();
            AddSkipped(checks, 0, reason);
            return checks;
        }

        /// <summary>Adds a Skipped record for every guardrail from the given position onwards.</summary>
        private void AddSkipped(List<GuardrailCheck> checks, int fromIndex, string reason)
        {
            for (int i = fromIndex; i < _guardrails.Count; i++)
                checks.Add(new GuardrailCheck(Stage, _guardrails[i].Name, GuardrailOutcome.Skipped, reason, 0));
        }
    }

    /// <summary>The two pipelines the agent uses: one for user input, one for model output.</summary>
    public sealed class AgentGuardrails
    {
        /// <summary>Creates the pair of pipelines.</summary>
        public AgentGuardrails(GuardrailPipeline input, GuardrailPipeline output)
        {
            Input = input;
            Output = output;
        }

        /// <summary>Runs on the user's message before the model sees it.</summary>
        public GuardrailPipeline Input { get; }

        /// <summary>Runs on the model's answer before the user sees it.</summary>
        public GuardrailPipeline Output { get; }
    }
}
