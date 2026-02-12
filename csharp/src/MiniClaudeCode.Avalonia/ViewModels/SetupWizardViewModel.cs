using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the setup wizard dialog.
/// </summary>
public partial class SetupWizardViewModel : ObservableObject
{
    private readonly TaskCompletionSource<SetupResult?> _tcs = new();

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private int _totalSteps = 3;

    [ObservableProperty]
    private string _selectedProvider = "OpenAI";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelId = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isVisible;

    public ObservableCollection<string> AvailableProviders { get; } = ["OpenAI", "DeepSeek", "Zhipu"];

    public ObservableCollection<string> AvailableModels { get; } = [];

    public Task<SetupResult?> WaitForResultAsync() => _tcs.Task;

    partial void OnSelectedProviderChanged(string value)
    {
        AvailableModels.Clear();
        var models = value switch
        {
            "OpenAI" => new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo" },
            "DeepSeek" => new[] { "deepseek-chat", "deepseek-reasoner" },
            "Zhipu" => new[] { "glm-4-plus", "glm-4-flash", "glm-4" },
            _ => Array.Empty<string>()
        };
        foreach (var m in models) AvailableModels.Add(m);
        ModelId = AvailableModels.FirstOrDefault() ?? string.Empty;
    }

    [RelayCommand]
    private void Next()
    {
        ErrorMessage = string.Empty;

        if (CurrentStep == 1)
        {
            if (string.IsNullOrWhiteSpace(SelectedProvider))
            {
                ErrorMessage = "Please select a provider.";
                return;
            }
            CurrentStep = 2;
        }
        else if (CurrentStep == 2)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                ErrorMessage = "Please enter your API key.";
                return;
            }
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 1)
            CurrentStep--;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = "API key is required.";
            return;
        }

        _tcs.TrySetResult(new SetupResult
        {
            Provider = SelectedProvider,
            ApiKey = ApiKey,
            ModelId = string.IsNullOrEmpty(ModelId) ? GetDefaultModel(SelectedProvider) : ModelId
        });
        IsVisible = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        _tcs.TrySetResult(null);
        IsVisible = false;
    }

    private static string GetDefaultModel(string provider) => provider switch
    {
        "OpenAI" => "gpt-4o",
        "DeepSeek" => "deepseek-chat",
        "Zhipu" => "glm-4-plus",
        _ => "gpt-4o"
    };

    public void Show()
    {
        IsVisible = true;
        SelectedProvider = "OpenAI";
        OnSelectedProviderChanged("OpenAI");
    }
}

public class SetupResult
{
    public string Provider { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
}
