using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniClaudeCode.Avalonia.Models;
using MiniClaudeCode.Avalonia.Services;

namespace MiniClaudeCode.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the notification system (toast notifications + notification center).
/// </summary>
public partial class NotificationViewModel : ObservableObject
{
    public ObservableCollection<NotificationItem> Notifications { get; } = [];
    public ObservableCollection<NotificationItem> ToastQueue { get; } = [];

    [ObservableProperty]
    private bool _showCenter;

    public int UnreadCount => Notifications.Count(n => n.IsVisible);

    public void ShowInfo(string message)
    {
        AddNotification(new NotificationItem { Message = message, Severity = NotificationSeverity.Info });
    }

    public void ShowWarning(string message)
    {
        AddNotification(new NotificationItem { Message = message, Severity = NotificationSeverity.Warning });
    }

    public void ShowError(string message)
    {
        AddNotification(new NotificationItem { Message = message, Severity = NotificationSeverity.Error });
    }

    public NotificationItem ShowProgress(string message)
    {
        var item = new NotificationItem { Message = message, Severity = NotificationSeverity.Info, Progress = 0 };
        AddNotification(item);
        return item;
    }

    private void AddNotification(NotificationItem item)
    {
        DispatcherService.Post(() =>
        {
            Notifications.Insert(0, item);
            ToastQueue.Add(item);
            OnPropertyChanged(nameof(UnreadCount));

            // Auto-dismiss toast after 5 seconds
            _ = AutoDismissToast(item);
        });
    }

    private async Task AutoDismissToast(NotificationItem item)
    {
        await Task.Delay(5000);
        DispatcherService.Post(() =>
        {
            ToastQueue.Remove(item);
        });
    }

    [RelayCommand]
    private void DismissToast(NotificationItem? item)
    {
        if (item != null)
        {
            ToastQueue.Remove(item);
        }
    }

    [RelayCommand]
    private void DismissNotification(NotificationItem? item)
    {
        if (item != null)
        {
            item.IsVisible = false;
            Notifications.Remove(item);
            OnPropertyChanged(nameof(UnreadCount));
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Notifications.Clear();
        ToastQueue.Clear();
        OnPropertyChanged(nameof(UnreadCount));
    }

    [RelayCommand]
    public void ToggleCenter()
    {
        ShowCenter = !ShowCenter;
    }
}
