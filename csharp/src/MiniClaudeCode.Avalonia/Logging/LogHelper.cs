using NLog;

namespace MiniClaudeCode.Avalonia.Logging;

/// <summary>
/// 统一日志入口，便于在 Avalonia 层记录详细日志，支持问题分析与崩溃排查。
/// </summary>
public static class LogHelper
{
    private static ILogger? _appLogger;
    private static ILogger? _engineLogger;
    private static ILogger? _uiLogger;

    /// <summary>应用程序生命周期、启动、配置等</summary>
    public static ILogger App => _appLogger ??= LogManager.GetLogger("App");

    /// <summary>引擎、Agent、Plugin 等核心逻辑</summary>
    public static ILogger Engine => _engineLogger ??= LogManager.GetLogger("Engine");

    /// <summary>UI、ViewModel、Adapter 等界面层</summary>
    public static ILogger UI => _uiLogger ??= LogManager.GetLogger("UI");

    /// <summary>根据类型获取命名 Logger，用于其他模块</summary>
    public static ILogger For(string name) => LogManager.GetLogger(name);
}
