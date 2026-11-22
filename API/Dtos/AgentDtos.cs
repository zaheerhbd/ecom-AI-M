using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace API.Dtos
{
    /// <summary>Body of POST api/agent/chat.</summary>
    public class AgentChatRequestDto
    {
        /// <summary>The user's message.</summary>
        [Required]
        public string Message { get; set; }

        /// <summary>
        /// The conversation id from the previous answer, so the agent remembers the conversation.
        /// Leave it empty to start a new conversation.
        /// </summary>
        public string SessionId { get; set; }
    }

    /// <summary>Body of POST api/agent/evals.</summary>
    public class AgentEvalRequestDto
    {
        /// <summary>How many times each case runs. Empty = Agent:Evals:DefaultRepeat (3).</summary>
        [Range(1, 10)]
        public int? Repeat { get; set; }

        /// <summary>True = an LLM judge also scores each answer (costs extra).</summary>
        public bool UseJudge { get; set; }

        /// <summary>Run only these case ids, e.g. ["TC-001","TC-007"]. Empty = all cases.</summary>
        public List<string> CaseIds { get; set; }

        /// <summary>Run only cases of this category, e.g. "Safety". Empty = all categories.</summary>
        public string Category { get; set; }
    }
}
