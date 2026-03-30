using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LibVLCSharp;

// Load audio samples from file using SetAudioCallbacks, similar to this example https://code.videolan.org/mfkl/libvlcsharp-samples/-/blob/master/AudioCallbacks/Program.cs
public class VLCLoadAudioSamples : MonoBehaviour
{
    private const float LoadAudioClipTimeoutInSeconds = 10f;

    public AudioSource audioSource;
    
    private LibVLC libVLC;
    private MediaPlayer mediaPlayer;

    async void Awake()
    {
        TaskScheduler.UnobservedTaskException +=
            (_, e) => Debug.LogException(e.Exception);
        Core.Initialize(Application.dataPath);
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        string fileName = "Stone Sour - Through Glass - excerpt2.ogg";
        string path = Application.streamingAssetsPath + $"/{fileName}";

        // PlayMedia(path);

        AudioClip audioClip = await LoadAudioClipViaVlc(path);
        if (audioClip != null)
        {
            Debug.Log(
                $"Loaded audio clip via VLC. length: {audioClip.length}, channels: {audioClip.channels}, frequency: {audioClip.frequency}");
            audioSource.clip = audioClip;
            audioSource.Play();
        }
        else
        {
            Debug.LogError($"Failed to load audio clip via VLC for path: {path}");
        }
    }

    private void PlayMedia(string absolutePath)
    {
        libVLC = new LibVLC(enableDebugLogs: true);
        mediaPlayer = new MediaPlayer(libVLC);

        mediaPlayer.Media = new Media(new Uri("file://" + absolutePath));
        mediaPlayer.Play();
    }

    private async Awaitable<AudioClip> LoadAudioClipViaVlc(string absolutePath)
    {
        Debug.Log($"Starting LoadAudioClipViaVlc for path: {absolutePath}");
        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"File does not exist at path: {absolutePath}");
            return null;
        }

        AudioClip result = null;
        List<float> capturedSamples = new();
        int channels = 2;
        int sampleRate = 48000;
        bool finished = false;
        float timeBeforeLoading = Time.realtimeSinceStartup;

        // Use a separate LibVLC instance for loading to avoid side effects
        // and to pass specific options for fast decoding if possible.
        // "--no-video" ensures we don't waste time on video decoding.
        using LibVLC libVLC = new LibVLC(enableDebugLogs: true);
        libVLC.Log += (s, e) => Debug.Log($"[VLC] {e.FormattedLog}");

        Debug.Log(
            $"LibVLC Version: {libVLC.Version}, Changeset: {libVLC.Changeset}, Assembly version: {typeof(LibVLC).Assembly.GetName().Version}");

        using Media media = new Media(new Uri("file://" + absolutePath));
        await media.ParseAsync(libVLC);
        var audioTracks = media.TrackList(TrackType.Audio);
        if (audioTracks.Count > 0)
        {
            channels = (int)audioTracks[0].Data.Audio.Channels;
            sampleRate = (int)audioTracks[0].Data.Audio.Rate;
        }
        Debug.Log($"VLC media parsed. channels: {channels}, sampleRate: {sampleRate}");

        using MediaPlayer mediaPlayer = new MediaPlayer(libVLC);
        mediaPlayer.Media = media;

        // Setup audio format and callbacks
        mediaPlayer.SetAudioFormat("FL32", (uint) sampleRate, (uint) channels);

        mediaPlayer.SetAudioCallbacks(
            playCb: (IntPtr data, IntPtr samplesPtr, uint count, long pts) =>
            {
                int floatCount = (int)count * channels;
                float[] samples = new float[floatCount]; // TODO: Bad allocation
                System.Runtime.InteropServices.Marshal.Copy(samplesPtr, samples, 0, floatCount);
                lock (capturedSamples)
                {
                    capturedSamples.AddRange(samples);
                }
            },
            pauseCb: (IntPtr data, long pts) => { },
            resumeCb: (IntPtr data, long pts) => { },
            flushCb: (IntPtr data, long pts) =>
            {
                Debug.Log("flushCallback");
            },
            drainCb: (IntPtr data) => { });

        mediaPlayer.Stopped += (sender, args) =>
        {
            Debug.Log("VLC MediaPlayer Stopped event triggered");
            finished = true;
        };

        mediaPlayer.EncounteredError += (sender, args) =>
        {
            Debug.LogError($"VLC encountered an error while loading audio from path: {absolutePath}");
            finished = true;
        };

        // media.AddOption(":no-video");
        // media.AddOption(":no-spu");
        // media.AddOption(":clock-jitter=0");
        // media.AddOption(":clock-synchro=0");
        // media.AddOption(":no-audio-sync");
        Debug.Log($"Calling mediaPlayer.PlayAsync() for path: {absolutePath}");
        await mediaPlayer.PlayAsync();

        // Wait for it to finish.
        float startTime = Time.realtimeSinceStartup;
        while (!finished && (Time.realtimeSinceStartup - startTime) < LoadAudioClipTimeoutInSeconds)
        {
            Debug.Log($"mediaPlayer.State: {mediaPlayer.State}");
            if (mediaPlayer.State is VLCState.Error or VLCState.Stopped or VLCState.Stopping)
            {
                Debug.Log($"VLC finished decoding with state: {mediaPlayer.State}");
                finished = true;
                break;
            }

            await Awaitable.NextFrameAsync();
        }

        if (finished && (Time.realtimeSinceStartup - startTime) < LoadAudioClipTimeoutInSeconds)
        {
            Debug.Log($"VLC finished decoding with state: {mediaPlayer.State}");
        }
        else if (!finished)
        {
            Debug.LogError($"VLC loading audio timed out for path: {absolutePath}. Current state: {mediaPlayer.State}");
        }

        TimeSpan duration = TimeSpan.FromSeconds(Time.realtimeSinceStartup - timeBeforeLoading);
        Debug.Log($"Stopping mediaPlayer and creating AudioClip. Took {duration.TotalSeconds:F2} s, Samples captured: {capturedSamples.Count}");
        mediaPlayer.Stop();

        float[] allSamples;
        lock (capturedSamples)
        {
            allSamples = capturedSamples.ToArray();
        }

        if (allSamples.Length > 0)
        {
            result = AudioClip.Create(Path.GetFileName(absolutePath), allSamples.Length / channels, channels,
                sampleRate, false);
            result.SetData(allSamples, 0);
        }

        return result;
    }

    private void OnApplicationQuit()
    {
        libVLC?.Dispose();
        mediaPlayer?.Dispose();
    }
}
