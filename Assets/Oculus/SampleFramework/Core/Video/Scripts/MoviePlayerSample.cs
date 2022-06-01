/************************************************************************************

Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.  

************************************************************************************/

using UnityEngine;
using System;
using System.IO;
using System.Threading;
using UnityEngine.Video;

public class MoviePlayerSample : MonoBehaviour
{
    private bool videoPausedBeforeAppPause = false;

    private UnityEngine.Video.VideoPlayer videoPlayer = null;
    private OVROverlay overlay = null;
    private Renderer mediaRenderer = null;

    public bool IsPlaying { get; private set; }
    public long Duration { get; private set; }
    public long PlaybackPosition { get; private set; }

    private RenderTexture copyTexture;
    private Material externalTex2DMaterial;

    public string MovieName;
    public string DrmLicenseUrl;
    public bool LoopVideo;
    public VideoShape Shape;
    public VideoStereo Stereo;
    public bool AutoDetectStereoLayout;
    public bool DisplayMono;

    // keep track of last state so we know when to update our display
    VideoShape _LastShape = (VideoShape) (-1);
    VideoStereo _LastStereo = (VideoStereo) (-1);
    bool _LastDisplayMono = false;

    public enum VideoShape
    {
        _360,
        _180,
        Quad
    }

    public enum VideoStereo
    {
        Mono,
        TopBottom,
        LeftRight,
        BottomTop
    }

    /// <summary>
    /// Initialization of the movie surface
    /// </summary>
    void Awake()
    {
        Debug.Log("MovieSample Awake");

        mediaRenderer = GetComponent<Renderer>();

        videoPlayer = GetComponent<UnityEngine.Video.VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
        videoPlayer.isLooping = LoopVideo;

        overlay = GetComponent<OVROverlay>();
        if (overlay == null)
            overlay = gameObject.AddComponent<OVROverlay>();

        // disable it to reset it.
        overlay.enabled = false;
        // only can use external surface with native plugin
        overlay.isExternalSurface = NativeVideoPlayer.IsAvailable;
        // only mobile has Equirect shape
        overlay.enabled = (overlay.currentOverlayShape != OVROverlay.OverlayShape.Equirect ||
                           Application.platform == RuntimePlatform.Android);

#if UNITY_EDITOR
        overlay.currentOverlayShape = OVROverlay.OverlayShape.Quad;
        overlay.enabled = true;
#endif
    }

    private bool IsLocalVideo(string movieName)
    {
        // if the path contains any url scheme, it is not local
        return !movieName.Contains("://");
    }

    private void UpdateShapeAndStereo()
    {
        if (AutoDetectStereoLayout)
        {
            if (overlay.isExternalSurface)
            {
                int w = NativeVideoPlayer.VideoWidth;
                int h = NativeVideoPlayer.VideoHeight;
                switch (NativeVideoPlayer.VideoStereoMode)
                {
                    case NativeVideoPlayer.StereoMode.Mono:
                        Stereo = VideoStereo.Mono;
                        break;
                    case NativeVideoPlayer.StereoMode.LeftRight:
                        Stereo = VideoStereo.LeftRight;
                        break;
                    case NativeVideoPlayer.StereoMode.TopBottom:
                        Stereo = VideoStereo.TopBottom;
                        break;
                    case NativeVideoPlayer.StereoMode.Unknown:
                        if (w > h)
                        {
                            Stereo = VideoStereo.LeftRight;
                        }
                        else
                        {
                            Stereo = VideoStereo.TopBottom;
                        }

                        break;
                }
            }
        }

        if (Shape != _LastShape || Stereo != _LastStereo || DisplayMono != _LastDisplayMono)
        {
            Rect destRect = new Rect(0, 0, 1, 1);
            switch (Shape)
            {
                case VideoShape._360:
                    // set shape to Equirect
                    overlay.currentOverlayShape = OVROverlay.OverlayShape.Equirect;
                    break;
                case VideoShape._180:
                    overlay.currentOverlayShape = OVROverlay.OverlayShape.Equirect;
                    destRect = new Rect(0.25f, 0, 0.5f, 1.0f);
                    break;
                case VideoShape.Quad:
                default:
                    overlay.currentOverlayShape = OVROverlay.OverlayShape.Quad;
                    break;
            }

            overlay.overrideTextureRectMatrix = true;
            overlay.invertTextureRects = false;

            Rect sourceLeft = new Rect(0, 0, 1, 1);
            Rect sourceRight = new Rect(0, 0, 1, 1);
            switch (Stereo)
            {
                case VideoStereo.LeftRight:
                    // set source matrices for left/right
                    sourceLeft = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
                    sourceRight = new Rect(0.5f, 0.0f, 0.5f, 1.0f);
                    break;
                case VideoStereo.TopBottom:
                    // set source matrices for top/bottom
                    sourceLeft = new Rect(0.0f, 0.5f, 1.0f, 0.5f);
                    sourceRight = new Rect(0.0f, 0.0f, 1.0f, 0.5f);
                    break;
                case VideoStereo.BottomTop:
                    // set source matrices for top/bottom
                    sourceLeft = new Rect(0.0f, 0.0f, 1.0f, 0.5f);
                    sourceRight = new Rect(0.0f, 0.5f, 1.0f, 0.5f);
                    break;
            }

            overlay.SetSrcDestRects(sourceLeft, DisplayMono ? sourceLeft : sourceRight, destRect, destRect);

            _LastDisplayMono = DisplayMono;
            _LastStereo = Stereo;
            _LastShape = Shape;
        }
    }

    private void Start()
    {
        print("Start");
        Loom.Initialize();
#if UNITY_EDITOR
        StartRecvDataTimer(true);
        
        // methodName:方法名  time:方法在time秒后被调用 repeatRate: 被调用后每隔repeatRate秒的频率循环调用
        // public method InvokeRepeating(methodName: string, time: float, repeatRate: float): void;

        //第一个参数MethodName：被调用的方法名字  time：time秒后被调用
        Invoke("Play2", 5);
        // Play(MovieName);
#else
        jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        jo = jc.GetStatic<AndroidJavaObject>("currentActivity");
        // 由安卓那边调用Play(MovieName)，因为那边要先做一些准备处理，比如解析埋点数据;
        Invoke("Play2", 5);
#endif
    }

    void Play2()
    {
        Play(MovieName);
    }

    void Play(string moviePath, string drmLicencesUrl)
    {
        if (moviePath != string.Empty)
        {
            Debug.Log("Playing Video: " + moviePath);
            if (overlay.isExternalSurface)
            {
                OVROverlay.ExternalSurfaceObjectCreated surfaceCreatedCallback = () =>
                {
                    Debug.Log("Playing ExoPlayer with SurfaceObject");
                    NativeVideoPlayer.PlayVideo(moviePath, drmLicencesUrl, overlay.externalSurfaceObject);
                    NativeVideoPlayer.SetLooping(LoopVideo);
                };

                if (overlay.externalSurfaceObject == IntPtr.Zero)
                {
                    overlay.externalSurfaceObjectCreated = surfaceCreatedCallback;
                }
                else
                {
                    surfaceCreatedCallback.Invoke();
                }
            }
            else
            {
                Debug.Log("Playing Unity VideoPlayer");
                videoPlayer.url = moviePath;
                videoPlayer.Prepare();
                videoPlayer.Play();
            }

            Debug.Log("MovieSample Start");
            IsPlaying = true;
        }
        else
        {
            Debug.LogError("No media file name provided");
        }
    }

    public void Play()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Play();
        }
        else
        {
            videoPlayer.Play();
        }

        IsPlaying = true;
    }

    public void Pause()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Pause();
        }
        else
        {
            videoPlayer.Pause();
        }

        IsPlaying = false;
    }

    public void SeekTo(long position)
    {
        long seekPos = Math.Max(0, Math.Min(Duration, position));
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.PlaybackPosition = seekPos;
        }
        else
        {
            videoPlayer.time = seekPos / 1000.0;
        }
    }

    void Update()
    {
        UpdateShapeAndStereo();
        if (!overlay.isExternalSurface)
        {
            var displayTexture = videoPlayer.texture != null ? videoPlayer.texture : Texture2D.blackTexture;
            if (overlay.enabled)
            {
                if (overlay.textures[0] != displayTexture)
                {
                    // OVROverlay won't check if the texture changed, so disable to clear old texture
                    overlay.enabled = false;
                    overlay.textures[0] = displayTexture;
                    overlay.enabled = true;
                }
            }
            else
            {
                mediaRenderer.material.mainTexture = displayTexture;
                mediaRenderer.material.SetVector("_SrcRectLeft", overlay.srcRectLeft.ToVector());
                mediaRenderer.material.SetVector("_SrcRectRight", overlay.srcRectRight.ToVector());
            }

            IsPlaying = videoPlayer.isPlaying;
            PlaybackPosition = (long) (videoPlayer.time * 1000L);

#if UNITY_2019_1_OR_NEWER
            Duration = (long) (videoPlayer.length * 1000L);
#else
            Duration = videoPlayer.frameRate > 0 ? (long)(videoPlayer.frameCount / videoPlayer.frameRate * 1000L) : 0L;
#endif
        }
        else
        {
            NativeVideoPlayer.SetListenerRotation(Camera.main.transform.rotation);
            IsPlaying = NativeVideoPlayer.IsPlaying;
            PlaybackPosition = NativeVideoPlayer.PlaybackPosition;
            Duration = NativeVideoPlayer.Duration;
            if (IsPlaying && (int) OVRManager.display.displayFrequency != 60)
            {
                OVRManager.display.displayFrequency = 60.0f;
            }
            else if (!IsPlaying && (int) OVRManager.display.displayFrequency != 72)
            {
                OVRManager.display.displayFrequency = 72.0f;
            }
        }
    }

    public void SetPlaybackSpeed(float speed)
    {
        // clamp at 0
        speed = Mathf.Max(0, speed);
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.SetPlaybackSpeed(speed);
        }
        else
        {
            videoPlayer.playbackSpeed = speed;
        }
    }

    public void Stop()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Stop();
        }
        else
        {
            videoPlayer.Stop();
        }

        IsPlaying = false;
    }

    /// <summary>
    /// Pauses video playback when the app loses or gains focus
    /// </summary>
    void OnApplicationPause(bool appWasPaused)
    {
        Debug.Log("OnApplicationPause: " + appWasPaused);
        if (appWasPaused)
        {
            videoPausedBeforeAppPause = !IsPlaying;
        }

        // Pause/unpause the video only if it had been playing prior to app pause
        if (!videoPausedBeforeAppPause)
        {
            if (appWasPaused)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }
    }

    #region 实现与蓝牙设备联动的代码

    //通过包名获取java class
    AndroidJavaClass jc;

    //获取当前的activity
    AndroidJavaObject jo;

    //该方法给安卓调用
    void ShowToast(string msg)
    {
        print("ShowToast from unity" + msg);
        jo.Call("showToast", msg);
    }

    //定时器
    void StartRecvDataTimer(bool autoFlag)
    {
        //实例化Timer类，设置间隔时间为100毫秒；
        System.Timers.Timer t = new System.Timers.Timer(100);
        t.Elapsed += new System.Timers.ElapsedEventHandler(ShowRecv);
        t.AutoReset = autoFlag; //设置是执行一次（false）还是一直执行(true)；
        t.Enabled = true; //是否执行System.Timers.Timer.Elapsed事件；
    }

    //到达时间的时候执行事件；
    void ShowRecv(object source, System.Timers.ElapsedEventArgs e)
    {
        // print("ShowRecv");
        // int i = Thread.CurrentThread.ManagedThreadId;
        // print(i);
        //异步在多线程下运行
        //回到unity线程继续运行
        //耗时操作执行完毕后，就可以用Loom的方法在Unity主线程中调用Text组件
        Loom.QueueOnMainThread((param) =>
        {
            // int i2 = Thread.CurrentThread.ManagedThreadId;
            // print(i2);
#if UNITY_EDITOR
            print("当前播放进度:" + GetTime());
#else
        print("当前播放进度:"+GetTime());
        //调用安卓那边触发埋点的方法，并把当前播放进度传过去
        jo.Call("showToast", "你好哇");
#endif
        }, null);
    }

    //获取当前播放进度，并转换为毫秒，这样的转换其实不够准确，但是能精确到100毫秒即可
    public long GetTime()
    {
        return (long) (videoPlayer.time * 1000.0);
    }

    //在合适的时候调用播放,注意：如果是在start方法里调用最好延迟一秒，因为untiy有bug，但是我未发现，是这个库的官方提示要这么做
    void Play(string moviePathOrUrl)
    {
        //在start里调用加上如下注释代码，并让start方法返回System.Collections.IEnumerator，这是库的官方提示要这么做
        // if (mediaRenderer.material == null)
        // {
        //     Debug.LogError("No material for movie surface");
        //     yield break;
        // }
        //
        // // wait 1 second to start (there is a bug in Unity where starting
        // // the video too soon will cause it to fail to load)
        // yield return new WaitForSeconds(1.0f);
        print("调用了Play");
        MovieName = moviePathOrUrl;
        if (!string.IsNullOrEmpty(MovieName))
        {
            if (IsLocalVideo(MovieName))
            {
#if UNITY_EDITOR
                // in editor, just pull in the movie file from wherever it lives (to test without putting in streaming assets)
                var guids = UnityEditor.AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(MovieName));

                if (guids.Length > 0)
                {
                    string video = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    print("111111");
                    Play(video, null);
                }
#else
                 print("2222222");
                Play(Application.streamingAssetsPath +"/" + MovieName, null);
#endif
            }
            else
            {
                print("33333");
                Play(MovieName, DrmLicenseUrl);
            }
        }
        //播放视频后开启定时器，每100毫秒调用一次安卓那边触发埋点的方法，并把当前播放进度传过去，注:如果有视频播放成功回调方法，可以放到回调方法里执行
#if UNITY_EDITOR
#else
      StartRecvDataTimer(true);
#endif
    }

    #endregion
}