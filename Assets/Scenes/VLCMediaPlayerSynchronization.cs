using UnityEngine;
using System;
using LibVLCSharp;
using TMPro;

public class VLCMediaPlayerSynchronization : MonoBehaviour
{
    private static LibVLC _libVLC;
    private const int seekTimeDelta = 250;
    
    public TMP_Text timeLabel;
    public string mediaName = $"Stone Sour - Through Glass.mp4";
    public VLCMediaPlayerSynchronization syncTo;

    private MediaPlayer _mediaPlayer;
    private bool playing;
    private Texture2D tex;

    private TimeSynchronizer timeSynchronizer;
    
    void Awake()
    {
        InitLibVlc();

        if (syncTo != null)
        {
            timeSynchronizer = new TimeSynchronizer(
                () => syncTo._mediaPlayer.Time,
                () => _mediaPlayer.Time,
                newTime => _mediaPlayer.SetTime((long)newTime),
                newPause => _mediaPlayer.SetPause(newPause));
        }

        PlayPause();
    }

    private void InitLibVlc()
    {
        if (_libVLC != null)
        {
            return;
        }

        Core.Initialize(Application.dataPath);

        _libVLC = new LibVLC(enableDebugLogs: true);
        Debug.Log($"LibVLC Version: {_libVLC.Version}, Changeset: {_libVLC.Changeset}, Assembly version: {typeof(LibVLC).Assembly.GetName().Version}");

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // Log output to file
        string timeStamp = DateTime.Now.ToString("HHmmss");
        string logFile = Application.persistentDataPath + $"/vlc-{timeStamp}-{mediaName}.txt";
        Debug.Log($"Logging vlc output to '{logFile}'");
        _libVLC.SetLogFile(logFile);
        
        // Enable this for logs in the editor. Note that the log file is not used then.
        // _libVLC.Log += (s, e) => Debug.Log(e.FormattedLog);
    }

    public void SeekForward()
    {
        Debug.Log("[VLC] Seeking forward !");
        _mediaPlayer.SetTime(_mediaPlayer.Time + seekTimeDelta);
    }

    public void SeekBackward()
    {
        Debug.Log("[VLC] Seeking backward !");
        _mediaPlayer.SetTime(_mediaPlayer.Time - seekTimeDelta);
    }

    private void OnDisable()
    {
        Dispose();
    }

    private void OnApplicationQuit()
    {
        Dispose();
    }

    void Dispose()
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;

        _libVLC?.CloseLogFile();
        _libVLC?.Dispose();
        _libVLC = null;
    }

    public void PlayPause()
    {
        if (_mediaPlayer == null)
        {
            _mediaPlayer = new MediaPlayer(_libVLC);
        }
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
        }
        else
        {
            playing = true;

            if(_mediaPlayer.Media == null)
            {
                // playing remote media
                string mediaUri = $"file://{Application.dataPath}/StreamingAssets/{mediaName}";
                _mediaPlayer.Media = new Media(new Uri(mediaUri));
            }

            _mediaPlayer.Play();
        }
    }

    public void Stop ()
    {
        Debug.Log ("[VLC] Stopping Player !");

        playing = false;
        _mediaPlayer?.Stop();
        
        // there is no need to dispose every time you stop, but you should do so when you're done using the mediaplayer and this is how:
        // _mediaPlayer?.Dispose(); 
        // _mediaPlayer = null;
        GetComponent<Renderer>().material.mainTexture = null;
        tex = null;
    }

    void Update()
    {
        UpdateTimeLabel();
        
        if(!playing) return;

        if (tex == null)
        {
            tex = TextureHelper.CreateNativeTexture(ref _mediaPlayer, linear: true);
            GetComponent<Renderer>().material.mainTexture = tex;
        }
        else if (tex != null)
        {
            TextureHelper.UpdateTexture(tex, ref _mediaPlayer);
        }

        if (_mediaPlayer.IsPlaying && timeSynchronizer != null)
        {
            timeSynchronizer.Update();
        }
    }
    
    private void UpdateTimeLabel()
    {
        if (_mediaPlayer != null && _mediaPlayer.Media != null)
        {
            timeLabel.text = $"Time ({(_mediaPlayer.IsPlaying ? "playing" : "paused")}): {ToHumanReadableTime(_mediaPlayer.Time)}";
        }
    }

    private string ToHumanReadableTime(long milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss\.fff");
    }
}

public class TimeSynchronizer
{
    private const int ImmediatePlaybackPositionSyncThresholdInMillis = 2000;
    private const int MinOffsetToSyncPositionInMillis = 100;
    private const double SyncCheckIntervalInSeconds = 4;
    
    public bool SyncingPositionWithPause { get; private set; }
    
    private readonly Func<double> getTargetTime;
    private readonly Func<double> getTime;
    private readonly Action<double> setTime;
    private readonly Action<bool> setPause;

    private float lastSyncTimeInSeconds;
    
    public TimeSynchronizer(
        Func<double> getTargetTime,
        Func<double> getTime,
        Action<double> setTime,
        Action<bool> setPause)
    {
        this.getTargetTime = getTargetTime;
        this.getTime = getTime;
        this.setTime = setTime;
        this.setPause = setPause;
    }

    public void Update()
    {
        double targetTime = getTargetTime();
        if (targetTime < 0)
        {
            return;
        }
        
        SyncToTime(targetTime);
    }

    private void SyncToTime(double targetTime)
    {
        if (Time.time < lastSyncTimeInSeconds + SyncCheckIntervalInSeconds)
        {
            return;
        }
        lastSyncTimeInSeconds = Time.time;

        // Positive when target (audio) is ahead.
        double currentTime = getTime();
        double offsetInMillis = targetTime - currentTime;
        if (Math.Abs(offsetInMillis) < MinOffsetToSyncPositionInMillis)
        {
            Debug.Log($"No sync to audio position. Offset: {offsetInMillis:F1} ms");
            return;
        }

        // A big mismatch is corrected immediately.
        // A short mismatch is either paused or skipped to reach target position.
        // This is a workaround because unfortunately, SetRate did not work well to smooth out the difference.
        if (Math.Abs(offsetInMillis) > ImmediatePlaybackPositionSyncThresholdInMillis)
        {
            Debug.Log($"Hard sync to audio position. Offset: {offsetInMillis:F1} ms");
            HardSync(targetTime);
        }
        else if (offsetInMillis < 0)
        {
            Debug.Log($"Target is behind, pausing a moment. Offset: {offsetInMillis:F1} ms");
            SoftSyncViaPause(offsetInMillis);
        }
        else if (offsetInMillis > 0)
        {
            Debug.Log($"Target is ahead, skipping a moment. Offset: {offsetInMillis:F1} ms");
            SoftSyncViaSkip(currentTime, offsetInMillis);
        }
    }

    private void HardSync(double targetTime)
    {
        setTime(targetTime);
    }

    private async Awaitable SoftSyncViaPause(double offsetInMillis)
    {
        try
        {
            SyncingPositionWithPause = true;
            setPause(true);
            // Offset is negative because video is ahead (larger time) of audio (smaller time).
            float waitTimeSeconds = (float)Math.Abs(offsetInMillis / 1000.0);
            Debug.Log($"Pause: {waitTimeSeconds} s");
            await Awaitable.WaitForSecondsAsync(waitTimeSeconds);
            setPause(false);
        }
        finally
        {
            SyncingPositionWithPause = false;
        }
    }

    private void SoftSyncViaSkip(double currentTime, double offsetInMillis)
    {
        // Changing time needs some time itself, so add a little bit of delay.
        double skipTimeMs = offsetInMillis + 600;
        Debug.Log($"Skip time: {skipTimeMs} ms");
        setTime(currentTime + skipTimeMs);
    }
}
