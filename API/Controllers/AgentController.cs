using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent;
using Agent.Evals;
using Agent.Llm;
using Agent.Runs;
using Agent.Tools;
using API.Dtos;
using API.Errors;
using API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace API.Controllers
{
    /// <summary>
    /// The AI shopping agent (Phase 3).
    ///
    ///   POST api/agent/chat        talk to the agent (anyone; signed-in users can also ask about their orders)
    ///   GET  api/agent/info        what the agent is: model, tools, limits
    ///   GET  api/agent/runs        saved runs, newest first          (admin in production)
    ///   GET  api/agent/runs/{id}   one run with every step          (admin in production)
    ///   GET  api/agent/stats       totals across runs               (admin in production)
    ///   POST api/agent/evals       start an eval run (background)   (admin in production)
    ///   GET  api/agent/evals       eval runs, newest first          (admin in production)
    ///   GET  api/agent/evals/{id}  one eval run with case results   (admin in production)
    /// </summary>
    public class AgentController : BaseApiController
    {
        private readonly AgentRunner _runner;
        private readonly AgentRequestContext _context;
        private readonly IAgentRunStore _runs;
        private readonly IEvalRunStore _evals;
        private readonly EvalRunner _evalRunner;
        private readonly ContentPathResolver _paths;
        private readonly IEnumerable<ITool> _tools;
        private readonly ILlmClient _llm;
        private readonly AgentOptions _options;
        private readonly IConfiguration _config;

        public AgentController(AgentRunner runner, AgentRequestContext context, IAgentRunStore runs,
            IEvalRunStore evals, EvalRunner evalRunner, ContentPathResolver paths, IEnumerable<ITool> tools,
            ILlmClient llm, IOptions<AgentOptions> options, IConfiguration config)
        {
            _runner = runner;
            _context = context;
            _runs = runs;
            _evals = evals;
            _evalRunner = evalRunner;
            _paths = paths;
            _tools = tools;
            _llm = llm;
            _options = options.Value;
            _config = config;
        }

        // ------------------------------------------------------------------ chat

        /// <summary>
        /// Sends one message to the agent and returns its answer, with every step it took.
        /// No sign-in needed. If a valid token is sent, the agent knows who you are and can show
        /// YOUR orders. The user's identity always comes from the token, never from the message.
        /// </summary>
        [HttpPost("chat")]
        [AllowAnonymous]
        [EnableRateLimiting(AgentServiceExtensions.ChatRateLimitPolicy)]
        public async Task<ActionResult<AgentResult>> Chat(AgentChatRequestDto request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new ApiResponse(400, "Message is required."));

            // Fill the request context from the JWT (null when anonymous). Tools read the user from here.
            _context.UserEmail = User.RetrieveEmailFromPrincipal();
            _context.Channel = AgentRequestContext.ChatChannel;

            AgentResult result = await _runner.RunAsync(request.SessionId, request.Message, cancellationToken);
            return Ok(result);
        }

        /// <summary>What the agent is: name, model, tools and limits. No secrets.</summary>
        [HttpGet("info")]
        [AllowAnonymous]
        public ActionResult Info()
        {
            return Ok(new
            {
                appName = _options.AppName,
                model = AgentEngines.ModelLabel(_llm.Model, _options.Engine), // e.g. "gemini-3.5-flash (Agent Framework)"
                maxSteps = _options.MaxSteps,
                maxHistoryMessages = _options.MaxHistoryMessages,
                maxInputChars = _options.MaxInputChars,
                rateLimitPerMinute = _config.GetValue("Agent:RateLimit:PermitPerMinute", 10),
                runRetentionDays = _config.GetValue("Agent:RunRetentionDays", 30),
                tools = _tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    requiresSignIn = t is IRequiresSignIn,
                    parameters = t.ParametersSchema
                })
            });
        }

        // ------------------------------------------------------------------ runs

        [HttpGet("runs")]
        [Authorize(Policy = AgentServiceExtensions.AdminPolicy)]
        public async Task<ActionResult<AgentRunPage>> GetRuns([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _runs.ListAsync(page, pageSize, cancellationToken));
        }

        [HttpGet("runs/{id:guid}")]
        [Authorize(Policy = AgentServiceExtensions.AdminPolicy)]
        public async Task<ActionResult<AgentRunRecord>> GetRun(Guid id, CancellationToken cancellationToken)
        {
            AgentRunRecord run = await _runs.GetAsync(id, cancellationToken);
            if (run == null)
                return NotFound(new ApiResponse(404));
            return Ok(run);
        }

        [HttpGet("stats")]
        [Authorize(Policy = AgentServiceExtensions.AdminPolicy)]
        public async Task<ActionResult<AgentRunStats>> GetStats(CancellationToken cancellationToken)
        {
            return Ok(await _runs.GetStatsAsync(cancellationToken));
        }

        // ------------------------------------------------------------------ evals

        /// <summary>
        /// Starts an eval run in the background and returns at once (status "Running").
        /// Follow it with GET api/agent/evals/{id}. Only one eval run at a time.
        /// Each case calls the real model, so a full run costs money: start small (caseIds or category).
        /// </summary>
        [HttpPost("evals")]
        [Authorize(Policy = AgentServiceExtensions.AdminPolicy)]
        public async Task<ActionResult<EvalRunRecord>> StartEval(AgentEvalRequestDto request)
        {
            List<EvalCase> cases;
            try
            {
                cases = EvalCaseLoader.LoadFile(_paths.Resolve(_options.Evals.CasesPath));
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse(400, "Could not load the eval cases: " + ex.Message));
            }

            request = request ?? new AgentEvalRequestDto();
            if (request.CaseIds != null && request.CaseIds.Count > 0)
                cases = cases.Where(c => request.CaseIds.Contains(c.Id, StringComparer.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(request.Category))
                cases = cases.Where(c => string.Equals(c.Category, request.Category, StringComparison.OrdinalIgnoreCase)).ToList();

            if (cases.Count == 0)
                return BadRequest(new ApiResponse(400, "No eval cases match the filter."));

            try
            {
                EvalRunRecord run = await _evalRunner.StartInBackgroundAsync(new EvalRunRequest
                {
                    Cases = cases,
                    Repeat = request.Repeat,
                    UseJudge = request.UseJudge
                });
                return Accepted(run);
            }
            catch (InvalidOperationException)
            {
                return Conflict(new ApiResponse(409, "An eval run is already going. Wait for it to finish."));
            }
        }

        [HttpGet("evals")]
        [Authorize(Policy = AgentServiceExtensions.AdminPolicy)]
        public async Task<ActionResult<EvalRunPage>> GetEvals([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            return Ok(await _evals.ListRunsAsync(page, pageSize, cancellationToken));
        }

        /// <summary>One eval run and the result of every case so far.</summary>
        [HttpGet("evals/{id:guid}")]
        [Authorize(Policy = AgentServiceExtensions.AdminPolicy)]
        public async Task<ActionResult> GetEval(Guid id, CancellationToken cancellationToken)
        {
            EvalRunRecord run = await _evals.GetRunAsync(id, cancellationToken);
            if (run == null)
                return NotFound(new ApiResponse(404));

            List<EvalCaseResultRecord> results = await _evals.GetCaseResultsAsync(id, cancellationToken);
            return Ok(new { run, results });
        }
    }
}
