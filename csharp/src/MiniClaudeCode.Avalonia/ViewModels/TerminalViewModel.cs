using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the integrated terminal panel with multiple sessions.
/// </summary>
public partial class TerminalViewModel : ObservableObject, IDisposable
{
    public ObservableCollection<TerminalSession> Sessions { get; } = [];

    [ObservableProperty]
    private TerminalSession? _activeSession;

    [ObservableProperty]
    private string _outputText = "";

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _selectedShell = "powershell";

    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// Fired when output changes (for auto-scroll).
    /// </summary>
    public event Action? OutputChanged;

    public string[] AvailableShells { get; } = ["powershell", "cmd"];

    [RelayCommand]
    private void NewSession()
    {
        var session = new TerminalSession
        {
            ShellType = SelectedShell,
            WorkingDirectory = WorkingDirectory
        };

        session.OutputReceived += output =>
        {
            DispatcherService.Post(() =>
            {
                if (session == ActiveSession)
                {
                    OutputText += output;
                    OutputChanged?.Invoke();
                }
            });
        };

        session.ProcessExited += exitCode =>
        {
            DispatcherService.Post(() =>
            {
                OutputText += $"\n[Process exited with code {exitCode}]\n";
                OutputChanged?.Invoke();
            });
        };

        Sessions.Add(session);
        ActiveSession = session;
        OutputText = "";
        session.Start();
        IsVisible = true;
    }

    [RelayCommand]
    private void SendCommand()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrEmpty(text) || ActiveSession == null) return;

        ActiveSession.SendCommand(text);
        InputText = "";
    }

    [RelayCommand]
    private void KillSession()
    {
        if (ActiveSession == null) return;

        ActiveSession.Kill();
        OutputText += "\n[Session terminated]\n";
        OutputChanged?.Invoke();
    }

    [RelayCommand]
    private void ClearOutput()
    {
        if (ActiveSession != null)
        {
            ActiveSession.ClearOutput();
        }
        OutputText = "";
    }

    [RelayCommand]
    private void CloseSession()
    {
        if (ActiveSession == null) return;

        var session = ActiveSession;
        session.Kill();
        session.Dispose();
        Sessions.Remove(session);

        ActiveSession = Sessions.Count > 0 ? Sessions[^1] : null;
        if (ActiveSession != null)
        {
            OutputText = ActiveSession.Output;
        }
        else
        {
            OutputText = "";
        }
    }

    [RelayCommand]
    private void SwitchSession(TerminalSession? session)
    {
        if (session == null) return;
        ActiveSession = session;
        OutputText = session.Output;
    }

    /// <summary>
    /// Mirror a tool bash call to the terminal for visibility.
    /// </summary>
    public void MirrorToolCall(string command, string output)
    {
        DispatcherService.Post(() =>
        {
            OutputText += $"\n[Tool] > {command}\n{output}\n";
            OutputChanged?.Invoke();
        });
    }

    public void Dispose()
    {
        foreach (var session in Sessions)
        {
            session.Kill();
            session.Dispose();
        }
        Sessions.Clear();
        GC.SuppressFinalize(this);
    }
}
