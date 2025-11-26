using SharpDX;
using SharpDX.IO;
using SharpDX.Multimedia;
using SharpDX.XAudio2;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Pixi2D.Audio;

/// <summary>
/// 音效播放器类。
/// 支持播放短促的音效（如 WAV 格式）。
/// 同一时间只能播放一个声音，新声音会打断旧声音。
/// </summary>
public class SoundEffect : IDisposable
{
    // --- 静态资源缓存 ---
    // 缓存音频数据，避免重复从磁盘读取。
    // key: 声音名称, value: (AudioBuffer, DecodedPacketsInfo, WaveFormat)
    private static readonly ConcurrentDictionary<string, CachedSound> _soundCache = new();

    private record CachedSound(byte[] AudioData, uint[] DecodedPacketsInfo, WaveFormat WaveFormat);

    // --- 实例字段 ---
    private readonly XAudio2 _device;
    private readonly MasteringVoice _masteringVoice;
    private SourceVoice? _sourceVoice;

    // 用于控制 Play 任务的完成
    private TaskCompletionSource<bool>? _playTcs;
    private readonly Lock _lock = new();

    /// <summary>
    /// 初始化音效引擎。
    /// </summary>
    public SoundEffect()
    {
        _device = new XAudio2();
        _masteringVoice = new MasteringVoice(_device);
    }

    /// <summary>
    /// 预加载音频文件到缓存。
    /// </summary>
    /// <param name="name">音效名称。</param>
    /// <param name="filepath">文件路径。</param>
    public static void Preload(string name, string filepath)
    {
        if (_soundCache.ContainsKey(name)) return;

        if (!File.Exists(filepath))
        {
            throw new FileNotFoundException($"Sound file not found: {filepath}");
        }

        using var stream = new NativeFileStream(filepath, NativeFileMode.Open, NativeFileAccess.Read);
        using var soundStream = new SoundStream(stream);

        // 读取整个流到内存
        var buffer = new byte[soundStream.Length];
        soundStream.ReadExactly(buffer, 0, (int)soundStream.Length);

        var cachedSound = new CachedSound(
            buffer,
            soundStream.DecodedPacketsInfo,
            soundStream.Format
        );

        _soundCache.TryAdd(name, cachedSound);
    }

    /// <summary>
    /// 预加载音频流到缓存。
    /// </summary>
    public static void Preload(string name, Stream stream)
    {
        if (_soundCache.ContainsKey(name)) return;

        using var soundStream = new SoundStream(stream);
        var buffer = new byte[soundStream.Length];
        soundStream.ReadExactly(buffer, 0, (int)soundStream.Length);

        var cachedSound = new CachedSound(
            buffer,
            soundStream.DecodedPacketsInfo,
            soundStream.Format
        );

        _soundCache.TryAdd(name, cachedSound);
    }

    /// <summary>
    /// Plays the audio clip identified by the specified name using default volume and pitch settings.
    /// </summary>
    /// <param name="name">The name of the audio clip to play. Cannot be null or empty.</param>
    public void Play(string name) => _ = Play(name, 0f, -1f);

    /// <summary>
    /// 播放指定名称的音效。
    /// 如果当前有声音正在播放，会被立即停止。
    /// </summary>
    /// <param name="name">已预加载的音效名称。</param>
    /// <param name="start">播放起始时间（秒）。</param>
    /// <param name="end">播放结束时间（秒）。-1 表示播放到结尾。</param>
    /// <returns>代表播放过程的任务。播放完成或被停止时任务结束。</returns>
    public Task Play(string name, float start = 0f, float end = -1f)
    {
        lock (_lock)
        {
            // 1. 停止当前播放
            StopInternal();

            // 2. 获取音频数据
            if (!_soundCache.TryGetValue(name, out var sound))
            {
                Debug.WriteLine($"[SoundEffect] Sound not found: {name}");
                return Task.CompletedTask;
            }

            try
            {
                // 3. 创建新的 SourceVoice
                // 注意：由于不同音频可能有不同的 WaveFormat，无法简单复用 SourceVoice
                _sourceVoice = new SourceVoice(_device, sound.WaveFormat, true);

                // 设置回调以处理播放结束
                _sourceVoice.BufferEnd += OnBufferEnd;

                // 4. 准备 AudioBuffer
                var stream = new DataStream(sound.AudioData.Length, true, true);
                stream.Write(sound.AudioData, 0, sound.AudioData.Length);
                stream.Position = 0;

                var audioBuffer = new AudioBuffer
                {
                    Stream = stream,
                    AudioBytes = (int)stream.Length,
                    Flags = BufferFlags.EndOfStream
                };

                // 计算播放范围
                // PlayBegin/PlayLength 是以采样点(Sample)为单位
                int bytesPerSample = sound.WaveFormat.BlockAlign;
                int samplesPerSecond = sound.WaveFormat.SampleRate;

                // 计算起始位置 (字节偏移)
                // start * format.AverageBytesPerSecond 

                // 注意：AudioBuffer.PlayBegin/PlayLength 是以 Sample 为单位的
                if (start > 0)
                {
                    audioBuffer.PlayBegin = (int)(start * samplesPerSecond);
                }

                if (end > 0 && end > start)
                {
                    int durationSamples = (int)((end - start) * samplesPerSecond);
                    audioBuffer.PlayLength = durationSamples;
                }

                _sourceVoice.SubmitSourceBuffer(audioBuffer, sound.DecodedPacketsInfo);
                _sourceVoice.Start();

                _playTcs = new TaskCompletionSource<bool>();
                return _playTcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundEffect] Play failed: {ex.Message}");
                StopInternal();
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// 停止当前播放的声音。
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            StopInternal();
        }
    }

    private void StopInternal()
    {
        if (_sourceVoice != null && !_sourceVoice.IsDisposed)
        {
            _sourceVoice.Stop();
            _sourceVoice.FlushSourceBuffers();
            _sourceVoice.DestroyVoice();
            _sourceVoice.Dispose();
            _sourceVoice = null;
        }

        // 结束之前的 Task
        if (_playTcs != null)
        {
            _playTcs.TrySetResult(false); // false 表示被中断或手动停止
            _playTcs = null;
        }
    }

    private void OnBufferEnd(IntPtr obj)
    {
        // 播放自然结束
        lock (_lock)
        {
            // 只有当当前的 TCS 还是同一个实例时才设置结果
            // 防止在 StopInternal 中已经被清理
            if (_playTcs != null && !_playTcs.Task.IsCompleted)
            {
                _playTcs.TrySetResult(true); // true 表示自然播放完成
            }

            // 清理 Voice (如果不打算复用)
            // 注意：不要在回调线程中做耗时操作或复杂的锁操作
            // 这里我们通常不立即 Dispose，或者在下一次 Play 时清理。
            // 但简单起见，我们可以在这里触发一个清理信号，或者什么都不做，等待下一次 Play/Stop 清理。
            // 最好的做法是让 Play 方法去管理生命周期。
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopInternal();
            _masteringVoice?.Dispose();
            _device?.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 清除所有缓存的音频数据。
    /// </summary>
    public static void ClearCache()
    {
        _soundCache.Clear();
    }
}