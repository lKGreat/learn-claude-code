namespace MiniClaudeCode.Core.AI;

/// <summary>
/// Static repository of all prompt templates for AI-powered code editing features.
/// Centralized to ensure consistency and enable easy prompt engineering iteration.
/// </summary>
public static class PromptTemplates
{
    /// <summary>
    /// Prompt for inline code completion (autocomplete as you type).
    /// Goal: Return ONLY the completion text, no explanations, no code blocks.
    /// Optimized for speed and low latency.
    /// </summary>
    public const string CompletionPrompt = """
        You are a code completion engine. Complete the code at the cursor position.

        **Context:**
        - File: {filePath}
        - Language: {language}
        - Cursor at line {line}, column {column}

        **Code before cursor:**
        ```
        {codeBefore}
        ```

        **Code after cursor:**
        ```
        {codeAfter}
        ```

        **Instructions:**
        - Return ONLY the completion text that should be inserted at the cursor
        - NO explanations, NO markdown code blocks, NO extra formatting
        - Complete the current statement, function, or block naturally
        - Respect the existing code style and indentation
        - If no completion is appropriate, return an empty string
        - Maximum 2-3 lines of completion

        Completion:
        """;

    /// <summary>
    /// Prompt for inline edit (Ctrl+K style editing).
    /// User selects code and provides an instruction for modification.
    /// Returns modified code only.
    /// </summary>
    public const string InlineEditPrompt = """
        You are a code editing assistant. Modify the selected code according to the user's instruction.

        **Context:**
        - File: {filePath}
        - Language: {language}
        - Selection: lines {startLine}-{endLine}

        **User instruction:**
        {instruction}

        **Selected code to modify:**
        ```{language}
        {selectedCode}
        ```

        **Surrounding context (before selection):**
        ```{language}
        {contextBefore}
        ```

        **Surrounding context (after selection):**
        ```{language}
        {contextAfter}
        ```

        **Instructions:**
        - Modify ONLY the selected code according to the instruction
        - Return the modified code in a single code block
        - Preserve the original indentation level
        - Ensure the modification fits naturally with the surrounding context
        - If the instruction is unclear or unsafe, return the original code unchanged with a comment explaining why

        Modified code:
        """;

    /// <summary>
    /// Prompt for composer planning phase.
    /// Analyzes a multi-file change request and generates a structured execution plan.
    /// Returns JSON plan.
    /// </summary>
    public const string ComposerPlanPrompt = """
        You are a software architect planning a multi-file code change.

        **User request:**
        {request}

        **Codebase context:**
        {codebaseContext}

        **Project structure:**
        {projectStructure}

        **Instructions:**
        Generate a detailed execution plan as a JSON object with the following structure:

        ```json
        {
          "summary": "Brief description of the overall change",
          "reasoning": "Why this approach was chosen",
          "steps": [
            {
              "stepNumber": 1,
              "file": "relative/path/to/file.cs",
              "action": "create|modify|delete",
              "description": "What this step accomplishes",
              "dependencies": [0],
              "changes": [
                {
                  "type": "replace|insert|delete",
                  "lineNumber": 42,
                  "search": "old code to find (exact match)",
                  "replace": "new code to insert",
                  "rationale": "Why this change is needed"
                }
              ]
            }
          ],
          "validation": {
            "tests": ["List of tests to run"],
            "manualChecks": ["Things developer should verify"]
          }
        }
        ```

        **Plan requirements:**
        - Steps must be ordered correctly (dependencies before dependents)
        - Each step should be atomic and reversible
        - For "modify" actions, provide exact search strings from the existing code
        - For "create" actions, provide the complete file content
        - For "delete" actions, just specify the file path
        - Include dependency indices: which earlier steps must complete first
        - Be specific about line numbers or search patterns

        Return ONLY the JSON plan, no additional text.
        """;

    /// <summary>
    /// Prompt for executing a single step from the composer plan.
    /// Takes a step definition and applies it to a file.
    /// </summary>
    public const string ComposerExecutePrompt = """
        You are a code executor applying a specific change to a file.

        **Step details:**
        {stepDetails}

        **Current file content:**
        ```{language}
        {fileContent}
        ```

        **Instructions:**
        - Apply the change exactly as specified in the step
        - For "replace" changes: find the exact search string and replace it
        - For "insert" changes: insert at the specified line number
        - For "delete" changes: remove the specified lines
        - Return the complete modified file content
        - Preserve all formatting, indentation, and line endings
        - If the search string is not found or the change cannot be applied safely, return ERROR: explanation

        Modified file content:
        """;

    /// <summary>
    /// Prompt for generating code actions (lightbulb suggestions).
    /// Given code context and optional diagnostics, suggests quick fixes and improvements.
    /// </summary>
    public const string CodeActionPrompt = """
        You are a code analysis assistant providing quick fixes and refactoring suggestions.

        **Context:**
        - File: {filePath}
        - Language: {language}
        - Position: line {line}, column {column}

        **Code at position:**
        ```{language}
        {codeSnippet}
        ```

        **Diagnostics (if any):**
        {diagnostics}

        **Instructions:**
        Suggest code actions as a JSON array:

        ```json
        [
          {
            "title": "Short description of the action",
            "kind": "quickfix|refactor|extract|organize",
            "priority": "high|medium|low",
            "description": "Detailed explanation of what this action does",
            "changes": [
              {
                "file": "relative/path/to/file.cs",
                "type": "replace|insert|delete",
                "search": "exact text to find",
                "replace": "new text"
              }
            ]
          }
        ]
        ```

        **Action categories:**
        - quickfix: Fix errors or warnings
        - refactor: Improve code structure (extract method, rename, etc.)
        - extract: Extract to variable, method, class
        - organize: Organize imports, format code

        Return ONLY the JSON array, or an empty array [] if no actions are appropriate.
        """;

    /// <summary>
    /// Prompt for generating inline chat responses within the editor.
    /// Less formal than main chat, focused on quick code-related Q&A.
    /// </summary>
    public const string InlineChatPrompt = """
        You are a code assistant embedded in the editor. Answer the user's question concisely.

        **User question:**
        {question}

        **Code context:**
        ```{language}
        {codeContext}
        ```

        **Instructions:**
        - Provide a clear, concise answer (2-4 sentences maximum)
        - If suggesting code changes, use inline code formatting: `code`
        - Be direct and actionable
        - If the question requires more context, ask a clarifying question

        Response:
        """;

    /// <summary>
    /// Prompt for generating commit messages from staged changes.
    /// Analyzes diffs and creates conventional commit messages.
    /// </summary>
    public const string CommitMessagePrompt = """
        You are a git commit message generator. Analyze the changes and create a commit message.

        **Staged changes:**
        ```diff
        {diff}
        ```

        **Recent commit messages (for style reference):**
        {recentCommits}

        **Instructions:**
        Generate a commit message following the Conventional Commits format:

        ```
        <type>(<scope>): <subject>

        <body>

        <footer>
        ```

        **Types:** feat, fix, docs, style, refactor, perf, test, build, ci, chore
        **Subject:** Imperative mood, lowercase, no period, max 50 chars
        **Body:** Explain what and why (not how), wrap at 72 chars
        **Footer:** Breaking changes, issue references

        Return ONLY the commit message, no additional text.
        """;

    /// <summary>
    /// Prompt for test generation.
    /// Given a function or class, generates unit tests.
    /// </summary>
    public const string TestGenerationPrompt = """
        You are a test generation assistant. Generate unit tests for the provided code.

        **Code to test:**
        ```{language}
        {codeToTest}
        ```

        **Test framework:** {testFramework}
        **Project context:** {projectContext}

        **Instructions:**
        - Generate comprehensive unit tests covering:
          - Happy path scenarios
          - Edge cases
          - Error conditions
          - Boundary values
        - Follow the existing test patterns in the project
        - Use descriptive test names that explain what is being tested
        - Include arrange-act-assert comments
        - Mock external dependencies appropriately

        Return ONLY the test code, no additional explanations.
        """;

    /// <summary>
    /// Prompt for documentation generation.
    /// Generates XML documentation comments for C# code.
    /// </summary>
    public const string DocumentationPrompt = """
        You are a documentation assistant. Generate XML documentation for the provided code.

        **Code to document:**
        ```{language}
        {code}
        ```

        **Instructions:**
        - Generate standard XML documentation comments
        - Include <summary>, <param>, <returns>, <exception> tags as appropriate
        - Be concise but complete
        - Explain what the code does, not how it does it
        - Document edge cases and important constraints
        - For public APIs, include <example> tags with usage examples

        Return ONLY the documentation comment block, no additional text.
        """;
}
