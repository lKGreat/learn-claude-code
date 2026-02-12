using System.Collections.ObjectModel;
using Terminal.Gui;
using MiniClaudeCode.Core.Configuration;

namespace MiniClaudeCode.Tui.Dialogs;

/// <summary>
/// First-time setup wizard using Terminal.Gui dialogs.
/// </summary>
public static class SetupWizardDialog
{
    /// <summary>
    /// Run the setup wizard and return provider configs.
    /// Returns empty dictionary if user cancels.
    /// </summary>
    public static async Task<Dictionary<ModelProvider, ModelProviderConfig>> RunAsync()
    {
        string? selectedProvider = null;
        string? apiKey = null;

        // Step 1: Select provider
        var providerDialog = new Dialog
        {
            Title = "MiniClaudeCode - First Time Setup",
            Width = Dim.Percent(60),
            Height = 12,
        };

        var label = new Label
        {
            Text = "No API key found. Select a provider:",
            X = 1,
            Y = 1,
        };

        var choices = new List<string> { "DeepSeek", "Zhipu (GLM)", "OpenAI" };
        var listView = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Height = 3,
        };
        listView.SetSource(new ObservableCollection<string>(choices));

        var okBtn = new Button { Text = "Next", IsDefault = true };
        okBtn.Accepting += (_, _) =>
        {
            var idx = listView.SelectedItem;
            if (idx >= 0 && idx < choices.Count)
                selectedProvider = choices[idx];
            Application.RequestStop();
        };

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Accepting += (_, _) => Application.RequestStop();

        providerDialog.Add(label, listView);
        providerDialog.AddButton(okBtn);
        providerDialog.AddButton(cancelBtn);

        Application.Run(providerDialog);
        providerDialog.Dispose();

        if (string.IsNullOrEmpty(selectedProvider))
            return [];

        // Step 2: Enter API key
        var keyDialog = new Dialog
        {
            Title = $"Enter {selectedProvider} API Key",
            Width = Dim.Percent(60),
            Height = 10,
        };

        var (envKey, defaultModel) = selectedProvider switch
        {
            "DeepSeek" => ("DEEPSEEK_API_KEY", "deepseek-chat"),
            "Zhipu (GLM)" => ("ZHIPU_API_KEY", "glm-4-plus"),
            _ => ("OPENAI_API_KEY", "gpt-4o"),
        };

        var keyLabel = new Label
        {
            Text = $"Enter your API key ({envKey}):",
            X = 1,
            Y = 1,
        };

        var keyInput = new TextField
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(1),
            Secret = true,
        };

        var saveBtn = new Button { Text = "Save", IsDefault = true };
        saveBtn.Accepting += (_, _) =>
        {
            apiKey = keyInput.Text?.ToString();
            Application.RequestStop();
        };

        var cancelKeyBtn = new Button { Text = "Cancel" };
        cancelKeyBtn.Accepting += (_, _) => Application.RequestStop();

        keyDialog.Add(keyLabel, keyInput);
        keyDialog.AddButton(saveBtn);
        keyDialog.AddButton(cancelKeyBtn);

        Application.Run(keyDialog);
        keyDialog.Dispose();

        if (string.IsNullOrWhiteSpace(apiKey))
            return [];

        // Save to .env
        var providerName = selectedProvider switch
        {
            "DeepSeek" => "deepseek",
            "Zhipu (GLM)" => "zhipu",
            _ => "openai"
        };

        var envContent = $"ACTIVE_PROVIDER={providerName}\n{envKey}={apiKey}\n";

        try
        {
            var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
            await File.WriteAllTextAsync(envPath, envContent);
        }
        catch
        {
            try
            {
                var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                await File.WriteAllTextAsync(envPath, envContent);
            }
            catch { /* Best effort */ }
        }

        Environment.SetEnvironmentVariable(envKey, apiKey);
        return ModelProviderConfig.LoadAll();
    }
}
