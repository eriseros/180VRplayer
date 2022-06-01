/* Copyright (c) 2017-present Evereal. All rights reserved. */

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Evereal.VRVideoPlayer
{
    [RequireComponent(typeof(VRVideoPlayer))]
    public class VideoPlayerCtrl : MonoBehaviour
    {
        #region Properties

        // Video playlist
        public List<string> playlist = new List<string>();
        public VideoTitle videoTitle;
        public PlayButton playButton;
        public VideoTime currentTime;
        public VideoTime totalTime;
        public ProgressBar progressBar;
        public VolumeButton volumeButton;
        public VolumeBar volumeBar;
        public RenderModeButton normalButton;
        public RenderModeButton _180Button;
        public RenderModeButton _360Button;
        public StereoModeButton monoButton;
        public StereoModeButton leftRightButton;
        public StereoModeButton topBottomButton;
        public Fade fade;

        private VRVideoPlayer vrVideoPlayer;
        public bool isVideoJumping { get; private set; }

        public bool isVideoPlaying
        {
            get { return vrVideoPlayer.isPlaying; }
        }

        public bool isVideoPaused
        {
            get { return vrVideoPlayer.isPaused; }
        }

        public bool isAudioMute
        {
            get { return vrVideoPlayer.IsAudioMute(0); }
        }

        public double videoTime
        {
            get { return vrVideoPlayer.time; }
            set { vrVideoPlayer.time = value; }
        }

        public double videoLength
        {
            get { return vrVideoPlayer.length; }
        }

        // Get current video index
        private int videoIndex = 0;

        // Log message format template
        private const string LOG_FORMAT = "[VideoPlayerCtrl] {0}";

        #endregion

        #region Events

        private void OnStarted(VRVideoPlayer player)
        {
            isVideoJumping = false;
            // set video title
            videoTitle.SetText(player.GetFileName());
            // set total time
            totalTime.SetTime(player.length);
            // toggle play button
            playButton.Toggle();
            // toggle render button
            SwitchVideoRendererButton(player.renderMode);
            // toggle volume button
            volumeButton.Toggle();
            // set volume bar
            volumeBar.SetProgress(vrVideoPlayer.GetAudioVolume(0));
        }

        private void OnLoopPointReached(VRVideoPlayer player)
        {
            // toggle play button
            playButton.Toggle();
        }

        #endregion

        #region Video Player Ctrl

        public string GetVideo()
        {
            if (playlist.Count == 0 || videoIndex >= playlist.Count)
                return null;

            string nextVideo = playlist[videoIndex];

            return nextVideo;
        }

        public void RestartVideo()
        {
            if (isVideoPlaying)
            {
                vrVideoPlayer.Stop();
            }

            string videoUrl = GetVideo();
            vrVideoPlayer.Load(videoUrl, true);
        }

        public void NextVideo()
        {
            if (playlist.Count == 0 || videoIndex >= playlist.Count)
                return;

            videoIndex += 1;
            videoIndex %= playlist.Count;
        }

        public void PrevVideo()
        {
            if (playlist.Count == 0 || videoIndex < 0)
                return;

            videoIndex -= 1;
            if (videoIndex < 0)
                videoIndex = playlist.Count - 1;
        }

        public void PlayNextVideo()
        {
            // move to next video index
            NextVideo();
            if (fade)
            {
                StartCoroutine(fade.StartFadeOut());
            }
            else
            {
                if (isVideoPlaying)
                    vrVideoPlayer.Stop();
                RestartVideo();
            }
        }

        public void PlayPrevVideo()
        {
            // move to prev video index
            PrevVideo();
            if (fade)
            {
                StartCoroutine(fade.StartFadeOut());
            }
            else
            {
                if (isVideoPlaying)
                    vrVideoPlayer.Stop();
                RestartVideo();
            }
        }

        public void PauseVideo()
        {
            print("PauseVideo");
            vrVideoPlayer.Pause();
            playButton.Toggle();
#if UNITY_EDITOR
            print("请求暂停蓝牙设备");
#else
       jo.Call("sendBleStop", "请求暂停蓝牙设备");
#endif
        }

        public void PlayVideo()
        {
            vrVideoPlayer.Play();
            playButton.Toggle();
        }

        public void ReplayVideo()
        {
            vrVideoPlayer.Replay();
            playButton.Toggle();
        }

        public void ToggleVideoPlay()
        {
            if (isVideoPlaying)
            {
                PauseVideo();
            }
            else
            {
                PlayVideo();
            }
        }

        public void FastForward(double step = 5)
        {
            videoTime += step;
        }

        public void Rewind(double step = 5)
        {
            videoTime -= step;
        }

        public void ToggleAudioMute()
        {
            vrVideoPlayer.SetAudioMute(0, !isAudioMute);
            volumeButton.Toggle();
        }

        public void SetAudioVolume(float volume)
        {
            vrVideoPlayer.SetAudioVolume(0, volume);
            volumeBar.SetProgress(volume);
        }

        public void SetVideoRenderMode(RenderMode mode, ProjectionType proj = ProjectionType.NONE)
        {
            // TODO, support 360 equirectangular & cubemap
            if (mode == RenderMode._360)
            {
                proj = ProjectionType.EQUIRECT;
            }

            vrVideoPlayer.SetRenderMode(mode, proj);
            SwitchVideoRendererButton(mode);
        }

        public void SetVideoStereoMode(StereoMode mode)
        {
            vrVideoPlayer.SetStereoMode(mode);
            SwitchVideoStereoButton(mode);
        }

        #endregion

        #region Internal

        private void RestartPlayAndFadeIn()
        {
            StartCoroutine(RestartPlayAndDelayFadeIn());
        }

        private IEnumerator RestartPlayAndDelayFadeIn()
        {
            if (isVideoPlaying)
                vrVideoPlayer.Stop();
            RestartVideo();
            yield return new WaitForSeconds(0.3f);
            if (fade)
                StartCoroutine(fade.StartFadeIn());
        }

        private void SwitchVideoRendererButton(RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.NORMAL:
                    normalButton.SetEnable();
                    _180Button.SetDisable();
                    _360Button.SetDisable();
                    break;
                case RenderMode._180:
                    normalButton.SetDisable();
                    _180Button.SetEnable();
                    _360Button.SetDisable();
                    break;
                case RenderMode._360:
                    normalButton.SetDisable();
                    _180Button.SetDisable();
                    _360Button.SetEnable();
                    break;
            }
        }

        private void SwitchVideoStereoButton(StereoMode mode)
        {
            switch (mode)
            {
                case StereoMode.NONE:
                    monoButton.SetEnable();
                    leftRightButton.SetDisable();
                    topBottomButton.SetDisable();
                    break;
                case StereoMode.LEFT_RIGHT:
                    monoButton.SetDisable();
                    leftRightButton.SetEnable();
                    topBottomButton.SetDisable();
                    break;
                case StereoMode.TOP_BOTTOM:
                    monoButton.SetDisable();
                    leftRightButton.SetDisable();
                    topBottomButton.SetEnable();
                    break;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            vrVideoPlayer = GetComponent<VRVideoPlayer>();

            if (videoTitle == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "VideoTitle not attached!");
            }

            if (playButton == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "PlayButton not attached!");
            }

            if (currentTime == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "CurrentTime not attached!");
            }

            if (totalTime == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "TotalTime not attached!");
            }

            if (volumeButton == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "VolumeButton not attached!");
            }

            if (volumeBar == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "VolumeBar not attached!");
            }

            if (normalButton == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "NormalButton not attached!");
            }

            if (_180Button == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "180Button not attached!");
            }

            if (_360Button == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "360Button not attached!");
            }

            if (monoButton == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "MonoButton not attached!");
            }

            if (leftRightButton == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "LeftRightButton not attached!");
            }

            if (topBottomButton == null)
            {
                Debug.LogErrorFormat(LOG_FORMAT, "TopBottomButton not attached!");
            }
        }

        private void Start()
        {
            print("Start");
            Loom.Initialize();
#if UNITY_EDITOR
            StartRecvDataTimer(true);
            startPlay("Warcraft_360.mp4");
#else
        jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        jo = jc.GetStatic<AndroidJavaObject>("currentActivity");
        StartRecvDataTimer(true);
        // 由安卓那边调用Play(MovieName)，因为那边要先做一些准备处理，比如解析埋点数据;
        //startPlay("RollerCoaster_180.mp4");
#endif
        }

        private void Update()
        {
            if (isVideoPlaying)
            {
                currentTime.SetTime(vrVideoPlayer.time);
                progressBar.SetProgress((float) (vrVideoPlayer.time / vrVideoPlayer.length));
            }
        }

        private void OnEnable()
        {
            vrVideoPlayer.started += OnStarted;
            vrVideoPlayer.loopPointReached += OnLoopPointReached;
            if (fade)
                fade.fadeOutCompleted += RestartPlayAndFadeIn;
        }

        private void OnDisable()
        {
            vrVideoPlayer.started -= OnStarted;
            vrVideoPlayer.loopPointReached -= OnLoopPointReached;
            if (fade)
                fade.fadeOutCompleted -= RestartPlayAndFadeIn;
        }

        #endregion

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
        
        //给安卓调用，用于开始播放视频，
        void startPlay(string pathOrUrl)
        {
            if (vrVideoPlayer.autoPlay)
            {
                MyRestartVideo(pathOrUrl);
            }
        }
        
        public void MyRestartVideo(string pathOrUrl)
        {
            if (isVideoPlaying)
            {
                vrVideoPlayer.Stop();
            }

            vrVideoPlayer.Load(pathOrUrl, true);
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
            print("ShowRecv");
            int i = Thread.CurrentThread.ManagedThreadId;
            print(i);
            //异步在多线程下运行
            //回到unity线程继续运行
            //耗时操作执行完毕后，就可以用Loom的方法在Unity主线程中调用Text组件
            Loom.QueueOnMainThread((param) =>
            {
                int i2 = Thread.CurrentThread.ManagedThreadId;
                print(i2);
#if UNITY_EDITOR
                print("当前播放进度:" + GetTime().ToString());
#else
        print("当前播放进度:"+GetTime());
        //调用安卓那边触发埋点的方法，并把当前播放进度传过去
        // jo.Call("showToast", "你好哇");
        jo.Call("sendBle", GetTime().ToString());
#endif
            }, null);
        }

        //获取当前播放进度，并转换为毫秒，这样的转换其实不够准确，但是能精确到100毫秒即可
        public long GetTime()
        {
            return (long) (vrVideoPlayer.time * 1000.0);
        }

        #endregion
    }
}