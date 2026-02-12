namespace MiniClaudeCode.Avalonia.Editor.TextBuffer;

/// <summary>
/// Node color for the red-black tree.
/// </summary>
public enum NodeColor
{
    Red,
    Black
}

/// <summary>
/// A node in the red-black tree that backs the piece tree.
/// Augmented with subtree size and line feed count for O(log n) lookups.
/// </summary>
public class TreeNode
{
    public Piece Piece { get; set; }
    public NodeColor Color { get; set; }
    public TreeNode? Left { get; set; }
    public TreeNode? Right { get; set; }
    public TreeNode? Parent { get; set; }

    /// <summary>Size (chars) of left subtree.</summary>
    public int SizeLeft { get; set; }

    /// <summary>Line feed count of left subtree.</summary>
    public int LfLeft { get; set; }

    public TreeNode(Piece piece, NodeColor color)
    {
        Piece = piece;
        Color = color;
    }
}

/// <summary>
/// Piece tree implementation using a red-black tree for O(log n) edit operations.
/// Based on VS Code's PieceTreeBase in pieceTreeBase.ts.
/// 
/// The tree is augmented: each node stores the total char size and line-feed count
/// of its left subtree, enabling O(log n) offset-to-node and line-to-offset lookups.
/// </summary>
public class PieceTreeBase
{
    private readonly List<StringBuffer> _buffers = [];
    private TreeNode? _root;
    private int _totalLength;
    private int _totalLineCount = 1;

    // Sentinel for NIL leaves (simplifies RB tree logic)
    private static readonly TreeNode Sentinel = new(
        new Piece(0, new BufferCursor(0, 0), new BufferCursor(0, 0), 0, 0),
        NodeColor.Black);

    /// <summary>Total character length of the document.</summary>
    public int Length => _totalLength;

    /// <summary>Total number of lines.</summary>
    public int LineCount => _totalLineCount;

    /// <summary>
    /// Create a piece tree from initial string buffers.
    /// </summary>
    public PieceTreeBase(List<StringBuffer> buffers)
    {
        _buffers.Add(new StringBuffer("", [0])); // buffer 0 = change buffer

        foreach (var buf in buffers)
        {
            var bufferIndex = _buffers.Count;
            _buffers.Add(buf);

            if (buf.Content.Length > 0)
            {
                var lineFeeds = buf.LineStarts.Length - 1;
                var piece = new Piece(
                    bufferIndex,
                    new BufferCursor(0, 0),
                    new BufferCursor(buf.LineStarts.Length - 1,
                        buf.Content.Length - buf.LineStarts[^1]),
                    lineFeeds,
                    buf.Content.Length
                );
                RbInsert(piece);
                _totalLength += buf.Content.Length;
                _totalLineCount += lineFeeds;
            }
        }
    }

    /// <summary>
    /// Get the text content of a specific line (1-based).
    /// </summary>
    public string GetLineContent(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _totalLineCount) return "";

        var startOffset = GetOffsetOfLine(lineNumber);
        var endOffset = lineNumber < _totalLineCount
            ? GetOffsetOfLine(lineNumber + 1)
            : _totalLength;

        var content = GetValueInRange(startOffset, endOffset);

        if (content.EndsWith("\r\n")) return content[..^2];
        if (content.EndsWith("\n") || content.EndsWith("\r")) return content[..^1];
        return content;
    }

    /// <summary>
    /// Get all text content.
    /// </summary>
    public string GetAllText()
    {
        var sb = new System.Text.StringBuilder(_totalLength);
        InOrderTraversal(_root, node =>
        {
            var buffer = _buffers[node.Piece.BufferIndex];
            var start = GetBufferOffset(buffer, node.Piece.Start);
            sb.Append(buffer.Content, start, node.Piece.Length);
        });
        return sb.ToString();
    }

    /// <summary>
    /// Insert text at a given offset.
    /// </summary>
    public void Insert(int offset, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Append to change buffer
        var changeBuffer = _buffers[0];
        var insertStart = changeBuffer.Content.Length;
        var newContent = changeBuffer.Content + text;
        var newLineStarts = StringBuffer.ComputeLineStarts(newContent);
        _buffers[0] = new StringBuffer(newContent, newLineStarts);

        var startCursor = ComputeBufferCursor(_buffers[0], insertStart);
        var endCursor = ComputeBufferCursor(_buffers[0], insertStart + text.Length);
        var lineFeeds = CountLineFeeds(text);

        var newPiece = new Piece(0, startCursor, endCursor, lineFeeds, text.Length);

        if (_root == null)
        {
            RbInsert(newPiece);
        }
        else if (offset <= 0)
        {
            InsertBeforeFirst(newPiece);
        }
        else if (offset >= _totalLength)
        {
            InsertAfterLast(newPiece);
        }
        else
        {
            // Find the node that contains the offset and split
            var (node, remainder) = NodeAtOffset(offset);
            if (node != null && remainder > 0 && remainder < node.Piece.Length)
            {
                SplitAndInsert(node, remainder, newPiece);
            }
            else if (node != null)
            {
                InsertAfterNode(node, newPiece);
            }
        }

        _totalLength += text.Length;
        _totalLineCount += lineFeeds;
    }

    /// <summary>
    /// Delete text from offset with given length.
    /// </summary>
    public void Delete(int offset, int length)
    {
        if (length <= 0 || offset < 0 || offset >= _totalLength) return;
        length = Math.Min(length, _totalLength - offset);

        var deletedText = GetValueInRange(offset, offset + length);
        var removedLf = CountLineFeeds(deletedText);

        DeleteRange(offset, length);

        _totalLength -= length;
        _totalLineCount -= removedLf;
    }

    /// <summary>
    /// Create a snapshot for saving.
    /// </summary>
    public IEnumerable<string> CreateSnapshot()
    {
        var result = new List<string>();
        InOrderTraversal(_root, node =>
        {
            var buffer = _buffers[node.Piece.BufferIndex];
            var start = GetBufferOffset(buffer, node.Piece.Start);
            result.Add(buffer.Content.Substring(start, node.Piece.Length));
        });
        return result;
    }

    // =========================================================================
    // Tree-based offset lookups
    // =========================================================================

    /// <summary>Find the node containing the given offset and the remainder within it.</summary>
    private (TreeNode? node, int remainder) NodeAtOffset(int offset)
    {
        var node = _root;
        while (node != null)
        {
            if (offset < node.SizeLeft)
            {
                node = node.Left;
            }
            else if (offset < node.SizeLeft + node.Piece.Length)
            {
                return (node, offset - node.SizeLeft);
            }
            else
            {
                offset -= node.SizeLeft + node.Piece.Length;
                node = node.Right;
            }
        }
        return (null, 0);
    }

    private string GetValueInRange(int startOffset, int endOffset)
    {
        if (startOffset >= endOffset) return "";

        var sb = new System.Text.StringBuilder(endOffset - startOffset);
        int remaining = endOffset - startOffset;

        // Find starting node
        var (startNode, startRemainder) = NodeAtOffset(startOffset);
        if (startNode == null) return sb.ToString();

        // Read from starting node
        var buffer = _buffers[startNode.Piece.BufferIndex];
        var bufStart = GetBufferOffset(buffer, startNode.Piece.Start);
        var readLen = Math.Min(startNode.Piece.Length - startRemainder, remaining);
        sb.Append(buffer.Content, bufStart + startRemainder, readLen);
        remaining -= readLen;

        if (remaining <= 0) return sb.ToString();

        // Continue in-order from the next node
        var current = InOrderSuccessor(startNode);
        while (current != null && remaining > 0)
        {
            buffer = _buffers[current.Piece.BufferIndex];
            bufStart = GetBufferOffset(buffer, current.Piece.Start);
            readLen = Math.Min(current.Piece.Length, remaining);
            sb.Append(buffer.Content, bufStart, readLen);
            remaining -= readLen;
            current = InOrderSuccessor(current);
        }

        return sb.ToString();
    }

    private int GetOffsetOfLine(int lineNumber)
    {
        if (lineNumber <= 1) return 0;

        int currentLine = 1;
        int currentOffset = 0;
        var pieces = new List<TreeNode>();
        InOrderTraversal(_root, n => pieces.Add(n));

        foreach (var node in pieces)
        {
            if (currentLine + node.Piece.LineFeedCount >= lineNumber)
            {
                var buffer = _buffers[node.Piece.BufferIndex];
                var bufferStart = GetBufferOffset(buffer, node.Piece.Start);
                var text = buffer.Content.Substring(bufferStart, node.Piece.Length);

                int lineInPiece = lineNumber - currentLine;
                int pos = 0;
                for (int i = 0; i < lineInPiece && pos < text.Length; i++)
                {
                    var idx = text.IndexOf('\n', pos);
                    if (idx < 0) break;
                    pos = idx + 1;
                }
                return currentOffset + pos;
            }
            currentLine += node.Piece.LineFeedCount;
            currentOffset += node.Piece.Length;
        }

        return _totalLength;
    }

    // =========================================================================
    // Insert / Delete helpers
    // =========================================================================

    private void InsertBeforeFirst(Piece piece)
    {
        var first = TreeMinimum(_root!);
        InsertBeforeNode(first, piece);
    }

    private void InsertAfterLast(Piece piece)
    {
        var last = TreeMaximum(_root!);
        InsertAfterNode(last, piece);
    }

    private void InsertBeforeNode(TreeNode node, Piece piece)
    {
        var newNode = new TreeNode(piece, NodeColor.Red);
        if (node.Left == null)
        {
            node.Left = newNode;
            newNode.Parent = node;
        }
        else
        {
            var pred = TreeMaximum(node.Left);
            pred.Right = newNode;
            newNode.Parent = pred;
        }
        UpdateMetadataUpward(newNode);
        RbInsertFixup(newNode);
    }

    private void InsertAfterNode(TreeNode node, Piece piece)
    {
        var newNode = new TreeNode(piece, NodeColor.Red);
        if (node.Right == null)
        {
            node.Right = newNode;
            newNode.Parent = node;
        }
        else
        {
            var succ = TreeMinimum(node.Right);
            succ.Left = newNode;
            newNode.Parent = succ;
        }
        UpdateMetadataUpward(newNode);
        RbInsertFixup(newNode);
    }

    private void SplitAndInsert(TreeNode node, int splitPos, Piece newPiece)
    {
        var original = node.Piece;
        var buffer = _buffers[original.BufferIndex];
        var bufferStart = GetBufferOffset(buffer, original.Start);

        // Left piece
        var leftLen = splitPos;
        var leftText = buffer.Content.Substring(bufferStart, leftLen);
        var leftLf = CountLineFeeds(leftText);
        var leftEnd = ComputeBufferCursor(buffer, bufferStart + leftLen);
        var leftPiece = new Piece(original.BufferIndex, original.Start, leftEnd, leftLf, leftLen);

        // Right piece
        var rightLen = original.Length - splitPos;
        var rightStart = ComputeBufferCursor(buffer, bufferStart + splitPos);
        var rightLf = original.LineFeedCount - leftLf;
        var rightPiece = new Piece(original.BufferIndex, rightStart, original.End, Math.Max(0, rightLf), rightLen);

        // Replace original node's piece with left piece
        node.Piece = leftPiece;
        UpdateMetadataUpward(node);

        // Insert new piece after the left part
        InsertAfterNode(node, newPiece);

        // Find the newly inserted node (it's now the successor of node)
        var newNode = InOrderSuccessor(node);
        if (newNode != null)
        {
            InsertAfterNode(newNode, rightPiece);
        }
    }

    private void DeleteRange(int offset, int length)
    {
        var deleteEnd = offset + length;

        // Collect nodes to modify
        var (startNode, startRemainder) = NodeAtOffset(offset);
        if (startNode == null) return;

        var (endNode, endRemainder) = NodeAtOffset(deleteEnd > 0 ? deleteEnd - 1 : 0);

        if (startNode == endNode)
        {
            // Deletion within a single node
            DeleteWithinNode(startNode, startRemainder, length);
            return;
        }

        // Multi-node deletion: trim start node, remove middle nodes, trim end node
        var buffer = _buffers[startNode.Piece.BufferIndex];
        var bufferStart = GetBufferOffset(buffer, startNode.Piece.Start);

        // Trim start node (keep left part)
        if (startRemainder > 0)
        {
            var leftLen = startRemainder;
            var leftText = buffer.Content.Substring(bufferStart, leftLen);
            var leftLf = CountLineFeeds(leftText);
            var leftEnd = ComputeBufferCursor(buffer, bufferStart + leftLen);
            startNode.Piece = new Piece(startNode.Piece.BufferIndex, startNode.Piece.Start, leftEnd, leftLf, leftLen);
            UpdateMetadataUpward(startNode);
        }

        // Trim end node (keep right part)
        if (endNode != null)
        {
            var endBuffer = _buffers[endNode.Piece.BufferIndex];
            var endBufStart = GetBufferOffset(endBuffer, endNode.Piece.Start);
            var skipLen = endRemainder + 1;
            if (skipLen < endNode.Piece.Length)
            {
                var rightLen = endNode.Piece.Length - skipLen;
                var rightStart = ComputeBufferCursor(endBuffer, endBufStart + skipLen);
                var skippedText = endBuffer.Content.Substring(endBufStart, skipLen);
                var skippedLf = CountLineFeeds(skippedText);
                var rightLf = endNode.Piece.LineFeedCount - skippedLf;
                endNode.Piece = new Piece(endNode.Piece.BufferIndex, rightStart, endNode.Piece.End, Math.Max(0, rightLf), rightLen);
                UpdateMetadataUpward(endNode);
            }
            else
            {
                RbDelete(endNode);
            }
        }

        // Remove nodes between start and end
        if (startRemainder == 0)
        {
            // Start node is fully deleted
            var next = InOrderSuccessor(startNode);
            RbDelete(startNode);
            // Remove intermediate nodes
            while (next != null && next != endNode)
            {
                var nextNext = InOrderSuccessor(next);
                RbDelete(next);
                next = nextNext;
            }
        }
        else
        {
            // Start node was trimmed, remove nodes after it until endNode
            var next = InOrderSuccessor(startNode);
            while (next != null && next != endNode)
            {
                var nextNext = InOrderSuccessor(next);
                RbDelete(next);
                next = nextNext;
            }
        }
    }

    private void DeleteWithinNode(TreeNode node, int startPos, int length)
    {
        var original = node.Piece;
        var buffer = _buffers[original.BufferIndex];
        var bufferStart = GetBufferOffset(buffer, original.Start);

        if (startPos == 0 && length >= original.Length)
        {
            // Delete entire node
            RbDelete(node);
            return;
        }

        if (startPos == 0)
        {
            // Delete from start
            var newStart = ComputeBufferCursor(buffer, bufferStart + length);
            var deletedText = buffer.Content.Substring(bufferStart, length);
            var deletedLf = CountLineFeeds(deletedText);
            var newLen = original.Length - length;
            node.Piece = new Piece(original.BufferIndex, newStart, original.End,
                Math.Max(0, original.LineFeedCount - deletedLf), newLen);
            UpdateMetadataUpward(node);
            return;
        }

        if (startPos + length >= original.Length)
        {
            // Delete from middle to end
            var leftEnd = ComputeBufferCursor(buffer, bufferStart + startPos);
            var leftText = buffer.Content.Substring(bufferStart, startPos);
            var leftLf = CountLineFeeds(leftText);
            node.Piece = new Piece(original.BufferIndex, original.Start, leftEnd, leftLf, startPos);
            UpdateMetadataUpward(node);
            return;
        }

        // Delete from middle - split into two
        var leftLen = startPos;
        var leftPartText = buffer.Content.Substring(bufferStart, leftLen);
        var leftPartLf = CountLineFeeds(leftPartText);
        var leftPartEnd = ComputeBufferCursor(buffer, bufferStart + leftLen);
        var leftPiece = new Piece(original.BufferIndex, original.Start, leftPartEnd, leftPartLf, leftLen);

        var rightOffset = bufferStart + startPos + length;
        var rightLen = original.Length - startPos - length;
        var rightStart = ComputeBufferCursor(buffer, rightOffset);
        var deletedMidText = buffer.Content.Substring(bufferStart + startPos, length);
        var deletedMidLf = CountLineFeeds(deletedMidText);
        var rightLf = original.LineFeedCount - leftPartLf - deletedMidLf;
        var rightPiece = new Piece(original.BufferIndex, rightStart, original.End, Math.Max(0, rightLf), rightLen);

        node.Piece = leftPiece;
        UpdateMetadataUpward(node);
        InsertAfterNode(node, rightPiece);
    }

    // =========================================================================
    // Red-Black Tree operations
    // =========================================================================

    private void RbInsert(Piece piece)
    {
        var node = new TreeNode(piece, NodeColor.Red);

        if (_root == null)
        {
            _root = node;
            _root.Color = NodeColor.Black;
            return;
        }

        // Standard BST insert by in-order position (append to rightmost)
        var current = _root;
        TreeNode? parent = null;

        while (current != null)
        {
            parent = current;
            current = current.Right; // Always go right to append
        }

        node.Parent = parent;
        if (parent != null)
            parent.Right = node;

        UpdateMetadataUpward(node);
        RbInsertFixup(node);
    }

    private void RbInsertFixup(TreeNode node)
    {
        while (node != _root && node.Parent?.Color == NodeColor.Red)
        {
            var parent = node.Parent!;
            var grandparent = parent.Parent;
            if (grandparent == null) break;

            if (parent == grandparent.Left)
            {
                var uncle = grandparent.Right;
                if (uncle?.Color == NodeColor.Red)
                {
                    parent.Color = NodeColor.Black;
                    uncle.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    node = grandparent;
                }
                else
                {
                    if (node == parent.Right)
                    {
                        node = parent;
                        RotateLeft(node);
                        parent = node.Parent!;
                        grandparent = parent?.Parent;
                        if (grandparent == null) break;
                    }
                    parent!.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    RotateRight(grandparent);
                }
            }
            else
            {
                var uncle = grandparent.Left;
                if (uncle?.Color == NodeColor.Red)
                {
                    parent.Color = NodeColor.Black;
                    uncle.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    node = grandparent;
                }
                else
                {
                    if (node == parent.Left)
                    {
                        node = parent;
                        RotateRight(node);
                        parent = node.Parent!;
                        grandparent = parent?.Parent;
                        if (grandparent == null) break;
                    }
                    parent!.Color = NodeColor.Black;
                    grandparent.Color = NodeColor.Red;
                    RotateLeft(grandparent);
                }
            }
        }
        if (_root != null) _root.Color = NodeColor.Black;
    }

    private void RbDelete(TreeNode node)
    {
        TreeNode? replacement;
        TreeNode? fixupNode;
        TreeNode? fixupParent;

        if (node.Left == null || node.Right == null)
        {
            replacement = node;
        }
        else
        {
            replacement = InOrderSuccessor(node);
        }

        fixupNode = replacement!.Left ?? replacement.Right;
        fixupParent = replacement.Parent;

        if (fixupNode != null)
            fixupNode.Parent = replacement.Parent;

        if (replacement.Parent == null)
        {
            _root = fixupNode;
        }
        else if (replacement == replacement.Parent.Left)
        {
            replacement.Parent.Left = fixupNode;
        }
        else
        {
            replacement.Parent.Right = fixupNode;
        }

        if (replacement != node)
        {
            node.Piece = replacement.Piece;
            UpdateMetadataUpward(node);
        }

        if (fixupParent != null)
            UpdateMetadataUpward(fixupParent);

        if (replacement.Color == NodeColor.Black && fixupNode != null)
        {
            RbDeleteFixup(fixupNode);
        }

        // If the tree is now empty
        if (_root != null && _root.Piece.Length == 0 && _root.Left == null && _root.Right == null)
            _root = null;
    }

    private void RbDeleteFixup(TreeNode node)
    {
        while (node != _root && node.Color == NodeColor.Black)
        {
            if (node.Parent == null) break;

            if (node == node.Parent.Left)
            {
                var sibling = node.Parent.Right;
                if (sibling == null) break;

                if (sibling.Color == NodeColor.Red)
                {
                    sibling.Color = NodeColor.Black;
                    node.Parent.Color = NodeColor.Red;
                    RotateLeft(node.Parent);
                    sibling = node.Parent.Right;
                    if (sibling == null) break;
                }

                if ((sibling.Left?.Color ?? NodeColor.Black) == NodeColor.Black &&
                    (sibling.Right?.Color ?? NodeColor.Black) == NodeColor.Black)
                {
                    sibling.Color = NodeColor.Red;
                    node = node.Parent;
                }
                else
                {
                    if ((sibling.Right?.Color ?? NodeColor.Black) == NodeColor.Black)
                    {
                        if (sibling.Left != null) sibling.Left.Color = NodeColor.Black;
                        sibling.Color = NodeColor.Red;
                        RotateRight(sibling);
                        sibling = node.Parent.Right;
                        if (sibling == null) break;
                    }
                    sibling.Color = node.Parent.Color;
                    node.Parent.Color = NodeColor.Black;
                    if (sibling.Right != null) sibling.Right.Color = NodeColor.Black;
                    RotateLeft(node.Parent);
                    node = _root!;
                }
            }
            else
            {
                var sibling = node.Parent.Left;
                if (sibling == null) break;

                if (sibling.Color == NodeColor.Red)
                {
                    sibling.Color = NodeColor.Black;
                    node.Parent.Color = NodeColor.Red;
                    RotateRight(node.Parent);
                    sibling = node.Parent.Left;
                    if (sibling == null) break;
                }

                if ((sibling.Right?.Color ?? NodeColor.Black) == NodeColor.Black &&
                    (sibling.Left?.Color ?? NodeColor.Black) == NodeColor.Black)
                {
                    sibling.Color = NodeColor.Red;
                    node = node.Parent;
                }
                else
                {
                    if ((sibling.Left?.Color ?? NodeColor.Black) == NodeColor.Black)
                    {
                        if (sibling.Right != null) sibling.Right.Color = NodeColor.Black;
                        sibling.Color = NodeColor.Red;
                        RotateLeft(sibling);
                        sibling = node.Parent.Left;
                        if (sibling == null) break;
                    }
                    sibling.Color = node.Parent.Color;
                    node.Parent.Color = NodeColor.Black;
                    if (sibling.Left != null) sibling.Left.Color = NodeColor.Black;
                    RotateRight(node.Parent);
                    node = _root!;
                }
            }
        }
        node.Color = NodeColor.Black;
    }

    private void RotateLeft(TreeNode node)
    {
        var right = node.Right;
        if (right == null) return;

        node.Right = right.Left;
        if (right.Left != null)
            right.Left.Parent = node;

        right.Parent = node.Parent;
        if (node.Parent == null)
            _root = right;
        else if (node == node.Parent.Left)
            node.Parent.Left = right;
        else
            node.Parent.Right = right;

        right.Left = node;
        node.Parent = right;

        // Update metadata
        RecomputeMetadata(node);
        RecomputeMetadata(right);
    }

    private void RotateRight(TreeNode node)
    {
        var left = node.Left;
        if (left == null) return;

        node.Left = left.Right;
        if (left.Right != null)
            left.Right.Parent = node;

        left.Parent = node.Parent;
        if (node.Parent == null)
            _root = left;
        else if (node == node.Parent.Right)
            node.Parent.Right = left;
        else
            node.Parent.Left = left;

        left.Right = node;
        node.Parent = left;

        RecomputeMetadata(node);
        RecomputeMetadata(left);
    }

    // =========================================================================
    // Metadata maintenance
    // =========================================================================

    private static void RecomputeMetadata(TreeNode node)
    {
        node.SizeLeft = SubtreeSize(node.Left);
        node.LfLeft = SubtreeLf(node.Left);
    }

    private static int SubtreeSize(TreeNode? node)
    {
        if (node == null) return 0;
        return node.SizeLeft + node.Piece.Length + SubtreeSize(node.Right);
    }

    private static int SubtreeLf(TreeNode? node)
    {
        if (node == null) return 0;
        return node.LfLeft + node.Piece.LineFeedCount + SubtreeLf(node.Right);
    }

    private void UpdateMetadataUpward(TreeNode node)
    {
        var current = node;
        while (current != null)
        {
            RecomputeMetadata(current);
            current = current.Parent;
        }
    }

    // =========================================================================
    // Tree traversal helpers
    // =========================================================================

    private static TreeNode TreeMinimum(TreeNode node)
    {
        while (node.Left != null) node = node.Left;
        return node;
    }

    private static TreeNode TreeMaximum(TreeNode node)
    {
        while (node.Right != null) node = node.Right;
        return node;
    }

    private static TreeNode? InOrderSuccessor(TreeNode node)
    {
        if (node.Right != null) return TreeMinimum(node.Right);
        var parent = node.Parent;
        while (parent != null && node == parent.Right)
        {
            node = parent;
            parent = parent.Parent;
        }
        return parent;
    }

    private static void InOrderTraversal(TreeNode? node, Action<TreeNode> action)
    {
        if (node == null) return;
        InOrderTraversal(node.Left, action);
        action(node);
        InOrderTraversal(node.Right, action);
    }

    // =========================================================================
    // Buffer helpers
    // =========================================================================

    private static BufferCursor ComputeBufferCursor(StringBuffer buffer, int offset)
    {
        offset = Math.Min(offset, buffer.Content.Length);
        var line = Array.BinarySearch(buffer.LineStarts, offset);
        if (line < 0) line = ~line - 1;
        var column = offset - buffer.LineStarts[Math.Max(0, line)];
        return new BufferCursor(Math.Max(0, line), column);
    }

    private static int GetBufferOffset(StringBuffer buffer, BufferCursor cursor)
    {
        if (cursor.Line < buffer.LineStarts.Length)
            return buffer.LineStarts[cursor.Line] + cursor.Column;
        return buffer.Content.Length;
    }

    private static int CountLineFeeds(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') count++;
            else if (text[i] == '\r')
            {
                count++;
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
        }
        return count;
    }
}
