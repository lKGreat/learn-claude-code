namespace MiniClaudeCode.Avalonia.Services;

/// <summary>
/// Abstraction for Source Control Management providers.
/// Git is the primary provider, but this enables future support for SVN, Mercurial, etc.
/// Mirrors VS Code's ISCMProvider interface from scm.ts.
/// </summary>
public interface ISCMProvider
{
    /// <summary>Provider identifier (e.g., "git", "svn").</summary>
    string Id { get; }

    /// <summary>Display name (e.g., "Git", "Subversion").</summary>
    string Label { get; }

    /// <summary>Whether this provider is available (tool installed, etc.).</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>Whether the workspace uses this SCM system.</summary>
    Task<bool> IsInitializedAsync();

    /// <summary>Get the current branch/ref name.</summary>
    Task<string> GetCurrentBranchAsync();

    /// <summary>Get file status changes.</summary>
    Task<IReadOnlyList<SCMResourceGroup>> GetResourceGroupsAsync();

    /// <summary>Stage a file.</summary>
    Task StageAsync(string relativePath);

    /// <summary>Unstage a file.</summary>
    Task UnstageAsync(string relativePath);

    /// <summary>Discard changes in a file.</summary>
    Task DiscardAsync(string relativePath);

    /// <summary>Commit staged changes.</summary>
    Task<bool> CommitAsync(string message);

    /// <summary>Get diff for a file.</summary>
    Task<string> GetDiffAsync(string? relativePath = null);

    /// <summary>Set the working directory.</summary>
    void SetWorkDirectory(string workDir);
}

/// <summary>
/// A group of SCM resources (e.g., "Staged Changes", "Changes").
/// </summary>
public class SCMResourceGroup
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public IReadOnlyList<SCMResource> Resources { get; init; } = [];
}

/// <summary>
/// A single resource in an SCM resource group.
/// </summary>
public class SCMResource
{
    public string RelativePath { get; init; } = "";
    public string FullPath { get; init; } = "";
    public SCMResourceState State { get; init; }
}

public enum SCMResourceState
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted
}
