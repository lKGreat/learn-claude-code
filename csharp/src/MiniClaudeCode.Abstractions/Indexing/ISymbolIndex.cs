namespace MiniClaudeCode.Abstractions.Indexing;

/// <summary>
/// Manages symbol-level indexing of the codebase (classes, methods, properties, etc.).
/// Supports fuzzy symbol search and navigation.
/// </summary>
public interface ISymbolIndex
{
    /// <summary>
    /// Retrieves all symbols defined in a specific file.
    /// Returns an empty list if the file is not indexed or has no symbols.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of symbols in the file.</returns>
    Task<IReadOnlyList<SymbolInfo>> GetSymbolsAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for symbols matching the query using fuzzy matching.
    /// Returns results ordered by relevance.
    /// </summary>
    /// <param name="query">The search query (symbol name or partial match).</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="kind">Optional filter by symbol kind.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching symbols.</returns>
    Task<IReadOnlyList<SymbolInfo>> SearchSymbolsAsync(
        string query,
        int limit = 20,
        SymbolKind? kind = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed information for a specific symbol.
    /// Returns null if the symbol is not found.
    /// </summary>
    /// <param name="fullyQualifiedName">Fully qualified name of the symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Symbol information, or null if not found.</returns>
    Task<SymbolInfo?> GetSymbolAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all references to a symbol across the workspace.
    /// </summary>
    /// <param name="fullyQualifiedName">Fully qualified name of the symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of reference locations.</returns>
    Task<IReadOnlyList<SymbolReference>> FindReferencesAsync(
        string fullyQualifiedName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a symbol (class, method, property, etc.) in the codebase.
/// </summary>
public record SymbolInfo
{
    /// <summary>Simple name of the symbol.</summary>
    public required string Name { get; init; }

    /// <summary>Fully qualified name (e.g., "Namespace.ClassName.MethodName").</summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>Kind of symbol (class, method, property, etc.).</summary>
    public required SymbolKind Kind { get; init; }

    /// <summary>Absolute path to the file containing this symbol.</summary>
    public required string FilePath { get; init; }

    /// <summary>Zero-based line number where the symbol is defined.</summary>
    public required int Line { get; init; }

    /// <summary>Zero-based column number where the symbol is defined.</summary>
    public required int Column { get; init; }

    /// <summary>Signature of the symbol (e.g., method signature with parameters).</summary>
    public string? Signature { get; init; }

    /// <summary>Documentation comment/summary for the symbol.</summary>
    public string? Documentation { get; init; }

    /// <summary>Container/parent symbol (e.g., class containing a method).</summary>
    public string? ContainerName { get; init; }

    /// <summary>Relevance score for search results (0.0 to 1.0).</summary>
    public double RelevanceScore { get; init; }
}

/// <summary>
/// Kind of symbol following LSP conventions.
/// </summary>
public enum SymbolKind
{
    /// <summary>File or module.</summary>
    File,

    /// <summary>Namespace or package.</summary>
    Namespace,

    /// <summary>Class definition.</summary>
    Class,

    /// <summary>Interface definition.</summary>
    Interface,

    /// <summary>Struct definition.</summary>
    Struct,

    /// <summary>Enum definition.</summary>
    Enum,

    /// <summary>Method or function.</summary>
    Method,

    /// <summary>Property.</summary>
    Property,

    /// <summary>Field or member variable.</summary>
    Field,

    /// <summary>Constructor.</summary>
    Constructor,

    /// <summary>Enum member.</summary>
    EnumMember,

    /// <summary>Event.</summary>
    Event,

    /// <summary>Variable.</summary>
    Variable,

    /// <summary>Constant.</summary>
    Constant,

    /// <summary>Type parameter (generic).</summary>
    TypeParameter
}

/// <summary>
/// Represents a reference to a symbol at a specific location.
/// </summary>
public record SymbolReference
{
    /// <summary>Absolute path to the file containing the reference.</summary>
    public required string FilePath { get; init; }

    /// <summary>Zero-based line number of the reference.</summary>
    public required int Line { get; init; }

    /// <summary>Zero-based column number of the reference.</summary>
    public required int Column { get; init; }

    /// <summary>The text content of the line containing the reference.</summary>
    public string? LineContent { get; init; }

    /// <summary>True if this is the definition site, false if it's a usage.</summary>
    public bool IsDefinition { get; init; }
}
