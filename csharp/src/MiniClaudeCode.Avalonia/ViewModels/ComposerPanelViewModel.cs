using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Core.AI;
using System.Diagnostics;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the multi-file composer panel.
/// Allows users to describe multi-file changes, generate plans, and execute them.
/// </summary>
public partial class ComposerPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isGeneratingPlan;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private ComposerPlanDisplay? _plan;

    [ObservableProperty]
    private ComposerResultDisplay? _result;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusMessage = "Describe the changes you want to make...";

    /// <summary>
    /// Whether a plan has been generated.
    /// </summary>
    public bool HasPlan => Plan != null;

    /// <summary>
    /// Whether execution has completed and a result is available.
    /// </summary>
    public bool HasResult => Result != null;

    private ComposerService? _composerService;
    private CancellationTokenSource? _currentCts;

    /// <summary>
    /// Set the composer service instance.
    /// </summary>
    public void SetComposerService(ComposerService service)
    {
        _composerService = service;
    }

    /// <summary>
    /// Show the composer panel.
    /// </summary>
    public void Show()
    {
        IsVisible = true;
        ClearCommand.Execute(null);
    }

    /// <summary>
    /// Hide the composer panel.
    /// </summary>
    public void Hide()
    {
        IsVisible = false;
        _currentCts?.Cancel();
    }

    /// <summary>
    /// Generate an execution plan from the user's description.
    /// </summary>
    [RelayCommand]
    private async Task GeneratePlanAsync()
    {
        if (_composerService == null)
        {
            ErrorMessage = "Composer service not initialized.";
            return;
        }

        var description = Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            ErrorMessage = "Please describe the changes you want to make.";
            return;
        }

        IsGeneratingPlan = true;
        ErrorMessage = null;
        StatusMessage = "Analyzing request and generating plan...";
        Plan = null;
        Result = null;

        _currentCts?.Cancel();
        _currentCts = new CancellationTokenSource();

        try
        {
            var request = new ComposerRequest
            {
                Description = description,
                CodebaseContext = "" // Could add file tree or context here
            };

            var plan = await _composerService.GeneratePlanAsync(request, _currentCts.Token);

            if (plan == null)
            {
                ErrorMessage = "Failed to generate plan. Please try again.";
                StatusMessage = "Plan generation failed.";
                return;
            }

            // Convert to display model
            var displayPlan = new ComposerPlanDisplay
            {
                Summary = plan.Summary,
                Reasoning = plan.Reasoning
            };

            foreach (var step in plan.Steps)
            {
                displayPlan.Steps.Add(new ComposerStepDisplay
                {
                    StepNumber = step.StepNumber,
                    File = step.File,
                    Action = step.Action.ToLowerInvariant(),
                    Description = step.Description
                });
            }

            Plan = displayPlan;
            StatusMessage = $"Plan generated with {plan.Steps.Count} steps. Review and execute.";

            OnPropertyChanged(nameof(HasPlan));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Plan generation cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            StatusMessage = "Plan generation failed.";
        }
        finally
        {
            IsGeneratingPlan = false;
        }
    }

    /// <summary>
    /// Execute the generated plan.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasPlan))]
    private async Task ExecutePlanAsync()
    {
        if (_composerService == null || Plan == null)
        {
            ErrorMessage = "No plan to execute.";
            return;
        }

        IsExecuting = true;
        ErrorMessage = null;
        StatusMessage = "Executing plan...";
        Result = null;

        _currentCts?.Cancel();
        _currentCts = new CancellationTokenSource();

        try
        {
            // Convert display model back to execution model
            var executionPlan = new ComposerPlan
            {
                Summary = Plan.Summary,
                Reasoning = Plan.Reasoning,
                Steps = Plan.Steps.Select(s => new ComposerStep
                {
                    StepNumber = s.StepNumber,
                    File = s.File,
                    Action = s.Action,
                    Description = s.Description,
                    Changes = [], // Assume the plan has changes embedded
                    Dependencies = []
                }).ToList()
            };

            var sw = Stopwatch.StartNew();
            var result = await _composerService.ExecutePlanAsync(executionPlan, _currentCts.Token);
            sw.Stop();

            // Update step results in real-time
            foreach (var stepResult in result.StepResults)
            {
                var displayStep = Plan.Steps.FirstOrDefault(s => s.StepNumber == stepResult.StepNumber);
                if (displayStep != null)
                {
                    displayStep.IsCompleted = stepResult.Success;
                    displayStep.IsFailed = !stepResult.Success;
                    displayStep.ErrorMessage = stepResult.ErrorMessage;
                }
            }

            var successCount = result.StepResults.Count(r => r.Success);
            var failedCount = result.StepResults.Count(r => !r.Success);

            Result = new ComposerResultDisplay
            {
                Status = result.Status.ToString(),
                Duration = FormatDuration(sw.Elapsed),
                SuccessCount = successCount,
                FailedCount = failedCount
            };

            StatusMessage = result.Status switch
            {
                ComposerStatus.Completed => $"Execution completed successfully in {FormatDuration(sw.Elapsed)}.",
                ComposerStatus.Failed => $"Execution failed. {successCount} succeeded, {failedCount} failed.",
                ComposerStatus.Cancelled => "Execution cancelled.",
                _ => "Execution completed."
            };

            OnPropertyChanged(nameof(HasResult));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Execution cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Execution error: {ex.Message}";
            StatusMessage = "Execution failed.";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Refine the plan based on user feedback.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasPlan))]
    private async Task RefinePlanAsync()
    {
        if (_composerService == null || Plan == null)
        {
            ErrorMessage = "No plan to refine.";
            return;
        }

        // For now, just regenerate with the original description
        // In a full implementation, we'd show a feedback input dialog
        StatusMessage = "Refining plan...";
        await GeneratePlanAsync();
    }

    /// <summary>
    /// Close the composer panel.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        Hide();
    }

    /// <summary>
    /// Clear the composer state and start over.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Description = string.Empty;
        Plan = null;
        Result = null;
        ErrorMessage = null;
        StatusMessage = "Describe the changes you want to make...";
        _currentCts?.Cancel();
        OnPropertyChanged(nameof(HasPlan));
        OnPropertyChanged(nameof(HasResult));
    }

    private static string FormatDuration(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 1)
            return $"{elapsed.TotalMilliseconds:F0}ms";
        if (elapsed.TotalMinutes < 1)
            return $"{elapsed.TotalSeconds:F1}s";
        return $"{elapsed.TotalMinutes:F1}m";
    }

    partial void OnPlanChanged(ComposerPlanDisplay? value)
    {
        OnPropertyChanged(nameof(HasPlan));
        ExecutePlanCommand.NotifyCanExecuteChanged();
        RefinePlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnResultChanged(ComposerResultDisplay? value)
    {
        OnPropertyChanged(nameof(HasResult));
    }
}

/// <summary>
/// Display model for a composer plan.
/// </summary>
public partial class ComposerPlanDisplay : ObservableObject
{
    public required string Summary { get; init; }
    public required string Reasoning { get; init; }
    public ObservableCollection<ComposerStepDisplay> Steps { get; } = [];
}

/// <summary>
/// Display model for a single step in the plan.
/// </summary>
public partial class ComposerStepDisplay : ObservableObject
{
    public required int StepNumber { get; init; }
    public required string File { get; init; }
    public required string Action { get; init; } // create, modify, delete
    public required string Description { get; init; }

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isFailed;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Icon representing the action type.
    /// </summary>
    public string ActionIcon => Action switch
    {
        "create" => "+",
        "modify" => "~",
        "delete" => "-",
        _ => "?"
    };

    /// <summary>
    /// Color for the action icon.
    /// </summary>
    public string ActionColor => Action switch
    {
        "create" => "#A6E3A1",
        "modify" => "#89B4FA",
        "delete" => "#F38BA8",
        _ => "#6C7086"
    };

    /// <summary>
    /// Status icon shown during/after execution.
    /// </summary>
    public string StatusIcon
    {
        get
        {
            if (IsCompleted) return "✓";
            if (IsFailed) return "✗";
            return "";
        }
    }

    /// <summary>
    /// Status color for the status icon.
    /// </summary>
    public string StatusColor
    {
        get
        {
            if (IsCompleted) return "#A6E3A1";
            if (IsFailed) return "#F38BA8";
            return "#6C7086";
        }
    }

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnIsFailedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
    }
}

/// <summary>
/// Display model for execution result summary.
/// </summary>
public class ComposerResultDisplay
{
    public required string Status { get; init; }
    public required string Duration { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailedCount { get; init; }
}
