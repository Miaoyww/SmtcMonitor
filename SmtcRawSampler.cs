using Windows.Media.Control;

namespace SmtcMonitor;

/// <summary>
/// 轮询所有 SMTC 会话并原样返回其数据：
/// 不外推进度、不筛选或排序会话、不做进程推断等元数据回退。
/// </summary>
internal sealed class SmtcRawSampler : IDisposable
{
    private static readonly TimeSpan MediaRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, CachedMedia> _mediaCache = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private bool _disposed;

    private sealed record CachedMedia(RawMediaProperties Media, DateTimeOffset FetchedAtUtc);

    public async Task<SmtcSample> SampleAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var manager = await EnsureManagerAsync(cancellationToken);
        if (manager is null)
        {
            return new SmtcSample("SMTC 管理器暂不可用，将在下次采样时重试。", DateTimeOffset.UtcNow, []);
        }

        IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions;
        try
        {
            sessions = manager.GetSessions();
        }
        catch (Exception exception)
        {
            return new SmtcSample($"GetSessions 失败：{Describe(exception)}", DateTimeOffset.UtcNow, []);
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var snapshots = new List<RawSmtcSessionSnapshot>(sessions.Count);
        foreach (var session in sessions)
        {
            snapshots.Add(await SampleSessionAsync(session, capturedAtUtc, cancellationToken));
        }

        PruneMediaCache(sessions);
        return new SmtcSample(null, capturedAtUtc, snapshots);
    }

    private async Task<GlobalSystemMediaTransportControlsSessionManager?> EnsureManagerAsync(
        CancellationToken cancellationToken)
    {
        if (_manager is not null)
        {
            return _manager;
        }

        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager
                .RequestAsync()
                .AsTask(cancellationToken);
            return _manager;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<RawSmtcSessionSnapshot> SampleSessionAsync(
        GlobalSystemMediaTransportControlsSession session,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        RawPlaybackInfo? playback = null;
        try
        {
            var info = session.GetPlaybackInfo();
            playback = new RawPlaybackInfo(
                info.PlaybackStatus,
                info.PlaybackRate,
                info.PlaybackType,
                info.AutoRepeatMode,
                info.IsShuffleActive);
        }
        catch (Exception exception)
        {
            errors.Add($"GetPlaybackInfo: {Describe(exception)}");
        }

        RawTimelineProperties? timeline = null;
        try
        {
            var properties = session.GetTimelineProperties();
            timeline = new RawTimelineProperties(
                properties.Position,
                properties.StartTime,
                properties.EndTime,
                properties.MinSeekTime,
                properties.MaxSeekTime,
                properties.LastUpdatedTime);
        }
        catch (Exception exception)
        {
            errors.Add($"GetTimelineProperties: {Describe(exception)}");
        }

        var media = await ReadMediaAsync(session, capturedAtUtc, errors, cancellationToken);

        return new RawSmtcSessionSnapshot(
            session,
            session.SourceAppUserModelId ?? string.Empty,
            playback,
            timeline,
            media,
            errors.Count > 0 ? string.Join("; ", errors) : null,
            capturedAtUtc);
    }

    private async Task<RawMediaProperties?> ReadMediaAsync(
        GlobalSystemMediaTransportControlsSession session,
        DateTimeOffset capturedAtUtc,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        CachedMedia? cached = _mediaCache.TryGetValue(session, out var entry) ? entry : null;
        if (cached is not null && capturedAtUtc - cached.FetchedAtUtc < MediaRefreshInterval)
        {
            return cached.Media;
        }

        try
        {
            var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
            var media = new RawMediaProperties(
                properties.Title ?? string.Empty,
                properties.Artist ?? string.Empty,
                properties.Subtitle ?? string.Empty,
                properties.AlbumTitle ?? string.Empty,
                properties.AlbumArtist ?? string.Empty,
                properties.TrackNumber,
                properties.AlbumTrackCount,
                properties.Genres?.ToList() ?? []);
            _mediaCache[session] = new CachedMedia(media, capturedAtUtc);
            return media;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            errors.Add($"TryGetMediaPropertiesAsync: {Describe(exception)}");
            return cached?.Media;
        }
    }

    private void PruneMediaCache(IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions)
    {
        List<GlobalSystemMediaTransportControlsSession>? staleKeys = null;
        foreach (var key in _mediaCache.Keys)
        {
            var exists = false;
            foreach (var session in sessions)
            {
                if (ReferenceEquals(key, session))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                (staleKeys ??= []).Add(key);
            }
        }

        if (staleKeys is null)
        {
            return;
        }

        foreach (var key in staleKeys)
        {
            _mediaCache.Remove(key);
        }
    }

    private static string Describe(Exception exception)
    {
        return $"{exception.GetType().Name} (0x{exception.HResult:X8}) {exception.Message}".Trim();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mediaCache.Clear();
        _manager = null;
    }
}
