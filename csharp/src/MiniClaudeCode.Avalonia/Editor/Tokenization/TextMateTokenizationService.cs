namespace MiniClaudeCode.Avalonia.Editor.Tokenization;

/// <summary>
/// TextMate-based tokenization service that manages grammar loading,
/// language detection, and theme application for syntax highlighting.
/// Uses TextMateSharp.Grammars for grammar definitions.
/// </summary>
public class TextMateTokenizationService : ITokenizationService
{
    private readonly Dictionary<string, string> _languageToScope = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = "source.cs",
        ["typescript"] = "source.ts",
        ["javascript"] = "source.js",
        ["python"] = "source.python",
        ["json"] = "source.json",
        ["xml"] = "text.xml",
        ["html"] = "text.html.basic",
        ["css"] = "source.css",
        ["markdown"] = "text.html.markdown",
        ["yaml"] = "source.yaml",
        ["rust"] = "source.rust",
        ["go"] = "source.go",
        ["java"] = "source.java",
        ["cpp"] = "source.cpp",
        ["c"] = "source.c",
        ["shell"] = "source.shell",
        ["powershell"] = "source.powershell",
        ["sql"] = "source.sql",
        ["ruby"] = "source.ruby",
        ["php"] = "source.php",
        ["swift"] = "source.swift",
        ["kotlin"] = "source.kotlin",
        ["lua"] = "source.lua",
        ["perl"] = "source.perl",
        ["r"] = "source.r",
        ["toml"] = "source.toml",
        ["ini"] = "source.ini",
        ["dockerfile"] = "source.dockerfile",
        ["makefile"] = "source.makefile",
        ["protobuf"] = "source.protobuf",
    };

    private readonly Dictionary<string, string> _extensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".ts"] = "typescript", [".tsx"] = "typescript",
        [".js"] = "javascript", [".jsx"] = "javascript",
        [".py"] = "python",
        [".json"] = "json",
        [".xml"] = "xml", [".axaml"] = "xml", [".xaml"] = "xml",
        [".csproj"] = "xml", [".props"] = "xml", [".targets"] = "xml",
        [".html"] = "html", [".htm"] = "html",
        [".css"] = "css", [".scss"] = "css", [".less"] = "css",
        [".md"] = "markdown",
        [".yaml"] = "yaml", [".yml"] = "yaml",
        [".rs"] = "rust",
        [".go"] = "go",
        [".java"] = "java",
        [".cpp"] = "cpp", [".cc"] = "cpp", [".cxx"] = "cpp",
        [".h"] = "cpp", [".hpp"] = "cpp",
        [".c"] = "c",
        [".sh"] = "shell", [".bash"] = "shell",
        [".ps1"] = "powershell", [".psm1"] = "powershell",
        [".sql"] = "sql",
        [".rb"] = "ruby",
        [".php"] = "php",
        [".swift"] = "swift",
        [".kt"] = "kotlin", [".kts"] = "kotlin",
        [".lua"] = "lua",
        [".pl"] = "perl",
        [".r"] = "r",
        [".toml"] = "toml",
        [".ini"] = "ini", [".cfg"] = "ini",
        [".proto"] = "protobuf",
    };

    public IReadOnlyList<string> RegisteredLanguages =>
        _languageToScope.Keys.ToList().AsReadOnly();

    public string? GetScopeName(string languageId)
    {
        return _languageToScope.TryGetValue(languageId, out var scope) ? scope : null;
    }

    public string GetLanguageId(string fileExtension)
    {
        return _extensionToLanguage.TryGetValue(fileExtension, out var lang) ? lang : "plaintext";
    }

    public bool ShouldDisableTokenization(long fileSize, int lineCount)
    {
        return fileSize > LargeFileConstants.LargeFileSizeThreshold ||
               lineCount > LargeFileConstants.LargeFileLineCountThreshold;
    }

    /// <summary>
    /// Register a new language mapping (for extensions to add custom languages).
    /// </summary>
    public void RegisterLanguage(string languageId, string scopeName, IEnumerable<string> extensions)
    {
        _languageToScope[languageId] = scopeName;
        foreach (var ext in extensions)
        {
            _extensionToLanguage[ext] = languageId;
        }
    }
}
