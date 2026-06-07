using UnityEngine;
using System;
using LibVLCSharp;

public class VLCReplayGainDemo : MonoBehaviour
{
    const int SeekTimeDelta = 5000;
    
    public enum ReplayGainMode
    {
        None, Track, Album
    }

    public ReplayGainMode replayGainMode;
    
    private LibVLC _libVLC;
    private MediaPlayer _mediaPlayer;
    private Texture2D tex;
    private bool playing;

    private string audioFileUri = $"file://{Application.dataPath}/StreamingAssets/ReplayGain-demo.mp3";
    
    void Awake()
    {
        Core.Initialize(Application.dataPath);

        string[] vlcOptions = GetVlcReplayGainOptions();
        _libVLC = new LibVLC(enableDebugLogs: true, vlcOptions);
        Debug.Log($"LibVLC Version: {_libVLC.Version}, Changeset: {_libVLC.Changeset}, Assembly version: {typeof(LibVLC).Assembly.GetName().Version}");

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        
        string timeStamp = DateTime.Now.ToString("HHmmss");
        string logFile = Application.persistentDataPath + $"/vlc-{timeStamp}.txt";
        Debug.Log($"Logging vlc output to '{logFile}'");
        _libVLC.SetLogFile(logFile);
        //_libVLC.Log += (s, e) => UnityEngine.Debug.Log(e.FormattedLog); // enable this for logs in the editor

        PlayPause();
    }

    private string[] GetVlcReplayGainOptions()
    {
        switch (replayGainMode)
        {
            case ReplayGainMode.None:
                return new string[]{};
            case ReplayGainMode.Track:
                // TODO: Track mode does not work, only Album mode.
                // The mp3 file has both set to -40 dB, so should play quietly in both modes. 
                return new string[]{"--audio-replay-gain-mode=track"};
            case ReplayGainMode.Album:
                return new string[]{"--audio-replay-gain-mode=album"};
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SeekForward()
    {
        Debug.Log("[VLC] Seeking forward !");
        _mediaPlayer.SetTime(_mediaPlayer.Time + SeekTimeDelta);
    }

    public void SeekBackward()
    {
        Debug.Log("[VLC] Seeking backward !");
        _mediaPlayer.SetTime(_mediaPlayer.Time - SeekTimeDelta);
    }

    void OnDisable() 
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;

        _libVLC?.Dispose();
        _libVLC = null;
    }

    public void PlayPause()
    {
        Debug.Log ("[VLC] Toggling Play Pause !");
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
                _mediaPlayer.Media = new Media(new Uri(audioFileUri));
                _mediaPlayer.Media.AddOption(":audio-replay-gain-mode=track");
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
    }

    private string ToHumanReadableTime(long milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss\.fff");
    }
}
