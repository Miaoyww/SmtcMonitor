using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Windows.Media.Control;

namespace SmtcMonitor;

/// <summary>单个会话卡片的视图模型；只在值真正变化时发出通知，保证 100ms 采样不引发无谓重绘。</summary>
public sealed class SessionCardViewModel : INotifyPropertyChanged
{
    private static readonly Brush PlayingBadgeBackground = CreateFrozenBrush(0x1E, 0x3B, 0x2A);
    private static readonly Brush PlayingBadgeForeground = CreateFrozenBrush(0x5F, 0xD0, 0x8A);
    private static readonly Brush SuspendedBadgeBackground = CreateFrozenBrush(0x3B, 0x32, 0x22);
    private static readonly Brush SuspendedBadgeForeground = CreateFrozenBrush(0xE3, 0xB3, 0x41);
    private static readonly Brush NeutralBadgeBackground = CreateFrozenBrush(0x2F, 0x2F, 0x2F);
    private static readonly Brush NeutralBadgeForeground = CreateFrozenBrush(0x9E, 0x9E, 0x9E);

    private string _sourceAppUserModelId = string.Empty;
    private string _statusText = "—";
    private Brush _statusBackground = NeutralBadgeBackground;
    private Brush _statusForeground = NeutralBadgeForeground;
    private string _titleText = "—";
    private string _artistText = "—";
    private string _subtitleText = "—";
    private string _albumTitleText = "—";
    private string _albumArtistText = "—";
    private string _trackText = "—";
    private string _genresText = "—";
    private string _playbackStatusText = "—";
    private string _playbackRateText = "—";
    private string _playbackTypeText = "—";
    private string _autoRepeatModeText = "—";
    private string _shuffleText = "—";
    private string _positionText = "—";
    private string _startTimeText = "—";
    private string _endTimeText = "—";
    private string _minSeekTimeText = "—";
    private string _maxSeekTimeText = "—";
    private string _lastUpdatedText = "—";
    private string _capturedAtText = "—";
    private string? _errorText;
    private Visibility _errorVisibility = Visibility.Collapsed;

    public SessionCardViewModel(RawSmtcSessionSnapshot snapshot)
    {
        Session = snapshot.Session;
        Update(snapshot);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GlobalSystemMediaTransportControlsSession Session { get; }

    public string SourceAppUserModelId { get => _sourceAppUserModelId; private set => Set(ref _sourceAppUserModelId, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public Brush StatusBackground { get => _statusBackground; private set => Set(ref _statusBackground, value); }
    public Brush StatusForeground { get => _statusForeground; private set => Set(ref _statusForeground, value); }
    public string TitleText { get => _titleText; private set => Set(ref _titleText, value); }
    public string ArtistText { get => _artistText; private set => Set(ref _artistText, value); }
    public string SubtitleText { get => _subtitleText; private set => Set(ref _subtitleText, value); }
    public string AlbumTitleText { get => _albumTitleText; private set => Set(ref _albumTitleText, value); }
    public string AlbumArtistText { get => _albumArtistText; private set => Set(ref _albumArtistText, value); }
    public string TrackText { get => _trackText; private set => Set(ref _trackText, value); }
    public string GenresText { get => _genresText; private set => Set(ref _genresText, value); }
    public string PlaybackStatusText { get => _playbackStatusText; private set => Set(ref _playbackStatusText, value); }
    public string PlaybackRateText { get => _playbackRateText; private set => Set(ref _playbackRateText, value); }
    public string PlaybackTypeText { get => _playbackTypeText; private set => Set(ref _playbackTypeText, value); }
    public string AutoRepeatModeText { get => _autoRepeatModeText; private set => Set(ref _autoRepeatModeText, value); }
    public string ShuffleText { get => _shuffleText; private set => Set(ref _shuffleText, value); }
    public string PositionText { get => _positionText; private set => Set(ref _positionText, value); }
    public string StartTimeText { get => _startTimeText; private set => Set(ref _startTimeText, value); }
    public string EndTimeText { get => _endTimeText; private set => Set(ref _endTimeText, value); }
    public string MinSeekTimeText { get => _minSeekTimeText; private set => Set(ref _minSeekTimeText, value); }
    public string MaxSeekTimeText { get => _maxSeekTimeText; private set => Set(ref _maxSeekTimeText, value); }
    public string LastUpdatedText { get => _lastUpdatedText; private set => Set(ref _lastUpdatedText, value); }
    public string CapturedAtText { get => _capturedAtText; private set => Set(ref _capturedAtText, value); }
    public string? ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public Visibility ErrorVisibility { get => _errorVisibility; private set => Set(ref _errorVisibility, value); }

    public void Update(RawSmtcSessionSnapshot snapshot)
    {
        SourceAppUserModelId = snapshot.SourceAppUserModelId;

        var playback = snapshot.Playback;
        StatusText = playback?.PlaybackStatus.ToString() ?? "—";
        StatusBackground = playback is null
            ? NeutralBadgeBackground
            : playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? PlayingBadgeBackground
                : SuspendedBadgeBackground;
        StatusForeground = playback is null
            ? NeutralBadgeForeground
            : playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? PlayingBadgeForeground
                : SuspendedBadgeForeground;

        var media = snapshot.Media;
        TitleText = OrDash(media?.Title);
        ArtistText = OrDash(media?.Artist);
        SubtitleText = OrDash(media?.Subtitle);
        AlbumTitleText = OrDash(media?.AlbumTitle);
        AlbumArtistText = OrDash(media?.AlbumArtist);
        TrackText = FormatTrack(media);
        GenresText = FormatGenres(media);

        PlaybackStatusText = OrDash(playback?.PlaybackStatus.ToString());
        PlaybackRateText = playback?.PlaybackRate is { } playbackRate
            ? playbackRate.ToString("0.####", CultureInfo.InvariantCulture)
            : "—";
        PlaybackTypeText = OrDash(playback?.PlaybackType?.ToString());
        AutoRepeatModeText = OrDash(playback?.AutoRepeatMode?.ToString());
        ShuffleText = playback?.IsShuffleActive is { } shuffleActive ? shuffleActive.ToString() : "—";

        var timeline = snapshot.Timeline;
        PositionText = FormatDuration(timeline?.Position);
        StartTimeText = FormatDuration(timeline?.StartTime);
        EndTimeText = FormatDuration(timeline?.EndTime);
        MinSeekTimeText = FormatDuration(timeline?.MinSeekTime);
        MaxSeekTimeText = FormatDuration(timeline?.MaxSeekTime);
        LastUpdatedText = FormatLastUpdated(timeline?.LastUpdatedTime);
        CapturedAtText = FormatTimestamp(snapshot.CapturedAtUtc);

        ErrorText = snapshot.Error;
        ErrorVisibility = string.IsNullOrEmpty(snapshot.Error) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string OrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string FormatTrack(RawMediaProperties? media)
    {
        if (media is null || media.TrackNumber <= 0)
        {
            return "—";
        }

        return media.AlbumTrackCount > 0
            ? $"{media.TrackNumber} / {media.AlbumTrackCount}"
            : media.TrackNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatGenres(RawMediaProperties? media)
    {
        return media is null || media.Genres.Count == 0 ? "—" : string.Join(" | ", media.Genres);
    }

    private static string FormatDuration(TimeSpan? value)
    {
        if (value is null)
        {
            return "—";
        }

        var totalMilliseconds = (long)value.Value.TotalMilliseconds;
        var sign = totalMilliseconds < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(totalMilliseconds);
        return $"{sign}{absolute / 60000}:{absolute % 60000 / 1000:D2}.{absolute % 1000:D3}";
    }

    private static string FormatLastUpdated(DateTimeOffset? value)
    {
        if (value is null)
        {
            return "—";
        }

        var age = DateTimeOffset.UtcNow - value.Value;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return $"{value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}（{HumanizeAge(age)} 前）";
    }

    private static string HumanizeAge(TimeSpan age)
    {
        if (age.TotalMinutes < 1)
        {
            return $"{age.TotalSeconds:0.0}s";
        }

        if (age.TotalHours < 1)
        {
            return $"{age.TotalMinutes:0}m";
        }

        if (age.TotalDays < 1)
        {
            return $"{age.TotalHours:0}h";
        }

        return $"{age.TotalDays:0}d";
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static SolidColorBrush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
