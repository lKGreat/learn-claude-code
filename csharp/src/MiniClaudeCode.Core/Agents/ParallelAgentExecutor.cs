using MiniClaudeCode.Abstractions.Agents;

namespace MiniClaudeCode.Core.Agents;

/// <summary>
/// Executes multiple sub-agent tasks concurrently with configurable max concurrency.
/// </summary>
public class ParallelAgentExecutor
{
    private readonly SubAgentRunner _runner;

    public ParallelAgentExecutor(SubAgentRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// Run multiple agent tasks in parallel with bounded concurrency.
    /// </summary>
    /// <param name="tasks">The tasks to execute.</param>
    /// <param name="maxConcurrency">Maximum number of agents running simultaneously.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of results in the same order as input tasks.</returns>
    public async Task<AgentResult[]> RunParallelAsync(
        IEnumerable<AgentTask> tasks,
        int maxConcurrency = 4,
        CancellationToken ct = default)
    {
        var taskList = tasks.ToList();
        if (taskList.Count == 0)
            return [];

        // For single task, just run directly
        if (taskList.Count == 1)
        {
            var singleResult = await _runner.RunAsync(taskList[0], ct);
            return [singleResult];
        }

        // Use SemaphoreSlim for bounded concurrency
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var resultTasks = new Task<AgentResult>[taskList.Count];

        for (int i = 0; i < taskList.Count; i++)
        {
            var task = taskList[i];
            resultTasks[i] = RunWithSemaphoreAsync(semaphore, task, ct);
        }

        return await Task.WhenAll(resultTasks);
    }

    private async Task<AgentResult> RunWithSemaphoreAsync(
        SemaphoreSlim semaphore,
        AgentTask task,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            return await _runner.RunAsync(task, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
