﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using MediaPortal.GUI.Library;
using MediaPortal.Player;
using System.Drawing;
using MediaPortal.Profile;
using System.Runtime.InteropServices;
using MediaPortal.ProcessPlugins.Auto3D.Devices;
using MediaPortal.Dialogs;
using MediaPortal.Configuration;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Win32;
using MediaPortal.ProcessPlugins.Auto3D.UPnP;
 
namespace MediaPortal.ProcessPlugins.Auto3D
{
  [PluginIcons("MediaPortal.ProcessPlugins.Auto3D.Auto3d-Icon2.png", "MediaPortal.ProcessPlugins.Auto3D.Auto3d-Icon2Disabled.png")]
  public class Auto3D : ISetupForm, IPlugin
  {
    enum eSubTitle { None, TextBased, ImageBased };

    [Flags]
    private enum MatchingVideoFormat
    {
        Simple2D = 0,
        SydeBySide3D = 1, 
        TopBottom3D = 2, 
        Convert2DTo3D = 4,
        Reverse = 8,
        SydeBySide3DReverse = SydeBySide3D | Reverse,
        TopBottom3DReverse = TopBottom3D | Reverse
    }

    private volatile bool _run;
    private volatile bool _bPlaying;

    VideoFormat _currentMode = VideoFormat.Fmt2D; // default
    VideoFormat _nameFormat = VideoFormat.Fmt3DSBS; // default

    static List<IAuto3D> _listDevices = new List<IAuto3D>();
    IAuto3D _activeDevice = null;

    String _currentName = "";

    bool b3DMenuAlways = false;
    bool b3DMenuOnKey = false;
    bool bCheckNameSimple = false;
    bool bCheckNameFull = false;
    bool bCheckSideBySide = false;
    bool bCheckTopAndBottom = false;
    bool bAnalyzeNetworkStream;
    bool bTV = false;
    bool bVideo = false;

    bool bMenuHotKeyShift = false;
    bool bMenuHotKeyCtrl = true;
    bool bMenuHotKeyAlt = false;
    Keys _menuHotKey = Keys.D;

    bool bMenuMCERemote = false;
    String mceRemoteKey;

    bool bSuppressSwitchBackTo2D = false;
    bool bConvert3DTo2D = false;

    bool bTurnDeviceOff = false;
	int  nTurnDeviceOffVia = 0;
    int  nTurnDeviceOffWhen = 0;

	bool bTurnDeviceOn = false;
	int  nTurnDeviceOnVia = 0;
	int  nTurnDeviceOnWhen = 0;
	
    bool bConvert2Dto3DEnabled = false;

    Thread _workerThread = null;

    GUIDialogMenu _dlgMenu = null;

    List<String> _keywordsSBS = new List<string>();
    List<String> _keywordsSBSR = new List<string>();
    List<String> _keywordsTAB = new List<string>();
    List<String> _keywordsTABR = new List<string>();

    eSubTitle subTitleType = eSubTitle.None;
    bool bStretchSubtitles = false;

	public Auto3D()
    {
      Auto3DUPnP.Init();

	  // add new instances of all existing devices here...

      _listDevices.Add(new NoDevice());

      String fullPath = System.Reflection.Assembly.GetAssembly(typeof(Auto3D)).Location;
      String directoryName = Path.GetDirectoryName(fullPath);
      directoryName = Path.Combine(directoryName, "Auto3D");

      String[] files = Directory.GetFiles(directoryName, "Auto3D-*.dll");

      foreach (String file in files)
      {
        if (!file.Contains("Auto3D-BaseDevice.dll"))
        {
          Assembly asm = Assembly.LoadFrom(file); // pre-load assembly...

          IAuto3D[] results = (from type in asm.GetTypes()
                               where typeof(IAuto3D).IsAssignableFrom(type)
                               select (IAuto3D)Activator.CreateInstance(type)).ToArray();

          foreach (IAuto3D result in results)
          {
            _listDevices.Add(result);

            // if this is an UPnP device we register for callbacks

            if (result is IAuto3DUPnPServiceCallBack)
            {
              Auto3DUPnP.RegisterForCallbacks((IAuto3DUPnPServiceCallBack)result);
            }
          }
        }
      }     
    }

    // Returns the name of the plugin which is shown in the plugin menu

    public string PluginName()
    {
      return "Auto3D";
    }

    // Returns the description of the plugin is shown in the plugin menu

    public string Description()
    {
      return "Recognize a 3D movie and switch the tv into 3D accordingly";
    }

    // Returns the author of the plugin which is shown in the plugin menu

    public string Author()
    {
      return "Marcus Venturi";
    }

    // show the setup dialog

    public void ShowPlugin()
    {
      Form setup = new Auto3DSetup(_listDevices);
      setup.Show();
    }

    // Indicates whether plugin can be enabled/disabled

    public bool CanEnable()
    {
      return true;
    }

    // Get Windows-ID

    public int GetWindowId()
    {
      return -1; // it's a process plugin
    }

    // Indicates if plugin is enabled by default;

    public bool DefaultEnabled()
    {
      return true;
    }

    // indicates if a plugin has it's own setup screen

    public bool HasSetup()
    {
      return true;
    }

    /// <summary>
    /// If the plugin should have it's own button on the main menu of MediaPortal then it
    /// should return true to this method, otherwise if it should not be on home
    /// it should return false
    /// </summary>
    /// <param name="strButtonText">text the button should have</param>
    /// <param name="strButtonImage">image for the button, or empty for default</param>
    /// <param name="strButtonImageFocus">image for the button, or empty for default</param>
    /// <param name="strPictureImage">subpicture for the button or empty for none</param>
    /// <returns>true : plugin needs it's own button on home
    /// false : plugin does not need it's own button on home</returns>

    public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
    {
      strButtonText = null;
      strButtonImage = null;
      strButtonImageFocus = null;
      strPictureImage = null;
      return false;
    }

    public void Start()
    {
      _run = true;

      g_Player.PlayBackEnded += OnVideoEnded;
      g_Player.PlayBackStopped += OnVideoStopped;
      g_Player.PlayBackStarted += OnVideoStarted;
      g_Player.TVChannelChanged += OnTVChannelChanged;

      using (Settings reader = new MPSettings())
      {
        b3DMenuAlways = reader.GetValueAsBool("Auto3DPlugin", "3DMenuAlways", false);
        b3DMenuOnKey = reader.GetValueAsBool("Auto3DPlugin", "3DMenuOnKey", false);
        String menuHotKey = reader.GetValueAsString("Auto3DPlugin", "3DMenuKey", "CTRL + D");

        if (menuHotKey.StartsWith("MCE")) // reject old configs
          menuHotKey = "";

        if (menuHotKey.StartsWith("HID"))
        {
          bMenuMCERemote = true;
          mceRemoteKey = menuHotKey;

          //HIDInput.getInstance().HidEvent += Auto3DSetup_HidEvent;		
        }
        else
        {
          bMenuHotKeyShift = menuHotKey.Contains("SHIFT");
          bMenuHotKeyCtrl = menuHotKey.Contains("CTRL");
          bMenuHotKeyAlt = menuHotKey.Contains("ALT");

          if (menuHotKey.Contains("+"))
          {
            int pos = menuHotKey.LastIndexOf('+');
            menuHotKey = menuHotKey.Substring(pos + 1).Trim();
          }

          _menuHotKey = (Keys)Enum.Parse(typeof(Keys), menuHotKey, true);
        }

        bCheckNameSimple = reader.GetValueAsBool("Auto3DPlugin", "CheckNameSimple", true);
        bCheckNameFull = reader.GetValueAsBool("Auto3DPlugin", "CheckNameFull", true);

        bCheckSideBySide = reader.GetValueAsBool("Auto3DPlugin", "SideBySide", true);
        bCheckTopAndBottom = reader.GetValueAsBool("Auto3DPlugin", "TopAndBottom", false);
        bAnalyzeNetworkStream = reader.GetValueAsBool("Auto3DPlugin", "AnalyzeNetworkStream", true);

        String activeDeviceName = reader.GetValueAsString("Auto3DPlugin", "ActiveDevice", "");

        bTV = reader.GetValueAsBool("Auto3DPlugin", "TV", false);
        bVideo = reader.GetValueAsBool("Auto3DPlugin", "Video", true);

        if (reader.GetValueAsBool("Auto3DPlugin", "CheckNameFormatSBS", true))
          _nameFormat = VideoFormat.Fmt3DSBS;
        else
          _nameFormat = VideoFormat.Fmt3DTAB;

        foreach (IAuto3D device in _listDevices)
        {
          if (device.ToString() == activeDeviceName)
            _activeDevice = device;
        }

        if (_activeDevice == null)
          _activeDevice = _listDevices[0];

        Log.Info("Auto3D: Connecting to Device " + _activeDevice.ToString());

        _activeDevice.Start();

        if (_activeDevice is Auto3DUPnPBaseDevice)
            Auto3DUPnP.StartSSDP();

        if (b3DMenuOnKey)
        {
          Auto3DHelpers.GetMainForm().PreviewKeyDown += form_PreviewKeyDown;
        }

        GUIGraphicsContext.Render3DSubtitle = reader.GetValueAsBool("Auto3DPlugin", "3DSubtitles", true);
        GUIGraphicsContext.Render3DSubtitleDistance = -reader.GetValueAsInt("Auto3DPlugin", "SubtitleDepth", 0);
        
        bConvert2Dto3DEnabled = reader.GetValueAsBool("Auto3DPlugin", "ConvertTo3D", false);
        GUIGraphicsContext.Convert2Dto3DSkewFactor = reader.GetValueAsInt("Auto3DPlugin", "SkewFactor", 10);
        
        bStretchSubtitles = reader.GetValueAsBool("Auto3DPlugin", "StretchSubtitles", false);

        bSuppressSwitchBackTo2D = reader.GetValueAsBool("Auto3DPlugin", "SupressSwitchBackTo2D", false);
        bConvert3DTo2D = reader.GetValueAsBool("Auto3DPlugin", "Convert3DTo2D", false);
        
        SplitKeywords(ref _keywordsSBS, reader.GetValueAsString("Auto3DPlugin", "SwitchSBSLabels", "\"3DSBS\", \"3D SBS\""));
        SplitKeywords(ref _keywordsSBSR, reader.GetValueAsString("Auto3DPlugin", "SwitchSBSRLabels", "\"3DSBSR\", \"3D SBS R\""));
        SplitKeywords(ref _keywordsTAB, reader.GetValueAsString("Auto3DPlugin", "SwitchTABLabels", "\"3DTAB\", \"3D TAB\""));
        SplitKeywords(ref _keywordsTABR, reader.GetValueAsString("Auto3DPlugin", "SwitchTABRLabels", "\"3DTABR\", \"3D TAB R\""));

        bTurnDeviceOff = reader.GetValueAsBool("Auto3DPlugin", "TurnDeviceOff", false);
		nTurnDeviceOffVia = reader.GetValueAsInt("Auto3DPlugin", "TurnDeviceOffVia", 0);
        nTurnDeviceOffWhen = reader.GetValueAsInt("Auto3DPlugin", "TurnDeviceOffWhen", 0);

		bTurnDeviceOn = reader.GetValueAsBool("Auto3DPlugin", "TurnDeviceOn", false);
		nTurnDeviceOnVia = reader.GetValueAsInt("Auto3DPlugin", "TurnDeviceOnVia", 0);
        nTurnDeviceOnWhen = reader.GetValueAsInt("Auto3DPlugin", "TurnDeviceOnWhen", 0);
      }

      SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;      
      SystemEvents.SessionEnding += SystemEvents_SessionEnding;	  
      GUIGraphicsContext.OnNewAction += GUIGraphicsContext_OnNewAction;

	  if (bTurnDeviceOff && (nTurnDeviceOnWhen == 0 || nTurnDeviceOnWhen == 2) && _activeDevice.GetTurnOffInterfaces() != DeviceInterface.None)
		  _activeDevice.TurnOn((DeviceInterface)nTurnDeviceOnVia);
    }

    // system was shut down through MediaPortal GUI

    void GUIGraphicsContext_OnNewAction(GUI.Library.Action action)
    {
        if (action.wID == GUI.Library.Action.ActionType.ACTION_SHUTDOWN && 
            GUIGraphicsContext.CurrentState == GUIGraphicsContext.State.STOPPING)
        {
            Log.Debug("Auto3D: MediaPortal ShutDown");

            SystemShutDown();
        }
    }

    // system was shut down through Windows GUI

    void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
    {
        Log.Debug("Auto3D: SessionEnding");

        if (e.Reason == SessionEndReasons.SystemShutdown)
        {
            SystemShutDown();
        }
    }

    private void SystemShutDown()
    {
        Log.Debug("Auto3D: SystemShutDown");

        if (bTurnDeviceOff && (nTurnDeviceOffWhen == 1 || nTurnDeviceOffWhen == 2) && _activeDevice.GetTurnOffInterfaces() != DeviceInterface.None)
        {
            Log.Debug("Auto3D: Turn TV off");

			_activeDevice.TurnOff((DeviceInterface)nTurnDeviceOffVia);
        }
    }

    void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        Log.Debug("Auto3D: PowerModeChanged");

        switch (e.Mode)
        {
            case PowerModes.Suspend:

                if (bTurnDeviceOff && (nTurnDeviceOffWhen == 0 || nTurnDeviceOffWhen == 2) && _activeDevice.GetTurnOffInterfaces() != DeviceInterface.None)
                {
                    Log.Debug("Auto3D: Trying to turn TV off");
                    _activeDevice.TurnOff((DeviceInterface)nTurnDeviceOffVia);
                }

                _activeDevice.Suspend();
                break;

            case PowerModes.Resume:

				_activeDevice.Resume();

				if (bTurnDeviceOn && (nTurnDeviceOnWhen == 1 || nTurnDeviceOnWhen == 2) && _activeDevice.GetTurnOnInterfaces() != DeviceInterface.None)
				{
					Log.Debug("Auto3D: Trying to turn TV on");
					_activeDevice.TurnOn((DeviceInterface)nTurnDeviceOnVia);
				}

				// after resume we are always in 2D mode because normally the TV has 2D configuration after turning it on...
				GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.None;
				_currentMode = VideoFormat.Fmt2D;
				break;
        }
    }

    void SplitKeywords(ref List<String> list, String keywords)
    {
      String[] split = keywords.Split(',');

      foreach (String keyword in split)
      {
          list.Add(keyword.Trim("\" ".ToCharArray()));
      }
    }

    void form_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
      if (bMenuMCERemote) // ignore keyboard commands
        return;

      if (bMenuHotKeyCtrl == ((Control.ModifierKeys & Keys.Control) == Keys.Control) &&
          bMenuHotKeyShift == ((Control.ModifierKeys & Keys.Shift) == Keys.Shift) &&
          bMenuHotKeyAlt == ((Control.ModifierKeys & Keys.Alt) == Keys.Alt))
      {
        if (_dlgMenu == null && e.KeyValue == (int)_menuHotKey)
        {
          Log.Info("Auto3D: Manual Mode via Hotkey");
          ManualSelect3DFormat(VideoFormat.Fmt2D);
          UpdateSubtitleRenderFormat();
        }
      }
    }

    private bool Auto3DSetup_HidEvent(object aSender, String key)
    {
      if (key == mceRemoteKey)
      {
        if (_dlgMenu == null)
          ManualSelectThread();
        else
        {
          _dlgMenu.PageDestroy();
        }

        return true;
      }

      return false;
    }

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int smIndex);

    public void Stop()
    {
      // sometimes stop is called before SystemEvents_SessionEnding
      // in this case we shut down devices here, if necessary (before connection is closed)

      bool bShutDownPending = GetSystemMetrics(0x2000) != 0;

      Log.Debug("Auto3D: Stop - ShutDownPending = " + bShutDownPending);
      Log.Debug("Auto3D: Stop - MePoPowerOff = " + GUIGraphicsContext.StoppingToPowerOff);

      if (bShutDownPending || GUIGraphicsContext.StoppingToPowerOff)
          SystemShutDown();

      // stop UPnP
      Auto3DUPnP.StopSSDP();

      _run = false;
      _activeDevice.Stop();

      if (bMenuMCERemote)
      {
        //HIDInput.getInstance().HidEvent -= Auto3DSetup_HidEvent;
      }

      if (!bSuppressSwitchBackTo2D)
        GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.None;

      g_Player.PlayBackEnded -= OnVideoEnded;
      g_Player.PlayBackStopped -= OnVideoStopped;
      g_Player.PlayBackStarted -= OnVideoStarted;

      SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }

    private void RunAnalyzeVideo()
    {
      Log.Info("Auto3D: Start Video Analysis");

      FrameGrabber fg = FrameGrabber.GetInstance();

      int maxAnalyzeSteps = 20;
      int treshold = 5;

      VideoFormat[] vf = new VideoFormat[maxAnalyzeSteps + 1];

      int iStep = 0;

      while (_run && _bPlaying)
      {
        // wait 200 ms

        for (int i = 0; i < 10; i++)
        {
          if (!_bPlaying) // if playing is stopped while we wait then return
            return;

          Thread.Sleep(20);
        }

        System.Drawing.Bitmap image = fg.GetCurrentImage();

        if (image != null)
        {
          Bitmap fastCompareImage = new Bitmap(96, 96);

          // set the resolutions the same to avoid cropping due to resolution differences
          fastCompareImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

          //use a graphics object to draw the resized image into the bitmap
          using (Graphics graphics = Graphics.FromImage(fastCompareImage))
          {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bicubic;
            graphics.DrawImage(image, 0, 0, fastCompareImage.Width, fastCompareImage.Height);
          }

          // Lock the bitmap's bits.
          Rectangle rect = new Rectangle(0, 0, fastCompareImage.Width, fastCompareImage.Height);
          System.Drawing.Imaging.BitmapData bmpData = fastCompareImage.LockBits(rect,
            System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

          double similarity = 0;

          vf[iStep] = VideoFormat.Fmt2D; // assume normal format

          if (bCheckSideBySide)
            similarity = Auto3DAnalyzer.CheckFor3DFormat(bmpData, bmpData.Width/2, bmpData.Height, true);

          if (similarity == -1) // not bright enough for analysis
            continue;

          if (similarity > 0.925)
            vf[iStep] = VideoFormat.Fmt3DSBS;
          else
          {
            if (bCheckTopAndBottom)
              similarity = Auto3DAnalyzer.CheckFor3DFormat(bmpData, bmpData.Width, bmpData.Height/2, false);

            if (similarity == -1) // not bright enough for analysis -> continue
              continue;

            if (similarity > 0.925)
              vf[iStep] = VideoFormat.Fmt3DTAB;
          }

          fastCompareImage.UnlockBits(bmpData);

          if (image != null)
          {
            image.Dispose();
            image = null;
          }

          Log.Debug("Similarity: " + similarity + " - " + vf[iStep].ToString());
        }
        else
        {
          // Wait for a valid frame
          iStep = 0;
        }

        if (iStep > 3)
        {
          // check if we can make a decision

          int countNormal = 0;
          int countSideBySide3D = 0;
          int countTopBottom3D = 0;

          for (int i = 0; i <= iStep; i++)
          {
            switch (vf[i])
            {
              case VideoFormat.Fmt2D:

                countNormal++;
                break;

              case VideoFormat.Fmt3DSBS:

                countSideBySide3D++;
                break;

              case VideoFormat.Fmt3DTAB:

                countTopBottom3D++;
                break;
            }
          }

          Log.Debug("Results(" + iStep + ") - Normal=" + countNormal + " - SBS3D=" + countSideBySide3D + " - TB3D=" + countTopBottom3D);

          if ((countSideBySide3D >= (countNormal + treshold)) || (countTopBottom3D >= (countNormal + treshold)) || (countSideBySide3D >= countNormal && iStep == maxAnalyzeSteps) || (countTopBottom3D >= countNormal && iStep == maxAnalyzeSteps))
          {
            VideoFormat videoFormat = countTopBottom3D > countSideBySide3D ? VideoFormat.Fmt3DTAB : VideoFormat.Fmt3DSBS;

            if ((videoFormat == VideoFormat.Fmt3DSBS) || (videoFormat == VideoFormat.Fmt3DTAB))
            {
              if (videoFormat == VideoFormat.Fmt3DTAB)
                Log.Info("Auto3D: Video Analysis Finished: Switch TV to TAB 3D");
              else
                Log.Info("Auto3D: Video Analysis Finished: Switch TV to SBS 3D");

              if (bConvert3DTo2D)
              {
                switch (videoFormat)
                {
                  case VideoFormat.Fmt3DSBS:

                    GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.SideBySideTo2D;
                    break;

                  case VideoFormat.Fmt3DTAB:

                    GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.TopAndBottomTo2D;
                    break;
                }

                _currentMode = videoFormat;
              }
              else
              {
                if (_activeDevice.SwitchFormat(_currentMode, videoFormat))
                {
                  switch (videoFormat)
                  {
                    case VideoFormat.Fmt3DSBS:

                      GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.SideBySide;
                      break;

                    case VideoFormat.Fmt3DTAB:

                      GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.TopAndBottom;
                      break;
                  }

                  _currentMode = videoFormat;
                }
              }

              UpdateSubtitleRenderFormat();
            }
            else
            {
              ManualSelect3DFormat(videoFormat);
              UpdateSubtitleRenderFormat();
            }

            return; // exit thread
          }
          else
            if ((_currentMode == VideoFormat.Fmt2D) && ((countNormal > countSideBySide3D + treshold) || (countNormal > countTopBottom3D + treshold)))
            {
              // current format is normal and video is normal too, we do not need to switch
              Log.Info("Auto3D: Format is 2D. No switch necessary");
              return; // exit thread
            }
            else
              if (_currentMode != VideoFormat.Fmt2D)
              {
                // current format 3d and video is 2d, so we must switch back to normal
                RunSwitchBack();
                return; // exit thread
              }
              else
                if (iStep > maxAnalyzeSteps)
                {
                  // we could not make a decision within the maximum allowed steps
                  Log.Info("Auto3D: Video Analysis failed!");
                  return; // exit thread
                }
        }

        iStep++;
      }
    }

   private void UpdateSubtitleRenderFormat()
    {
        if (bStretchSubtitles)
        {
            GUIGraphicsContext.StretchSubtitles = true;
        }
        else
        {
            GUIGraphicsContext.StretchSubtitles = subTitleType == eSubTitle.ImageBased ? true : false;
        }       
    }

    private void AnalyzeVideo()
    {
      _workerThread = new Thread(new ThreadStart(RunAnalyzeVideo));
      _workerThread.IsBackground = true;
      _workerThread.Name = "Auto3D analyze thread";
      _workerThread.Priority = ThreadPriority.AboveNormal;
      _workerThread.Start();
    }

    private void RunSwitchBack()
    {
      Log.Info("Auto3D: Switch TV back to Normal Mode");
      if (!bSuppressSwitchBackTo2D && (bConvert3DTo2D || _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt2D)))
      {
        _currentMode = VideoFormat.Fmt2D;
        GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.None;
        GUIGraphicsContext.Switch3DSides = false;        
      }
    }

    private void SwitchBack()
    {
      _workerThread = new Thread(new ThreadStart(RunSwitchBack));
      _workerThread.IsBackground = true;
      _workerThread.Name = "Auto3D switch back thread";
      _workerThread.Priority = ThreadPriority.AboveNormal;
      _workerThread.Start();
    }

    private void RunManualSwitch()
    {
      Log.Info("Auto3D: Manual Mode via Remote");
      ManualSelect3DFormat(VideoFormat.Fmt2D);
    }

    private void ManualSelectThread()
    {
      Thread thread = new Thread(new ThreadStart(RunManualSwitch));
      thread.IsBackground = true;
      thread.Name = "Auto3D manual select thread";
      thread.Priority = ThreadPriority.AboveNormal;
      thread.Start();
    }

    private void ProcessingVideoStop(g_Player.MediaType type)
    {
      lock (this)
      {
        _bPlaying = false;

        // wait for ending worker thread

        if (_workerThread != null && _workerThread.IsAlive)
          Thread.Sleep(20);

        // is 3d mode is active switch back to normal mode

        if (_currentMode != VideoFormat.Fmt2D)
          SwitchBack();
      }
    }

    private static VideoFormat ConvertMatchingFormatToVideoFormat(MatchingVideoFormat source)
    {
      switch (source)
      {
        case MatchingVideoFormat.Simple2D:
          return VideoFormat.Fmt2D;
        case MatchingVideoFormat.SydeBySide3D:
        case MatchingVideoFormat.SydeBySide3DReverse:
          return VideoFormat.Fmt3DSBS;
        case MatchingVideoFormat.TopBottom3D:
        case MatchingVideoFormat.TopBottom3DReverse:
          return VideoFormat.Fmt3DTAB;
        case MatchingVideoFormat.Convert2DTo3D:
          return VideoFormat.Fmt2D3D;
      }
      return VideoFormat.Fmt2D;
    }

    private void Analyze3DFormatVideo(g_Player.MediaType type)
    {
      lock (this)
      {
        if (type == g_Player.MediaType.TV)
        {
          Thread.Sleep(500); // wait 500ms to get a valid channel name

          String channel = GUIPropertyManager.GetProperty("#TV.View.channel");

          if (channel == _currentName)
            return;

          _currentName = channel;
        }

        _bPlaying = false;

        // wait for ending worker thread

        if (_workerThread != null && _workerThread.IsAlive)
          Thread.Sleep(20);

        // is 3d mode is active switch back to normal mode

        if (_currentMode != VideoFormat.Fmt2D)
          SwitchBack();

        if ((type == g_Player.MediaType.Video && bVideo) || (type == g_Player.MediaType.TV && bTV))
        {
          _bPlaying = true;

          if (!b3DMenuAlways)
          {
            Log.Info("Auto3D: Automatic Mode");

            if (bCheckNameFull)
            {
              var matchedKeywords = new Dictionary<string, MatchingVideoFormat>();
              foreach (var keyword in _keywordsSBSR)
              {
                Log.Debug("Auto3D: Check if name contains \"" + keyword + "\"");

                if (_currentName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                  matchedKeywords.Add(keyword, MatchingVideoFormat.SydeBySide3DReverse);
                }
              }

              foreach (var keyword in _keywordsSBS)
              {
                Log.Debug("Auto3D: Check if name contains \"" + keyword + "\"");

                if (_currentName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                  matchedKeywords.Add(keyword, MatchingVideoFormat.SydeBySide3D);
                }
              }

              foreach (var keyword in _keywordsTABR)
              {
                Log.Debug("Auto3D: Check if name contains \"" + keyword + "\"");

                if (_currentName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                  matchedKeywords.Add(keyword, MatchingVideoFormat.TopBottom3DReverse);
                }
              }

              foreach (var keyword in _keywordsTAB)
              {
                Log.Debug("Auto3D: Check if name contains \"" + keyword + "\"");

                if (_currentName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                  matchedKeywords.Add(keyword, MatchingVideoFormat.TopBottom3D);
                }
              }

              if (matchedKeywords.Any())
              {
                Log.Info("Auto3D: Name contains \"{0}\"", string.Join("\", \"", matchedKeywords.Keys));

                var keyword = matchedKeywords.Keys.OrderByDescending(x => x).FirstOrDefault();
                var detectedFormat = MatchingVideoFormat.Simple2D;
                if (!string.IsNullOrEmpty(keyword))
                {
                    if (!matchedKeywords.TryGetValue(keyword, out detectedFormat))
                    {
                        Log.Info("Auto3D: not matched key for keyword \"{0}\" and 3D format is going to default {1}", keyword, detectedFormat);
                        detectedFormat = MatchingVideoFormat.Simple2D;
                    }
                    else
                    {
                        Log.Info("Auto3D: most matched is \"{0}\" and 3D format is {1}", keyword, detectedFormat);
                    }
                }
                else
                {
                    Log.Info("Auto3D: key is empty and 3D format is going to default {0}", detectedFormat);
                }

                var format = ConvertMatchingFormatToVideoFormat(detectedFormat);
                if (_activeDevice.SwitchFormat(_currentMode, format))
                {
                  GUIGraphicsContext.Render3DMode = format == VideoFormat.Fmt3DSBS ? GUIGraphicsContext.eRender3DMode.SideBySide : GUIGraphicsContext.eRender3DMode.TopAndBottom;
                  GUIGraphicsContext.Switch3DSides = detectedFormat.HasFlag(MatchingVideoFormat.Reverse);
                  _currentMode = format;
                  UpdateSubtitleRenderFormat();
                  return;
                }
              }
            }

            if (_currentMode == VideoFormat.Fmt2D && bCheckNameSimple)
            {
              if (_currentName.ToUpper().Contains("3D"))
              {
                Log.Info("Auto3D: Name contains \"3D\"");

                if (_activeDevice.SwitchFormat(_currentMode, _nameFormat))
                {
                  switch (_nameFormat)
                  {
                    case VideoFormat.Fmt3DSBS:

                      GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.SideBySide;
                      break;

                    case VideoFormat.Fmt3DTAB:

                      GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.TopAndBottom;
                      break;
                  }

                  _currentMode = _nameFormat;
                  UpdateSubtitleRenderFormat();
                }

                return;
              }
            }
          }
          else // b3DMenuAlways
          {
            Log.Info("Auto3D: Manual Mode");

            ManualSelect3DFormat(VideoFormat.Fmt2D);
            UpdateSubtitleRenderFormat();
            return;
          }

          if ((bCheckSideBySide || bCheckTopAndBottom) /* && type == g_Player.MediaType.Video*/)
            AnalyzeVideo();         
        }
      }
    }

    public void ManualSelect3DFormat(VideoFormat preSelected)
    {
      _dlgMenu = (GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);

      if (_dlgMenu != null)
      {
        _dlgMenu.Reset();
        _dlgMenu.SetHeading("Select 2D/3D Format for TV");

        if (preSelected == VideoFormat.Fmt2D)
          _dlgMenu.Add("2D");

        if (preSelected == VideoFormat.Fmt2D || preSelected == VideoFormat.Fmt3DSBS)
        {
          _dlgMenu.Add("3D Side by Side");
          _dlgMenu.Add("3D SBS -> 2D via MediaPortal");
        }

        if (preSelected == VideoFormat.Fmt2D || preSelected == VideoFormat.Fmt3DTAB)
        {
          _dlgMenu.Add("3D Top and Bottom");
          _dlgMenu.Add("3D TAB -> 2D via MediaPortal");
        }
        
        if (bConvert2Dto3DEnabled && preSelected == VideoFormat.Fmt2D)
        {
            _dlgMenu.Add("2D -> 3D SBS via MediaPortal");
        }

        if (_currentMode == VideoFormat.Fmt3DSBS || _currentMode == VideoFormat.Fmt3DTAB)
        {
          if (!GUIGraphicsContext.Switch3DSides)
            _dlgMenu.Add("3D Reverse Mode");
          else
            _dlgMenu.Add("3D Normal Mode");          
        }

        if (_activeDevice.IsDefined(VideoFormat.Fmt2D3D))
          _dlgMenu.Add("2D -> 3D via TV");

        _dlgMenu.DoModal((int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO);

        Log.Info("Auto3D: Manually selected " + _dlgMenu.SelectedLabelText);

        switch (_dlgMenu.SelectedLabelText)
        {
          case "2D":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt2D);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.None;
            _currentMode = VideoFormat.Fmt2D;            
            break;

          case "3D Side by Side":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt3DSBS);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.SideBySide;
            _currentMode = VideoFormat.Fmt3DSBS;
            break;

          case "3D Top and Bottom":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt3DTAB);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.TopAndBottom;
            _currentMode = VideoFormat.Fmt3DTAB;
            break;

          case "2D -> 3D via TV":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt2D3D);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.None;
            _currentMode = VideoFormat.Fmt2D3D;
            break;

          case "3D SBS -> 2D via MediaPortal":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt2D);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.SideBySideTo2D;
            _currentMode = VideoFormat.Fmt2D;
            break;

          case "3D TAB -> 2D via MediaPortal":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt2D);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.TopAndBottomTo2D;
            _currentMode = VideoFormat.Fmt2D;
            break;

          case "3D Reverse Mode":

            GUIGraphicsContext.Switch3DSides = true;
            break;

          case "3D Normal Mode":

            GUIGraphicsContext.Switch3DSides = false;
            break;
          
          case "2D -> 3D SBS via MediaPortal":

            _activeDevice.SwitchFormat(_currentMode, VideoFormat.Fmt3DSBS);
            GUIGraphicsContext.Render3DMode = GUIGraphicsContext.eRender3DMode.SideBySideFrom2D;
            _currentMode = VideoFormat.Fmt3DSBS;
            break;
        }

        _dlgMenu = null;
      }
    }

    public static bool IsNetworkVideo(string strPath)
    {
      if (string.IsNullOrEmpty(strPath)) return false;
      return strPath.StartsWith("rtsp:", StringComparison.OrdinalIgnoreCase) ||
        (strPath.StartsWith("mms:", StringComparison.OrdinalIgnoreCase) && strPath.EndsWith(".ymvp", StringComparison.OrdinalIgnoreCase)) ||
        strPath.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
        strPath.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
        strPath.StartsWith("udp:", StringComparison.OrdinalIgnoreCase) ||
        strPath.StartsWith("rtmp:", StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Handles the g_Player.PlayBackEnded event
    /// </summary>
    /// <param name="type"></param>
    /// <param name="s"></param>
    public void OnVideoEnded(g_Player.MediaType type, string s)
    {
      // do not handle e.g. visualization window, last.fm player, etc
      if (type == g_Player.MediaType.Video || type == g_Player.MediaType.TV)
      {
        subTitleType = eSubTitle.None;
        Task.Factory.StartNew(() => ProcessingVideoStop(type));
      }
    }

    /// <summary>
    /// Handles the g_Player.PlayBackStopped event
    /// </summary>
    /// <param name="type"></param>
    /// <param name="i"></param>
    /// <param name="s"></param>
    public void OnVideoStopped(g_Player.MediaType type, int i, string s)
    {
      // do not handle e.g. visualization window, last.fm player, etc
      if (type == g_Player.MediaType.Video || type == g_Player.MediaType.TV)
      {
        subTitleType = eSubTitle.None;
        Task.Factory.StartNew(() => ProcessingVideoStop(type));
      }
    }

    /// <summary>
    /// Handles the g_Player.PlayBackStarted event
    /// </summary>
    public void OnVideoStarted(g_Player.MediaType type, string s)
    {
      // do not handle e.g. visualization window, last.fm player, etc
      if (type == g_Player.MediaType.Video || type == g_Player.MediaType.TV)
      {
        _currentName = s;
        var isNetwork = IsNetworkVideo(s);
        subTitleType = isNetwork ? eSubTitle.None : DetectSubtitleType(s);
        if (!isNetwork || bAnalyzeNetworkStream)
        {
          Task.Factory.StartNew(() => Analyze3DFormatVideo(type));
        }
      }
    }

    private static eSubTitle DetectSubtitleType(string s)
    {
      var baseName = Path.Combine(Path.GetDirectoryName(s), Path.GetFileNameWithoutExtension(s));

      // text based subtitles

      string[] textSubtitleFormatsFileAndEmbedded = 
        {
            "aqt",
            "srt",
            "ssa",
            "mpl",
            "txt",
            "dks",
            "js",
            "jss",
            "pjs",
            "asc",
            "ass",
            "smi",
            "psb",
            "lrc",
            "ovr",
            "rt",
            "rtf",
            "zeg",
            "sbt",
            "sst",
            "ssts",
            "stl",
            "vkt",
            "vsf",
            "pan",
            "s2k"
        };

      string[] imageSubtitleFormatsFile = 
        {        
            "idx",
            "sub",
            "scr",
            "son"
        };

      string[] imageSubtitleFormatsEmbedded = 
        {        
            "vobsub",
            "dvb subtitle",
            "pgs",
            "rle",
            "xsub",
        };

      // check for file based subtitles

      if (textSubtitleFormatsFileAndEmbedded.Any(subFormat => File.Exists(baseName + "." + subFormat)))
      {
        return eSubTitle.TextBased;
      }

      if (imageSubtitleFormatsFile.Any(subFormat => File.Exists(baseName + "." + subFormat)))
      {
        return eSubTitle.ImageBased;
      }

      // check for embedded subtitles

      var result = eSubTitle.None;
      var mi = new MediaInfo();

      mi.Open(s);

      var sct = mi.Count_Get(StreamKind.Text);

      for (var i = 0; i < sct; ++i)
      {
        var format = mi.Get(StreamKind.Text, i, "Format").ToLowerInvariant();

        if (textSubtitleFormatsFileAndEmbedded.Contains(format))
        {
          result = eSubTitle.TextBased;
          break;
        }

        if (imageSubtitleFormatsEmbedded.Contains(format))
        {
          result = eSubTitle.ImageBased;
          break;
        }
      }
 
      mi.Close();
      return result;
    }

    void OnTVChannelChanged()
    {
      g_Player.MediaType type = g_Player.MediaType.TV;
      subTitleType = eSubTitle.None;
      Task.Factory.StartNew(() => Analyze3DFormatVideo(type));
    }
  }
}
