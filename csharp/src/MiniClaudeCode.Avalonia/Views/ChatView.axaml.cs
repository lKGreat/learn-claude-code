using System.Collections.Specialized;
using Avalonia.Controls;
using MiniClaudeCode.Avalonia.ViewModels;

namespace MiniClaudeCode.Avalonia.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ChatViewModel vm)
        {
            vm.Messages.CollectionChanged += OnMessagesChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll to bottom when new messages are added
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            var scroller = this.FindControl<ScrollViewer>("ChatScroller");
            scroller?.ScrollToEnd();
        }
    }
}
