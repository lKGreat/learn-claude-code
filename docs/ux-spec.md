# MiniClaudeCode AI-Native Editor Features
## UI/UX Interaction Specification

**Version:** 1.0
**Date:** 2026-02-12
**Platform:** Avalonia 11.3 (Windows, macOS, Linux)
**Theme:** Catppuccin Mocha + Semi.Avalonia Dark
**Editor:** AvaloniaEdit

---

## Table of Contents

1. [Design System Foundation](#1-design-system-foundation)
2. [Ghost Text (Inline AI Completion)](#2-ghost-text-inline-ai-completion)
3. [Inline Edit Widget (Ctrl+K / Cmd+K)](#3-inline-edit-widget-ctrlk--cmdk)
4. [@ Mention Picker (in Chat)](#4--mention-picker-in-chat)
5. [Composer Panel](#5-composer-panel)
6. [Enhanced Chat Input](#6-enhanced-chat-input)
7. [Diff Preview (AI Edits)](#7-diff-preview-ai-edits)
8. [Accessibility & Keyboard Navigation](#8-accessibility--keyboard-navigation)
9. [Implementation Notes](#9-implementation-notes)

---

## 1. Design System Foundation

### 1.1 Catppuccin Mocha Color Tokens

Define these as application-wide design tokens (resources or constants):

```xml
<!-- Core Background & Surface -->
<Color x:Key="ColorBase">#1E1E2E</Color>           <!-- Main background -->
<Color x:Key="ColorMantle">#181825</Color>         <!-- Deeper background -->
<Color x:Key="ColorCrust">#11111B</Color>          <!-- Deepest background -->

<!-- Surface Elevations -->
<Color x:Key="ColorSurface0">#313244</Color>       <!-- Elevated surface 1 -->
<Color x:Key="ColorSurface1">#45475A</Color>       <!-- Elevated surface 2 -->
<Color x:Key="ColorSurface2">#585B70</Color>       <!-- Elevated surface 3 -->

<!-- Overlay & Subtle -->
<Color x:Key="ColorOverlay0">#6C7086</Color>       <!-- Subtle text/borders -->
<Color x:Key="ColorOverlay1">#7F849C</Color>       <!-- Muted elements -->
<Color x:Key="ColorOverlay2">#9399B2</Color>       <!-- Disabled elements -->

<!-- Text Colors -->
<Color x:Key="ColorText">#CDD6F4</Color>           <!-- Primary text -->
<Color x:Key="ColorSubtext1">#BAC2DE</Color>       <!-- Secondary text -->
<Color x:Key="ColorSubtext0">#A6ADC8</Color>       <!-- Tertiary text -->

<!-- Accent Colors -->
<Color x:Key="ColorBlue">#89B4FA</Color>           <!-- Primary accent -->
<Color x:Key="ColorGreen">#A6E3A1</Color>          <!-- Success -->
<Color x:Key="ColorRed">#F38BA8</Color>            <!-- Error/Delete -->
<Color x:Key="ColorYellow">#F9E2AF</Color>         <!-- Warning -->
<Color x:Key="ColorMauve">#CBA6F7</Color>          <!-- Secondary accent -->
<Color x:Key="ColorPeach">#FAB387</Color>          <!-- Modify/Change -->
<Color x:Key="ColorTeal">#94E2D5</Color>           <!-- Info -->
<Color x:Key="ColorSky">#89DCEB</Color>            <!-- Highlight -->
```

### 1.2 Semantic Token Mapping

Map Catppuccin colors to semantic meanings:

| Semantic Token | Catppuccin Color | Hex Value | Usage |
|----------------|------------------|-----------|-------|
| `--bg-primary` | Base | `#1E1E2E` | Editor background, main panels |
| `--bg-secondary` | Mantle | `#181825` | Sidebar, tab bar |
| `--bg-tertiary` | Crust | `#11111B` | Input fields, deep insets |
| `--surface-elevated` | Surface0 | `#313244` | Cards, popups, tooltips |
| `--surface-hover` | Surface1 | `#45475A` | Hover states |
| `--surface-active` | Surface2 | `#585B70` | Active/pressed states |
| `--border-subtle` | Surface0 | `#313244` | Dividers, borders |
| `--border-default` | Overlay0 | `#6C7086` | Input borders, focus outlines |
| `--text-primary` | Text | `#CDD6F4` | Body text, headings |
| `--text-secondary` | Subtext1 | `#BAC2DE` | Labels, secondary info |
| `--text-tertiary` | Subtext0 | `#A6ADC8` | Placeholders, hints |
| `--text-ghost` | Overlay0 | `#6C7086` | Ghost text (60% opacity) |
| `--accent-primary` | Blue | `#89B4FA` | Links, primary actions |
| `--accent-success` | Green | `#A6E3A1` | Success states, additions |
| `--accent-error` | Red | `#F38BA8` | Errors, deletions |
| `--accent-warning` | Yellow | `#F9E2AF` | Warnings, caution |
| `--accent-modify` | Peach | `#FAB387` | Modifications |

### 1.3 Typography System

```
Font Families:
- UI Text: System default (Segoe UI on Windows, SF Pro on macOS, Ubuntu on Linux)
- Code/Monospace: "Cascadia Code, Consolas, Menlo, monospace"

Font Sizes (px):
- 10px: Fine print, hints, badges
- 11px: Secondary labels, breadcrumbs
- 12px: UI labels, tree items, tabs
- 13px: Body text, editor text, input text
- 14px: Icons (emoji/Unicode glyphs)
- 28px: Welcome screen title

Font Weights:
- Light (300): Welcome screen
- Regular (400): Body text
- SemiBold (600): Buttons, emphasis
- Bold (700): Section headers, role labels

Line Heights:
- 1.2: Compact (tabs, tree items)
- 1.4: Default (body text)
- 1.6: Relaxed (editor text)
```

### 1.4 Spacing System (4px Grid)

```
0: 0px      (no spacing)
1: 4px      (tight spacing, minimal padding)
2: 8px      (standard item spacing)
3: 12px     (section spacing)
4: 16px     (component padding)
5: 20px     (large gaps)
6: 24px     (section padding)
8: 32px     (major sections)
10: 40px    (large components)
12: 48px    (welcome screen elements)
16: 64px    (layout margins)
```

### 1.5 Border Radius

```
- 0px: None (tabs, window edges)
- 4px: Small (badges, chips, tree items)
- 6px: Medium (inputs, buttons, cards)
- 8px: Large (floating panels, dialogs)
- 12px: Extra large (modal dialogs)
```

### 1.6 Shadow Elevation Levels

```xml
<!-- Level 0: No shadow -->
<BoxShadow x:Key="ShadowNone">0 0 0 0 #00000000</BoxShadow>

<!-- Level 1: Subtle (tooltips, dropdowns) -->
<BoxShadow x:Key="ShadowSubtle">0 2 8 0 #00000040</BoxShadow>

<!-- Level 2: Card (floating widgets) -->
<BoxShadow x:Key="ShadowCard">0 4 16 0 #00000060</BoxShadow>

<!-- Level 3: Modal (dialogs) -->
<BoxShadow x:Key="ShadowModal">0 8 32 0 #00000080</BoxShadow>
```

### 1.7 Animation Timings

```
Duration:
- Instant: 0ms (no animation)
- Fast: 150ms (small state changes, hover)
- Default: 200ms (most transitions)
- Slow: 300ms (panel slides, complex transitions)
- Very slow: 500ms (major layout changes)

Easing:
- ease-out: Most UI transitions (element appearing)
- ease-in: Elements disappearing
- ease-in-out: Continuous animations, pulsing
- linear: Progress bars, spinners
```

---

## 2. Ghost Text (Inline AI Completion)

### 2.1 Feature Overview

**Purpose:** Provide non-intrusive AI code completion suggestions directly in the editor at the cursor position, similar to GitHub Copilot.

**Activation Context:**
- User must be editing a code file (not in read-only mode)
- Cursor is at a position where code can be inserted
- AI completion feature is enabled in settings

### 2.2 Trigger Logic

```
State Machine:
IDLE → (user types) → TYPING
TYPING → (300ms no input) → DEBOUNCING
DEBOUNCING → (700ms no input) → REQUESTING_COMPLETION
REQUESTING_COMPLETION → (AI response) → SHOWING_GHOST
SHOWING_GHOST → (user action) → ACCEPTING / DISMISSING → IDLE

Configurable Debounce:
- Default: 1000ms (1 second total pause)
- Settings range: 500ms - 3000ms
- Split: 300ms typing cooldown + 700ms debounce
```

**Debounce Calculation:**
1. User stops typing
2. Wait 300ms (typing cooldown) - ensures user has paused, not just between keystrokes
3. Wait additional 700ms (debounce period) - gather context, prepare request
4. If no new input during entire 1000ms: trigger AI completion request

### 2.3 Visual Specification

#### 2.3.1 Ghost Text Appearance

```
Text Style:
- Font: Same as editor font (Cascadia Code, etc.)
- Font Size: Same as editor (13px default)
- Font Style: Italic
- Color: ColorOverlay0 (#6C7086)
- Opacity: 60% (0.6)
- Effective Color: #6C7086 at 60% opacity = ~#4A4F5F perceived
- Background: None (transparent)

Position:
- Inline at cursor position
- Follows existing indentation
- Multi-line completions maintain proper indentation

Example Rendering:
[Normal Code]if (condition) {
[Normal Code]    console.log("test");
[Cursor]|[Ghost Text in italic]    return true;
[Ghost Text in italic]}
```

#### 2.3.2 Loading State

When AI is thinking (request sent, no response yet):

```xml
<Border Classes="ghostLoadingIndicator"
        Width="16" Height="16"
        Margin="0,0,4,0"
        HorizontalAlignment="Left"
        VerticalAlignment="Center">
    <TextBlock Text="⟳"
               FontSize="14"
               Foreground="{StaticResource ColorBlue}"
               Opacity="0.6"
               VerticalAlignment="Center">
        <!-- Rotation animation -->
        <TextBlock.RenderTransform>
            <RotateTransform />
        </TextBlock.RenderTransform>
        <TextBlock.Styles>
            <Style Selector="TextBlock">
                <Style.Animations>
                    <Animation Duration="0:0:1.2"
                               IterationCount="INFINITE">
                        <KeyFrame Cue="0%">
                            <Setter Property="RenderTransform.Angle" Value="0" />
                        </KeyFrame>
                        <KeyFrame Cue="100%">
                            <Setter Property="RenderTransform.Angle" Value="360" />
                        </KeyFrame>
                    </Animation>
                </Style.Animations>
            </Style>
        </TextBlock.Styles>
    </TextBlock>
</Border>
```

**Loading Indicator Placement:**
- Appears in the editor gutter (line number area)
- Positioned on the current line where cursor is
- Spinner icon: ⟳ (Unicode U+27F3)
- Color: Blue (#89B4FA) at 60% opacity
- Animation: 360° clockwise rotation, 1.2s duration, infinite loop, linear easing

#### 2.3.3 Fade-in Animation

```xml
<Style Selector="TextBlock.ghostText">
    <Setter Property="Opacity" Value="0" />
    <Style.Animations>
        <Animation Duration="0:0:0.2" Easing="CubicEaseOut">
            <KeyFrame Cue="0%">
                <Setter Property="Opacity" Value="0" />
            </KeyFrame>
            <KeyFrame Cue="100%">
                <Setter Property="Opacity" Value="0.6" />
            </KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

Duration: 200ms
Easing: CubicEaseOut (ease-out curve)
From: Opacity 0 (invisible)
To: Opacity 0.6 (60% visible)

### 2.4 Interaction Behavior

#### 2.4.1 Keyboard Shortcuts

| Key | Action | Behavior |
|-----|--------|----------|
| **Tab** | Accept Completion | Insert full ghost text at cursor, move cursor to end of inserted text, dismiss ghost text |
| **Esc** | Dismiss | Remove ghost text, no insertion, cursor stays in place |
| **Any typing** | Dismiss & Recalculate | Remove current ghost text, reset debounce timer, start new completion cycle |
| **Arrow keys** (↑↓←→) | Dismiss | Remove ghost text, move cursor normally |
| **Delete/Backspace** | Dismiss & Recalculate | Same as typing - remove ghost text, reset timer |
| **Ctrl+Space** | Manual Trigger | Force immediate completion request (bypass debounce) |

#### 2.4.2 State Transitions

```
User Action Flow:

[TYPING]
  ↓ (stops typing for 1s)
[REQUESTING] → Loading spinner appears in gutter
  ↓ (AI responds)
[SHOWING_GHOST] → Ghost text fades in
  ↓ (user presses Tab)
[ACCEPTING]
  → Insert text into document
  → Move cursor to end of insertion
  → Clear ghost text
  → Return to IDLE

[SHOWING_GHOST] → (user presses Esc)
[DISMISSING]
  → Clear ghost text immediately (no animation)
  → Return to IDLE

[SHOWING_GHOST] → (user types any character)
[DISMISSING + TYPING]
  → Clear ghost text
  → Insert typed character
  → Reset debounce timer
  → Start new completion cycle
```

#### 2.4.3 Multi-line Completions

When ghost text spans multiple lines:

```
Visual Rendering:
Line 1: if (condition) {|← cursor here
Line 2: [GHOST]    return calculateResult(
Line 3: [GHOST]        param1,
Line 4: [GHOST]        param2
Line 5: [GHOST]    );
Line 6: [GHOST]}

Acceptance (Tab):
- Insert all lines with proper indentation
- Cursor moves to end of last line (after "}")
- Maintain existing code below without shifting

Indentation Rules:
- Match current editor indentation settings (tabs vs spaces, width)
- Respect EditorConfig if present
- Ghost text lines maintain relative indentation to first line
```

### 2.5 Edge Cases & Error Handling

#### 2.5.1 Context-Specific Behavior

| Context | Behavior |
|---------|----------|
| **End of file** | Ghost text appears normally, cursor moves to new end-of-file after acceptance |
| **Inside string literals** | Show completions for string content, no syntax highlighting in ghost text |
| **Inside comments** | Show completions for comment text, maintain comment syntax in ghost text |
| **Read-only file** | No ghost text shown, feature disabled |
| **Multi-cursor editing** | Ghost text shown only at primary cursor (first cursor) |
| **Selected text exists** | Ghost text hidden until selection is cleared |

#### 2.5.2 Error States

| Error Condition | User-Visible Behavior | Recovery |
|-----------------|----------------------|----------|
| **AI request timeout** (>10s) | Loading spinner disappears, no ghost text shown | User can manually trigger with Ctrl+Space |
| **AI returns empty completion** | No ghost text shown, silent failure | Next debounce cycle retries |
| **Network error** | Toast notification: "AI completion unavailable" (auto-dismiss 3s) | Retry on next pause |
| **Rate limit exceeded** | Toast notification: "Completion rate limit reached, paused for 1min" | Feature auto-resumes after cooldown |
| **Invalid/malformed response** | No ghost text, log error silently | Retry on next cycle |

#### 2.5.3 Performance Considerations

```
Throttling:
- Max 1 request per 1 second per file
- Max 10 concurrent requests across all open files
- Request queue: FIFO, max 3 queued requests per file

Cancellation:
- Cancel in-flight request if user types before response arrives
- Cancel if cursor moves more than 5 lines away from request position
- Cancel if file is closed

Context Gathering:
- Send 50 lines before cursor (max 2KB)
- Send 10 lines after cursor (max 500B)
- Include file type, language mode, project context
- Total context payload: <5KB per request
```

### 2.6 Settings & Configuration

Expose these settings in the Settings panel:

```
[AI Completions]
☑ Enable inline completions
  Debounce delay: [1000] ms (500-3000)
  Context before cursor: [50] lines (10-200)
  Context after cursor: [10] lines (5-50)
  ☑ Show loading indicator
  ☑ Multi-line completions
  ☐ Auto-accept on End key press
```

---

## 3. Inline Edit Widget (Ctrl+K / Cmd+K)

### 3.1 Feature Overview

**Purpose:** Allow users to describe natural language edits to selected code, see a diff preview, and accept/reject changes inline in the editor.

**Activation:**
- Select one or more lines of code
- Press Ctrl+K (Windows/Linux) or Cmd+K (macOS)
- Widget appears below the selection

### 3.2 Trigger & Placement

#### 3.2.1 Selection Requirements

```
Valid Selections:
- Minimum: 1 character
- Maximum: 5000 characters (configurable)
- Can span multiple lines
- Can include mixed content (code, comments, whitespace)

Invalid Selections:
- Empty selection (no text) → Show tooltip "Select code first"
- Selection in read-only file → Show tooltip "File is read-only"
- Selection during active AI operation → Queue request
```

#### 3.2.2 Widget Positioning Logic

```
Vertical Position:
1. Calculate bottom edge of selection (last selected line)
2. Position widget 8px below the selection's bottom line
3. If widget would overflow below viewport:
   - Try positioning above selection (8px above first line)
   - If still not enough space: anchor to bottom of viewport, allow scrolling

Horizontal Position:
- Left-align with the editor content area (not line numbers)
- Minimum margin: 16px from editor edges
- Maximum width: 600px
- Center if editor width > 800px

Z-Index:
- Layer above editor content, below modal dialogs
- Dim editor background slightly (overlay with 10% black: #00000019)
```

### 3.3 Visual Specification

#### 3.3.1 Widget Structure

```
┌─────────────────────────────────────────────────────┐
│ [Icon] Describe the change...                   [✓][✗]│ ← Header
├─────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────┐ │
│ │ [User input textbox]                            │ │ ← Input Area
│ │ Multi-line, auto-height (min 1, max 4 lines)   │ │
│ └─────────────────────────────────────────────────┘ │
│ [Esc] Cancel • [Enter] Submit • [Ctrl+Enter] Accept │ ← Footer hints
└─────────────────────────────────────────────────────┘
         ↑
    Widget appears here after selection
```

#### 3.3.2 XAML Structure

```xml
<Border Classes="inlineEditWidget"
        Background="{StaticResource ColorBase}"
        BorderBrush="{StaticResource ColorSurface1}"
        BorderThickness="1"
        CornerRadius="8"
        Padding="12"
        BoxShadow="{StaticResource ShadowCard}">
    <DockPanel>
        <!-- Header: Icon + Close buttons -->
        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
            <StackPanel DockPanel.Dock="Right"
                        Orientation="Horizontal"
                        Spacing="4">
                <!-- Accept button (only visible after diff) -->
                <Button Classes="inlineEditAction"
                        Content="✓"
                        Background="{StaticResource ColorGreen}"
                        Foreground="{StaticResource ColorBase}"
                        IsVisible="{Binding HasDiff}"
                        Command="{Binding AcceptCommand}"
                        ToolTip.Tip="Accept (Ctrl+Enter)"
                        Width="28" Height="28"
                        CornerRadius="4"
                        Padding="0" />

                <!-- Reject button (always visible) -->
                <Button Classes="inlineEditAction"
                        Content="✗"
                        Background="{StaticResource ColorRed}"
                        Foreground="{StaticResource ColorBase}"
                        Command="{Binding RejectCommand}"
                        ToolTip.Tip="Reject (Esc)"
                        Width="28" Height="28"
                        CornerRadius="4"
                        Padding="0" />
            </StackPanel>

            <!-- Icon + Title -->
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="✨"
                           FontSize="16"
                           VerticalAlignment="Center" />
                <TextBlock Text="AI Edit"
                           FontSize="13"
                           FontWeight="SemiBold"
                           Foreground="{StaticResource ColorText}"
                           VerticalAlignment="Center" />
            </StackPanel>
        </DockPanel>

        <!-- Footer: Hints -->
        <TextBlock DockPanel.Dock="Bottom"
                   Margin="0,8,0,0"
                   FontSize="10"
                   Foreground="{StaticResource ColorSubtext0}"
                   Opacity="0.6"
                   Text="Esc Cancel • Enter Submit • Ctrl+Enter Accept" />

        <!-- Input Area -->
        <TextBox Classes="inlineEditInput"
                 Watermark="Describe the change..."
                 Text="{Binding EditInstruction, Mode=TwoWay}"
                 AcceptsReturn="True"
                 TextWrapping="Wrap"
                 MinHeight="32"
                 MaxHeight="96"
                 Background="{StaticResource ColorCrust}"
                 BorderBrush="{StaticResource ColorSurface0}"
                 BorderThickness="1"
                 CornerRadius="6"
                 Padding="8"
                 FontSize="13"
                 FontFamily="Cascadia Code, Consolas, Menlo, monospace"
                 KeyDown="OnInputKeyDown" />
    </DockPanel>
</Border>
```

#### 3.3.3 Loading State (During AI Processing)

```xml
<!-- Replace input textbox with loading indicator -->
<Border Background="{StaticResource ColorCrust}"
        BorderBrush="{StaticResource ColorSurface0}"
        BorderThickness="1"
        CornerRadius="6"
        Padding="12"
        MinHeight="40">
    <StackPanel Orientation="Horizontal"
                Spacing="8"
                HorizontalAlignment="Center"
                VerticalAlignment="Center">
        <!-- Spinner -->
        <TextBlock Text="⟳"
                   FontSize="16"
                   Foreground="{StaticResource ColorBlue}">
            <TextBlock.RenderTransform>
                <RotateTransform />
            </TextBlock.RenderTransform>
            <TextBlock.Styles>
                <Style Selector="TextBlock">
                    <Style.Animations>
                        <Animation Duration="0:0:1.2" IterationCount="INFINITE">
                            <KeyFrame Cue="0%">
                                <Setter Property="RenderTransform.Angle" Value="0" />
                            </KeyFrame>
                            <KeyFrame Cue="100%">
                                <Setter Property="RenderTransform.Angle" Value="360" />
                            </KeyFrame>
                        </Animation>
                    </Style.Animations>
                </Style>
            </TextBlock.Styles>
        </TextBlock>

        <!-- Progress text -->
        <TextBlock Text="Generating edit..."
                   FontSize="12"
                   Foreground="{StaticResource ColorSubtext1}"
                   VerticalAlignment="Center" />
    </StackPanel>
</Border>
```

### 3.4 Diff Preview

#### 3.4.1 Inline Diff Rendering

After AI responds, replace selected text in the editor with a side-by-side or unified diff view:

```
Unified Diff (Preferred for small changes):
  1 │ function calculateTotal(items) {
- 2 │     let sum = 0;                      ← Red background (deletion)
- 3 │     for (let i = 0; i < items.length; i++) {
- 4 │         sum += items[i].price;
- 5 │     }
+ 2 │     return items.reduce(              ← Green background (addition)
+ 3 │         (sum, item) => sum + item.price,
+ 4 │         0
+ 5 │     );
  6 │ }

Color Coding:
- Deletion background: ColorRed (#F38BA8) at 15% opacity = #F38BA826
- Addition background: ColorGreen (#A6E3A1) at 15% opacity = #A6E3A126
- Deletion text: ColorRed (#F38BA8) at 100%
- Addition text: ColorGreen (#A6E3A1) at 100%
- Unchanged lines: Normal editor colors
```

#### 3.4.2 AvaloniaEdit Integration

```csharp
// Create custom TextMarker for diff highlighting
public class DiffMarker : IBackgroundRenderer
{
    public DiffMarkerType Type { get; set; } // Addition, Deletion, Unchanged
    public int StartOffset { get; set; }
    public int Length { get; set; }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var backgroundColor = Type switch
        {
            DiffMarkerType.Addition => Color.FromArgb(38, 166, 227, 161), // #A6E3A126
            DiffMarkerType.Deletion => Color.FromArgb(38, 243, 139, 168), // #F38BA826
            _ => Colors.Transparent
        };

        // Draw background rectangle for the line
        var geometry = BackgroundGeometryBuilder.GetRectsForSegment(
            textView, new TextSegment { StartOffset = StartOffset, Length = Length }
        );
        drawingContext.DrawGeometry(
            new SolidColorBrush(backgroundColor),
            null,
            geometry
        );
    }
}
```

#### 3.4.3 Diff Navigation Controls

If diff contains multiple change hunks (separated unchanged blocks), show navigation:

```xml
<!-- Appears above the diff area -->
<Border Background="{StaticResource ColorSurface0}"
        BorderRadius="4"
        Padding="6,4"
        Margin="0,0,0,4">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock Text="Changes:"
                   FontSize="11"
                   Foreground="{StaticResource ColorSubtext1}"
                   VerticalAlignment="Center" />

        <!-- Previous change -->
        <Button Content="↑"
                Classes="diffNav"
                Command="{Binding PreviousChangeCommand}"
                ToolTip.Tip="Previous change (Alt+F5)"
                Width="24" Height="24"
                Padding="0"
                FontSize="12" />

        <!-- Change counter -->
        <TextBlock FontSize="11"
                   Foreground="{StaticResource ColorSubtext0}">
            <Run Text="{Binding CurrentChangeIndex}" />
            <Run Text="/" />
            <Run Text="{Binding TotalChanges}" />
        </TextBlock>

        <!-- Next change -->
        <Button Content="↓"
                Classes="diffNav"
                Command="{Binding NextChangeCommand}"
                ToolTip.Tip="Next change (F5)"
                Width="24" Height="24"
                Padding="0"
                FontSize="12" />
    </StackPanel>
</Border>
```

### 3.5 Interaction Behavior

#### 3.5.1 Keyboard Shortcuts

| Key | Context | Action |
|-----|---------|--------|
| **Ctrl+K / Cmd+K** | Selection exists | Show inline edit widget |
| **Enter** | Input focused | Submit edit instruction to AI |
| **Shift+Enter** | Input focused | New line in textbox |
| **Esc** | Widget visible | Reject/cancel, close widget, restore original |
| **Ctrl+Enter** | Diff preview shown | Accept changes, apply to document |
| **Tab** | Input focused | Focus next interactive element |
| **Shift+Tab** | Input focused | Focus previous element |
| **F5** | Diff preview | Navigate to next change hunk |
| **Alt+F5** | Diff preview | Navigate to previous change hunk |

#### 3.5.2 State Flow

```
State Machine:

[IDLE]
  ↓ (User selects code, presses Ctrl+K)
[WIDGET_OPEN]
  → Show widget below selection
  → Focus input textbox
  → Dim editor background

[WIDGET_OPEN]
  ↓ (User types instruction, presses Enter)
[PROCESSING]
  → Show loading spinner in widget
  → Disable input
  → Send request to AI with context:
    - Selected text
    - User instruction
    - File language/type
    - Surrounding context (20 lines before/after)

[PROCESSING]
  ↓ (AI responds with edited code)
[DIFF_PREVIEW]
  → Render diff in editor (inline, unified style)
  → Highlight deletions (red bg) and additions (green bg)
  → Show Accept (✓) and Reject (✗) buttons
  → Show Retry (↻) button to re-edit

[DIFF_PREVIEW]
  ↓ (User clicks Accept or Ctrl+Enter)
[ACCEPTING]
  → Apply changes to document
  → Close widget with fade-out (150ms)
  → Remove dim overlay
  → Return to IDLE

[DIFF_PREVIEW]
  ↓ (User clicks Reject or Esc)
[REJECTING]
  → Restore original selected text
  → Close widget immediately
  → Remove dim overlay
  → Return to IDLE

[DIFF_PREVIEW]
  ↓ (User clicks Retry ↻)
[WIDGET_OPEN]
  → Keep selection
  → Clear input textbox
  → Focus input
  → User can type new instruction
```

#### 3.5.3 Retry Mechanism

```xml
<!-- Retry button appears after diff is shown -->
<Button Classes="inlineEditAction"
        Content="↻"
        Background="{StaticResource ColorSurface1}"
        Foreground="{StaticResource ColorBlue}"
        Command="{Binding RetryCommand}"
        ToolTip.Tip="Retry with new instruction"
        Width="28" Height="28"
        CornerRadius="4"
        Padding="0"
        Margin="4,0,0,0" />
```

**Retry Behavior:**
1. User clicks Retry (↻)
2. Restore original selection (undo diff)
3. Keep widget open
4. Clear input textbox
5. Re-focus input
6. User enters new instruction
7. Press Enter to submit again

### 3.6 Edge Cases & Error Handling

| Scenario | Behavior |
|----------|----------|
| **AI returns unchanged text** | Show notification: "No changes suggested", dismiss widget |
| **AI returns invalid syntax** | Show warning banner: "Generated code may have syntax errors", user can still accept/reject |
| **Request timeout (>15s)** | Show error in widget: "Request timed out. [Retry]" button |
| **Network error** | Show error in widget: "Connection failed. [Retry]" button |
| **Selection modified during processing** | Cancel request, close widget, show toast: "Selection changed, edit cancelled" |
| **File closed during processing** | Cancel request, no user notification |
| **Very large selection (>5000 chars)** | Show warning tooltip: "Selection too large (max 5000 characters)", prevent widget from opening |

---

## 4. @ Mention Picker (in Chat)

### 4.1 Feature Overview

**Purpose:** Allow users to reference files, symbols, and scoped content in chat messages by typing `@` and selecting from a searchable dropdown.

**Activation:**
- User types `@` in the chat input box
- Dropdown appears immediately below the `@` character
- User can type to fuzzy search, use arrows to navigate, Enter to select

### 4.2 Trigger Logic

```
Detection:
- Listen for `@` character in chat input TextBox
- Track caret position when `@` is typed
- Show picker immediately (no debounce)

Query Parsing:
@            → Show all files (root workspace)
@file        → Fuzzy search files matching "file"
@#           → Show symbols (classes, methods, functions)
@#MyClass    → Fuzzy search symbols matching "MyClass"
@folder/     → Show files in "folder" directory
@folder/file → Fuzzy search files in "folder" matching "file"

Special Prefixes:
@                    → File search (workspace root)
@#                   → Symbol search (current file or workspace)
@/                   → Absolute path search (from workspace root)
@./                  → Relative path search (from current file's directory)
@src/                → Scoped file search (within src/ directory)
```

### 4.3 Visual Specification

#### 4.3.1 Dropdown Structure

```
┌────────────────────────────────────────────────────┐
│ 📄 FileExplorer.axaml              csharp/Views/   │ ← Item 1
│ 📄 FileExplorerViewModel.cs        csharp/ViewM... │ ← Item 2 (truncated)
│ 🟣 FileTreeNode                    Models/         │ ← Item 3 (class symbol)
│ 🔵 LoadChildren()                  FileExplorer... │ ← Item 4 (method symbol)
│ 📄 FileService.cs                  Services/       │ ← Item 5
└────────────────────────────────────────────────────┘
  ↑ Selected item (Surface1 background)

Dimensions:
- Width: 400px (fixed)
- Height: Auto (max 5 items visible, scroll if more)
- Item height: 36px
- Padding: 8px vertical, 12px horizontal per item
```

#### 4.3.2 XAML Structure

```xml
<Popup x:Name="MentionPicker"
       IsOpen="{Binding ShowMentionPicker}"
       PlacementMode="Pointer"
       PlacementTarget="{Binding #InputBox}"
       StaysOpen="False"
       Width="400"
       MaxHeight="180">
    <Border Background="{StaticResource ColorBase}"
            BorderBrush="{StaticResource ColorSurface1}"
            BorderThickness="1"
            CornerRadius="8"
            BoxShadow="{StaticResource ShadowCard}">
        <ScrollViewer MaxHeight="180"
                      VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding MentionResults}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="models:MentionItem">
                        <Border Classes="mentionItem"
                                Padding="12,8"
                                Cursor="Hand"
                                PointerPressed="OnMentionItemClick">
                            <Border.Styles>
                                <Style Selector="Border.mentionItem">
                                    <Setter Property="Background" Value="Transparent" />
                                </Style>
                                <Style Selector="Border.mentionItem:pointerover">
                                    <Setter Property="Background" Value="{StaticResource ColorSurface0}" />
                                </Style>
                                <Style Selector="Border.mentionItem[IsSelected=true]">
                                    <Setter Property="Background" Value="{StaticResource ColorSurface1}" />
                                </Style>
                            </Border.Styles>

                            <DockPanel>
                                <!-- File path / location (right-aligned, secondary) -->
                                <TextBlock DockPanel.Dock="Right"
                                           Text="{Binding LocationPath}"
                                           FontSize="11"
                                           Foreground="{StaticResource ColorSubtext0}"
                                           Opacity="0.6"
                                           TextTrimming="CharacterEllipsis"
                                           MaxWidth="150"
                                           VerticalAlignment="Center"
                                           Margin="8,0,0,0" />

                                <!-- Icon + Name (left-aligned, primary) -->
                                <StackPanel Orientation="Horizontal" Spacing="8">
                                    <!-- Icon (emoji or colored circle) -->
                                    <TextBlock Text="{Binding Icon}"
                                               FontSize="14"
                                               VerticalAlignment="Center" />

                                    <!-- Item name -->
                                    <TextBlock Text="{Binding DisplayName}"
                                               FontSize="13"
                                               Foreground="{StaticResource ColorText}"
                                               VerticalAlignment="Center"
                                               TextTrimming="CharacterEllipsis" />
                                </StackPanel>
                            </DockPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </Border>
</Popup>
```

#### 4.3.3 File Icons by Language

```csharp
public static string GetFileIcon(string extension)
{
    return extension.ToLowerInvariant() switch
    {
        ".cs" => "🟣",      // C# - Mauve/Purple
        ".csproj" => "🟣",
        ".ts" => "🔵",      // TypeScript - Blue
        ".tsx" => "🔵",
        ".js" => "🟡",      // JavaScript - Yellow
        ".jsx" => "🟡",
        ".py" => "🟡",      // Python - Yellow
        ".java" => "🟠",    // Java - Orange/Peach
        ".go" => "🔵",      // Go - Blue
        ".rs" => "🟠",      // Rust - Orange
        ".cpp" => "🔵",     // C++ - Blue
        ".c" => "🔵",
        ".h" => "🔵",
        ".rb" => "🔴",      // Ruby - Red
        ".php" => "🟣",     // PHP - Purple
        ".swift" => "🟠",   // Swift - Orange
        ".kt" => "🟣",      // Kotlin - Purple
        ".md" => "⚪",      // Markdown - White/Gray
        ".json" => "🟡",    // JSON - Yellow
        ".xml" => "🟠",     // XML - Orange
        ".yaml" => "🟣",    // YAML - Purple
        ".yml" => "🟣",
        ".html" => "🟠",    // HTML - Orange
        ".css" => "🔵",     // CSS - Blue
        ".scss" => "🟣",    // SCSS - Purple
        ".sql" => "🟠",     // SQL - Orange
        ".sh" => "🟢",      // Shell - Green
        ".bat" => "🟢",
        ".ps1" => "🔵",     // PowerShell - Blue
        _ => "📄"           // Default - Document icon
    };
}

public static string GetSymbolIcon(SymbolKind kind)
{
    return kind switch
    {
        SymbolKind.Class => "🟣",       // Purple circle
        SymbolKind.Interface => "🔵",   // Blue circle
        SymbolKind.Method => "🟢",      // Green circle
        SymbolKind.Function => "🟢",
        SymbolKind.Property => "🟡",    // Yellow circle
        SymbolKind.Field => "🟡",
        SymbolKind.Variable => "⚪",    // White circle
        SymbolKind.Enum => "🟠",        // Orange circle
        SymbolKind.Namespace => "🟣",   // Purple circle
        _ => "◯"                        // Hollow circle
    };
}
```

### 4.4 Search & Filtering

#### 4.4.1 Fuzzy Search Algorithm

```
Algorithm: Substring matching with score ranking

Score Calculation:
- Exact match at start: +100 points
- Exact match anywhere: +50 points
- Partial match (subsequence): +10 points per matched char
- Case-insensitive bonus: +5 points
- Path segment match: +20 points (e.g., "views" matches "csharp/Views/")

Example:
Query: "fileex"
Results (sorted by score):
1. FileExplorer.axaml       (score: 150 - exact start match)
2. FileExplorerView.axaml   (score: 140 - exact start + longer name)
3. FileService.cs           (score: 50 - contains "file")
4. MyFile.cs                (score: 50 - contains "file")

Max Results: 20 items
Response Time: < 50ms (use cached file index)
```

#### 4.4.2 Context-Aware Results

```
File Search (@):
- Prioritize files in currently open editors
- Prioritize recently accessed files
- Prioritize files in same directory as current file
- Show full workspace path (relative to root)

Symbol Search (@#):
- If current file has focus: prioritize symbols in current file
- Otherwise: show workspace-wide symbols
- Group by file (show file path as secondary text)
- Sort by: relevance > type (classes first, then methods) > alphabetical

Scoped Search (@folder/):
- Filter results to only files within specified folder
- Show paths relative to the scoped folder
- Still apply fuzzy matching to filenames
```

### 4.5 Interaction Behavior

#### 4.5.1 Keyboard Navigation

| Key | Action |
|-----|--------|
| **@** | Open picker (if empty or at end of word) |
| **Arrow Down** | Select next item |
| **Arrow Up** | Select previous item |
| **Enter** | Insert selected item as tag/chip |
| **Tab** | Insert selected item and add space |
| **Esc** | Close picker without inserting |
| **Backspace** (after @) | Close picker |
| **Any typing** | Update search query, filter results |

#### 4.5.2 Mouse Interaction

```
Hover: Highlight item with Surface0 background (#313244)
Click: Select item, insert as chip, close picker
Click outside picker: Close picker without inserting
Scroll: Use vertical scrollbar if > 5 items
```

#### 4.5.3 Tag Insertion

When user selects an item from the picker:

```
1. Remove the @ query text from input (e.g., "@fileex" → "")
2. Insert a styled chip/tag at the @ position
3. Close the picker
4. Move cursor after the inserted chip
5. Add a space after the chip for continued typing

Visual Result in Input:
Before: "Can you update @fileex"
After:  "Can you update [@FileExplorer.axaml] "
                          ↑ Styled chip    ↑ Cursor here
```

#### 4.5.4 Tag Styling (Chip Component)

```xml
<Border Classes="mentionChip"
        Background="{StaticResource ColorSurface0}"
        BorderBrush="{StaticResource ColorBlue}"
        BorderThickness="1"
        CornerRadius="4"
        Padding="6,2"
        Margin="2,0">
    <StackPanel Orientation="Horizontal" Spacing="4">
        <!-- Icon -->
        <TextBlock Text="{Binding Icon}"
                   FontSize="12"
                   VerticalAlignment="Center" />

        <!-- Filename -->
        <TextBlock Text="{Binding FileName}"
                   FontSize="12"
                   Foreground="{StaticResource ColorBlue}"
                   VerticalAlignment="Center" />

        <!-- Remove button -->
        <Button Content="×"
                Classes="chipRemove"
                FontSize="10"
                Width="14" Height="14"
                Padding="0"
                Background="Transparent"
                Foreground="{StaticResource ColorSubtext0}"
                Command="{Binding RemoveChipCommand}"
                ToolTip.Tip="Remove"
                VerticalAlignment="Center" />
    </StackPanel>
</Border>

<Style Selector="Border.mentionChip">
    <Setter Property="Background" Value="{StaticResource ColorSurface0}" />
    <Setter Property="BorderBrush" Value="{StaticResource ColorBlue}" />
</Style>
<Style Selector="Border.mentionChip:pointerover">
    <Setter Property="Background" Value="{StaticResource ColorSurface1}" />
    <Setter Property="BorderBrush" Value="{StaticResource ColorSky}" />
</Style>
```

**Chip Color Coding:**
- Background: Surface0 (#313244)
- Border: Blue (#89B4FA)
- Text: Blue (#89B4FA)
- Remove button: Subtext0 (#A6ADC8), hover → Text (#CDD6F4)

### 4.6 Advanced Features

#### 4.6.1 Multi-File Selection

Users can mention multiple files in one message:

```
Input: "Compare @FileExplorer.axaml and @EditorView.axaml"
Result: Two chips, both added to context
```

#### 4.6.2 Symbol Mention Example

```
Input: "Explain @#LoadChildren"
Picker shows:
  🟢 LoadChildren()          FileExplorerViewModel.cs
  🟢 LoadChildren()          TreeNode.cs

User selects first result
Result: "Explain [@FileExplorerViewModel.LoadChildren()] "
```

#### 4.6.3 Scoped Search Example

```
Input: "@Views/"
Picker shows only files in Views/ directory:
  📄 ChatView.axaml
  📄 EditorView.axaml
  📄 FileExplorerView.axaml
  📄 InputView.axaml
  ... (all files in Views/)

User types "ex" → "@Views/ex"
Picker filters to:
  📄 FileExplorerView.axaml
  📄 ExtensionsPanelView.axaml
```

### 4.7 Edge Cases & Error Handling

| Scenario | Behavior |
|----------|----------|
| **No matches found** | Show "No files found" message in picker, keep picker open |
| **@ at start of input** | Normal behavior, show picker |
| **@ in middle of word** | Do not trigger picker (e.g., "user@email.com") |
| **Multiple @ in input** | Each @ independently triggers picker at cursor position |
| **Backspace removes @** | Close picker immediately |
| **File renamed/deleted** | Chip shows (!) icon, tooltip: "File not found", click to remove |
| **Symbol no longer exists** | Same as file not found behavior |
| **Very long file path** | Truncate path with ellipsis (...) in middle, show full path in tooltip |

---

## 5. Composer Panel

### 5.1 Feature Overview

**Purpose:** Display a multi-file AI editing plan with tree-structured file list, action badges, expandable diffs, and batch apply/reject controls.

**Location:** Right panel area, new tab alongside Agent/Todo/ToolCall panels

**Activation:**
- User sends chat message requesting multi-file changes
- AI generates a plan with affected files
- Composer panel auto-opens and switches to active tab

### 5.2 Panel Structure

```
┌─────────────────────────────────────────────────────┐
│ Composer                                        [×] │ ← Header
├─────────────────────────────────────────────────────┤
│ ▼ 📁 src/                                           │ ← Folder (expandable)
│   ▶ 📄 FileExplorer.cs               [Modify]      │ ← File item (collapsed)
│   ▼ 📄 EditorViewModel.cs            [Modify]      │ ← File item (expanded)
│     ┌─────────────────────────────────────────────┐│
│     │ - old line 1                                ││ ← Diff preview
│     │ + new line 1                                ││
│     │   unchanged line 2                          ││
│     └─────────────────────────────────────────────┘│
│   📄 NewService.cs                   [Create]      │ ← New file
│ ▼ 📁 tests/                                         │
│   📄 OldTests.cs                     [Delete]      │ ← File to delete
├─────────────────────────────────────────────────────┤
│ [Apply All] [Apply Selected] [Discard]            │ ← Action buttons
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ 60%             │ ← Progress bar (during apply)
└─────────────────────────────────────────────────────┘
```

### 5.3 Visual Specification

#### 5.3.1 XAML Structure

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             x:Class="MiniClaudeCode.Avalonia.Views.ComposerPanelView"
             x:DataType="vm:ComposerPanelViewModel">

    <Border Background="{StaticResource ColorBase}">
        <DockPanel>
            <!-- Header -->
            <Border DockPanel.Dock="Top"
                    Background="{StaticResource ColorMantle}"
                    BorderBrush="{StaticResource ColorSurface0}"
                    BorderThickness="0,0,0,1"
                    Padding="12,8">
                <DockPanel>
                    <!-- Close button -->
                    <Button DockPanel.Dock="Right"
                            Content="×"
                            Classes="panelClose"
                            Command="{Binding CloseCommand}"
                            Width="24" Height="24"
                            Padding="0"
                            Background="Transparent"
                            Foreground="{StaticResource ColorSubtext0}" />

                    <!-- Title -->
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <TextBlock Text="📝"
                                   FontSize="16"
                                   VerticalAlignment="Center" />
                        <TextBlock Text="Composer"
                                   FontSize="14"
                                   FontWeight="SemiBold"
                                   Foreground="{StaticResource ColorText}"
                                   VerticalAlignment="Center" />
                        <TextBlock Text="{Binding FileCount, StringFormat='({0} files)'}"
                                   FontSize="11"
                                   Foreground="{StaticResource ColorSubtext0}"
                                   VerticalAlignment="Center"
                                   Margin="4,0,0,0" />
                    </StackPanel>
                </DockPanel>
            </Border>

            <!-- Action buttons (bottom) -->
            <Border DockPanel.Dock="Bottom"
                    Background="{StaticResource ColorMantle}"
                    BorderBrush="{StaticResource ColorSurface0}"
                    BorderThickness="0,1,0,0"
                    Padding="12">
                <StackPanel Spacing="8">
                    <!-- Progress bar (only visible during apply) -->
                    <ProgressBar Value="{Binding Progress}"
                                 IsVisible="{Binding IsApplying}"
                                 Height="4"
                                 Background="{StaticResource ColorSurface0}"
                                 Foreground="{StaticResource ColorBlue}"
                                 CornerRadius="2" />

                    <!-- Button row -->
                    <Grid ColumnDefinitions="*,*,*" ColumnSpacing="8">
                        <!-- Apply All -->
                        <Button Grid.Column="0"
                                Classes="composer-primary"
                                Content="Apply All"
                                Command="{Binding ApplyAllCommand}"
                                IsEnabled="{Binding !IsApplying}"
                                HorizontalAlignment="Stretch"
                                Background="{StaticResource ColorBlue}"
                                Foreground="{StaticResource ColorBase}"
                                CornerRadius="6"
                                Padding="0,8"
                                FontSize="12"
                                FontWeight="SemiBold" />

                        <!-- Apply Selected -->
                        <Button Grid.Column="1"
                                Classes="composer-secondary"
                                Content="Apply Selected"
                                Command="{Binding ApplySelectedCommand}"
                                IsEnabled="{Binding HasSelection}"
                                HorizontalAlignment="Stretch"
                                Background="{StaticResource ColorSurface1}"
                                Foreground="{StaticResource ColorText}"
                                CornerRadius="6"
                                Padding="0,8"
                                FontSize="12" />

                        <!-- Discard -->
                        <Button Grid.Column="2"
                                Classes="composer-danger"
                                Content="Discard"
                                Command="{Binding DiscardCommand}"
                                IsEnabled="{Binding !IsApplying}"
                                HorizontalAlignment="Stretch"
                                Background="{StaticResource ColorSurface1}"
                                Foreground="{StaticResource ColorRed}"
                                CornerRadius="6"
                                Padding="0,8"
                                FontSize="12" />
                    </Grid>
                </StackPanel>
            </Border>

            <!-- File tree (center) -->
            <ScrollViewer VerticalScrollBarVisibility="Auto"
                          Padding="8">
                <TreeView ItemsSource="{Binding RootNodes}"
                          SelectionMode="Multiple"
                          SelectedItems="{Binding SelectedFiles}">
                    <TreeView.ItemTemplate>
                        <TreeDataTemplate ItemsSource="{Binding Children}"
                                          x:DataType="models:ComposerFileNode">
                            <!-- See section 5.3.2 for item template -->
                        </TreeDataTemplate>
                    </TreeView.ItemTemplate>
                </TreeView>
            </ScrollViewer>
        </DockPanel>
    </Border>
</UserControl>
```

#### 5.3.2 File Item Template

```xml
<TreeDataTemplate ItemsSource="{Binding Children}"
                  x:DataType="models:ComposerFileNode">
    <Border Classes="composerFileItem"
            Padding="8,6"
            Margin="0,2"
            CornerRadius="4">
        <Border.Styles>
            <Style Selector="Border.composerFileItem">
                <Setter Property="Background" Value="Transparent" />
            </Style>
            <Style Selector="Border.composerFileItem:pointerover">
                <Setter Property="Background" Value="{StaticResource ColorSurface0}" />
            </Style>
        </Border.Styles>

        <DockPanel>
            <!-- Action badge (right) -->
            <Border DockPanel.Dock="Right"
                    Classes="actionBadge"
                    Background="{Binding ActionColor}"
                    CornerRadius="4"
                    Padding="8,2"
                    VerticalAlignment="Center">
                <TextBlock Text="{Binding ActionLabel}"
                           FontSize="10"
                           FontWeight="SemiBold"
                           Foreground="{StaticResource ColorBase}" />
            </Border>

            <!-- Status icon (right, before badge) -->
            <TextBlock DockPanel.Dock="Right"
                       Text="{Binding StatusIcon}"
                       FontSize="14"
                       Foreground="{Binding StatusColor}"
                       VerticalAlignment="Center"
                       Margin="0,0,8,0"
                       IsVisible="{Binding HasStatus}" />

            <!-- File icon + name (left) -->
            <StackPanel Orientation="Horizontal" Spacing="8">
                <!-- Expand/collapse chevron -->
                <TextBlock Text="{Binding IsExpanded, Converter={StaticResource BoolToChevron}}"
                           FontSize="10"
                           Foreground="{StaticResource ColorSubtext0}"
                           VerticalAlignment="Center"
                           IsVisible="{Binding HasDiff}" />

                <!-- File icon -->
                <TextBlock Text="{Binding Icon}"
                           FontSize="14"
                           VerticalAlignment="Center" />

                <!-- File path -->
                <TextBlock Text="{Binding DisplayPath}"
                           FontSize="12"
                           Foreground="{StaticResource ColorText}"
                           VerticalAlignment="Center" />
            </StackPanel>
        </DockPanel>
    </Border>

    <!-- Diff preview (when expanded) -->
    <Border IsVisible="{Binding IsExpanded}"
            Background="{StaticResource ColorCrust}"
            BorderBrush="{StaticResource ColorSurface0}"
            BorderThickness="1"
            CornerRadius="4"
            Padding="8"
            Margin="24,4,0,4">
        <SelectableTextBlock Text="{Binding DiffPreview}"
                             FontFamily="Cascadia Code, Consolas, monospace"
                             FontSize="11"
                             Foreground="{StaticResource ColorText}"
                             TextWrapping="NoWrap" />
    </Border>
</TreeDataTemplate>
```

### 5.4 Action Badges

#### 5.4.1 Badge Color Mapping

```csharp
public class ComposerFileNode
{
    public ComposerActionType Action { get; set; }

    public string ActionLabel => Action switch
    {
        ComposerActionType.Create => "Create",
        ComposerActionType.Modify => "Modify",
        ComposerActionType.Delete => "Delete",
        ComposerActionType.Rename => "Rename",
        _ => "Unknown"
    };

    public Brush ActionColor => Action switch
    {
        ComposerActionType.Create => new SolidColorBrush(Color.Parse("#A6E3A1")), // Green
        ComposerActionType.Modify => new SolidColorBrush(Color.Parse("#89B4FA")), // Blue
        ComposerActionType.Delete => new SolidColorBrush(Color.Parse("#F38BA8")), // Red
        ComposerActionType.Rename => new SolidColorBrush(Color.Parse("#FAB387")), // Peach
        _ => new SolidColorBrush(Color.Parse("#6C7086"))                          // Overlay0
    };
}

public enum ComposerActionType
{
    Create,
    Modify,
    Delete,
    Rename
}
```

#### 5.4.2 Badge Visual Specs

```
Badge Component:
- Font size: 10px
- Font weight: SemiBold (600)
- Text color: ColorBase (#1E1E2E) - dark text on bright badge
- Padding: 8px horizontal, 2px vertical
- Border radius: 4px
- Minimum width: 56px (for consistent sizing)

Color Assignments:
- Create: Green (#A6E3A1)
- Modify: Blue (#89B4FA)
- Delete: Red (#F38BA8)
- Rename: Peach (#FAB387)
```

### 5.5 Status Icons & States

#### 5.5.1 Per-File Status

Each file item can have these states during execution:

```csharp
public enum ComposerFileStatus
{
    Pending,      // Not yet processed (no icon)
    Applying,     // Currently being applied (spinner)
    Success,      // Successfully applied (checkmark)
    Failed,       // Failed to apply (X mark)
    Skipped       // User skipped this file (minus)
}

public string StatusIcon => Status switch
{
    ComposerFileStatus.Applying => "⟳",  // Spinner (animated)
    ComposerFileStatus.Success => "✓",   // Checkmark
    ComposerFileStatus.Failed => "✗",    // X mark
    ComposerFileStatus.Skipped => "–",   // Minus
    _ => ""                               // No icon for Pending
};

public Brush StatusColor => Status switch
{
    ComposerFileStatus.Applying => new SolidColorBrush(Color.Parse("#89B4FA")), // Blue
    ComposerFileStatus.Success => new SolidColorBrush(Color.Parse("#A6E3A1")),  // Green
    ComposerFileStatus.Failed => new SolidColorBrush(Color.Parse("#F38BA8")),   // Red
    ComposerFileStatus.Skipped => new SolidColorBrush(Color.Parse("#6C7086")),  // Overlay0
    _ => Brushes.Transparent
};
```

#### 5.5.2 Status Icon Animation (Applying State)

```xml
<!-- Spinner for "Applying" status -->
<TextBlock Text="⟳"
           FontSize="14"
           Foreground="{StaticResource ColorBlue}"
           IsVisible="{Binding IsApplying}">
    <TextBlock.RenderTransform>
        <RotateTransform />
    </TextBlock.RenderTransform>
    <TextBlock.Styles>
        <Style Selector="TextBlock">
            <Style.Animations>
                <Animation Duration="0:0:1.2" IterationCount="INFINITE">
                    <KeyFrame Cue="0%">
                        <Setter Property="RenderTransform.Angle" Value="0" />
                    </KeyFrame>
                    <KeyFrame Cue="100%">
                        <Setter Property="RenderTransform.Angle" Value="360" />
                    </KeyFrame>
                </Animation>
            </Style.Animations>
        </Style>
    </TextBlock.Styles>
</TextBlock>
```

### 5.6 Diff Preview Rendering

#### 5.6.1 Unified Diff Format

```
Format:
- Deletion lines: Prefix with "- " (minus space), ColorRed text
- Addition lines: Prefix with "+ " (plus space), ColorGreen text
- Unchanged lines: Prefix with "  " (two spaces), normal text
- Context: Show 2 unchanged lines before/after each change block

Example:
  public class FileExplorer {
-     private List<string> files;
+     private ObservableCollection<FileNode> files;

      public void LoadFiles() {
-         files = Directory.GetFiles(path).ToList();
+         files = new ObservableCollection<FileNode>(
+             Directory.GetFiles(path).Select(f => new FileNode(f))
+         );
      }
  }
```

#### 5.6.2 Syntax Highlighting (Optional Enhancement)

If possible, apply basic syntax highlighting to diff lines:
- Keywords: Mauve (#CBA6F7)
- Strings: Green (#A6E3A1)
- Comments: Overlay1 (#7F849C)
- Numbers: Peach (#FAB387)
- Operators: Sky (#89DCEB)

**Note:** This is a nice-to-have. Plain text diffs are acceptable if syntax highlighting is too complex.

### 5.7 Interaction Behavior

#### 5.7.1 Expand/Collapse

```
Folder Nodes:
- Click chevron (▶/▼) or folder name to toggle expand
- Expanded: Show all child files
- Collapsed: Hide all child files

File Nodes (with diffs):
- Click chevron or file name to toggle diff preview
- Expanded: Show inline diff in code block below file item
- Collapsed: Hide diff
- Keyboard: Space bar toggles expand when item focused
```

#### 5.7.2 Selection

```
Multi-Selection:
- Click checkbox (or Ctrl+Click on item) to select file
- Shift+Click: Select range from last selected to clicked item
- Ctrl+A: Select all files
- Selected files have checkmark icon or highlighted background

Selection Effects:
- "Apply Selected" button enabled only if ≥1 file selected
- "Apply All" applies all files regardless of selection
- Progress bar shows progress based on selected subset (if "Apply Selected")
```

#### 5.7.3 Apply Actions

```
Apply All Flow:
1. User clicks "Apply All"
2. Disable all buttons except "Cancel" (not shown, use Esc)
3. Show progress bar at 0%
4. For each file in list (top to bottom):
   a. Set file status to "Applying" (show spinner)
   b. Execute action (create/modify/delete file)
   c. Update status to "Success" or "Failed" (show icon)
   d. Update progress bar: (completed / total) * 100
5. When all files done:
   a. Re-enable buttons
   b. If all successful: Show toast "All changes applied ✓"
   c. If any failed: Show toast "N files failed, see Composer for details"

Apply Selected Flow:
- Same as Apply All, but only process selected files
- Progress based on selected count, not total count

Discard Flow:
1. User clicks "Discard"
2. Show confirmation dialog: "Discard all pending changes?"
3. If confirmed:
   a. Clear composer panel file list
   b. Close composer panel (return to previous right panel tab)
   c. Show toast "Changes discarded"
```

#### 5.7.4 Error Handling

```
File-Level Errors:
- File locked by another process: Status = Failed, tooltip: "File is locked"
- File not found (for modify/delete): Status = Failed, tooltip: "File not found"
- Permission denied: Status = Failed, tooltip: "Permission denied"
- Syntax error in generated code: Status = Success (file written), show warning badge

Panel-Level Errors:
- No files in plan: Show empty state "No changes to apply"
- All files failed: Show error banner "All changes failed, check file permissions"
```

### 5.8 Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Ctrl+Enter** | Apply All |
| **Enter** | Apply Selected (if any selected) |
| **Esc** | Close composer panel |
| **Space** | Toggle expand/collapse on focused item |
| **Ctrl+A** | Select all files |
| **Arrow Up/Down** | Navigate file list |
| **Ctrl+Click** | Toggle file selection |
| **Shift+Click** | Range selection |

---

## 6. Enhanced Chat Input

### 6.1 Feature Overview

**Purpose:** Provide a rich chat input box with context indicators, model selection, token counting, and inline mention chips.

**Location:** Bottom of the main window, above status bar, docked to chat panel area.

### 6.2 Visual Specification

#### 6.2.1 Full Layout

```
┌──────────────────────────────────────────────────────────────┐
│ [T:5|TC:12]  PLAN  ×                          📎 3 files     │ ← Context row
├──────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ Can you update [@FileExplorer.cs] and add lazy loading? │ │ ← Input area
│ │                                                            │ │   (multi-line)
│ └──────────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────┤
│ Enter send · Shift+Enter newline     [GPT-4o ▼] ~2.4K [▶]  │ ← Action row
└──────────────────────────────────────────────────────────────┘
```

#### 6.2.2 Context Row (Top)

```xml
<Border DockPanel.Dock="Top"
        Padding="10,4"
        Background="{StaticResource ColorMantle}"
        IsVisible="{Binding ShowContextRow}">
    <DockPanel>
        <!-- File attachment indicator (right) -->
        <Border DockPanel.Dock="Right"
                Background="{StaticResource ColorSurface0}"
                CornerRadius="4"
                Padding="8,2"
                IsVisible="{Binding HasAttachments}">
            <StackPanel Orientation="Horizontal" Spacing="4">
                <TextBlock Text="📎"
                           FontSize="12"
                           VerticalAlignment="Center" />
                <TextBlock Text="{Binding AttachmentCount, StringFormat='{0} files'}"
                           FontSize="10"
                           Foreground="{StaticResource ColorSubtext1}"
                           VerticalAlignment="Center" />
            </StackPanel>
        </Border>

        <!-- Left side: badges and counters -->
        <StackPanel Orientation="Horizontal" Spacing="6">
            <!-- Turn/ToolCall counter -->
            <Border Background="{StaticResource ColorSurface0}"
                    CornerRadius="4"
                    Padding="6,2"
                    IsVisible="{Binding HasWorkspace}">
                <TextBlock FontSize="10"
                           Opacity="0.6"
                           Foreground="{StaticResource ColorText}">
                    <Run Text="T:" />
                    <Run Text="{Binding TurnCount}" />
                    <Run Text=" | TC:" />
                    <Run Text="{Binding ToolCallCount}" />
                </TextBlock>
            </Border>

            <!-- Plan mode badge -->
            <Border IsVisible="{Binding IsPlanMode}"
                    Background="{StaticResource ColorSurface1}"
                    CornerRadius="4"
                    Padding="8,2">
                <StackPanel Orientation="Horizontal" Spacing="4">
                    <TextBlock Text="PLAN"
                               FontSize="10"
                               FontWeight="Bold"
                               Foreground="{StaticResource ColorYellow}"
                               VerticalAlignment="Center" />
                    <Button Content="×"
                            FontSize="9"
                            Padding="2,0"
                            Background="Transparent"
                            Foreground="{StaticResource ColorSubtext0}"
                            Command="{Binding TogglePlanModeCommand}"
                            ToolTip.Tip="Exit Plan mode"
                            VerticalAlignment="Center" />
                </StackPanel>
            </Border>
        </StackPanel>
    </DockPanel>
</Border>
```

#### 6.2.3 Input Area (Center)

```xml
<!-- Multi-line input with inline chips -->
<TextBox x:Name="InputBox"
         Classes="chatInput"
         Text="{Binding Chat.InputText, Mode=TwoWay}"
         AcceptsReturn="True"
         TextWrapping="Wrap"
         MinHeight="40"
         MaxHeight="150"
         Margin="8,4"
         Watermark="Type a message or press / for commands..."
         IsEnabled="{Binding !IsProcessing}"
         KeyDown="OnInputKeyDown"
         Background="{StaticResource ColorCrust}"
         BorderBrush="{StaticResource ColorSurface0}"
         BorderThickness="1"
         CornerRadius="6"
         Padding="10,8"
         FontSize="13"
         FontFamily="Cascadia Code, Consolas, Menlo, monospace"
         Foreground="{StaticResource ColorText}" />
```

**Inline Chips:**

Chips (mention tags) are rendered inline within the TextBox using adorners or inline elements:

```xml
<!-- Chip representation (conceptual, actual implementation may vary) -->
<InlineUIContainer>
    <Border Classes="mentionChip"
            Background="{StaticResource ColorSurface0}"
            BorderBrush="{StaticResource ColorBlue}"
            BorderThickness="1"
            CornerRadius="4"
            Padding="6,2"
            Margin="2,0">
        <StackPanel Orientation="Horizontal" Spacing="4">
            <TextBlock Text="📄" FontSize="11" />
            <TextBlock Text="FileExplorer.cs"
                       FontSize="11"
                       Foreground="{StaticResource ColorBlue}" />
            <Button Content="×" FontSize="9" Padding="0" Width="12" Height="12" />
        </StackPanel>
    </Border>
</InlineUIContainer>
```

#### 6.2.4 Action Row (Bottom)

```xml
<Grid DockPanel.Dock="Bottom"
      Margin="10,4,10,6"
      ColumnDefinitions="*,Auto,Auto,Auto">

    <!-- Hint text (left) -->
    <TextBlock Grid.Column="0"
               Text="Enter send · Shift+Enter newline · / commands"
               FontSize="10"
               Opacity="0.35"
               Foreground="{StaticResource ColorText}"
               VerticalAlignment="Center" />

    <!-- Token counter (right side) -->
    <TextBlock Grid.Column="1"
               Text="{Binding EstimatedTokens, StringFormat='~{0}K tokens'}"
               FontSize="10"
               Foreground="{StaticResource ColorSubtext0}"
               Opacity="0.6"
               VerticalAlignment="Center"
               Margin="0,0,8,0"
               IsVisible="{Binding HasInput}" />

    <!-- Model selector dropdown (right side) -->
    <ComboBox Grid.Column="2"
              Classes="modelSelector"
              ItemsSource="{Binding AvailableModels}"
              SelectedItem="{Binding SelectedModel}"
              IsEnabled="{Binding !IsProcessing}"
              MinWidth="140"
              MaxWidth="220"
              Margin="0,0,6,0"
              VerticalAlignment="Center">
        <ComboBox.ItemTemplate>
            <DataTemplate x:DataType="models:ModelOption">
                <StackPanel Orientation="Horizontal" Spacing="6">
                    <TextBlock Text="{Binding ProviderName}"
                               FontSize="10"
                               Opacity="0.5" />
                    <TextBlock Text="{Binding ShortLabel}"
                               FontSize="12" />
                </StackPanel>
            </DataTemplate>
        </ComboBox.ItemTemplate>
    </ComboBox>

    <!-- Send/Cancel button (right side) -->
    <Button Grid.Column="3"
            Classes="accent"
            Content="▶"
            Command="{Binding Chat.SendMessageCommand}"
            IsVisible="{Binding !IsProcessing}"
            VerticalAlignment="Center"
            Padding="12,6"
            FontSize="12"
            ToolTip.Tip="Send (Enter)" />

    <Button Grid.Column="3"
            Classes="danger"
            Content="■"
            Command="{Binding CancelOperationCommand}"
            IsVisible="{Binding IsProcessing}"
            VerticalAlignment="Center"
            Padding="12,6"
            FontSize="12"
            ToolTip.Tip="Cancel (Escape)" />
</Grid>
```

### 6.3 Features

#### 6.3.1 File Attachment Indicator

```
Display:
- Icon: 📎 (paperclip emoji)
- Text: "N files" (e.g., "3 files")
- Background: Surface0 (#313244)
- Border radius: 4px
- Padding: 8px horizontal, 2px vertical

Behavior:
- Click to show list of attached files
- Tooltip shows file names
- Click "×" on individual files to remove from context
```

#### 6.3.2 Model Selector

```
Dropdown content:
┌──────────────────────────┐
│ OpenAI  GPT-4o          │ ← Selected
│ OpenAI  GPT-4o-mini     │
│ DeepSeek V3             │
│ Zhipu   GLM-4-Plus      │
└──────────────────────────┘

Item structure:
- Provider name (small, 10px, 50% opacity)
- Model short label (12px, normal weight)
- Horizontal spacing: 6px

Selected item display:
[GPT-4o ▼]  (show only model name, not provider)
```

#### 6.3.3 Token Counter

```
Calculation:
- Estimate tokens from input text length
- Include attached file contents in estimate
- Formula: total_chars / 4 = approximate tokens
- Format: "~2.4K tokens" (1 decimal place, K suffix for thousands)

Display:
- Font size: 10px
- Color: Subtext0 (#A6ADC8) at 60% opacity
- Position: Between hint text and model selector
- Only visible if input is not empty
```

#### 6.3.4 Input Behavior

```
Enter Key:
- Default: Send message (if not empty)
- If Shift held: Insert new line (no send)
- If in command mode (input starts with /): Execute command

Escape Key:
- If processing: Cancel current operation
- If picker open: Close picker
- If input focused: Clear focus (blur)

Tab Key:
- If ghost text shown: Accept ghost text
- If picker open: Navigate picker items
- Otherwise: Move focus to next element

Paste:
- Plain text: Insert normally
- File path: Offer to add as attachment
- Image: Not supported (show error)
```

### 6.4 Interaction Patterns

#### 6.4.1 Attachment Management

```
Add Attachment:
1. User types @filename and selects from picker
2. OR user clicks "Attach" button (if added to UI)
3. OR user drags file from explorer into input area

Display:
- Inline chip in input box: [@filename.cs]
- Count in context row: 📎 3 files

Remove Attachment:
- Click × on chip in input
- OR click × in attachment list popup (if implemented)
- Chip removed, count updated

Context Inclusion:
- Attached files sent as context with every message
- File contents read at send time (not at attachment time)
- Large files truncated to first 10KB per file
```

#### 6.4.2 Model Switching

```
User can switch model mid-conversation:
1. Click model selector dropdown
2. Select different model
3. Dropdown closes, selected model shown
4. Next message uses new model
5. Previous messages remain unchanged (model per message)

Model persistence:
- Selected model saved in user settings
- Restored on app restart
```

#### 6.4.3 Plan Mode Toggle

```
Enter Plan Mode:
- User types "/plan" command
- OR clicks "Plan Mode" button (if in UI)
- Badge appears in context row: [PLAN ×]
- Yellow color: #F9E2AF

Exit Plan Mode:
- Click × on PLAN badge
- OR type "/plan" again (toggle)
- Badge disappears

Effect:
- In Plan mode: AI generates multi-file edit plans
- In normal mode: AI responds directly to questions
```

---

## 7. Diff Preview (AI Edits)

### 7.1 Feature Overview

**Purpose:** Visualize code changes from AI edits in a clear, actionable format with inline highlighting or side-by-side comparison.

**Trigger:**
- After AI generates code edits (inline edit widget, composer panel)
- User can switch between inline and side-by-side modes

### 7.2 Inline Diff Mode

#### 7.2.1 Visual Style

```
Unified diff format (GitHub-style):
  Line 1: unchanged code
- Line 2: old code to be removed      ← Red background
+ Line 2: new code to be added        ← Green background
  Line 3: unchanged code

Color Scheme:
- Deletion line:
  - Background: ColorRed (#F38BA8) at 15% opacity = #F38BA826
  - Text: ColorRed (#F38BA8) at 100%
  - Prefix: "- " (minus space) in ColorRed

- Addition line:
  - Background: ColorGreen (#A6E3A1) at 15% opacity = #A6E3A126
  - Text: ColorGreen (#A6E3A1) at 100%
  - Prefix: "+ " (plus space) in ColorGreen

- Unchanged line:
  - Background: Transparent
  - Text: Normal editor color (ColorText #CDD6F4)
  - Prefix: "  " (two spaces) in Subtext0
```

#### 7.2.2 AvaloniaEdit Integration

```csharp
// Custom IBackgroundRenderer for diff highlighting
public class InlineDiffRenderer : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Background;

    private List<DiffLine> _diffLines;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        foreach (var line in _diffLines)
        {
            var visualLine = textView.GetVisualLine(line.LineNumber);
            if (visualLine == null) continue;

            var geometry = BackgroundGeometryBuilder.GetRectsForSegment(
                textView,
                new TextSegment {
                    StartOffset = visualLine.StartOffset,
                    Length = visualLine.Length
                }
            );

            var backgroundColor = line.Type switch
            {
                DiffLineType.Addition => Color.FromArgb(38, 166, 227, 161), // #A6E3A126
                DiffLineType.Deletion => Color.FromArgb(38, 243, 139, 168), // #F38BA826
                _ => Colors.Transparent
            };

            drawingContext.DrawGeometry(
                new SolidColorBrush(backgroundColor),
                null,
                geometry
            );
        }
    }
}

public class DiffLine
{
    public int LineNumber { get; set; }
    public DiffLineType Type { get; set; }
    public string Content { get; set; }
}

public enum DiffLineType
{
    Unchanged,
    Addition,
    Deletion,
    Context
}
```

### 7.3 Side-by-Side Diff Mode

#### 7.3.1 Layout Structure

```
┌─────────────────────────────────────────────────────────────┐
│ Original                          │ Modified                │ ← Headers
├──────────────────────────────────┼──────────────────────────┤
│   1 │ function calc(a, b) {      │   1 │ function calc(a, b) {│
│   2 │   let sum = 0;             │     │                      │ ← Deletion (red)
│   3 │   sum = a + b;             │   2 │   return a + b;      │ ← Modification
│   4 │   return sum;              │     │                      │ ← Deletion (red)
│   5 │ }                          │   3 │ }                    │
└──────────────────────────────────┴──────────────────────────┘
     ↑ Left pane (original)              ↑ Right pane (modified)
```

#### 7.3.2 XAML Structure

```xml
<Grid ColumnDefinitions="*,1,*">
    <!-- Left pane: Original code -->
    <Border Grid.Column="0"
            Background="{StaticResource ColorBase}"
            BorderBrush="{StaticResource ColorSurface0}"
            BorderThickness="0,0,1,0">
        <DockPanel>
            <!-- Header -->
            <Border DockPanel.Dock="Top"
                    Background="{StaticResource ColorMantle}"
                    Padding="8,4"
                    BorderBrush="{StaticResource ColorSurface0}"
                    BorderThickness="0,0,0,1">
                <TextBlock Text="Original"
                           FontSize="11"
                           FontWeight="SemiBold"
                           Foreground="{StaticResource ColorSubtext1}" />
            </Border>

            <!-- Code editor -->
            <ae:TextEditor x:Name="OriginalEditor"
                           IsReadOnly="True"
                           FontFamily="Cascadia Code, Consolas, monospace"
                           FontSize="12"
                           Foreground="{StaticResource ColorText}"
                           Background="{StaticResource ColorBase}"
                           ShowLineNumbers="True" />
        </DockPanel>
    </Border>

    <!-- Divider -->
    <GridSplitter Grid.Column="1"
                  Width="1"
                  Background="{StaticResource ColorSurface0}"
                  ResizeDirection="Columns" />

    <!-- Right pane: Modified code -->
    <Border Grid.Column="2"
            Background="{StaticResource ColorBase}">
        <DockPanel>
            <!-- Header -->
            <Border DockPanel.Dock="Top"
                    Background="{StaticResource ColorMantle}"
                    Padding="8,4"
                    BorderBrush="{StaticResource ColorSurface0}"
                    BorderThickness="0,0,0,1">
                <TextBlock Text="Modified"
                           FontSize="11"
                           FontWeight="SemiBold"
                           Foreground="{StaticResource ColorSubtext1}" />
            </Border>

            <!-- Code editor -->
            <ae:TextEditor x:Name="ModifiedEditor"
                           IsReadOnly="True"
                           FontFamily="Cascadia Code, Consolas, monospace"
                           FontSize="12"
                           Foreground="{StaticResource ColorText}"
                           Background="{StaticResource ColorBase}"
                           ShowLineNumbers="True" />
        </DockPanel>
    </Border>
</Grid>
```

#### 7.3.3 Synchronized Scrolling

```csharp
// Synchronize vertical scrolling between both editors
public class SideBySideDiffView
{
    private TextEditor _leftEditor;
    private TextEditor _rightEditor;
    private bool _isSyncing;

    public void Initialize()
    {
        _leftEditor.TextArea.TextView.ScrollOffsetChanged += OnLeftScroll;
        _rightEditor.TextArea.TextView.ScrollOffsetChanged += OnRightScroll;
    }

    private void OnLeftScroll(object sender, EventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        _rightEditor.TextArea.TextView.ScrollOffset = _leftEditor.TextArea.TextView.ScrollOffset;
        _isSyncing = false;
    }

    private void OnRightScroll(object sender, EventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        _leftEditor.TextArea.TextView.ScrollOffset = _rightEditor.TextArea.TextView.ScrollOffset;
        _isSyncing = false;
    }
}
```

### 7.4 Change Navigation

#### 7.4.1 Navigation Controls

```xml
<!-- Navigation bar (appears above diff area) -->
<Border Background="{StaticResource ColorSurface0}"
        BorderRadius="4"
        Padding="8,4"
        Margin="0,0,0,4">
    <StackPanel Orientation="Horizontal" Spacing="12">
        <TextBlock Text="Changes:"
                   FontSize="11"
                   Foreground="{StaticResource ColorSubtext1}"
                   VerticalAlignment="Center" />

        <!-- Previous change button -->
        <Button Content="↑ Previous"
                Classes="diffNav"
                Command="{Binding PreviousChangeCommand}"
                IsEnabled="{Binding HasPreviousChange}"
                ToolTip.Tip="Alt+F5"
                Background="{StaticResource ColorSurface1}"
                Padding="8,4"
                FontSize="11"
                CornerRadius="4" />

        <!-- Change counter -->
        <TextBlock FontSize="11"
                   Foreground="{StaticResource ColorSubtext0}"
                   VerticalAlignment="Center">
            <Run Text="{Binding CurrentChangeIndex}" FontWeight="SemiBold" />
            <Run Text=" of " />
            <Run Text="{Binding TotalChanges}" FontWeight="SemiBold" />
        </TextBlock>

        <!-- Next change button -->
        <Button Content="Next ↓"
                Classes="diffNav"
                Command="{Binding NextChangeCommand}"
                IsEnabled="{Binding HasNextChange}"
                ToolTip.Tip="F5"
                Background="{StaticResource ColorSurface1}"
                Padding="8,4"
                FontSize="11"
                CornerRadius="4" />

        <!-- View mode toggle -->
        <ComboBox SelectedIndex="{Binding DiffViewMode}"
                  MinWidth="120"
                  FontSize="11">
            <ComboBoxItem Content="Inline" />
            <ComboBoxItem Content="Side-by-Side" />
        </ComboBox>
    </StackPanel>
</Border>
```

#### 7.4.2 Jump to Change

```csharp
public class DiffNavigator
{
    private List<DiffHunk> _hunks; // List of change blocks
    private int _currentHunkIndex;

    public void NextChange()
    {
        if (_currentHunkIndex < _hunks.Count - 1)
        {
            _currentHunkIndex++;
            ScrollToHunk(_hunks[_currentHunkIndex]);
            HighlightHunk(_hunks[_currentHunkIndex]);
        }
    }

    public void PreviousChange()
    {
        if (_currentHunkIndex > 0)
        {
            _currentHunkIndex--;
            ScrollToHunk(_hunks[_currentHunkIndex]);
            HighlightHunk(_hunks[_currentHunkIndex]);
        }
    }

    private void ScrollToHunk(DiffHunk hunk)
    {
        // Scroll editor to show the hunk at center of viewport
        var targetLine = hunk.StartLine;
        var textView = _editor.TextArea.TextView;
        var documentLine = _editor.Document.GetLineByNumber(targetLine);
        _editor.ScrollTo(targetLine, 0);
    }

    private void HighlightHunk(DiffHunk hunk)
    {
        // Briefly highlight the hunk (flash animation)
        // Add temporary background overlay for 500ms
    }
}

public class DiffHunk
{
    public int StartLine { get; set; }
    public int LineCount { get; set; }
    public DiffHunkType Type { get; set; } // Addition, Deletion, Modification
}
```

### 7.5 Accept/Reject Per Hunk

#### 7.5.1 Inline Action Buttons

For each change hunk, show accept/reject buttons:

```xml
<!-- Appears in gutter next to changed lines -->
<StackPanel Orientation="Horizontal"
            Spacing="4"
            Margin="4,0">
    <!-- Accept button -->
    <Button Content="✓"
            Classes="hunkAction accept"
            Command="{Binding AcceptHunkCommand}"
            CommandParameter="{Binding HunkIndex}"
            ToolTip.Tip="Accept this change"
            Width="20" Height="20"
            Padding="0"
            FontSize="10"
            Background="{StaticResource ColorGreen}"
            Foreground="{StaticResource ColorBase}"
            CornerRadius="3" />

    <!-- Reject button -->
    <Button Content="✗"
            Classes="hunkAction reject"
            Command="{Binding RejectHunkCommand}"
            CommandParameter="{Binding HunkIndex}"
            ToolTip.Tip="Reject this change"
            Width="20" Height="20"
            Padding="0"
            FontSize="10"
            Background="{StaticResource ColorRed}"
            Foreground="{StaticResource ColorBase}"
            CornerRadius="3" />
</StackPanel>
```

#### 7.5.2 Partial Acceptance Flow

```
User Flow:
1. Diff shows 5 change hunks (blocks of changes)
2. User reviews Hunk 1, clicks ✓ Accept
   - Hunk 1 applied to document
   - Hunk 1 changes from highlighted to "applied" state (checkmark icon)
3. User reviews Hunk 2, clicks ✗ Reject
   - Hunk 2 discarded (not applied)
   - Hunk 2 changes from highlighted to "rejected" state (grayed out)
4. User accepts remaining hunks (3, 4, 5)
5. Final result: Document has changes from hunks 1, 3, 4, 5; hunk 2 omitted

Hunk States:
- Pending: Normal diff colors (red/green), action buttons visible
- Accepted: Green checkmark icon, text grayed out, buttons hidden
- Rejected: Red X icon, text grayed out, buttons hidden
```

### 7.6 Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **F5** | Navigate to next change hunk |
| **Alt+F5** | Navigate to previous change hunk |
| **Ctrl+Enter** | Accept all changes |
| **Ctrl+Shift+Enter** | Accept current hunk only |
| **Esc** | Reject all changes, close diff view |
| **Ctrl+Alt+V** | Toggle inline/side-by-side view mode |

---

## 8. Accessibility & Keyboard Navigation

### 8.1 General Principles

```
WCAG 2.1 AA Compliance:
- Contrast ratio ≥ 4.5:1 for normal text
- Contrast ratio ≥ 3:1 for large text (18px+) and UI components
- All interactive elements keyboard-accessible
- Focus indicators visible (2px outline, ColorBlue)
- Screen reader support (ARIA labels, roles)
```

### 8.2 Contrast Validation

#### 8.2.1 Text Contrast

| Text Type | Foreground | Background | Ratio | Pass |
|-----------|------------|------------|-------|------|
| Body text | #CDD6F4 | #1E1E2E | 11.2:1 | ✓ AAA |
| Secondary text | #BAC2DE | #1E1E2E | 8.7:1 | ✓ AAA |
| Tertiary text | #A6ADC8 | #1E1E2E | 6.3:1 | ✓ AA |
| Ghost text (60%) | #6C7086 | #1E1E2E | 3.2:1 | ✗ Fail (intentional low contrast) |
| Blue accent | #89B4FA | #1E1E2E | 8.9:1 | ✓ AAA |
| Green success | #A6E3A1 | #1E1E2E | 10.1:1 | ✓ AAA |
| Red error | #F38BA8 | #1E1E2E | 7.2:1 | ✓ AAA |

**Note:** Ghost text intentionally has low contrast to appear subtle. It is not primary content and disappears when user interacts.

#### 8.2.2 UI Component Contrast

| Component | Foreground | Background | Ratio | Pass |
|-----------|------------|------------|-------|------|
| Button primary | #11111B | #89B4FA | 12.8:1 | ✓ AAA |
| Button danger | #F38BA8 | #45475A | 7.8:1 | ✓ AAA |
| Input border | #6C7086 | #1E1E2E | 3.2:1 | ✓ AA |
| Badge (Green) | #1E1E2E | #A6E3A1 | 11.4:1 | ✓ AAA |
| Badge (Red) | #1E1E2E | #F38BA8 | 8.1:1 | ✓ AAA |

### 8.3 Focus Management

#### 8.3.1 Focus Indicators

```xml
<!-- Universal focus style -->
<Style Selector="TextBox:focus,ComboBox:focus,Button:focus">
    <Setter Property="BorderBrush" Value="{StaticResource ColorBlue}" />
    <Setter Property="BorderThickness" Value="2" />
</Style>

<!-- For elements without borders (text blocks, menu items) -->
<Style Selector="ListBoxItem:focus,TreeViewItem:focus">
    <Setter Property="Background" Value="{StaticResource ColorSurface1}" />
    <Setter Property="BorderBrush" Value="{StaticResource ColorBlue}" />
    <Setter Property="BorderThickness" Value="2,0,0,0" /> <!-- Left border indicator -->
</Style>
```

#### 8.3.2 Focus Order (Tab Navigation)

```
Main Window Tab Order:
1. Activity Bar (Ctrl+B to toggle focus)
2. Sidebar (Ctrl+Shift+E to focus Explorer)
3. Editor area (Ctrl+1 to focus first editor tab)
4. Right panels (Ctrl+Shift+A to focus Agent panel)
5. Chat input (Ctrl+L to focus)
6. Status bar (Ctrl+Shift+B to focus)

Within Chat Input:
1. Input textbox
2. Model selector dropdown
3. Send button

Within Inline Edit Widget:
1. Input textbox
2. Accept button (if diff shown)
3. Reject button
4. Retry button (if diff shown)

Within Composer Panel:
1. File tree (navigate with arrow keys)
2. Apply All button
3. Apply Selected button
4. Discard button
```

### 8.4 Keyboard Shortcuts Summary

#### 8.4.1 Global Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+Shift+P** | Open command palette |
| **Ctrl+P** | Quick open file |
| **Ctrl+Shift+E** | Toggle Explorer sidebar |
| **Ctrl+Shift+G** | Toggle Git sidebar |
| **Ctrl+L** | Focus chat input |
| **Ctrl+`** | Toggle terminal |
| **Ctrl+B** | Toggle sidebar visibility |
| **Ctrl+K, Ctrl+S** | Open keyboard shortcuts editor |

#### 8.4.2 Editor Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+K** | Show inline edit widget (with selection) |
| **Tab** | Accept ghost text completion |
| **Esc** | Dismiss ghost text |
| **Ctrl+Space** | Manually trigger ghost text |
| **F5** | Navigate to next diff change |
| **Alt+F5** | Navigate to previous diff change |
| **Ctrl+Enter** | Accept diff changes |
| **Ctrl+Alt+V** | Toggle diff view mode |

#### 8.4.3 Chat Shortcuts

| Shortcut | Action |
|----------|--------|
| **Enter** | Send message |
| **Shift+Enter** | New line in input |
| **Esc** | Cancel operation (if processing) |
| **@** | Open mention picker |
| **Arrow Up/Down** | Navigate mention picker |
| **Tab** | Select mention item and continue |

#### 8.4.4 Composer Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+Enter** | Apply all changes |
| **Enter** | Apply selected changes |
| **Esc** | Close composer panel |
| **Space** | Toggle expand/collapse on focused file |
| **Ctrl+A** | Select all files |

### 8.5 Screen Reader Support

#### 8.5.1 ARIA Labels

```xml
<!-- Chat input with ARIA labels -->
<TextBox x:Name="InputBox"
         AutomationProperties.Name="Chat message input"
         AutomationProperties.HelpText="Type a message and press Enter to send, or Shift+Enter for new line"
         ... />

<!-- Model selector -->
<ComboBox x:Name="ModelSelector"
          AutomationProperties.Name="AI model selector"
          AutomationProperties.HelpText="Select which AI model to use for chat"
          ... />

<!-- Send button -->
<Button AutomationProperties.Name="Send message"
        AutomationProperties.HelpText="Send the chat message, or press Enter"
        Content="▶" />

<!-- Ghost text indicator (for screen readers) -->
<TextBlock Text="{Binding GhostText}"
           AutomationProperties.Name="AI completion suggestion"
           AutomationProperties.HelpText="Press Tab to accept, Escape to dismiss"
           IsVisible="False" /> <!-- Hidden but readable by screen readers -->
```

#### 8.5.2 Live Regions

```xml
<!-- Announce chat message updates -->
<TextBlock AutomationProperties.LiveSetting="Polite"
           Text="{Binding LatestMessage}"
           IsVisible="False" /> <!-- Hidden but announces changes -->

<!-- Announce status updates -->
<TextBlock AutomationProperties.LiveSetting="Assertive"
           Text="{Binding StatusMessage}"
           IsVisible="False" /> <!-- For urgent updates (errors, completions) -->
```

---

## 9. Implementation Notes

### 9.1 Performance Considerations

#### 9.1.1 Ghost Text

```
Optimization:
- Debounce AI requests (1s default, configurable)
- Cancel in-flight requests on new input
- Cache completion responses for 30 seconds
- Limit context size to 5KB per request
- Use streaming responses if API supports (show progressive completion)

Metrics to Monitor:
- Request latency: Target < 2s for 90th percentile
- UI thread blocking: Target < 16ms per frame (60 FPS)
- Memory usage: Max 50MB for completion cache
```

#### 9.1.2 Diff Rendering

```
Optimization:
- Use virtual scrolling for large diffs (>1000 lines)
- Render only visible lines + 50 lines buffer
- Lazy-load syntax highlighting (highlight visible lines first)
- Cache diff calculation results

Metrics:
- Render time: < 100ms for 500-line diff
- Scroll performance: Maintain 60 FPS during scrolling
```

#### 9.1.3 Mention Picker

```
Optimization:
- Index files on workspace open (background task)
- Cache fuzzy search results for 10 seconds
- Limit results to 20 items (top matches)
- Debounce search query (100ms)

Metrics:
- Picker open time: < 50ms
- Search response: < 50ms for 90th percentile
- Index size: < 10MB for 10K files
```

### 9.2 Error Handling Strategy

#### 9.2.1 User-Visible Errors

```
Toast Notifications:
- Network errors: "Connection failed. Check your network."
- API errors: "AI service unavailable. Please try again."
- File errors: "Could not save file. Check permissions."
- Timeout errors: "Request timed out. Try again or reduce context size."

Error Dialog (blocking):
- Critical errors only: Workspace load failure, settings corruption
- Provide recovery actions: "Restore defaults", "Open safe mode"

Inline Errors (non-intrusive):
- Show error icon next to failed item (e.g., composer file)
- Tooltip shows error message
- User can click to retry or dismiss
```

#### 9.2.2 Silent Errors (Logged Only)

```
Log to File:
- Completion request failures (after retry)
- Diff calculation errors (use plain text fallback)
- File watcher errors (background indexing)
- Syntax highlighting errors (use plain text fallback)

Log Format:
[2026-02-12 14:32:15] [ERROR] GhostTextService: Request failed after 3 retries
  Error: System.Net.Http.HttpRequestException: Connection timeout
  Context: File=Program.cs, Line=42, ContextSize=2.3KB
```

### 9.3 Testing Checklist

#### 9.3.1 Ghost Text

```
Manual Tests:
- [ ] Ghost text appears after 1s pause
- [ ] Tab key accepts completion
- [ ] Esc key dismisses completion
- [ ] Typing dismisses and starts new cycle
- [ ] Arrow keys dismiss completion
- [ ] Loading spinner shows during AI request
- [ ] Multi-line completions render correctly
- [ ] Ghost text respects indentation
- [ ] Works at end of file
- [ ] Works inside strings and comments

Automated Tests:
- [ ] Debounce timer cancels on new input
- [ ] Request cancellation works
- [ ] Cache hit/miss logic
- [ ] Context size limit enforced
```

#### 9.3.2 Inline Edit Widget

```
Manual Tests:
- [ ] Widget appears below selection
- [ ] Widget positions correctly at viewport edges
- [ ] Input accepts multi-line text
- [ ] Enter submits instruction
- [ ] Esc closes widget
- [ ] Diff renders with correct colors
- [ ] Accept applies changes to document
- [ ] Reject restores original text
- [ ] Retry clears input and keeps selection
- [ ] Loading spinner shows during processing

Automated Tests:
- [ ] Widget placement algorithm (viewport overflow)
- [ ] Diff algorithm (unified format)
- [ ] Accept/reject state transitions
- [ ] Error handling (timeout, network failure)
```

#### 9.3.3 Mention Picker

```
Manual Tests:
- [ ] Picker opens on @ character
- [ ] Fuzzy search filters results
- [ ] Arrow keys navigate list
- [ ] Enter selects item
- [ ] Tab selects and adds space
- [ ] Esc closes picker
- [ ] Backspace after @ closes picker
- [ ] File icons show correct colors
- [ ] Symbol search works (@#)
- [ ] Scoped search works (@folder/)

Automated Tests:
- [ ] Fuzzy search scoring algorithm
- [ ] File index building
- [ ] Result limit enforcement (20 items)
- [ ] Query parsing (@, @#, @folder/)
```

#### 9.3.4 Composer Panel

```
Manual Tests:
- [ ] Panel opens when AI plan received
- [ ] File tree renders all items
- [ ] Action badges show correct colors
- [ ] Expand/collapse works
- [ ] Diff preview renders correctly
- [ ] Apply All processes all files
- [ ] Apply Selected processes only selected
- [ ] Progress bar updates during apply
- [ ] Status icons show correctly
- [ ] Discard clears panel and closes

Automated Tests:
- [ ] File tree building from plan
- [ ] Selection state management
- [ ] Progress calculation
- [ ] File operation execution (create/modify/delete)
- [ ] Error handling per file
```

### 9.4 Accessibility Testing

```
Manual Tests:
- [ ] All features keyboard-accessible (no mouse required)
- [ ] Tab navigation follows logical order
- [ ] Focus indicators visible on all interactive elements
- [ ] Screen reader announces chat messages
- [ ] Screen reader announces status updates
- [ ] Screen reader reads ghost text suggestions
- [ ] Screen reader describes diff changes
- [ ] High contrast mode works (if OS supports)

Automated Tests (Accessibility Insights):
- [ ] No missing ARIA labels
- [ ] No keyboard traps
- [ ] Contrast ratios meet WCAG 2.1 AA
- [ ] Touch target sizes ≥ 24x24px
```

---

## 10. Visual Design Principles Summary

### 10.1 Color Usage Guidelines

```
Primary Actions:
- Use Blue (#89B4FA) for primary buttons, links, accents
- White/light text on Blue background for high contrast

Success States:
- Use Green (#A6E3A1) for success messages, additions, create actions
- Dark text on Green background

Error States:
- Use Red (#F38BA8) for errors, deletions, warnings
- Dark text on Red background

Neutral Actions:
- Use Surface colors (#313244, #45475A) for secondary buttons
- Light text on dark background

Backgrounds:
- Base (#1E1E2E) for main areas
- Mantle (#181825) for inset/recessed areas
- Crust (#11111B) for deep inset (inputs)
- Surface0-2 for elevated elements (cards, popups)
```

### 10.2 Animation Guidelines

```
When to Animate:
- State changes (button hover, focus)
- Element appearance (fade in, slide down)
- Loading indicators (spinner, progress bar)
- Attention-grabbing (error shake, success pulse)

When NOT to Animate:
- Large content updates (chat messages)
- Instant feedback actions (keyboard input)
- Continuous scrolling
- Background operations

Animation Specs:
- Duration: 150-300ms for most transitions
- Easing: ease-out for appearing elements, ease-in for disappearing
- Delay: 0ms (no artificial delays)
- Respect user preferences: Disable animations if OS prefers reduced motion
```

### 10.3 Typography Best Practices

```
Hierarchy:
- Use size for importance (headers > body > captions)
- Use weight for emphasis (bold for headers, regular for body)
- Use color for meaning (blue for links, red for errors, gray for secondary)

Readability:
- Line height 1.4-1.6 for body text
- Line length max 80 characters for prose
- Sufficient contrast (4.5:1 minimum for body text)
- Monospace for code, sans-serif for UI

Consistency:
- Limit to 3-4 font sizes per component
- Use semantic size variables (TextLarge, TextBody, TextSmall)
- Maintain consistent spacing between text elements
```

---

## Appendix A: Color Palette Quick Reference

```
Catppuccin Mocha Color Tokens (Hex Values):

Backgrounds:
  Base       #1E1E2E   Main editor background
  Mantle     #181825   Sidebar, tab bar
  Crust      #11111B   Deep inset, input fields

Surfaces:
  Surface0   #313244   Cards, borders, dividers
  Surface1   #45475A   Hover states
  Surface2   #585B70   Active/pressed states

Overlays:
  Overlay0   #6C7086   Ghost text, subtle elements
  Overlay1   #7F849C   Muted text
  Overlay2   #9399B2   Disabled elements

Text:
  Text       #CDD6F4   Primary text
  Subtext1   #BAC2DE   Secondary text
  Subtext0   #A6ADC8   Tertiary text, placeholders

Accents:
  Blue       #89B4FA   Primary accent, links
  Green      #A6E3A1   Success, additions
  Red        #F38BA8   Errors, deletions
  Yellow     #F9E2AF   Warnings
  Mauve      #CBA6F7   Secondary accent
  Peach      #FAB387   Modifications
  Teal       #94E2D5   Info
  Sky        #89DCEB   Highlights
```

---

## Appendix B: Component Dimensions

```
Standard Component Sizes:

Buttons:
  Height: 32px (default), 28px (small), 40px (large)
  Padding: 12px horizontal, 8px vertical
  Border radius: 6px
  Font size: 12px (small), 13px (default), 14px (large)

Inputs (TextBox):
  Height: 40px (single line), auto (multi-line, max 150px)
  Padding: 10px horizontal, 8px vertical
  Border radius: 6px
  Border width: 1px
  Font size: 13px

Badges:
  Height: 20px
  Padding: 8px horizontal, 2px vertical
  Border radius: 4px
  Font size: 10px

Chips (Mention Tags):
  Height: 24px
  Padding: 6px horizontal, 2px vertical
  Border radius: 4px
  Font size: 11px

Dropdowns (ComboBox):
  Height: 28-32px
  Padding: 8px horizontal, 4px vertical
  Border radius: 4px
  Item height: 32px

Panels:
  Header height: 36px
  Header padding: 12px horizontal, 8px vertical
  Content padding: 8-12px

Modals/Dialogs:
  Border radius: 12px
  Padding: 16px
  Max width: 600px
  Min width: 400px
```

---

## Appendix C: Glossary

```
Terms:

Ghost Text:
  AI-generated code completion shown inline at cursor position in gray/italic text.

Inline Edit Widget:
  Floating panel that appears below selected code, allowing natural language edit instructions.

Mention Picker:
  Dropdown that appears when typing @ in chat, for selecting files/symbols to reference.

Composer Panel:
  Right-side panel showing multi-file AI edit plan with tree view and diff previews.

Diff Hunk:
  A contiguous block of changed lines in a diff (addition, deletion, or modification).

Chip/Tag:
  Small styled inline element representing a file/symbol mention in chat input.

Action Badge:
  Colored label on composer file items showing action type (Create/Modify/Delete).

Status Icon:
  Icon showing file status in composer (pending/applying/success/failed).

Debounce:
  Delay before triggering action, allowing multiple rapid inputs to consolidate into one action.

Fuzzy Search:
  Search that matches partial, non-contiguous characters (e.g., "flexp" matches "FileExplorer").
```

---

**End of Document**

This specification provides implementation-ready details for all AI-native editor features in MiniClaudeCode. All color values, dimensions, animations, and interactions are precisely defined for direct translation to Avalonia XAML and C# code.
