using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace MiniClaudeCode.Plugins;

/// <summary>
/// Web tools: search the internet and fetch URL content.
/// Mirrors Cursor's "Web" and "Fetch" capabilities.
/// </summary>
public partial class WebPlugin
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private const int MaxOutputBytes = 50_000;

    static WebPlugin()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; MiniClaudeCode/1.0)");
        Http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,*/*");
    }

    // =========================================================================
    // WebSearch - Search the internet via DuckDuckGo
    // =========================================================================

    [KernelFunction("web_search")]
    [Description(@"Search the web for real-time information. Returns titles, URLs, and snippets from search results.
Use when you need up-to-date information, library docs, current events, or answers to factual questions.")]
    public async Task<string> WebSearch(
        [Description("Search query (be specific for better results)")] string query)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"  [web] Searching: {query}");
        Console.ResetColor();

        try
        {
            // Use DuckDuckGo HTML endpoint (no API key needed)
            var encodedQuery = WebUtility.UrlEncode(query);
            var url = $"https://html.duckduckgo.com/html/?q={encodedQuery}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["q"] = query,
                ["b"] = ""
            });

            var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            // Parse search results from DuckDuckGo HTML
            var results = ParseDuckDuckGoResults(html);

            if (results.Count == 0)
                return $"No search results found for: {query}";

            var sb = new StringBuilder();
            sb.AppendLine($"Search results for: {query}\n");

            for (int i = 0; i < Math.Min(results.Count, 8); i++)
            {
                var r = results[i];
                sb.AppendLine($"{i + 1}. {r.Title}");
                sb.AppendLine($"   URL: {r.Url}");
                if (!string.IsNullOrEmpty(r.Snippet))
                    sb.AppendLine($"   {r.Snippet}");
                sb.AppendLine();
            }

            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine($"  [web] Found {results.Count} results");
            Console.ResetColor();

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching the web: {ex.Message}";
        }
    }

    /// <summary>
    /// Parse DuckDuckGo HTML search results.
    /// </summary>
    private static List<SearchResult> ParseDuckDuckGoResults(string html)
    {
        var results = new List<SearchResult>();

        // Match result links and snippets from DuckDuckGo HTML
        var resultBlocks = ResultBlockRegex().Matches(html);

        foreach (Match block in resultBlocks)
        {
            var blockHtml = block.Groups[1].Value;

            // Extract URL
            var urlMatch = ResultUrlRegex().Match(blockHtml);
            var url = urlMatch.Success ? WebUtility.HtmlDecode(urlMatch.Groups[1].Value) : "";

            // Extract title
            var titleMatch = ResultTitleRegex().Match(blockHtml);
            var title = titleMatch.Success ? StripHtmlTags(WebUtility.HtmlDecode(titleMatch.Groups[1].Value)).Trim() : "";

            // Extract snippet
            var snippetMatch = SnippetRegex().Match(blockHtml);
            var snippet = snippetMatch.Success ? StripHtmlTags(WebUtility.HtmlDecode(snippetMatch.Groups[1].Value)).Trim() : "";

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
            {
                // DuckDuckGo wraps URLs in a redirect - extract the actual URL
                if (url.Contains("uddg="))
                {
                    var uddgMatch = UddgRegex().Match(url);
                    if (uddgMatch.Success)
                        url = WebUtility.UrlDecode(uddgMatch.Groups[1].Value);
                }

                results.Add(new SearchResult(title, url, snippet));
            }
        }

        return results;
    }

    private record SearchResult(string Title, string Url, string Snippet);

    [GeneratedRegex(@"<div class=""result[^""]*""[^>]*>(.*?)</div>\s*</div>", RegexOptions.Singleline)]
    private static partial Regex ResultBlockRegex();

    [GeneratedRegex(@"<a[^>]+href=""([^""]+)""[^>]*class=""result__a""", RegexOptions.Singleline)]
    private static partial Regex ResultUrlRegex();

    [GeneratedRegex(@"class=""result__a""[^>]*>(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex ResultTitleRegex();

    [GeneratedRegex(@"class=""result__snippet""[^>]*>(.*?)</", RegexOptions.Singleline)]
    private static partial Regex SnippetRegex();

    [GeneratedRegex(@"uddg=([^&]+)")]
    private static partial Regex UddgRegex();

    // =========================================================================
    // WebFetch - Fetch URL content
    // =========================================================================

    [KernelFunction("web_fetch")]
    [Description(@"Fetch content from a URL and return it as readable text. HTML is stripped to plain text.
Use to read documentation pages, API references, blog posts, etc.")]
    public async Task<string> WebFetch(
        [Description("The URL to fetch")] string url)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"  [web] Fetching: {url}");
        Console.ResetColor();

        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
                return "Error: Invalid URL. Must be http or https.";

            var response = await Http.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var rawContent = await response.Content.ReadAsStringAsync();

            string text;
            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                text = HtmlToText(rawContent);
            }
            else if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                     contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
                     contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                text = rawContent;
            }
            else
            {
                return $"Error: Unsupported content type: {contentType}";
            }

            if (text.Length > MaxOutputBytes)
                text = text[..MaxOutputBytes] + $"\n\n... (truncated, {rawContent.Length} total chars)";

            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine($"  [web] Fetched {text.Length} chars from {uri.Host}");
            Console.ResetColor();

            return $"Content from {url}:\n\n{text}";
        }
        catch (TaskCanceledException)
        {
            return $"Error: Request timed out fetching {url}";
        }
        catch (Exception ex)
        {
            return $"Error fetching URL: {ex.Message}";
        }
    }

    // =========================================================================
    // HTML Processing Helpers
    // =========================================================================

    /// <summary>
    /// Convert HTML to readable plain text.
    /// Strips tags, decodes entities, collapses whitespace.
    /// </summary>
    private static string HtmlToText(string html)
    {
        // Remove script and style blocks entirely
        html = ScriptStyleRegex().Replace(html, " ");

        // Remove head section
        html = HeadRegex().Replace(html, "");

        // Add newlines for block elements
        html = BlockElementRegex().Replace(html, "\n");

        // Add newlines for <br> tags
        html = BrTagRegex().Replace(html, "\n");

        // Strip all remaining HTML tags
        html = StripHtmlTags(html);

        // Decode HTML entities
        html = WebUtility.HtmlDecode(html);

        // Collapse multiple whitespace to single space/newline
        html = MultipleSpacesRegex().Replace(html, " ");
        html = MultipleNewlinesRegex().Replace(html, "\n\n");

        return html.Trim();
    }

    private static string StripHtmlTags(string html)
    {
        return HtmlTagRegex().Replace(html, "");
    }

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<head[^>]*>.*?</head>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeadRegex();

    [GeneratedRegex(@"</(p|div|h[1-6]|li|tr|section|article|header|footer|nav|main)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockElementRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultipleSpacesRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();
}
