using System.Text;
using MiniClaudeCode.Abstractions.Agents;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Core.Agents;

/// <summary>
/// Coordinated multi-agent workflow orchestration.
/// Supports sequential pipeline, fan-out/fan-in, and supervisor patterns.
/// </summary>
public class AgentTeam
{
    private readonly SubAgentRunner _runner;
    private readonly ParallelAgentExecutor _parallelExecutor;
    private readonly IOutputSink _output;

    public AgentTeam(SubAgentRunner runner, ParallelAgentExecutor parallelExecutor, IOutputSink output)
    {
        _runner = runner;
        _parallelExecutor = parallelExecutor;
        _output = output;
    }

    /// <summary>
    /// Execute a team workflow based on the team definition.
    /// </summary>
    public async Task<AgentResult> ExecuteAsync(TeamDefinition team, string input, CancellationToken ct = default)
    {
        _output.WriteSystem($"Team '{team.Name}' starting with pattern: {team.Pattern}");

        return team.Pattern switch
        {
            TeamPattern.Sequential => await ExecuteSequentialAsync(team, input, ct),
            TeamPattern.FanOutFanIn => await ExecuteFanOutFanInAsync(team, input, ct),
            TeamPattern.Supervisor => await ExecuteSupervisorAsync(team, input, ct),
            _ => throw new ArgumentException($"Unknown team pattern: {team.Pattern}")
        };
    }

    /// <summary>
    /// Sequential pipeline: each role receives the previous role's output.
    /// Example: plan -> explore -> code -> review
    /// </summary>
    private async Task<AgentResult> ExecuteSequentialAsync(TeamDefinition team, string input, CancellationToken ct)
    {
        var currentInput = input;
        AgentResult? lastResult = null;

        for (int i = 0; i < team.Roles.Count; i++)
        {
            var role = team.Roles[i];
            _output.WriteSystem($"  Step {i + 1}/{team.Roles.Count}: {role.Name} ({role.AgentType})");

            var prompt = role.PromptTemplate
                .Replace("{input}", input)
                .Replace("{previous}", currentInput);

            var task = new AgentTask
            {
                Description = $"{team.Name}: {role.Name}",
                Prompt = prompt,
                AgentType = role.AgentType,
                ModelTier = role.ModelTier,
                ReadOnly = role.ReadOnly
            };

            lastResult = await _runner.RunAsync(task, ct);

            if (lastResult.IsError)
            {
                _output.WriteError($"  {role.Name} failed: {lastResult.ErrorMessage}");
                return lastResult;
            }

            currentInput = lastResult.Output;
        }

        return lastResult ?? new AgentResult
        {
            AgentId = "",
            Output = "No roles defined in team.",
            IsError = true
        };
    }

    /// <summary>
    /// Fan-out/fan-in: all roles run in parallel, results are merged.
    /// Example: multiple explore agents searching different areas -> merged summary
    /// </summary>
    private async Task<AgentResult> ExecuteFanOutFanInAsync(TeamDefinition team, string input, CancellationToken ct)
    {
        _output.WriteSystem($"  Fan-out: launching {team.Roles.Count} agents in parallel");

        var tasks = team.Roles.Select(role => new AgentTask
        {
            Description = $"{team.Name}: {role.Name}",
            Prompt = role.PromptTemplate.Replace("{input}", input),
            AgentType = role.AgentType,
            ModelTier = role.ModelTier,
            ReadOnly = role.ReadOnly
        });

        var results = await _parallelExecutor.RunParallelAsync(tasks, maxConcurrency: 4, ct);

        // Merge results
        var sb = new StringBuilder();
        sb.AppendLine("=== Merged Team Results ===\n");

        int totalToolCalls = 0;
        TimeSpan maxElapsed = TimeSpan.Zero;

        for (int i = 0; i < results.Length; i++)
        {
            var role = team.Roles[i];
            var result = results[i];

            sb.AppendLine($"--- {role.Name} ({role.AgentType}) ---");
            sb.AppendLine(result.Output);
            sb.AppendLine();

            totalToolCalls += result.ToolCallCount;
            if (result.Elapsed > maxElapsed)
                maxElapsed = result.Elapsed;
        }

        _output.WriteSystem($"  Fan-in: merged {results.Length} results");

        return new AgentResult
        {
            AgentId = "team_" + team.Name,
            Output = sb.ToString(),
            ToolCallCount = totalToolCalls,
            Elapsed = maxElapsed
        };
    }

    /// <summary>
    /// Supervisor pattern: first role acts as coordinator, delegating to others.
    /// The supervisor receives a description of available worker roles and their outputs.
    /// </summary>
    private async Task<AgentResult> ExecuteSupervisorAsync(TeamDefinition team, string input, CancellationToken ct)
    {
        if (team.Roles.Count < 2)
        {
            return new AgentResult
            {
                AgentId = "",
                Output = "Error: Supervisor pattern requires at least 2 roles (supervisor + workers).",
                IsError = true
            };
        }

        var supervisorRole = team.Roles[0];
        var workerRoles = team.Roles.Skip(1).ToList();

        // First, run all workers in parallel
        _output.WriteSystem($"  Supervisor: running {workerRoles.Count} workers");

        var workerTasks = workerRoles.Select(role => new AgentTask
        {
            Description = $"{team.Name}: {role.Name}",
            Prompt = role.PromptTemplate.Replace("{input}", input),
            AgentType = role.AgentType,
            ModelTier = role.ModelTier,
            ReadOnly = role.ReadOnly
        });

        var workerResults = await _parallelExecutor.RunParallelAsync(workerTasks, maxConcurrency: 4, ct);

        // Build context for supervisor
        var workerContext = new StringBuilder();
        workerContext.AppendLine("Worker results:\n");

        for (int i = 0; i < workerResults.Length; i++)
        {
            var role = workerRoles[i];
            workerContext.AppendLine($"--- {role.Name} ({role.AgentType}) ---");
            workerContext.AppendLine(workerResults[i].Output);
            workerContext.AppendLine();
        }

        // Run supervisor with worker results
        _output.WriteSystem($"  Supervisor: synthesizing results");

        var supervisorPrompt = supervisorRole.PromptTemplate
            .Replace("{input}", input)
            .Replace("{previous}", workerContext.ToString());

        var supervisorTask = new AgentTask
        {
            Description = $"{team.Name}: {supervisorRole.Name}",
            Prompt = supervisorPrompt,
            AgentType = supervisorRole.AgentType,
            ModelTier = supervisorRole.ModelTier,
            ReadOnly = supervisorRole.ReadOnly
        };

        return await _runner.RunAsync(supervisorTask, ct);
    }
}
