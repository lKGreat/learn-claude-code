using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniClaudeCode.Avalonia.Models;

/// <summary>
/// Wraps a shell process (cmd.exe / powershell.exe / bash) with stdin/stdout streaming.
/// </summary>
public class TerminalSession : IDisposable
{
    private Process? _process;
    private readonly StringBuilder _outputBuffer = new();
    private bool _disposed;

    public string Id { get; } = Guid.NewGuid().ToString()[..8];
    public string ShellType { get; init; } = "powershell";
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Fired when new output arrives (stdout or stderr).
    /// </summary>
    public event Action<string>? OutputReceived;

    /// <summary>
    /// Fired when the process exits.
    /// </summary>
    public event Action<int>? ProcessExited;

    /// <summary>
    /// Get the full output buffer.
    /// </summary>
    public string Output => _outputBuffer.ToString();

    /// <summary>
    /// Start the shell process.
    /// </summary>
    public void Start()
    {
        if (_process != null) return;

        string fileName;
        string arguments;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (ShellType.Equals("cmd", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "cmd.exe";
                arguments = "";
            }
            else
            {
                fileName = "powershell.exe";
                arguments = "-NoLogo -NoProfile";
            }
        }
        else
        {
            fileName = "/bin/bash";
            arguments = "";
        }

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                var line = e.Data + "\n";
                _outputBuffer.Append(line);
                OutputReceived?.Invoke(line);
            }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                var line = e.Data + "\n";
                _outputBuffer.Append(line);
                OutputReceived?.Invoke(line);
            }
        };

        _process.Exited += (_, _) =>
        {
            ProcessExited?.Invoke(_process?.ExitCode ?? -1);
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>
    /// Send a command to the shell's stdin.
    /// </summary>
    public void SendCommand(string command)
    {
        if (_process is not { HasExited: false }) return;

        _outputBuffer.Append($"> {command}\n");
        OutputReceived?.Invoke($"> {command}\n");

        _process.StandardInput.WriteLine(command);
        _process.StandardInput.Flush();
    }

    /// <summary>
    /// Kill the shell process.
    /// </summary>
    public void Kill()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch { /* ignore */ }
    }

    public void ClearOutput()
    {
        _outputBuffer.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Kill();
        _process?.Dispose();
        GC.SuppressFinalize(this);
    }
}
