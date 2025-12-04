using UnityEngine;
using System;
using LibVLCSharp;

public class VLCSetLogFile : MonoBehaviour
{
    LibVLC _libVLC;
    MediaPlayer _mediaPlayer;
    const int seekTimeDelta = 5000;
    Texture2D tex = null;
    bool playing;
    
    void Awake()
    {
        Core.Initialize(Application.dataPath);

        _libVLC = new LibVLC(enableDebugLogs: true);
        Debug.Log($"LibVLC Version: {_libVLC.Version}, Changeset: {_libVLC.Changeset}, Assembly version: {typeof(LibVLC).Assembly.GetName().Version}");

        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // Log output to file
        string timeStamp = DateTime.Now.ToString("HHmmss");
        string logFile = Application.persistentDataPath + $"/vlc-{timeStamp}.txt";
        Debug.Log($"Logging vlc output to '{logFile}'");
        _libVLC.SetLogFile(logFile);
        
        // Enable this for logs in the editor. Note that the log file is not used then.
        // _libVLC.Log += (s, e) => Debug.Log(e.FormattedLog);

        PlayPause();
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
                _mediaPlayer.Media = new Media(new Uri("https://download.blender.org/peach/bigbuckbunny_movies/big_buck_bunny_1080p_stereo.avi"));
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
}
