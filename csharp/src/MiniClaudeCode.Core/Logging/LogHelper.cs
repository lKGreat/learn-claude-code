using NLog;

namespace MiniClaudeCode.Core.Logging;

/// <summary>
/// Core 层统一日志入口，记录引擎、插件、服务等详细日志。
/// </summary>
public static class LogHelper
{
    private static ILogger? _engineLogger;
    private static ILogger? _pluginLogger;
    private static ILogger? _serviceLogger;

    /// <summary>引擎、Agent、配置等</summary>
    public static ILogger Engine => _engineLogger ??= LogManager.GetLogger("Engine");

    /// <summary>各 Plugin 的调用与结果</summary>
    public static ILogger Plugin => _pluginLogger ??= LogManager.GetLogger("Plugin");

    /// <summary>业务服务（Rules、Skill、Todo 等）</summary>
    public static ILogger Service => _serviceLogger ??= LogManager.GetLogger("Service");

    /// <summary>根据类型获取命名 Logger</summary>
    public static ILogger For(string name) => LogManager.GetLogger(name);
}
