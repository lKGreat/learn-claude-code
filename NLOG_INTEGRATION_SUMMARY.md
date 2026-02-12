# NLog 日志集成总结

## 概述
为 MiniClaudeCode 系统添加了全面的 NLog 日志记录，用于记录详细问题、分析问题和捕获程序崩溃。

## 添加的组件

### 1. NuGet 依赖
- **MiniClaudeCode.Avalonia.csproj**: 添加了 `NLog 5.*` 和 `NLog.Extensions.Logging 5.*`
- **MiniClaudeCode.Core.csproj**: 添加了 `NLog 5.*`

### 2. 配置文件
**文件**: `csharp/src/MiniClaudeCode.Avalonia/nlog.config`

配置特性：
- **主日志文件** (`miniclaudecode-{date}.log`): 记录所有 Debug 及以上级别的日志
  - 按天轮转，保留 30 天历史
  - 存储在 `%LOCALAPPDATA%/MiniClaudeCode/logs/`
  
- **错误专用日志** (`errors.log`): 记录 Error 及以上级别的日志
  - 便于快速定位问题
  
- **控制台输出** (可选): 开发时用于实时监控

**日志格式**:
```
${longdate}|${level}|${logger}|${message}${exception}
```
包含完整的异常堆栈信息，便于调试。

### 3. Bootstrap 代码
**文件**: `csharp/src/MiniClaudeCode.Avalonia/Program.cs`

功能：
- 应用程序启动时初始化 NLog
- 捕获未处理的异常 (`AppDomain.UnhandledException`)
- 捕获未观察的 Task 异常 (`TaskScheduler.UnobservedTaskException`)
- 程序关闭时 flush 日志缓冲区

### 4. LogHelper 工具类

**Avalonia 层** (`csharp/src/MiniClaudeCode.Avalonia/Logging/LogHelper.cs`):
```csharp
public static class LogHelper
{
    public static ILogger App    // 应用程序生命周期
    public static ILogger Engine // 引擎、Agent、Plugin
    public static ILogger UI     // UI、ViewModel、Adapter
    public static ILogger For(string name) // 自定义命名 Logger
}
```

**Core 层** (`csharp/src/MiniClaudeCode.Core/Logging/LogHelper.cs`):
```csharp
public static class LogHelper
{
    public static ILogger Engine  // 引擎、Agent、配置
    public static ILogger Plugin  // Plugin 调用与结果
    public static ILogger Service // 业务服务（Rules、Skill、Todo）
    public static ILogger For(string name)
}
```

### 5. 集成的日志点

#### App 启动
- `App.axaml.cs`: 应用初始化日志

#### 引擎构建
- `EngineBuilder.cs`: 
  - 引擎构建开始/完成
  - Provider 初始化

#### Plugin 执行
- `CodingPlugin.cs`: bash、read_file、write_file、edit_file、grep 各工具的错误日志
- `SubAgentRunner.cs`: SubAgent 运行/恢复 的详细日志

#### UI 层
- `MainWindowViewModel.cs`: 引擎初始化流程日志
- `EditorViewModel.cs`:
  - OpenFile / PreviewFile / ActivateTab 流程日志
  - 文件内容加载和绑定日志
  - Tab 关闭日志
  - 编码问题诊断日志
  
- `EditorView.axaml.cs`:
  - 文件激活事件处理日志
  - 内容加载日志
  - 语言语法高亮应用日志

#### Rendering 修复
- `MinimapView.axaml.cs`: VisualLinesInvalidException 的 try-catch 和日志

## 文件编码处理改进

### 多编码支持
在 `EditorViewModel.cs` 中添加了 `TryReadFileWithMultipleEncodings` 方法，尝试多种编码读取文件以避免乱码：
- UTF-8 (默认)
- System Default
- GB2312 (简体中文)
- Big5 (繁体中文)
- ASCII (回退)

## 异常处理改进

### MinimapView VisualLinesInvalidException
在 `MinimapView.axaml.cs` 中添加了：
- 对 `VisualLines` 无效异常的 catch
- Debug 级别日志记录（避免频繁崩溃）
- 安全跳过 viewport 绘制

## 日志查看位置

日志文件存储在：
```
%LOCALAPPDATA%/MiniClaudeCode/logs/
├── miniclaudecode-2026-02-13.log      # 今日主日志
├── miniclaudecode-2026-02-12.log      # 昨日日志
├── errors.log                          # 错误日志（不按日期轮转）
└── archives/                           # 历史日志归档
```

Windows 路径示例：
```
C:\Users\<username>\AppData\Local\MiniClaudeCode\logs\
```

## 日志级别

- **Debug**: 详细流程信息，用于开发调试
- **Info**: 重要事件（应用启动、文件打开、Agent 执行）
- **Warn**: 警告（非关键错误，可恢复）
- **Error**: 错误（操作失败）
- **Fatal**: 致命错误（应用即将崩溃）

## 使用示例

```csharp
// Avalonia 层
LogHelper.UI.Info("应用启动完成");
LogHelper.UI.Debug("正在加载文件: {0}", filePath);
LogHelper.UI.Error(ex, "文件加载失败");

// Core 层
LogHelper.Engine.Info("引擎构建开始");
LogHelper.Plugin.Error(ex, "bash 命令执行失败");
LogHelper.Service.Warn("规则加载超时");

// 自定义 Logger
var logger = LogHelper.For("MyCustomComponent");
logger.Info("自定义消息");
```

## 故障排查指南

### 问题：文件双击后无法显示内容
1. 查看 `logs/miniclaudecode-{date}.log`
2. 搜索 "===== OpenFile 开始 =====" 到 "===== OpenFile 完成 ====="
3. 检查 CreateTabAsync 和 ActivateTab 的日志输出
4. 如果看到 VisualLinesInvalidException，检查 MinimapView 日志

### 问题：文件显示乱码
1. 检查日志中的 "多编码尝试" 信息
2. 查看实际使用的编码
3. 如果仍为乱码，考虑在 TryReadFileWithMultipleEncodings 中添加更多编码

### 问题：程序崩溃
1. 查看 `logs/errors.log`
2. 查看最近的 `miniclaudecode-{date}.log`
3. 查看 `logs/nlog-internal.log` 了解 NLog 本身的问题

## 相关文件变更

| 文件 | 变更 |
|-----|------|
| `Program.cs` | NLog 初始化、全局异常处理 |
| `App.axaml.cs` | 应用初始化日志 |
| `LogHelper.cs` (×2) | 日志入口类 |
| `nlog.config` | NLog 配置 |
| `EngineBuilder.cs` | 引擎构建日志 |
| `MainWindowViewModel.cs` | 引擎初始化日志 |
| `EditorViewModel.cs` | 文件操作日志、编码处理、CloseTab 日志 |
| `EditorView.axaml.cs` | 文件激活事件日志 |
| `CodingPlugin.cs` | 工具执行日志 |
| `SubAgentRunner.cs` | Agent 运行日志 |
| `MinimapView.axaml.cs` | VisualLinesInvalidException 处理 |
| `*.csproj` (×2) | NLog 依赖 |

## 后续改进建议

1. **性能监控**: 在关键操作中记录 elapsed time
2. **性能分析**: 在日志中记录文件大小、读取时间等
3. **远程日志**: 考虑将关键错误上传到远程服务器
4. **日志查看器**: 在 UI 中添加日志查看面板
5. **动态日志级别**: 允许用户在运行时更改日志级别
