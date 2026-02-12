# VSCode 核心功能需求描述框架

## 1. 菜单系统 (Menu System)

### 顶部主菜单栏

```
File 菜单
├── New File (Ctrl+N)
├── Open File... (Ctrl+O)
├── Open Folder... (Ctrl+K Ctrl+O)
├── Open Recent... (最近工作区列表)
├── Save (Ctrl+S)
├── Save As... (Ctrl+Shift+S)
├── Save All (Ctrl+K S)
├── Revert File
├── Close Editor (Ctrl+W)
├── Close Folder
└── Exit (Alt+F4)

Edit 菜单
├── Undo (Ctrl+Z)
├── Redo (Ctrl+Y)
├── Cut (Ctrl+X)
├── Copy (Ctrl+C)
├── Paste (Ctrl+V)
├── Select All (Ctrl+A)
├── Find (Ctrl+F)
├── Replace (Ctrl+H)
├── Find in Files (Ctrl+Shift+F)
└── Toggle Line Comment (Ctrl+/)

View 菜单
├── Command Palette... (Ctrl+Shift+P)
├── Open View... (Ctrl+Q)
├── Appearance (主题设置)
├── Explorer (Ctrl+Shift+E)
├── Search (Ctrl+Shift+F)
├── Source Control (Ctrl+Shift+G)
├── Run and Debug (Ctrl+Shift+D)
├── Extensions (Ctrl+Shift+X)
├── Problems (Ctrl+Shift+M)
├── Output (Ctrl+Shift+U)
├── Debug Console (Ctrl+Shift+Y)
├── Terminal (Ctrl+`)
├── Editor Layout
│   ├── Split Left (Ctrl+\)
│   ├── Split Right (Ctrl+K Ctrl+\)
│   ├── Split Up
│   └── Split Down
└── Toggle Word Wrap (Alt+Z)

Go 菜单
├── Go to File... (Ctrl+P)
├── Go to Symbol in Workspace... (Ctrl+T)
├── Go to Definition (F12)
├── Go to Type Definition (Ctrl+Shift+F12)
├── Go to Implementation (Ctrl+F12)
├── Go to References (Shift+F12)
├── Go to Line... (Ctrl+G)
└── Previous/Next Error (F8/Shift+F8)

Run 菜单
├── Start Debugging (F5)
├── Run Without Debugging (Ctrl+F5)
├── Stop Debugging (Shift+F5)
├── Restart Debugging (Ctrl+Shift+F5)
└── Add Configuration...

Terminal 菜单
├── Create New Terminal (Ctrl+Shift+`)
├── Split Terminal (Ctrl+Shift+5)
├── Kill Terminal
└── Clear Terminal

Help 菜单
├── Welcome
├── Documentation
├── Keyboard Shortcuts (Ctrl+K Ctrl+S)
├── About
└── Toggle Developer Tools
```

---

## 2. 工作区能力 (Workspace Capabilities)

### 多文件夹工作区

```
核心功能:
├── 打开单个文件夹
├── 打开多个文件夹 (添加到工作区)
├── 保存工作区配置 (.code-workspace)
├── 最近工作区列表
│   ├── 按时间排序
│   ├── 固定常用工作区
│   └── 清除历史记录
├── 工作区设置
│   ├── 用户设置
│   ├── 工作区设置
│   └── 文件夹设置
├── 工作区信任
│   ├── 受信任文件夹
│   ├── 限制模式 (禁用代码执行)
│   └── 信任管理
└── 工作区存储
    ├── 保存会话状态
    ├── 恢复上次打开的文件
    └── 恢复编辑器布局
```

### 上下文菜单 (右键菜单)

```
文件夹右键菜单:
├── New File
├── New Folder
├── Copy Path
├── Copy Relative Path
├── Reveal in Explorer
├── Open in Terminal
├── Find in Folder...
├── Delete
├── Rename
└── Properties

文件右键菜单:
├── Open to the Side
├── Open Preview
├── Copy Path
├── Copy Relative Path
├── Reveal in Explorer
├── Open in Terminal (folder context)
├── Find in Folder...
├── Delete
├── Rename
└── Properties
```

---

## 3. 文件树 (File Explorer) 能力

### 核心导航功能

```
文件树结构:
├── 递归显示目录结构
├── 折叠/展开文件夹 (←/→ 或点击)
├── 自动展开打开文件的父目录
├── 文件图标 (基于扩展名)
├── 文件状态指示器
│   ├── M - Modified
│   ├── A - Added
│   ├── D - Deleted
│   ├── R - Renamed
│   ├── ? - Untracked
│   └── ! - Ignored
├── 文件颜色标识 (基于语言)
└── 空文件夹图标
```

### 交互功能

```
文件操作:
├── 单击选中
├── 双击打开文件
├── 拖拽移动文件/文件夹
├── 中键点击打开到新标签
├── Ctrl+点击打开到侧边
├── 右键上下文菜单
└── 搜索过滤 (Ctrl+P 模式)

显示控制:
├── 按名称排序
├── 按类型排序
├── 按修改时间排序
├── 排序方向切换
├── 显示隐藏文件
├── 文件树宽度调整
├── 折叠所有 (Ctrl+K Ctrl+0)
└── 展开所有 (Ctrl+K Ctrl+J)
```

### 高级功能

```
文件同步:
├── 文件系统监视 (自动刷新)
├── 外部修改提示
├── 自动重载或提示重载
├── 排除模式 (.gitignore 遵循)
└── include/exclude 配置

性能优化:
├── 虚拟滚动 (大型项目)
├── 延迟加载子目录
├── 缓存文件树状态
└── 懒加载图标
```

---

## 4. 代码编辑能力 (Code Editing)

### 编辑器核心功能

```
文本编辑:
├── 多光标编辑 (Alt+点击, Ctrl+Alt+↑/↓)
├── 块选择 (Shift+Alt+拖动)
├── 行操作
│   ├── 复制行 (Shift+Alt+↓/↑)
│   ├── 移动行 (Alt+↓/↑)
│   ├── 删除行 (Ctrl+Shift+K)
│   ├── 缩进 (Ctrl+[)
│   └── 取消缩进 (Ctrl+])
├── 快速修复 (Ctrl+.)
├── 格式化文档 (Shift+Alt+F)
├── 格式化选中 (Ctrl+K Ctrl+F)
└── 转换大小写 (Ctrl+Shift+P, then case)
```

### 智能编码

```
代码辅助:
├── 语法高亮
│   ├── 基于语言主题
│   ├── 支持200+语言
│   └── 可扩展的TextMate语法
├── 代码补全
│   ├── IntelliSense (Ctrl+Space)
│   ├── 参数提示
│   ├── 成员提示
│   ├── 代码片段 (Snippets)
│   └── 基于上下文的建议
├── 错误诊断
│   ├── 实时错误检测
│   ├── 警告提示
│   ├── 信息提示
│   └── 错误列表视图
└── 重构
    ├── 重命名符号 (F2)
    ├── 提取函数
    ├── 提取变量
    └── 内联变量
```

### 导航功能

```
代码导航:
├── 转到定义 (F12)
├── 转到实现 (Ctrl+F12)
├── 查找引用 (Shift+F12)
├── 转到符号 (Ctrl+Shift+O)
├── 转到行 (Ctrl+G)
├── 面包屑导航
├── 大纲视图
├── 文件大纲
├── 呼叫层次
└── 类型层次
```

### 搜索和替换

```
查找功能:
├── 当前文件查找 (Ctrl+F)
│   ├── 正则表达式
│   ├── 大小写敏感
│   ├── 全字匹配
│   ├── 使用选择内容查找
│   └── 下一个/上一个 (Enter/Shift+Enter)
├── 全局查找 (Ctrl+Shift+F)
│   ├── 文件模式过滤 (*.ts, *.js)
│   ├── 排除文件模式
│   ├── 搜索范围
│   └── 结果分组
├── 高亮所有匹配项
└── 查找历史

替换功能:
├── 当前文件替换 (Ctrl+H)
├── 全局替换 (Ctrl+Shift+H)
├── 预览替换结果
├── 替换全部
└── 替换历史
```

### 编辑器界面

```
标签页系统:
├── 多文件标签页
├── 标签页固定 (Pin)
├── 标签页拖拽排序
├── 标签页分组 (编辑器组)
├── 标签页右键菜单
├── 文件修改标识 (●)
├── 关闭按钮
├── 关闭其他
├── 关闭右侧
├── 关闭已修改
└── 关闭全部

编辑器组:
├── 垂直分割
├── 水平分割
├── 最多3组
├── 拖拽标签到其他组
├── 组布局保存/恢复
└── 组最大化

侧边栏和面板:
├── 迷你地图 (Minimap)
├── 代码折叠
├── 缩进参考线
├── 行号
├── 面包屑导航
├── Git状态栏
├── 颜色预览
└── 图片预览
```

### 代码片段 (Snippets)

```
内置片段:
├── HTML (div, p, a, etc.)
├── JavaScript (console, for, function)
├── TypeScript (interface, class, type)
├── C# (class, prop, forr)
└── 自定义片段

片段功能:
├── Tab触发 (如 for + Tab)
├── 占位符跳转 (Tab/Shift+Tab)
├── 变量替换 (${TM_FILENAME}, ${DATE})
├── 选项列表 (${1|one,two,three|})
└── 多光标编辑支持
```

### 差异编辑器 (Diff Editor)

```
显示模式:
├── 并排视图
├── 内联视图
└── 统一视图

差异功能:
├── 添加/删除/修改行高亮
├── 差异计数
├── 上一处/下一处差异
├── 复制行
├── 撤销修改
└── 忽略空格
```

---

## 5. 其他核心功能

### 命令面板 (Command Palette)

```
├── Ctrl+Shift+P 打开命令面板
├── Ctrl+P 快速打开文件
├── 模糊搜索
├── 快捷键显示
├── 命令历史
└── 自定义命令注册
```

### 终端 (Terminal)

```
终端功能:
├── 多终端标签页
├── 终端分割
├── Shell选择 (PowerShell, CMD, Git Bash, WSL)
├── 环境变量继承
├── 任务运行 (tasks.json)
└── 问题匹配器 (输出解析)
```

### 源代码管理 (Git)

```
Git集成:
├── 状态面板
├── 暂存/取消暂存
├── 提交
├── 推送/拉取
├── 分支管理
├── 合并冲突解决
├── 差异视图
├── 历史记录
├── Stash
└── 远程仓库管理
```

### 主题和外观

```
主题系统:
├── 明色主题
├── 暗色主题
├── 高对比度主题
├── 自定义主题
├── 图标主题
└── 字体设置
```

---

## 实现优先级建议

### P0 - 核心基础 (必须)
- 文件树基本导航
- 打开/保存文件
- 基本代码编辑
- 标签页管理
- 命令面板

### P1 - 高频功能 (重要)
- 多文件操作
- 搜索和替换
- 代码片段
- 快捷键系统
- 设置持久化

### P2 - 增强体验 (次要)
- 多编辑器组
- 迷你地图
- Git集成
- 终端集成
- 主题系统

### P3 - 高级功能 (可选)
- AI代码补全
- 调试器
- 扩展系统
- 远程开发

---

## 使用说明

这个框架可以帮助你：
1. **规划开发路线** - 按优先级分阶段实现
2. **编写需求文档** - 详细的功能描述
3. **与团队沟通** - 清晰的功能边界
4. **评估进度** - 已实现 vs 待实现
