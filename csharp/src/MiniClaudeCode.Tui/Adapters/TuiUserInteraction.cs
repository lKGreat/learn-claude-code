using System.Collections.ObjectModel;
using Terminal.Gui;
using MiniClaudeCode.Abstractions.UI;

namespace MiniClaudeCode.Tui.Adapters;

/// <summary>
/// IUserInteraction implementation using Terminal.Gui Dialogs.
/// </summary>
public class TuiUserInteraction : IUserInteraction
{
    public Task<string?> AskAsync(string question, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<string?>();

        Application.Invoke(() =>
        {
            var dialog = new Dialog
            {
                Title = "Agent Question",
                Width = Dim.Percent(60),
                Height = 10,
            };

            var label = new Label
            {
                Text = question,
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
            };

            var input = new TextField
            {
                X = 1,
                Y = 3,
                Width = Dim.Fill(1),
            };

            var okButton = new Button { Text = "OK", IsDefault = true };
            okButton.Accepting += (_, _) =>
            {
                tcs.TrySetResult(input.Text?.ToString());
                Application.RequestStop();
            };

            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, _) =>
            {
                tcs.TrySetResult(null);
                Application.RequestStop();
            };

            dialog.Add(label, input);
            dialog.AddButton(okButton);
            dialog.AddButton(cancelButton);

            Application.Run(dialog);
            dialog.Dispose();
        });

        return tcs.Task;
    }

    public Task<string?> SelectAsync(string title, IReadOnlyList<string> choices, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<string?>();

        Application.Invoke(() =>
        {
            var dialog = new Dialog
            {
                Title = title,
                Width = Dim.Percent(50),
                Height = Dim.Percent(60),
            };

            var listView = new ListView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(2),
            };
            listView.SetSource(new ObservableCollection<string>(choices));

            var okButton = new Button { Text = "OK", IsDefault = true };
            okButton.Accepting += (_, _) =>
            {
                var idx = listView.SelectedItem;
                tcs.TrySetResult(idx >= 0 && idx < choices.Count ? choices[idx] : null);
                Application.RequestStop();
            };

            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, _) =>
            {
                tcs.TrySetResult(null);
                Application.RequestStop();
            };

            dialog.Add(listView);
            dialog.AddButton(okButton);
            dialog.AddButton(cancelButton);

            Application.Run(dialog);
            dialog.Dispose();
        });

        return tcs.Task;
    }

    public Task<string?> AskSecretAsync(string prompt, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<string?>();

        Application.Invoke(() =>
        {
            var dialog = new Dialog
            {
                Title = "Secret Input",
                Width = Dim.Percent(60),
                Height = 10,
            };

            var label = new Label
            {
                Text = prompt,
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
            };

            var input = new TextField
            {
                X = 1,
                Y = 3,
                Width = Dim.Fill(1),
                Secret = true,
            };

            var okButton = new Button { Text = "OK", IsDefault = true };
            okButton.Accepting += (_, _) =>
            {
                tcs.TrySetResult(input.Text?.ToString());
                Application.RequestStop();
            };

            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Accepting += (_, _) =>
            {
                tcs.TrySetResult(null);
                Application.RequestStop();
            };

            dialog.Add(label, input);
            dialog.AddButton(okButton);
            dialog.AddButton(cancelButton);

            Application.Run(dialog);
            dialog.Dispose();
        });

        return tcs.Task;
    }

    public Task<bool> ConfirmAsync(string message, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();

        Application.Invoke(() =>
        {
            var result = MessageBox.Query("Confirm", message, "Yes", "No");
            tcs.TrySetResult(result == 0);
        });

        return tcs.Task;
    }
}
