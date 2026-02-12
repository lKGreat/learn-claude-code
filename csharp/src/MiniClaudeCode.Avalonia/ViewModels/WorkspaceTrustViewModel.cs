using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Workspace Trust dialog and restricted mode banner.
/// Mirrors VS Code's Workspace Trust feature for security.
/// </summary>
public partial class WorkspaceTrustViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;

    [ObservableProperty]
    private bool _isDialogVisible;

    [ObservableProperty]
    private bool _isBannerVisible;

    [ObservableProperty]
    private WorkspaceTrustState _trustState = WorkspaceTrustState.Unknown;

    [ObservableProperty]
    private string _workspacePath = "";

    public bool IsTrusted => TrustState == WorkspaceTrustState.Trusted;
    public bool IsRestricted => TrustState == WorkspaceTrustState.Untrusted;
    public bool IsUnknown => TrustState == WorkspaceTrustState.Unknown;

    public string TrustBadge => TrustState switch
    {
        WorkspaceTrustState.Trusted => "\u2705 Trusted",
        WorkspaceTrustState.Untrusted => "\u26A0 Restricted",
        _ => "\u2753 Unknown"
    };

    public WorkspaceTrustViewModel()
    {
        _workspaceService = new WorkspaceService();
    }

    public WorkspaceTrustViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    partial void OnTrustStateChanged(WorkspaceTrustState value)
    {
        OnPropertyChanged(nameof(IsTrusted));
        OnPropertyChanged(nameof(IsRestricted));
        OnPropertyChanged(nameof(IsUnknown));
        OnPropertyChanged(nameof(TrustBadge));
        IsBannerVisible = value == WorkspaceTrustState.Untrusted;
    }

    /// <summary>Show the workspace trust dialog.</summary>
    [RelayCommand]
    private void ShowDialog()
    {
        IsDialogVisible = true;
    }

    /// <summary>Trust the current workspace.</summary>
    [RelayCommand]
    private void TrustWorkspace()
    {
        TrustState = WorkspaceTrustState.Trusted;
        _workspaceService.SetTrustState(WorkspaceTrustState.Trusted);
        IsDialogVisible = false;
        IsBannerVisible = false;
    }

    /// <summary>Keep restricted mode.</summary>
    [RelayCommand]
    private void RestrictWorkspace()
    {
        TrustState = WorkspaceTrustState.Untrusted;
        _workspaceService.SetTrustState(WorkspaceTrustState.Untrusted);
        IsDialogVisible = false;
    }

    /// <summary>Dismiss the restricted mode banner.</summary>
    [RelayCommand]
    private void DismissBanner()
    {
        IsBannerVisible = false;
    }

    /// <summary>Set workspace and check trust.</summary>
    public void CheckWorkspaceTrust(string workspacePath)
    {
        WorkspacePath = workspacePath;
        TrustState = _workspaceService.TrustState;

        if (TrustState == WorkspaceTrustState.Unknown)
        {
            // First time opening - show trust dialog
            IsDialogVisible = true;
        }
    }
}
