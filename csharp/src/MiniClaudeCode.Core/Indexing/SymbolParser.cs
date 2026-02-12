using System.Text.RegularExpressions;
using MiniClaudeCode.Abstractions.Indexing;

namespace MiniClaudeCode.Core.Indexing;

/// <summary>
/// Regex-based symbol extraction for multiple programming languages.
/// Provides fast, lightweight symbol parsing without full AST analysis.
/// </summary>
public static partial class SymbolParser
{
    /// <summary>
    /// Parse symbols from source code based on language.
    /// </summary>
    public static IReadOnlyList<SymbolInfo> ParseSymbols(string filePath, string content, string language)
    {
        var symbols = new List<SymbolInfo>();

        try
        {
            switch (language)
            {
                case "csharp":
                    ParseCSharpSymbols(filePath, content, symbols);
                    break;
                case "typescript":
                case "javascript":
                    ParseTypeScriptSymbols(filePath, content, symbols);
                    break;
                case "python":
                    ParsePythonSymbols(filePath, content, symbols);
                    break;
                // Future: Add more languages as needed
                default:
                    // Unsupported language - return empty list
                    break;
            }
        }
        catch
        {
            // If parsing fails, return empty list rather than crash
        }

        return symbols;
    }

    private static void ParseCSharpSymbols(string filePath, string content, List<SymbolInfo> symbols)
    {
        var lines = content.Split('\n');

        // Parse namespaces
        var namespaceMatches = CSharpNamespaceRegex().Matches(content);
        string? currentNamespace = null;
        foreach (Match match in namespaceMatches)
        {
            var name = match.Groups[1].Value;
            currentNamespace = name;
            var lineNumber = GetLineNumber(content, match.Index);

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = name,
                Kind = SymbolKind.Namespace,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0,
                ContainerName = null
            });
        }

        // Parse classes, interfaces, structs, enums, records
        var typeMatches = CSharpTypeRegex().Matches(content);
        foreach (Match match in typeMatches)
        {
            var keyword = match.Groups[3].Value;
            var name = match.Groups[4].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            var kind = keyword switch
            {
                "class" => SymbolKind.Class,
                "interface" => SymbolKind.Interface,
                "struct" => SymbolKind.Struct,
                "enum" => SymbolKind.Enum,
                "record" => SymbolKind.Class,
                _ => SymbolKind.Class
            };

            var fqn = currentNamespace != null ? $"{currentNamespace}.{name}" : name;

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = fqn,
                Kind = kind,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0,
                ContainerName = currentNamespace,
                Signature = match.Groups[0].Value.Trim()
            });
        }

        // Parse methods
        var methodMatches = CSharpMethodRegex().Matches(content);
        foreach (Match match in methodMatches)
        {
            var returnType = match.Groups[2].Value;
            var name = match.Groups[3].Value;
            var parameters = match.Groups[4].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            // Determine container (find the enclosing type)
            var container = FindEnclosingType(symbols, lineNumber);
            var fqn = container != null ? $"{container.FullyQualifiedName}.{name}" : name;

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = fqn,
                Kind = SymbolKind.Method,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0,
                ContainerName = container?.Name,
                Signature = $"{returnType} {name}({parameters})"
            });
        }

        // Parse properties
        var propertyMatches = CSharpPropertyRegex().Matches(content);
        foreach (Match match in propertyMatches)
        {
            var type = match.Groups[2].Value;
            var name = match.Groups[3].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            var container = FindEnclosingType(symbols, lineNumber);
            var fqn = container != null ? $"{container.FullyQualifiedName}.{name}" : name;

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = fqn,
                Kind = SymbolKind.Property,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0,
                ContainerName = container?.Name,
                Signature = $"{type} {name}"
            });
        }
    }

    private static void ParseTypeScriptSymbols(string filePath, string content, List<SymbolInfo> symbols)
    {
        // Parse classes
        var classMatches = TypeScriptClassRegex().Matches(content);
        foreach (Match match in classMatches)
        {
            var name = match.Groups[2].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = name,
                Kind = SymbolKind.Class,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0
            });
        }

        // Parse interfaces
        var interfaceMatches = TypeScriptInterfaceRegex().Matches(content);
        foreach (Match match in interfaceMatches)
        {
            var name = match.Groups[2].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = name,
                Kind = SymbolKind.Interface,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0
            });
        }

        // Parse functions
        var functionMatches = TypeScriptFunctionRegex().Matches(content);
        foreach (Match match in functionMatches)
        {
            var name = match.Groups[2].Value;
            var parameters = match.Groups[3].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            var container = FindEnclosingType(symbols, lineNumber);
            var fqn = container != null ? $"{container.FullyQualifiedName}.{name}" : name;

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = fqn,
                Kind = SymbolKind.Method,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0,
                ContainerName = container?.Name,
                Signature = $"{name}({parameters})"
            });
        }

        // Parse enums
        var enumMatches = TypeScriptEnumRegex().Matches(content);
        foreach (Match match in enumMatches)
        {
            var name = match.Groups[2].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = name,
                Kind = SymbolKind.Enum,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0
            });
        }
    }

    private static void ParsePythonSymbols(string filePath, string content, List<SymbolInfo> symbols)
    {
        // Parse classes
        var classMatches = PythonClassRegex().Matches(content);
        foreach (Match match in classMatches)
        {
            var name = match.Groups[1].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = name,
                Kind = SymbolKind.Class,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0
            });
        }

        // Parse functions/methods
        var functionMatches = PythonFunctionRegex().Matches(content);
        foreach (Match match in functionMatches)
        {
            var name = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            var lineNumber = GetLineNumber(content, match.Index);

            var container = FindEnclosingType(symbols, lineNumber);
            var fqn = container != null ? $"{container.FullyQualifiedName}.{name}" : name;

            symbols.Add(new SymbolInfo
            {
                Name = name,
                FullyQualifiedName = fqn,
                Kind = SymbolKind.Method,
                FilePath = filePath,
                Line = lineNumber,
                Column = 0,
                ContainerName = container?.Name,
                Signature = $"def {name}({parameters})"
            });
        }
    }

    private static int GetLineNumber(string content, int index)
    {
        if (index >= content.Length)
            return 0;

        int line = 0;
        for (int i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n')
                line++;
        }
        return line;
    }

    private static SymbolInfo? FindEnclosingType(List<SymbolInfo> symbols, int lineNumber)
    {
        // Find the closest type symbol that appears before this line
        return symbols
            .Where(s => (s.Kind == SymbolKind.Class ||
                        s.Kind == SymbolKind.Interface ||
                        s.Kind == SymbolKind.Struct) &&
                       s.Line < lineNumber)
            .OrderByDescending(s => s.Line)
            .FirstOrDefault();
    }

    // C# Regex patterns
    [GeneratedRegex(@"namespace\s+([\w\.]+)", RegexOptions.Multiline)]
    private static partial Regex CSharpNamespaceRegex();

    [GeneratedRegex(@"(?:public|internal|private|protected)?\s*(?:static\s+)?(?:partial\s+)?(?:abstract\s+)?(?:sealed\s+)?(class|interface|struct|enum|record)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex CSharpTypeRegex();

    [GeneratedRegex(@"(?:public|internal|private|protected)?\s*(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?([\w\<\>\[\]]+)\s+(\w+)\s*\((.*?)\)", RegexOptions.Multiline)]
    private static partial Regex CSharpMethodRegex();

    [GeneratedRegex(@"(?:public|internal|private|protected)?\s*(?:static\s+)?([\w\<\>\[\]]+)\s+(\w+)\s*\{\s*(?:get|set)", RegexOptions.Multiline)]
    private static partial Regex CSharpPropertyRegex();

    // TypeScript/JavaScript Regex patterns
    [GeneratedRegex(@"(export\s+)?class\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptClassRegex();

    [GeneratedRegex(@"(export\s+)?interface\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptInterfaceRegex();

    [GeneratedRegex(@"(export\s+)?function\s+(\w+)\s*\((.*?)\)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptFunctionRegex();

    [GeneratedRegex(@"(export\s+)?enum\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptEnumRegex();

    // Python Regex patterns
    [GeneratedRegex(@"^class\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex PythonClassRegex();

    [GeneratedRegex(@"^def\s+(\w+)\s*\((.*?)\)", RegexOptions.Multiline)]
    private static partial Regex PythonFunctionRegex();
}
