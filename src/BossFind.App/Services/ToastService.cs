using Microsoft.UI.Dispatching;

namespace BossFind.App.Services;

/// <summary>
/// Toast 的语义级别。具体的颜色和图标由页面宿主决定，服务本身不依赖 XAML。
/// </summary>
public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// 一条 Toast 的显示或收起通知。
/// </summary>
public sealed class ToastNotificationEventArgs : EventArgs
{
    public ToastNotificationEventArgs(
        Guid id,
        string message,
        ToastKind kind,
        string iconGlyph,
        TimeSpan duration,
        bool isDismissal = false)
    {
        Id = id;
        Message = message;
        Kind = kind;
        IconGlyph = iconGlyph;
        Duration = duration;
        IsDismissal = isDismissal;
    }

    public Guid Id { get; }

    public string Message { get; }

    public ToastKind Kind { get; }

    /// <summary>
    /// Segoe MDL2 glyph，可直接绑定到 FontIcon.Glyph；自定义宿主也可忽略。
    /// </summary>
    public string IconGlyph { get; }

    public TimeSpan Duration { get; }

    public bool IsDismissal { get; }
}

/// <summary>
/// 应用级轻提示服务。它只发布生命周期事件，不持有任何窗口或 XAML 控件。
/// </summary>
public sealed class ToastService : IDisposable
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(2);
    private readonly DispatcherQueue? dispatcherQueue;
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, CancellationTokenSource> pending = new();
    private bool isDisposed;

    public ToastService(DispatcherQueue? dispatcherQueue = null)
    {
        this.dispatcherQueue = dispatcherQueue;
    }

    public event EventHandler<ToastNotificationEventArgs>? NotificationRequested;

    public Guid Show(
        string message,
        ToastKind kind = ToastKind.Info,
        TimeSpan? duration = null,
        string? iconGlyph = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var notificationId = Guid.NewGuid();
        var displayDuration = duration ?? DefaultDuration;
        var cancellation = new CancellationTokenSource();

        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            pending[notificationId] = cancellation;
        }

        Raise(new ToastNotificationEventArgs(
            notificationId,
            message.Trim(),
            kind,
            iconGlyph ?? GetDefaultGlyph(kind),
            displayDuration));

        if (displayDuration > TimeSpan.Zero)
        {
            _ = DismissAfterAsync(notificationId, displayDuration, cancellation);
        }

        return notificationId;
    }

    public Guid ShowInfo(string message, TimeSpan? duration = null) => Show(message, ToastKind.Info, duration);

    public Guid ShowSuccess(string message, TimeSpan? duration = null) => Show(message, ToastKind.Success, duration);

    public Guid ShowWarning(string message, TimeSpan? duration = null) => Show(message, ToastKind.Warning, duration);

    public Guid ShowError(string message, TimeSpan? duration = null) => Show(message, ToastKind.Error, duration);

    public void Dismiss(Guid notificationId)
    {
        CancellationTokenSource? cancellation;
        lock (syncRoot)
        {
            if (!pending.Remove(notificationId, out cancellation))
            {
                return;
            }
        }

        cancellation.Cancel();
        cancellation.Dispose();
        Raise(new ToastNotificationEventArgs(
            notificationId,
            string.Empty,
            ToastKind.Info,
            string.Empty,
            TimeSpan.Zero,
            isDismissal: true));
    }

    public void Dispose()
    {
        CancellationTokenSource[] cancellations;
        lock (syncRoot)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            cancellations = pending.Values.ToArray();
            pending.Clear();
        }

        foreach (var cancellation in cancellations)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private async Task DismissAfterAsync(
        Guid notificationId,
        TimeSpan duration,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(duration, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        var shouldNotify = false;
        lock (syncRoot)
        {
            if (pending.TryGetValue(notificationId, out var current) && ReferenceEquals(current, cancellation))
            {
                pending.Remove(notificationId);
                shouldNotify = true;
            }
        }

        cancellation.Dispose();
        if (shouldNotify)
        {
            Raise(new ToastNotificationEventArgs(
                notificationId,
                string.Empty,
                ToastKind.Info,
                string.Empty,
                TimeSpan.Zero,
                isDismissal: true));
        }
    }

    private void Raise(ToastNotificationEventArgs notification)
    {
        if (dispatcherQueue is { HasThreadAccess: false } && dispatcherQueue.TryEnqueue(() => Raise(notification)))
        {
            return;
        }

        if (isDisposed)
        {
            return;
        }

        NotificationRequested?.Invoke(this, notification);
    }

    private static string GetDefaultGlyph(ToastKind kind) => kind switch
    {
        ToastKind.Success => "\uE73E",
        ToastKind.Warning => "\uE7BA",
        ToastKind.Error => "\uEA39",
        _ => "\uE946"
    };
}
