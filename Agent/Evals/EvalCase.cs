using System.Collections.Generic;
using System.Text.Json;

namespace Agent.Evals
{
    /// <summary>
    /// One test scenario (an "eval case"): an input and what a GOOD answer must look like.
    ///
    /// We rarely check exact wording, because real models phrase things differently each time.
    /// Instead we check BEHAVIOUR: did it call the right tool with the right arguments, include the
    /// key fact, refuse when it should, avoid leaking data?
    ///
    /// Example (cases.json):
    ///   { "id": "MT-001", "category": "Multi-tool",
    ///     "input": "What's your cheapest hat and how fast can it arrive?",
    ///     "expectedTools": [ { "name": "search_products", "args": { "type": "Hats", "sort": "priceAsc" } },
    ///                        { "name": "get_delivery_options" } ] }
    /// </summary>
    public sealed class EvalCase
    {
        /// <summary>Sets the defaults. Values from cases.json replace them.</summary>
        public EvalCase()
        {
            Id = "";
            Category = "general";
            Input = "";
            MustContain = new List<string>();
            MustNotContain = new List<string>();
        }

        /// <summary>Short unique name, e.g. "TC-004".</summary>
        public string Id { get; set; }

        /// <summary>Group name used in the summary, e.g. "Safety".</summary>
        public string Category { get; set; }

        /// <summary>The message sent to the agent.</summary>
        public string Input { get; set; }

        /// <summary>
        /// The tools that must be called, each with arguments that must be included.
        /// Null (left out) = don't check tools. An empty list = no tool should be called.
        /// </summary>
        public List<ExpectedToolCall> ExpectedTools { get; set; }

        /// <summary>The answer must include each of these (ignoring upper/lower case).</summary>
        public List<string> MustContain { get; set; }

        /// <summary>The answer must NOT include any of these (ignoring upper/lower case).</summary>
        public List<string> MustNotContain { get; set; }

        /// <summary>True = a guardrail must block it. False (the default) = it must NOT be blocked.</summary>
        public bool ShouldBlock { get; set; }

        /// <summary>The question is ambiguous: the answer must ask a question back instead of guessing.</summary>
        public bool ShouldClarify { get; set; }

        /// <summary>The answer must say the shop doesn't offer this, or that the agent doesn't know.</summary>
        public bool ShouldDecline { get; set; }

        /// <summary>Run the case as this signed-in user (to test order tools). Null = anonymous.</summary>
        public string SignedInAs { get; set; }

        /// <summary>How many times to run this case. Null = use the run's setting.</summary>
        public int? Repeat { get; set; }

        /// <summary>
        /// MULTI-TURN: messages sent one after another in ONE conversation, instead of Input.
        /// The checks apply to the LAST answer. Tests memory, e.g. "Where's my last order?" -> "When will it arrive?".
        /// </summary>
        public List<string> Turns { get; set; }

        /// <summary>The answer may run at most this many tools (repeats that were not run don't count). Null = no check.</summary>
        public int? MaxToolCalls { get; set; }

        /// <summary>True when the case has more than one message.</summary>
        public bool IsMultiTurn
        {
            get { return Turns != null && Turns.Count > 0; }
        }

        /// <summary>The messages to send, in order: Turns, or just Input.</summary>
        public List<string> Messages()
        {
            return IsMultiTurn ? new List<string>(Turns) : new List<string> { Input };
        }
    }

    /// <summary>A tool the agent must call, and arguments the call must include.</summary>
    public sealed class ExpectedToolCall
    {
        /// <summary>Starts with no arguments to check.</summary>
        public ExpectedToolCall()
        {
            Args = new Dictionary<string, JsonElement>();
        }

        /// <summary>The tool's name, e.g. "search_products".</summary>
        public string Name { get; set; }

        /// <summary>
        /// Arguments the call must include. Other arguments are allowed. Text is compared ignoring
        /// upper/lower case, and numbers as numbers (so 40, 40.0 and "40" all match).
        /// </summary>
        public Dictionary<string, JsonElement> Args { get; set; }
    }

    /// <summary>The result of one check, e.g. "called search_products: pass".</summary>
    public sealed class EvalCheck
    {
        /// <summary>Empty constructor, needed when results are read back from JSON.</summary>
        public EvalCheck()
        {
        }

        /// <summary>Creates a check result.</summary>
        public EvalCheck(string name, bool passed, string detail)
        {
            Name = name;
            Passed = passed;
            Detail = detail ?? "";
        }

        /// <summary>What was checked.</summary>
        public string Name { get; set; }

        /// <summary>True if the check passed.</summary>
        public bool Passed { get; set; }

        /// <summary>Extra information, mostly useful when the check failed.</summary>
        public string Detail { get; set; }
    }
}
