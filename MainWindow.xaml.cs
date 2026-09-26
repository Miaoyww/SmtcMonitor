using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Media.Control;

namespace SmtcMonitor;

public partial class MainWindow : Window
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan StatusMessageDuration = TimeSpan.FromSeconds(1.6);

    private readonly SmtcRawSampler _sampler = new();
    private readonly CancellationTokenSource _disposal = new();
    private readonly List<SessionCardViewModel> _cards = [];
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _statusMessageTimer;
    private readonly Brush _footerNormalBrush;
    private readonly Brush _footerErrorBrush;
    private SmtcSample? _latestSample;
    private bool _statusMessageActive;
    private bool _paused;
    private bool _sampling;

    public MainWindow()
    {
        InitializeComponent();
        _footerNormalBrush = FooterStatus.Foreground;
        _footerErrorBrush = (Brush)FindResource("ErrorBrush");

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = SampleInterval
        };
        _timer.Tick += OnSampleTick;

        _statusMessageTimer = new DispatcherTimer { Interval = StatusMessageDuration };
        _statusMessageTimer.Tick += OnStatusMessageTick;

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        UpdateSamplingStatus();
        _timer.Start();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        DarkTitleBar.Apply(this);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _statusMessageTimer.Stop();
        _disposal.Cancel();
        _sampler.Dispose();
    }

    private async void OnSampleTick(object? sender, EventArgs e)
    {
        if (_sampling)
        {
            return;
        }

        _sampling = true;
        try
        {
            var sample = await _sampler.SampleAsync(_disposal.Token);
            if (_disposal.IsCancellationRequested)
            {
                return;
            }

            ApplySample(sample);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowStatusMessage($"采样失败：{exception.Message}", isError: true);
        }
        finally
        {
            _sampling = false;
        }
    }

    private void ApplySample(SmtcSample sample)
    {
        _latestSample = sample;
        var sessions = sample.Sessions;

        if (CardOrderChanged(sessions))
        {
            RebuildCards(sessions);
        }
        else
        {
            for (var i = 0; i < sessions.Count; i++)
            {
                _cards[i].Update(sessions[i]);
            }
        }

        var hasSessions = sessions.Count > 0;
        EmptyState.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
        SessionList.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
        if (!hasSessions && sample.ManagerError is not null)
        {
            EmptyStateHint.Text = sample.ManagerError;
        }

        if (!_statusMessageActive)
        {
            ApplyFooterStatus(sample);
        }
    }

    private void ApplyFooterStatus(SmtcSample sample)
    {
        if (sample.ManagerError is not null)
        {
            FooterStatus.Text = sample.ManagerError;
            FooterStatus.Foreground = _footerErrorBrush;
            return;
        }

        FooterStatus.Foreground = _footerNormalBrush;
        FooterStatus.Text =
            $"SMTC 会话 {sample.Sessions.Count} · 最近采样 {FormatFullTimestamp(sample.CapturedAtUtc)}";
    }

    private bool CardOrderChanged(IReadOnlyList<RawSmtcSessionSnapshot> sessions)
    {
        if (sessions.Count != _cards.Count)
        {
            return true;
        }

        for (var i = 0; i < sessions.Count; i++)
        {
            if (!ReferenceEquals(sessions[i].Session, _cards[i].Session))
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildCards(IReadOnlyList<RawSmtcSessionSnapshot> sessions)
    {
        var existing = new Dictionary<GlobalSystemMediaTransportControlsSession, SessionCardViewModel>(_cards.Count);
        foreach (var card in _cards)
        {
            existing[card.Session] = card;
        }

        var cards = new List<SessionCardViewModel>(sessions.Count);
        foreach (var snapshot in sessions)
        {
            if (!existing.TryGetValue(snapshot.Session, out var card))
            {
                card = new SessionCardViewModel(snapshot);
            }
            else
            {
                card.Update(snapshot);
            }

            cards.Add(card);
        }

        _cards.Clear();
        _cards.AddRange(cards);
        SessionList.ItemsSource = _cards;
    }

    private void UpdateSamplingStatus()
    {
        SamplingStatus.Text = _paused ? "已暂停 · 100ms" : "实时 · 100ms";
    }

    private void OnPauseClicked(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        if (_paused)
        {
            _timer.Stop();
        }
        else
        {
            _timer.Start();
        }

        PauseButton.Content = _paused ? "继续" : "暂停";
        UpdateSamplingStatus();
    }

    private void OnTopmostToggled(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostToggle.IsChecked == true;
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(RenderRawDump());
            ShowStatusMessage("已复制 SMTC 原始数据。");
        }
        catch (Exception exception)
        {
            ShowStatusMessage($"复制失败：{exception.Message}", isError: true);
        }
    }

    /// <summary>在底部状态栏显示一条临时消息；显示期间采样刷新不覆盖状态栏，超时后恢复。</summary>
    private void ShowStatusMessage(string message, bool isError = false)
    {
        _statusMessageActive = true;
        FooterStatus.Text = message;
        FooterStatus.Foreground = isError ? _footerErrorBrush : _footerNormalBrush;

        _statusMessageTimer.Stop();
        _statusMessageTimer.Start();
    }

    private void OnStatusMessageTick(object? sender, EventArgs e)
    {
        _statusMessageTimer.Stop();
        _statusMessageActive = false;
        if (_latestSample is { } sample)
        {
            ApplyFooterStatus(sample);
        }
    }

    private string RenderRawDump()
    {
        if (_latestSample is null)
        {
            return "（暂无 SMTC 采样数据）";
        }

        var sample = _latestSample;
        var builder = new StringBuilder();
        builder.AppendLine($"SMTC 原始数据 {FormatFullTimestamp(sample.CapturedAtUtc)}（本地时间）");
        if (sample.ManagerError is not null)
        {
            builder.AppendLine($"ManagerError: {sample.ManagerError}");
        }

        for (var i = 0; i < sample.Sessions.Count; i++)
        {
            var session = sample.Sessions[i];
            builder.AppendLine();
            builder.AppendLine($"会话 {i + 1}/{sample.Sessions.Count}");
            builder.AppendLine($"  SourceAppUserModelId: {EmptyAsDash(session.SourceAppUserModelId)}");

            if (session.Playback is { } playback)
            {
                builder.AppendLine($"  PlaybackStatus: {playback.PlaybackStatus}");
                builder.AppendLine(
                    $"  PlaybackRate: {(playback.PlaybackRate is { } playbackRate ? playbackRate.ToString("0.####", CultureInfo.InvariantCulture) : "-")}");
                builder.AppendLine($"  PlaybackType: {playback.PlaybackType?.ToString() ?? "-"}");
                builder.AppendLine($"  AutoRepeatMode: {playback.AutoRepeatMode?.ToString() ?? "-"}");
                builder.AppendLine($"  IsShuffleActive: {playback.IsShuffleActive}");
            }

            if (session.Media is { } media)
            {
                builder.AppendLine($"  Title: {EmptyAsDash(media.Title)}");
                builder.AppendLine($"  Artist: {EmptyAsDash(media.Artist)}");
                builder.AppendLine($"  Subtitle: {EmptyAsDash(media.Subtitle)}");
                builder.AppendLine($"  AlbumTitle: {EmptyAsDash(media.AlbumTitle)}");
                builder.AppendLine($"  AlbumArtist: {EmptyAsDash(media.AlbumArtist)}");
                builder.AppendLine($"  TrackNumber: {media.TrackNumber}");
                builder.AppendLine($"  AlbumTrackCount: {media.AlbumTrackCount}");
                builder.AppendLine(
                    $"  Genres: {(media.Genres.Count > 0 ? string.Join(" | ", media.Genres) : "-")}");
            }

            if (session.Timeline is { } timeline)
            {
                builder.AppendLine($"  Position: {FormatDuration(timeline.Position)}（原始值，未外推）");
                builder.AppendLine($"  StartTime: {FormatDuration(timeline.StartTime)}");
                builder.AppendLine($"  EndTime: {FormatDuration(timeline.EndTime)}");
                builder.AppendLine($"  MinSeekTime: {FormatDuration(timeline.MinSeekTime)}");
                builder.AppendLine($"  MaxSeekTime: {FormatDuration(timeline.MaxSeekTime)}");
                builder.AppendLine(
                    $"  LastUpdatedTime: {FormatFullTimestamp(timeline.LastUpdatedTime)}（本地时间）");
            }

            builder.AppendLine($"  Error: {session.Error ?? "-"}");
        }

        return builder.ToString();
    }

    private static string EmptyAsDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatDuration(TimeSpan value)
    {
        var totalMilliseconds = (long)value.TotalMilliseconds;
        var sign = totalMilliseconds < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(totalMilliseconds);
        return $"{sign}{absolute / 60000}:{absolute % 60000 / 1000:D2}.{absolute % 1000:D3}";
    }

    private static string FormatFullTimestamp(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}

internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModePre2004 = 19;

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        if (!TrySetDarkMode(handle, DwmwaUseImmersiveDarkMode))
        {
            TrySetDarkMode(handle, DwmwaUseImmersiveDarkModePre2004);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int sizeOfValue);

    private static bool TrySetDarkMode(nint hwnd, int attribute)
    {
        var enabled = 1;
        return DwmSetWindowAttribute(hwnd, attribute, ref enabled, sizeof(int)) == 0;
    }
}
