using Windows.Media;
using Windows.Media.Control;

namespace SmtcMonitor;

/// <summary>SMTC 播放信息的原始值，不做任何解释或加工；可空成员表示 SMTC 未上报。</summary>
public sealed record RawPlaybackInfo(
    GlobalSystemMediaTransportControlsSessionPlaybackStatus PlaybackStatus,
    double? PlaybackRate,
    MediaPlaybackType? PlaybackType,
    MediaPlaybackAutoRepeatMode? AutoRepeatMode,
    bool? IsShuffleActive);

/// <summary>SMTC 时间轴的原始值。<see cref="Position"/> 永远是 SMTC 上报值，绝不外推。</summary>
public sealed record RawTimelineProperties(
    TimeSpan Position,
    TimeSpan StartTime,
    TimeSpan EndTime,
    TimeSpan MinSeekTime,
    TimeSpan MaxSeekTime,
    DateTimeOffset LastUpdatedTime);

/// <summary>SMTC 媒体属性的原始值，不做进程推断等任何回退。</summary>
public sealed record RawMediaProperties(
    string Title,
    string Artist,
    string Subtitle,
    string AlbumTitle,
    string AlbumArtist,
    int TrackNumber,
    int AlbumTrackCount,
    IReadOnlyList<string> Genres);

/// <summary>某个 SMTC 会话在 <see cref="CapturedAtUtc"/> 的一次采样。为 <c>null</c> 的部分表示读取失败，见 <see cref="Error"/>。</summary>
public sealed record RawSmtcSessionSnapshot(
    GlobalSystemMediaTransportControlsSession Session,
    string SourceAppUserModelId,
    RawPlaybackInfo? Playback,
    RawTimelineProperties? Timeline,
    RawMediaProperties? Media,
    string? Error,
    DateTimeOffset CapturedAtUtc);

/// <summary>采样器一次完整采样（所有会话）的结果。</summary>
public sealed record SmtcSample(
    string? ManagerError,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<RawSmtcSessionSnapshot> Sessions);
