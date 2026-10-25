#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Kern.Core.Interfaces;
using UnityEngine;

namespace Kern.Game;

/// <summary>
/// Резервное воспроизведение серверных SFX через Unity-аудио, когда событие
/// отсутствует в банках FMOD (например, sfx/c190, пока банк не пересобран в
/// FMOD Studio). WAV ("audio/&lt;имя&gt;.wav") скачивается ассет-лоадером с
/// сервера, декодируется в AudioClip (PCM 8/16/24 бит или IEEE float) и
/// играется 3D-источником в точке события. Клипы кэшируются на сессию,
/// одновременные запуски не глушат друг друга.
/// </summary>
internal static class UnityWavFallback
{
    private const string TAG = "[UnityWavFallback]";
    private const string ServerPathFormat = "audio/{0}.wav";

    private static readonly Dictionary<string, AudioClip> _clipCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _reportedMessages = new(StringComparer.Ordinal);

    public static void Play(string eventName, Vector3 position, IAssetLoader assetLoader)
    {
        PlayAsync(eventName, position, assetLoader).Forget();
    }

    private static async UniTask PlayAsync(string eventName, Vector3 position, IAssetLoader assetLoader)
    {
        try
        {
            string clipName = eventName.Substring(eventName.LastIndexOf('/') + 1);
            if (!_clipCache.TryGetValue(clipName, out AudioClip? clip))
            {
                byte[]? bytes = await assetLoader.GetAssetBytesAsync(
                    string.Format(ServerPathFormat, clipName),
                    timeoutSeconds: 5);

                if (bytes == null || bytes.Length == 0)
                {
                    ReportOnce($"Сервер не отдал '{string.Format(ServerPathFormat, clipName)}' — фолбэк-звука нет.");
                    return;
                }

                clip = DecodeWav(clipName, bytes);
                if (clip == null)
                {
                    return;
                }

                _clipCache[clipName] = clip;
            }

            var voice = new GameObject($"WavFallback_{clipName}");
            voice.transform.position = position;
            var source = voice.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8f;
            source.maxDistance = 100f;
            source.dopplerLevel = 0f;
            source.playOnAwake = false;
            source.Play();
            Debug.Log($"{TAG} '{clipName}' играет (3D, {clip.length:F1} с).");

            await UniTask.Delay(
                TimeSpan.FromSeconds(clip.length + 0.1f),
                cancellationToken: voice.GetCancellationTokenOnDestroy());

            UnityEngine.Object.Destroy(voice);
        }
        catch (OperationCanceledException)
        {
            // Сцена/голос завершились раньше — это штатный путь выхода.
        }
        catch (Exception ex)
        {
            ReportOnce($"Не удалось воспроизвести '{eventName}': {ex.Message}");
        }
    }

    private static AudioClip? DecodeWav(string clipName, byte[] bytes)
    {
        if (bytes.Length < 12 ||
            bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F' ||
            bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
        {
            ReportOnce($"'{clipName}' — это не RIFF/WAV, фолбэк пропущен.");
            return null;
        }

        int channels = 0;
        int frequency = 0;
        int bitsPerSample = 0;
        int format = 0;
        byte[]? data = null;

        int pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            string chunkId = Encoding.ASCII.GetString(bytes, pos, 4);
            int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
            int payload = pos + 8;
            if (chunkId == "fmt " && chunkSize >= 16)
            {
                format = BitConverter.ToUInt16(bytes, payload);
                channels = BitConverter.ToUInt16(bytes, payload + 2);
                frequency = BitConverter.ToInt32(bytes, payload + 4);
                bitsPerSample = BitConverter.ToUInt16(bytes, payload + 14);
            }
            else if (chunkId == "data")
            {
                int size = Math.Min(chunkSize, bytes.Length - payload);
                data = new byte[size];
                Array.Copy(bytes, payload, data, 0, size);
            }

            pos = payload + chunkSize + (chunkSize & 1); // чанки выровнены по 2 байтам
        }

        if (data == null || channels <= 0 || channels > 2 || frequency <= 0)
        {
            ReportOnce($"'{clipName}' — в WAV нет валидных fmt/data чанков.");
            return null;
        }

        float[]? samples = format switch
        {
            1 when bitsPerSample == 16 => ConvertPcm16(data),
            1 when bitsPerSample == 8 => ConvertPcm8(data),
            1 when bitsPerSample == 24 => ConvertPcm24(data),
            3 when bitsPerSample == 32 => ConvertFloat32(data),
            _ => ReportUnsupported(clipName, format, bitsPerSample),
        };
        if (samples == null)
        {
            return null;
        }

        int frames = samples.Length / channels;
        var clip = AudioClip.Create($"wav_{clipName}", frames, channels, frequency, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float[]? ReportUnsupported(string clipName, int format, int bitsPerSample)
    {
        ReportOnce($"'{clipName}' — формат WAV {format}/{bitsPerSample} бит не поддержан.");
        return null;
    }

    private static float[] ConvertPcm16(byte[] data)
    {
        var result = new float[data.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
        }

        return result;
    }

    private static float[] ConvertPcm8(byte[] data)
    {
        var result = new float[data.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (data[i] - 128) / 128f;
        }

        return result;
    }

    private static float[] ConvertPcm24(byte[] data)
    {
        var result = new float[data.Length / 3];
        for (int i = 0; i < result.Length; i++)
        {
            int sample = data[i * 3] | (data[i * 3 + 1] << 8) | (data[i * 3 + 2] << 16);
            if ((sample & 0x800000) != 0)
            {
                sample |= unchecked((int)0xFF000000);
            }

            result[i] = sample / 8388608f;
        }

        return result;
    }

    private static float[] ConvertFloat32(byte[] data)
    {
        var result = new float[data.Length / 4];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BitConverter.ToSingle(data, i * 4);
        }

        return result;
    }

    private static void ReportOnce(string message)
    {
        if (_reportedMessages.Add(message))
        {
            Debug.LogWarning($"{TAG} {message}");
        }
    }
}
