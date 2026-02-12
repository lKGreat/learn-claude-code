using System.Collections.Specialized;
using Avalonia.Controls;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _currentVm;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous VM
        if (_currentVm != null)
        {
            _currentVm.Messages.CollectionChanged -= OnMessagesChanged;
            _currentVm.ScrollToBottomRequested -= ScrollToBottom;
        }

        if (DataContext is ChatViewModel vm)
        {
            _currentVm = vm;
            vm.Messages.CollectionChanged += OnMessagesChanged;
            vm.ScrollToBottomRequested += ScrollToBottom;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        var scroller = this.FindControl<ScrollViewer>("ChatScroller");
        scroller?.ScrollToEnd();
    }
}
