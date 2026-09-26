# SMTC 原始数据监视器（SmtcMonitor）

从 TaskbarLyrics 的"SMTC 时间轴监视器"独立出来的单进程调试工具：直接轮询 Windows SMTC
（System Media Transport Controls），按 SMTC 上报顺序**原样**显示每个会话的最原始数据。

## 与 TaskbarLyrics 内置监视器的区别

- **无外推，因此无漂移**：`Position` 永远显示 SMTC `GetTimelineProperties()` 的原始返回值，
  不做 `Position + (now − LastUpdatedTime)` 之类的推算，也没有外推漂移、策略选择、暂停冻结等逻辑。
- **不筛选会话**：SMTC 枚举到什么就显示什么（包括浏览器、系统 Shell 等），顺序保持 SMTC 原始顺序。
- **不做元数据回退**：不读取进程列表、不推断标题/艺术家，`TryGetMediaPropertiesAsync` 返回什么就显示什么。
- **完全独立**：不引用 TaskbarLyrics 任何代码，不读取其配置，可单独构建运行。
- **不展示歌词管线信息**（歌词源、歌词获取方式、策略名等）。

## 显示内容

每个 SMTC 会话一张卡片，键名与 WinRT API 属性一一对应：

- 媒体元数据：`Title`、`Artist`、`Subtitle`、`AlbumTitle`、`AlbumArtist`、`TrackNumber`、`AlbumTrackCount`、`Genres`
- 播放信息：`PlaybackStatus`、`PlaybackRate`、`PlaybackType`、`AutoRepeatMode`、`IsShuffleActive`
- 时间轴（原始值，未外推）：`Position`、`StartTime`、`EndTime`、`LastUpdatedTime`
- 某项读取失败时，卡片底部显示异常类型与 HRESULT，其余部分照常显示。

界面功能：暂停/继续采样、复制原始数据文本（供 issue 汇报）、置顶窗口。

## 采样策略

- 时间轴与播放信息每 **100ms** 读取一次（同步调用，开销极小）。
- 媒体元数据每 **1s** 刷新一次；仍为原始值，仅降低 WinRT 异步读取频率。
  需要更细粒度观察元数据变化时，可在 `SmtcRawSampler.MediaRefreshInterval` 中调整。

## 构建与运行

需要 Windows 10 1809+（SMTC API 于 17763 引入）与 .NET 8 SDK（更高版本 SDK 亦可构建 net8.0 目标）。
无第三方 NuGet 依赖。

```powershell
dotnet run --project SmtcMonitor.csproj
# 或
dotnet build SmtcMonitor.csproj -c Release
./bin/Release/net8.0-windows10.0.22621.0/SmtcMonitor.exe
```
