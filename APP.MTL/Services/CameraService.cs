using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using FFMpegCore;
using MTL.APP.Models;
using Microsoft.Extensions.Logging;

namespace MTL.APP.Services;

public interface ICameraService
{
    IReadOnlyList<CameraStreamInfo> GetCameras();
    string? GetFrameBase64(int cameraId);
    /// <summary>Thông báo lỗi gần nhất (ffmpeg / RTSP / mạng), phục vụ hiển thị trên UI.</summary>
    string? GetLastError(int cameraId);
    /// <summary>Có frame hợp lệ trong vài giây gần đây.</summary>
    bool IsCameraActive(int cameraId);
    /// <summary>Luồng capture đang chạy nhưng chưa có frame (hoặc đang thử lại).</summary>
    bool IsCameraConnecting(int cameraId);
    /// <summary>FFmpeg đã được phát hiện trên máy chủ.</summary>
    bool IsFfmpegAvailable();
    /// <summary>Lấy thông báo cài đặt FFmpeg cho UI.</summary>
    string GetFfmpegSetupMessage();
    Task StartAllAsync(CancellationToken ct = default);
    Task StopAllAsync();
}

public class CameraService : ICameraService, IDisposable
{
    private readonly ILogger<CameraService> _logger;
    private readonly List<CameraStreamInfo> _cameras = new();
    private readonly ConcurrentDictionary<int, string> _frames = new();
    private readonly ConcurrentDictionary<int, string> _lastErrors = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _ctsById = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastGoodFrameUtc = new();
    private readonly ConcurrentDictionary<int, byte> _captureRunning = new();
    private readonly object _startStopGate = new();
    private int _activeViewers;
    private bool _started;
    private CancellationTokenSource? _globalCts;
    private static bool _ffOptionsConfigured;
    /// <summary>Timeout I/O RTSP (microseconds) — ffmpeg -timeout trước -i (tương thích; tránh -stimeout vì một số bản báo “Option not found”).</summary>
    private readonly int _rtspSocketTimeoutMicroseconds;
    /// <summary>Thời gian chờ process ffmpeg thoát (ms) = timeout RTSP + đệm.</summary>
    private readonly int _ffmpegProcessWaitMs;
    /// <summary>Tham số bổ sung trước -i (ví dụ -probesize …), từ appsettings.</summary>
    private readonly string _ffmpegExtraInputArgs;
    /// <summary>Thêm -4 (ép phân giải IPv4) — tránh một số máy Windows ưu tiên AAAA nhưng không route được.</summary>
    private readonly bool _preferIpv4;
    private static bool _ffmpegMissingLogged;
    private static bool _ffmpegChecked;
    private static bool _ffmpegAvailable;
    private static string? _ffmpegCheckedPath;

    public CameraService(ILogger<CameraService> logger, IConfiguration configuration)
    {
        _logger = logger;
        TryConfigureFfmpegPath(configuration);
        var sec = configuration.GetValue("MTLCamera:RtspSocketTimeoutSeconds", 12);
        if (sec < 3) sec = 3;
        if (sec > 120) sec = 120;
        _rtspSocketTimeoutMicroseconds = checked(sec * 1_000_000);
        var extraWait = configuration.GetValue("MTLCamera:FfmpegProcessExtraTimeoutSeconds", 20);
        if (extraWait < 5) extraWait = 5;
        if (extraWait > 180) extraWait = 180;
        _ffmpegProcessWaitMs = checked(sec * 1000 + extraWait * 1000);
        _ffmpegExtraInputArgs = (configuration["MTLCamera:FfmpegExtraInputArgs"] ?? "").Trim();
        _preferIpv4 = configuration.GetValue("MTLCamera:PreferIpv4", true);
        LoadFromConfig(configuration);
    }

    private static void TryConfigureFfmpegPath(IConfiguration configuration)
    {
        if (_ffOptionsConfigured) return;
        var bin = configuration["MTLCamera:FFmpegBinaryFolder"]?.Trim();
        if (string.IsNullOrEmpty(bin)) return;
        GlobalFFOptions.Configure(o => o.BinaryFolder = bin);
        _ffOptionsConfigured = true;
    }

    private void LoadFromConfig(IConfiguration configuration)
    {
        _cameras.Clear();
        var section = configuration.GetSection("MTLCamera");
        if (!section.Exists()) return;

        foreach (var child in section.GetChildren())
        {
            if (string.Equals(child.Key, "FFmpegBinaryFolder", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(child.Key, "RtspSocketTimeoutSeconds", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(child.Key, "FfmpegProcessExtraTimeoutSeconds", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(child.Key, "FfmpegExtraInputArgs", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(child.Key, "PreferIpv4", StringComparison.OrdinalIgnoreCase))
                continue;
            _cameras.Add(new CameraStreamInfo
            {
                Id = int.TryParse(child["Id"], out var id) ? id : _cameras.Count + 1,
                Name = child["Name"] ?? $"Camera {_cameras.Count + 1}",
                RtspUrl = child["RtspUrl"] ?? "",
                IsActive = bool.TryParse(child["IsActive"], out var a) ? a : true
            });
        }
    }

    /// <summary>
    /// Mật khẩu RTSP có ký tự @ sẽ làm sai host (RFC: chỉ @ cuối cùng tách userinfo/host).
    /// Encode user/pass bằng Uri.EscapeDataString.
    /// </summary>
    private static string NormalizeRtspCredentials(string rtspUrl)
    {
        if (string.IsNullOrWhiteSpace(rtspUrl)) return rtspUrl;
        if (!rtspUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)) return rtspUrl;

        var rest = rtspUrl.AsSpan("rtsp://".Length);
        var restStr = rest.ToString();
        var slashIdx = restStr.IndexOf('/');
        var authority = slashIdx >= 0 ? restStr[..slashIdx] : restStr;
        var pathAndQuery = slashIdx >= 0 ? restStr[slashIdx..] : "";

        var at = authority.LastIndexOf('@');
        if (at <= 0) return rtspUrl;

        var userInfo = authority[..at];
        var hostPort = authority[(at + 1)..];

        var colon = userInfo.IndexOf(':');
        if (colon < 0) return rtspUrl;

        var user = userInfo[..colon];
        var password = userInfo[(colon + 1)..];

        try
        {
            var encUser = Uri.EscapeDataString(Uri.UnescapeDataString(user));
            var encPass = Uri.EscapeDataString(Uri.UnescapeDataString(password));
            return $"rtsp://{encUser}:{encPass}@{hostPort}{pathAndQuery}";
        }
        catch
        {
            return rtspUrl;
        }
    }

    public IReadOnlyList<CameraStreamInfo> GetCameras() => _cameras.AsReadOnly();

    public bool IsCameraActive(int cameraId)
    {
        if (!_lastGoodFrameUtc.TryGetValue(cameraId, out var utc)) return false;
        return (DateTime.UtcNow - utc).TotalSeconds <= 20;
    }

    public bool IsCameraConnecting(int cameraId) =>
        _captureRunning.ContainsKey(cameraId) && !IsCameraActive(cameraId);

    public string? GetFrameBase64(int cameraId) =>
        _frames.TryGetValue(cameraId, out var frame) ? frame : null;

    public string? GetLastError(int cameraId) =>
        _lastErrors.TryGetValue(cameraId, out var msg) ? msg : null;

    public bool IsFfmpegAvailable() => _ffmpegAvailable;

    public string GetFfmpegSetupMessage()
    {
        if (_ffmpegAvailable)
            return "";

        if (!string.IsNullOrEmpty(_ffmpegCheckedPath))
            return $"FFmpeg đã kiểm tra tại: {_ffmpegCheckedPath}";

        var os = OperatingSystem.IsWindows() ? "Windows" : (OperatingSystem.IsMacOS() ? "macOS" : "Linux");
        return $"Chưa phát hiện FFmpeg trên máy chủ ({os}).\n\n" +
               $"Cách khắc phục:\n" +
               $"1. Cài FFmpeg:\n" +
               $"   - Windows: winget install ffmpeg (hoặc tải từ https://ffmpeg.org)\n" +
               $"   - macOS: brew install ffmpeg\n" +
               $"   - Linux: sudo apt install ffmpeg\n\n" +
               $"2. Hoặc chỉ định thư mục FFmpeg trong appsettings.json:\n" +
               $"   \"MTLCamera\": {{\n" +
               $"     \"FFmpegBinaryFolder\": \"C:\\\\ffmpeg\\\\bin\"\n" +
               $"   }}\n\n" +
               $"3. Sau khi cài đặt, khởi động lại ứng dụng.";
    }

    public async Task StartAllAsync(CancellationToken ct = default)
    {
        lock (_startStopGate)
        {
            _activeViewers++;
            if (_started)
            {
                _logger.LogDebug("Camera viewer #{N} — luồng đã chạy", _activeViewers);
                return;
            }

            if (!TryProbeFfmpegExecutable())
            {
                const string msg =
                    "Không chạy được ffmpeg (kiểm tra PATH hoặc MTLCamera:FFmpegBinaryFolder). RTSP chỉ xử lý trên server.";
                if (!_ffmpegMissingLogged)
                {
                    _ffmpegMissingLogged = true;
                    _logger.LogError("{Msg}", msg);
                }

                foreach (var cam in _cameras.Where(c => c.IsActive && !string.IsNullOrWhiteSpace(c.RtspUrl)))
                    _lastErrors[cam.Id] = msg;
                return;
            }

            _started = true;
            _globalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _logger.LogWarning("Camera service starting: {N} active cameras", _cameras.Count(c => c.IsActive && !string.IsNullOrWhiteSpace(c.RtspUrl)));

            foreach (var cam in _cameras.Where(c => c.IsActive && !string.IsNullOrWhiteSpace(c.RtspUrl)))
            {
                var g = _globalCts!.Token;
                _ = Task.Run(() => CaptureLoopAsync(cam.Id, cam.RtspUrl, g), CancellationToken.None);
            }
        }

        await Task.CompletedTask;
    }

    public async Task StopAllAsync()
    {
        lock (_startStopGate)
        {
            _activeViewers = Math.Max(0, _activeViewers - 1);
            if (_activeViewers > 0)
            {
                _logger.LogDebug("Camera viewer còn lại: {N}", _activeViewers);
                return;
            }

            if (!_started)
                return;

            try { _globalCts?.Cancel(); } catch { /* ignore */ }
            _globalCts?.Dispose();
            _globalCts = null;

            // Reset state so StartAllAsync can run again after StopAllAsync
            _started = false;
            foreach (var kvp in _ctsById.ToArray())
            {
                try { kvp.Value.Cancel(); } catch { /* ignore */ }
                try { kvp.Value.Dispose(); } catch { /* ignore */ }
            }
            _ctsById.Clear();
        }

        await Task.CompletedTask;
    }

    /// <summary>Kiểm tra ffmpeg có thực thi được (cùng cách FFMpegCore gọi).</summary>
    private bool TryProbeFfmpegExecutable()
    {
        if (_ffmpegChecked)
            return _ffmpegAvailable;

        _ffmpegChecked = true;

        if (ProbeFfmpegOnce(GlobalFFOptions.Current?.BinaryFolder))
        {
            _ffmpegAvailable = true;
            _ffmpegCheckedPath = GlobalFFOptions.Current?.BinaryFolder ?? "PATH";
            return true;
        }

        // macOS/Linux: GUI/IDE thường không có PATH của Homebrew → tự thử thư mục phổ biến
        if (!OperatingSystem.IsWindows())
        {
            foreach (var dir in GetCommonUnixFfmpegDirectories())
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    continue;
                var exe = Path.Combine(dir, "ffmpeg");
                if (!File.Exists(exe))
                    continue;
                if (!ProbeFfmpegOnce(dir))
                    continue;
                GlobalFFOptions.Configure(o => o.BinaryFolder = dir);
                _logger.LogInformation("Camera: đã gắn FFmpeg từ thư mục {Dir} (PATH không có ffmpeg).", dir);
                _ffmpegAvailable = true;
                _ffmpegCheckedPath = dir;
                return true;
            }
        }
        else
        {
            // Windows IIS: Application Pool thường không kế thừa PATH người dùng → thử thư mục cài đặt hay gặp
            foreach (var dir in GetCommonWindowsFfmpegDirectories())
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    continue;
                var exe = Path.Combine(dir, "ffmpeg.exe");
                if (!File.Exists(exe))
                    continue;
                if (!ProbeFfmpegOnce(dir))
                    continue;
                GlobalFFOptions.Configure(o => o.BinaryFolder = dir);
                _logger.LogInformation("Camera: đã gắn FFmpeg từ thư mục {Dir} (PATH không có ffmpeg.exe).", dir);
                _ffmpegAvailable = true;
                _ffmpegCheckedPath = dir;
                return true;
            }
        }

        _ffmpegAvailable = false;
        _ffmpegCheckedPath = null;
        return false;
    }

    private static IEnumerable<string> GetCommonWindowsFfmpegDirectories()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(pf))
        {
            yield return Path.Combine(pf, "ffmpeg", "bin");
            yield return Path.Combine(pf, "FFmpeg", "bin");
        }
        if (!string.IsNullOrEmpty(pfx86))
            yield return Path.Combine(pfx86, "ffmpeg", "bin");
        yield return @"C:\ffmpeg\bin";
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
            yield return Path.Combine(local, "Programs", "ffmpeg", "bin");
    }

    private static IEnumerable<string> GetCommonUnixFfmpegDirectories()
    {
        yield return "/opt/homebrew/bin"; // Apple Silicon — brew
        yield return "/usr/local/bin"; // Intel Mac / một số Linux
        yield return "/usr/bin";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            yield return Path.Combine(home, ".local", "bin");
    }

    /// <param name="binaryFolder">Thư mục chứa ffmpeg, hoặc null để dùng PATH.</param>
    private static bool ProbeFfmpegOnce(string? binaryFolder)
    {
        try
        {
            var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            var path = string.IsNullOrEmpty(binaryFolder) ? name : Path.Combine(binaryFolder, name);

            var psi = new ProcessStartInfo
            {
                FileName = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-version");

            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(8000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(bool success, string? ffmpegStderrHint)> TryCaptureRtspJpegAsync(string rtspUrl, string tempPath, CancellationToken ct)
    {
        var normalized = NormalizeRtspCredentials(rtspUrl);

        // TCP trước, UDP làm fallback
        string? lastHint = null;
        foreach (var transport in new[] { "tcp", "udp" })
        {
            var (ok, hint) = await CaptureWithTransportAsync(normalized, tempPath, transport, ct);
            if (!string.IsNullOrWhiteSpace(hint))
                lastHint = hint;
            if (ok) return (true, null);
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
        }

        return (false, lastHint);
    }

    /// <summary>Ẩn mật khẩu trong URL rtsp khi ghi log / hiển thị UI.</summary>
    private static string RedactRtspCredentialsInText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        try
        {
            return Regex.Replace(
                text,
                @"(rtsp://)([^:/\s@]+):([^@\s]+)(@)",
                m => $"{m.Groups[1].Value}{m.Groups[2].Value}:***{m.Groups[4].Value}",
                RegexOptions.IgnoreCase);
        }
        catch
        {
            return text;
        }
    }

    /// <summary>Rút gọn stderr ffmpeg thành 1 dòng gợi ý (đã redact URL).</summary>
    private static string BuildFfmpegUiHint(string stderr, int exitCode, string transport)
    {
        var safe = RedactRtspCredentialsInText(stderr);
        if (string.IsNullOrWhiteSpace(safe))
            return $"ffmpeg thoát mã {exitCode} ({transport}). Máy chủ có thể không tới được camera (firewall/NAT/port forwarding).";

        var lines = safe.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .ToList();

        static bool IsInteresting(string l) =>
            l.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("unable", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("no route", StringComparison.OrdinalIgnoreCase);

        var picked = lines.Where(IsInteresting).Take(4).ToList();
        if (picked.Count == 0)
            picked = lines.TakeLast(Math.Min(3, lines.Count)).ToList();

        var joined = string.Join(" · ", picked);
        joined = $"[{transport}] (mã {exitCode}) {joined}";
        joined = AppendVietnameseNetworkHint(joined, safe);
        return joined.Length > 780 ? joined[..777] + "…" : joined;
    }

    /// <summary>
    /// Lỗi -138 / “Connection to tcp … failed” = máy chủ app không mở được socket tới camera (không phải lỗi Razor).
    /// </summary>
    private static string AppendVietnameseNetworkHint(string hint, string stderrRedacted)
    {
        if (string.IsNullOrWhiteSpace(stderrRedacted)) return hint;
        var tcpFail = stderrRedacted.Contains("Connection to tcp", StringComparison.OrdinalIgnoreCase)
                      && stderrRedacted.Contains("failed", StringComparison.OrdinalIgnoreCase);
        var e138 = stderrRedacted.Contains("-138", StringComparison.Ordinal)
                   || stderrRedacted.Contains("Error number -138", StringComparison.OrdinalIgnoreCase);
        var refused = stderrRedacted.Contains("Connection refused", StringComparison.OrdinalIgnoreCase);
        var noRoute = stderrRedacted.Contains("No route to host", StringComparison.OrdinalIgnoreCase);

        if (!tcpFail && !e138 && !refused && !noRoute) return hint;

        const string advice =
            " | Gợi ý: Đây là lỗi mạng — máy chạy web không kết nối TCP được tới host:port RTSP. " +
            "Kiểm tra DDNS đúng tên (vd. mtltechnology…), port forwarding trên router, firewall server/VPS; " +
            "trên máy chủ chạy: Test-NetConnection ten-ddns -Port 21554. " +
            "Nếu app trên cloud mà camera ở LAN thì phải NAT/port ra ngoài.";

        return hint + advice;
    }

    private async Task<(bool ok, string? stderrHint)> CaptureWithTransportAsync(string rtspUrl, string tempPath, string transport, CancellationToken ct)
    {
        // Đường dẫn ffmpeg đã được ProbeFfmpegOnce xác nhận ở StartAllAsync
        var binFolder = GlobalFFOptions.Current?.BinaryFolder;
        var exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var ffmpegPath = string.IsNullOrEmpty(binFolder) ? exe : Path.Combine(binFolder, exe);

        // Dùng ArgumentList (không gộp Arguments một chuỗi): trên Windows, đường dẫn có dấu cách
        // (vd. C:\Users\MTL TECHNOLOGY\...\Temp\...) làm vỡ parse argv → FFmpeg báo
        // "Error splitting the argument list: Option not found".
        // -timeout (µs) trước -i; -allowed_media_types / -fflags: tùy chọn tương thích (có thể thêm qua FfmpegExtraInputArgs).
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        static void AppendSpaceSeparatedArgs(ICollection<string> list, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var tok in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                list.Add(tok);
        }

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-y");
        AppendSpaceSeparatedArgs(psi.ArgumentList, _ffmpegExtraInputArgs);
        psi.ArgumentList.Add("-fflags");
        psi.ArgumentList.Add("nobuffer+discardcorrupt");
        psi.ArgumentList.Add("-allowed_media_types");
        psi.ArgumentList.Add("video");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(rtspUrl);
        psi.ArgumentList.Add("-an");
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-q:v");
        psi.ArgumentList.Add("2");
        psi.ArgumentList.Add("-update");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add(tempPath);

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Không khởi chạy được tiến trình ffmpeg.");

            var stderrTask = proc.StandardError.ReadToEndAsync();
            var waitOk = proc.WaitForExit(_ffmpegProcessWaitMs);

            if (!waitOk)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                proc.WaitForExit(2000);
                try { await stderrTask; } catch { /* ignore */ }
                var tSec = _ffmpegProcessWaitMs / 1000;
                return (false, $"ffmpeg không thoát trong {tSec}s — thử tăng MTLCamera:RtspSocketTimeoutSeconds hoặc FfmpegProcessExtraTimeoutSeconds.");
            }

            var exitCode = proc.ExitCode;
            string stderr;
            try
            {
                stderr = await stderrTask;
            }
            catch (OperationCanceledException)
            {
                return (false, "Đã hủy khi đọc log ffmpeg.");
            }
            catch (Exception readEx)
            {
                return (false, RedactRtspCredentialsInText(readEx.Message));
            }

            if (exitCode == 0 && File.Exists(tempPath))
            {
                var fi = new FileInfo(tempPath);
                if (fi.Length > 500)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Camera capture OK (transport={T}, size={S} bytes, path={P})", transport, fi.Length, tempPath);
                    return (true, null);
                }
            }

            var uiHint = BuildFfmpegUiHint(stderr, exitCode, transport);
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                var logSafe = RedactRtspCredentialsInText(stderr);
                var shortLog = logSafe.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5);
                _logger.LogWarning("Camera FFmpeg [{T}] exit {E}: {Msg}", transport, exitCode, string.Join(" | ", shortLog));
            }

            return (false, uiHint);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Camera capture [{T}] ngoại lệ", transport);
            return (false, RedactRtspCredentialsInText(ex.Message));
        }
    }

    private async Task CaptureLoopAsync(int cameraId, string rtspUrl, CancellationToken parentCt)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
        _ctsById[cameraId] = cts;
        _captureRunning[cameraId] = 1;

        const int frameIntervalMs = 400;
        var failStreak = 0;

        try
        {
            while (_ctsById.ContainsKey(cameraId) && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"cam_{cameraId}_{Guid.NewGuid():N}.jpg");

                    var (captureOk, ffmpegHint) = await TryCaptureRtspJpegAsync(rtspUrl, tempPath, cts.Token);

                    if (captureOk && File.Exists(tempPath))
                    {
                        var bytes = await File.ReadAllBytesAsync(tempPath);
                        if (bytes.Length > 500)
                        {
                            var base64 = Convert.ToBase64String(bytes);
                            _frames[cameraId] = "data:image/jpeg;base64," + base64;
                            _lastGoodFrameUtc[cameraId] = DateTime.UtcNow;
                            failStreak = 0;
                            _lastErrors.TryRemove(cameraId, out _);
                            if (_logger.IsEnabled(LogLevel.Debug))
                                _logger.LogDebug("Camera {Id}: frame OK, {Len} bytes", cameraId, bytes.Length);
                        }
                        else
                        {
                            failStreak++;
                            var hint = "FFmpeg trả file rỗng/quá nhỏ — kiểm tra URL RTSP, user/pass, và camera có bật substream.";
                            _lastErrors[cameraId] = hint;
                            _logger.LogWarning("Camera {Id}: ffmpeg trả file rỗng/quá nhỏ ({Len} bytes). Kiểm tra RTSP, ffmpeg trên server, và mạng tới camera.", cameraId, bytes.Length);
                        }

                        try { File.Delete(tempPath); } catch { /* ignore */ }
                    }
                    else
                    {
                        failStreak++;
                        var hint = !string.IsNullOrWhiteSpace(ffmpegHint)
                            ? ffmpegHint
                            : "FFmpeg không tạo được ảnh — server có thể không tới được camera (firewall/VPN), sai URL, hoặc timeout RTSP.";
                        _lastErrors[cameraId] = hint;
                        if (failStreak == 1 || failStreak % 15 == 0)
                            _logger.LogWarning("Camera {Id}: không lấy được ảnh. Chi tiết UI: {Hint}", cameraId, hint);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    failStreak++;
                    var shortMsg = ex.Message.Length > 180 ? ex.Message[..180] + "…" : ex.Message;
                    _lastErrors[cameraId] = shortMsg;
                    _logger.LogWarning(ex, "Camera {Id}: lỗi capture — {Message}. (RTSP chỉ chạy trên server, không phải trình duyệt; mật khẩu có @ cần được encode hoặc dùng Normalize tự động.)", cameraId, ex.Message);
                    try { await Task.Delay(3000, cts.Token); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                try
                {
                    await Task.Delay(frameIntervalMs, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            _captureRunning.TryRemove(cameraId, out _);
            _ctsById.TryRemove(cameraId, out _);
        }
    }

    public void Dispose()
    {
        StopAllAsync().GetAwaiter().GetResult();
    }
}
