using MiniClaudeCode.Abstractions.Agents;

namespace MiniClaudeCode.Rpc;

/// <summary>
/// RPC service interface for remote agent communication.
/// This mirrors the Core engine's capabilities and will be implemented
/// as a gRPC or SignalR service in a future phase.
///
/// Design intent:
/// - WinForms/Web clients call this interface via RPC
/// - Server-side implementation wraps Core's EngineContext
/// - Streaming responses use IAsyncEnumerable
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Send a user message and get the agent's response.
    /// </summary>
    /// <param name="message">User's message text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's response text.</returns>
    Task<string> SendMessageAsync(string message, CancellationToken ct = default);

    /// <summary>
    /// Send a user message and stream the response.
    /// </summary>
    IAsyncEnumerable<AgentStreamChunk> StreamMessageAsync(string message, CancellationToken ct = default);

    /// <summary>
    /// Spawn a sub-agent task.
    /// </summary>
    Task<AgentResult> SpawnAgentAsync(AgentTask task, CancellationToken ct = default);

    /// <summary>
    /// Get the status of all running agents.
    /// </summary>
    Task<IReadOnlyList<AgentInstanceInfo>> GetRunningAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Reset the conversation (start new thread).
    /// </summary>
    Task ResetConversationAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the current todo list.
    /// </summary>
    Task<string> GetTodosAsync(CancellationToken ct = default);
}

/// <summary>
/// A chunk of streaming agent response.
/// </summary>
public record AgentStreamChunk
{
    /// <summary>Type of chunk: Text, ToolCallStart, ToolCallEnd, Done.</summary>
    public required AgentStreamChunkType Type { get; init; }

    /// <summary>Text content (for Text chunks).</summary>
    public string? Content { get; init; }

    /// <summary>Tool call info (for ToolCallStart/End chunks).</summary>
    public ToolCallEvent? ToolCall { get; init; }
}

/// <summary>
/// Types of streaming chunks.
/// </summary>
public enum AgentStreamChunkType
{
    /// <summary>Text content from the agent.</summary>
    Text,

    /// <summary>A tool call has started.</summary>
    ToolCallStart,

    /// <summary>A tool call has completed.</summary>
    ToolCallEnd,

    /// <summary>The agent has finished responding.</summary>
    Done,

    /// <summary>An error occurred.</summary>
    Error
}
