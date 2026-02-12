using Avalonia;
using MiniClaudeCode.Avalonia.Logging;
using NLog;

namespace MiniClaudeCode.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 优先初始化 NLog，确保启动阶段和崩溃都能写入日志
        SetupNLog();
        SetupGlobalExceptionHandlers();

        try
        {
            LogHelper.App.Info("MiniClaudeCode 启动中...");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogHelper.App.Fatal(ex, "应用程序启动失败");
            throw;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    private static void SetupNLog()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");
        if (File.Exists(configPath))
            LogManager.Setup().LoadConfigurationFromFile(configPath);
    }

    private static void SetupGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                try
                {
                    LogHelper.App.Fatal(ex, "未处理的异常导致进程终止");
                    LogManager.Flush(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 避免二次崩溃
                }
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                LogHelper.App.Error(e.Exception, "未观察的 Task 异常");
                e.SetObserved();
            }
            catch
            {
                // 避免二次崩溃
            }
        };
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
