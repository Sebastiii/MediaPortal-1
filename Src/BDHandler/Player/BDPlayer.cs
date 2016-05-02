using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using BDInfo;
using DirectShowLib;
using DShowNET.Helper;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Player;
using MediaPortal.Player.Subtitles;
using MediaPortal.Plugins.BDHandler.Filters;
using MediaPortal.Profile;
using MediaPortal.Dialogs;
using MediaPortal.Util;
using MediaPortal.Player.PostProcessing;

namespace MediaPortal.Plugins.BDHandler.Player
{
    /// <summary>
    /// Special player class that handles blu-ray playback
    /// </summary>
    public class BDPlayer : VideoPlayer
    {

        /// <summary>
        /// The minimal feature length that should be taken into account
        /// </summary>
        public static double MinimalFullFeatureLength = 3000;
        
        /// <summary>
        /// Holds the relevant BDInfo instance after a scan
        /// </summary>
        protected BDInfo currentMediaInfo;

        /// <summary>
        /// Holds the relevant playlist after feature selection
        /// </summary>
        protected TSPlaylistFile currentPlaylistFile;

        /// <summary>
        /// Gets or sets the source filter that is to be forced when playing blurays
        /// </summary>
        /// <value>The source filter.</value>
        public IFilter SourceFilter
        {
            get { return this.sourceFilter; }
            set { this.sourceFilter = value; }
        } protected IFilter sourceFilter;

        /// <summary>
        /// Plays the specified file.
        /// </summary>
        /// <param name="strFile">filepath</param>
        /// <returns></returns>
        public override bool Play(string strFile)
        {
            string path = strFile.ToLowerInvariant();

            if (strFile.Length < 4)
            {
                path = Path.Combine(strFile, @"BDMV\index.bdmv");
                strFile = path;
            }

            if (path.EndsWith(".bdmv") || path.EndsWith(".m2ts"))
            {
                // only continue with playback if a feature was selected or the extension was m2ts.
                bool play = doFeatureSelection(ref strFile);
                if (play)
                {
                    return base.Play(strFile);
                }
            }

            // if we get here we always return true because the user called the dialog and we don't 
            // want an error saying we couldn't play the file
            return true;
        }

        /// <summary>
        /// Specifies if custom graph should be used.
        /// </summary>
        /// <returns></returns>
        protected override bool UseCustomGraph()
        {
            return (CurrentFile.ToLowerInvariant().EndsWith(".mpls"));
        }

        /// <summary>
        /// Renders a graph that can playback mpls files
        /// </summary>
        /// <returns></returns>
        protected override bool RenderCustomGraph()
        {
          try
          {
            bool vc1Codec = false;

            //Get filterCodecName
            filterCodec = GetFilterCodec();
            filterConfig = GetFilterConfiguration();

            graphBuilder = (IGraphBuilder)new FilterGraph();
            _rotEntry = new DsROTEntry((IFilterGraph)graphBuilder);
            List<string> filters = new List<string>();

            BDHandlerCore.LogDebug("Player is active.");

            //// Ask for resume for BD
            //GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAY_BD, 0, 0, 0, 0, 0, null);
            //msg.Param1 = g_Player.SetResumeBDTitleState;
            //GUIWindowManager.SendMessage(msg);

            GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_SWITCH_FULL_WINDOWED, 0, 0, 0, 1, 0, null);
            GUIWindowManager.SendMessage(msg);

            basicVideo = graphBuilder as IBasicVideo2;

            Vmr9 = new VMR9Util();
            Vmr9.AddVMR9(graphBuilder);
            Vmr9.Enable(false);

            // load the source filter
            _interfaceSourceFilter = DirectShowUtil.AddFilterToGraph(graphBuilder, this.sourceFilter.Name);

            // check if it's available
            if (_interfaceSourceFilter == null)
            {
              Error.SetError("Unable to load source filter", "Please register filter: " + this.sourceFilter.Name);
              BDHandlerCore.LogError("Unable to load DirectShowFilter: {0}", this.sourceFilter.Name);
              return false;
            }

            // load the file
            int result = ((IFileSourceFilter)_interfaceSourceFilter).Load(CurrentFile, null);
            if (result != 0) return false;

            IPin pinOut0, pinOut1;
            IPin pinIn0, pinIn1;
            pinOut0 = DsFindPin.ByDirection((IBaseFilter)_interfaceSourceFilter, PinDirection.Output, 0); //video
            pinOut1 = DsFindPin.ByDirection((IBaseFilter)_interfaceSourceFilter, PinDirection.Output, 1); //audio

            if (pinOut0 == null)
            {
              BDHandlerCore.LogInfo("FAILED: unable to get output pins of source splitter");
              Cleanup();
              return false;
            }

            if (pinOut0 != null)
            {
              //Detection if the Video Stream is VC-1 on output pin of the splitter
              IEnumMediaTypes enumMediaTypesVideo;
              int hr = pinOut0.EnumMediaTypes(out enumMediaTypesVideo);
              while (true)
              {
                AMMediaType[] mediaTypes = new AMMediaType[1];
                int typesFetched;
                hr = enumMediaTypesVideo.Next(1, mediaTypes, out typesFetched);
                if (hr != 0 || typesFetched == 0) break;
                if (mediaTypes[0].majorType == MediaType.Video && mediaTypes[0].subType == MediaSubType.VC1)
                {
                  BDHandlerCore.LogInfo("found VC-1 video out pin");
                  vc1Codec = true;
                }
              }
              DirectShowUtil.ReleaseComObject(enumMediaTypesVideo);
              enumMediaTypesVideo = null;
            }

            // add filters and audio renderer
            using (Settings settings = new Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
            {
              // Get the minimal settings required
              //bool useAutoDecoderSettings = settings.GetValueAsBool("movieplayer", "autodecodersettings", false);
              string filterAudioRenderer = settings.GetValueAsString("bdplayer", "audiorenderer", "Default DirectSound Device");

              // if "Auto Decoder Settings" is unchecked we add the filters specified in the codec configuration
              // otherwise the DirectShow merit system is used (except for renderer and source filter)
              //if (!useAutoDecoderSettings)
              {
                // Get the Video Codec configuration settings
                string filterVideoMpeg2 = settings.GetValueAsString("bdplayer", "mpeg2videocodec", "");
                string filterVideoH264 = settings.GetValueAsString("bdplayer", "h264videocodec", "");
                string filterAudioMpeg2 = settings.GetValueAsString("bdplayer", "mpeg2audiocodec", "");
                string filterVideoVC1 = settings.GetValueAsString("bdplayer", "vc1videocodec", "");

                //Add Post Process Video Codec
                PostProcessAddVideo();

                if (vc1Codec)
                {
                  if (!string.IsNullOrEmpty(filterVideoVC1))// && filterVideoMpeg2 != filterVideoVC1)
                  {
                    filterCodec.VideoCodec = DirectShowUtil.AddFilterToGraph(graphBuilder, filterVideoVC1);
                    BDHandlerCore.LogInfo("Load VC1 {0} in graph", filterVideoVC1);
                  }
                }
                else
                {
                  if (!string.IsNullOrEmpty(filterVideoH264))// && filterVideoMpeg2 != filterVideoH264)
                  {
                    filterCodec.VideoCodec = DirectShowUtil.AddFilterToGraph(graphBuilder, filterVideoH264);
                    BDHandlerCore.LogInfo("Load H264 {0} in graph", filterVideoH264);
                  }
                  else
                  {
                    filterCodec.VideoCodec = DirectShowUtil.AddFilterToGraph(graphBuilder, filterVideoMpeg2);
                  }
                }
                if (!string.IsNullOrEmpty(filterAudioMpeg2))
                {
                  filterCodec.AudioCodec = DirectShowUtil.AddFilterToGraph(graphBuilder, filterAudioMpeg2);
                  BDHandlerCore.LogInfo("Load AC3/DTS {0} in graph", filterAudioMpeg2);
                }

                if (filterAudioRenderer.Length > 0)
                {
                  filterCodec._audioRendererFilter = DirectShowUtil.AddAudioRendererToGraph(graphBuilder, filterAudioRenderer, false);
                }

                //Try to connect First Selected Video Filter
                pinIn0 = DsFindPin.ByDirection(filterCodec.VideoCodec, PinDirection.Input, 0); //video
                pinIn1 = DsFindPin.ByDirection(filterCodec.AudioCodec, PinDirection.Input, 0); //audio

                if (pinIn0 == null || pinIn1 == null)
                {
                  BDHandlerCore.LogInfo("FAILED: unable to get pins of video/audio codecs");
                }
                int hr = graphBuilder.Connect(pinOut0, pinIn0);
                if (hr != 0)
                {
                  DirectShowUtil.ReleaseComObject(filterCodec.VideoCodec); filterCodec.VideoCodec = null;
                  BDHandlerCore.LogInfo("FAILED: unable to connect video pins try next");
                }

                //Try to connect First Selected Audio Filter
                hr = graphBuilder.Connect(pinOut1, pinIn1);
                if (hr != 0)
                {
                  DirectShowUtil.ReleaseComObject(filterCodec.AudioCodec); filterCodec.AudioCodec = null;
                  BDHandlerCore.LogInfo("FAILED: unable to connect audio pins try next");
                }

                if (filterCodec.VideoCodec != null)
                DirectShowUtil.RenderUnconnectedOutputPins(graphBuilder, filterCodec.VideoCodec);
                if (filterCodec.AudioCodec != null)
                DirectShowUtil.RenderUnconnectedOutputPins(graphBuilder, filterCodec.AudioCodec);
                if (_interfaceSourceFilter != null)
                DirectShowUtil.RenderUnconnectedOutputPins(graphBuilder, _interfaceSourceFilter);
                DirectShowUtil.RemoveUnusedFiltersFromGraph(graphBuilder);
              }
            }

            //Remove Line21 if present
            disableCC();

            //Detection if it's the good audio renderer connected
            bool ResultPinAudioRenderer = false;
            IPin PinAudioRenderer = DsFindPin.ByDirection(filterCodec._audioRendererFilter, PinDirection.Input, 0); //audio
            DirectShowUtil.IsPinConnected(PinAudioRenderer, out ResultPinAudioRenderer);
            if (!ResultPinAudioRenderer)
            {
              this.graphBuilder.RemoveFilter(filterCodec._audioRendererFilter);
              DirectShowUtil.ReleaseComObject(filterCodec._audioRendererFilter);
              filterCodec._audioRendererFilter = null;
            }

            #region cleanup Sebastiii

            if (pinOut0 != null)
            {
              DirectShowUtil.ReleaseComObject(pinOut0);
              pinOut0 = null;
            }

            if (pinOut1 != null)
            {
              DirectShowUtil.ReleaseComObject(pinOut1);
              pinOut1 = null;
            }

            if (pinIn0 != null)
            {
              DirectShowUtil.ReleaseComObject(pinIn0);
              pinIn0 = null;
            }

            if (pinIn1 != null)
            {
              DirectShowUtil.ReleaseComObject(pinIn1);
              pinIn1 = null;
            }

            if (PinAudioRenderer != null)
            {
              DirectShowUtil.ReleaseComObject(PinAudioRenderer);
              PinAudioRenderer = null;
            }

            #endregion

            if (Vmr9 == null || !Vmr9.IsVMR9Connected)
            {
              BDHandlerCore.LogError("Failed to render file.");
              mediaCtrl = null;
              Cleanup();
              return false;
            }

            mediaCtrl = (IMediaControl)graphBuilder;
            mediaEvt = (IMediaEventEx)graphBuilder;
            mediaSeek = (IMediaSeeking)graphBuilder;
            mediaPos = (IMediaPosition)graphBuilder;
            basicAudio = (IBasicAudio)graphBuilder;
            basicVideo = (IBasicVideo2)graphBuilder;
            videoWin = (IVideoWindow)graphBuilder;
            m_iVideoWidth = Vmr9.VideoWidth;
            m_iVideoHeight = Vmr9.VideoHeight;
            Vmr9.SetDeinterlaceMode();

            return true;
          }
          catch (Exception e)
          {
            Error.SetError("Unable to play movie", "Unable build graph for VMR9");
            BDHandlerCore.LogError("Exception while creating DShow graph {0} {1}", e.Message, e.StackTrace);
            Cleanup();
            return false;
          }
        }

        /// <summary>
        /// Scans a bluray folder and returns a BDInfo object
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private BDInfo scanWorker(string path)
        {
            BDHandlerCore.LogInfo("Scanning bluray structure: {0}", path);
            BDInfo bluray = new BDInfo(path.ToUpper());
            bluray.Scan();
            return bluray;
        }

        /// <summary>
        /// Returns wether a choice was made and changes the file path
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if playback should continue, False if user cancelled.</returns>
        private bool doFeatureSelection(ref string filePath)
        {
            try
            {
                bool ChecklistToPlay = false;
                Func<string, BDInfo> scanner = scanWorker;
                IAsyncResult result = scanner.BeginInvoke(filePath, null, scanner);

                // Show the wait cursor during scan
                GUIWaitCursor.Init();
                GUIWaitCursor.Show();
                while (result.IsCompleted == false)
                {
                    GUIWindowManager.Process();
                    Thread.Sleep(100);
                }

                BDInfo bluray = scanner.EndInvoke(result);

                // Put the bluray info into a member variable (for later use)
                currentMediaInfo = bluray;

                List<TSPlaylistFile> allPlayLists = bluray.PlaylistFiles.Values.Where(p => p.IsValid).OrderByDescending(p => p.TotalLength).Distinct().ToList();

                List<TSPlaylistFile> allPlayListsFull = bluray.PlaylistFiles.Values.OrderBy(p => p.Name).Distinct().ToList();

                // this will be the title of the dialog, we strip the dialog of weird characters that might wreck the font engine.
                string heading = (bluray.Title != string.Empty) ? Regex.Replace(bluray.Title, @"[^\w\s\*\%\$\+\,\.\-\:\!\?\(\)]", "").Trim() : "Bluray: Select Feature";

                GUIWaitCursor.Hide();

                // Stop BDHandler if protected disk is detected
                if (bluray.scanfailed)
                {
                  GUIDialogOK pDlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
                  pDlgOK.SetHeading(heading);
                  pDlgOK.SetLine(1, string.Format("Unable to play protected disk or bad disk reading"));
                  pDlgOK.DoModal(GUIWindowManager.ActiveWindow);
                  return false;
                }

                // Feature selection logic 
                TSPlaylistFile listToPlay = null;
                try
                {
                  listToPlay = allPlayListsFull[g_Player.SetResumeBDTitleState - 1100];
                  ChecklistToPlay = true;
                }
                catch (Exception)
                {
                  // Handle index value outrange or negative, set higher value (2000) to be able to select menu if user cancel resume dialog
                  //g_Player.SetResumeBDTitleState = 2000;
                  ChecklistToPlay = false;
                }

                if (g_Player.ForcePlay && allPlayLists.Count != 1 && ChecklistToPlay)
                {
                  //try
                  //{
                    listToPlay = allPlayListsFull[g_Player.SetResumeBDTitleState - 1100];
                    BDHandlerCore.LogInfo("Force to play title, bypassing dialog.", allPlayLists.Count);
                  //}
                  //catch (Exception)
                  //{
                  //  // Handle index value outrange or negative, set higher value (2000) to be able to select menu if user cancel resume dialog
                  //  g_Player.SetResumeBDTitleState = 2000;
                  //}
                }
                else if (allPlayLists.Count == 0)
                {
                    BDHandlerCore.LogInfo("No playlists found, bypassing dialog.", allPlayLists.Count);
                    return true;
                }
                else if (allPlayLists.Count == 1)
                {
                  // if we have only one playlist to show just move on
                  BDHandlerCore.LogInfo("Found one valid playlist, bypassing dialog.", filePath);
                  listToPlay = allPlayLists[0];
                  // Only One title
                  g_Player.SetResumeBDTitleState = 900;
                }
                else
                {
                  // Show selection dialog
                  BDHandlerCore.LogInfo("Found {0} playlists, showing selection dialog.", allPlayLists.Count);

                  // first make an educated guess about what the real features are (more than one chapter, no loops and longer than one hour)
                  // todo: make a better filter on the playlists containing the real features
                  List<TSPlaylistFile> playLists =
                    allPlayLists.Where(
                      p => (p.Chapters.Count > 1 || p.TotalLength >= MinimalFullFeatureLength) && !p.HasLoops).ToList();

                  // if the filter yields zero results just list all playlists 
                  if (playLists.Count == 0)
                  {
                    playLists = allPlayLists;
                  }

                  IDialogbox dialog = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
                  int count;
                  while (true)
                  {
                    dialog.Reset();
                    dialog.SetHeading(heading);

                    count = 1;

                    for (int i = 0; i < playLists.Count; i++)
                    {
                      TSPlaylistFile playList = playLists[i];
                      TimeSpan lengthSpan = new TimeSpan((long)(playList.TotalLength * 10000000));
                      string length = string.Format("{0:D2}:{1:D2}:{2:D2}", lengthSpan.Hours, lengthSpan.Minutes,
                                                    lengthSpan.Seconds);
                      // todo: translation
                      string feature = string.Format("Feature #{0}, {2} Chapter{3} ({1})", count, length,
                                                     playList.Chapters.Count,
                                                     (playList.Chapters.Count > 1) ? "s" : string.Empty);
                      dialog.Add(feature);
                      count++;
                    }

                    if (allPlayLists.Count > playLists.Count)
                    {
                      // todo: translation
                      dialog.Add("List all features...");
                    }

                    dialog.DoModal(GUIWindowManager.ActiveWindow);

                    if (dialog.SelectedId == count)
                    {
                      // don't filter the playlists and continue to display the dialog again
                      playLists = allPlayLists;
                      continue;

                    }
                    else if (dialog.SelectedId < 1)
                    {
                      // user cancelled so we return
                      BDHandlerCore.LogDebug("User cancelled dialog.");
                      if (Util.Utils.IsISOImage(filePath))
                      {
                        if (!String.IsNullOrEmpty(Util.DaemonTools.GetVirtualDrive()) &&
                            g_Player.IsBDDirectory(Util.DaemonTools.GetVirtualDrive()) ||
                            g_Player.IsDvdDirectory(Util.DaemonTools.GetVirtualDrive()))
                          Util.DaemonTools.UnMount();
                      }
                      return false;
                    }

                    // end dialog
                    break;
                  }

                  listToPlay = playLists[dialog.SelectedId - 1];

                  // Parse BD Title count
                  int BDCount = 0;
                  foreach (TSPlaylistFile playlistFile in allPlayListsFull)
                  {
                    try
                    {
                      {
                        {
                          if (playlistFile.Name == listToPlay.Name)
                          {
                            //Set Resume in MP MyVideo DB higher than 1100
                            g_Player.SetResumeBDTitleState = (BDCount + 1100);
                            break;
                          }
                        }
                      }
                      BDCount++;
                    }
                    catch
                    {
                    }
                  }
                }

                // put the choosen playlist into our member variable for later use
                currentPlaylistFile = listToPlay;

                // load the chapters
                chapters = listToPlay.Chapters.ToArray();
                BDHandlerCore.LogDebug("Selected: Playlist={0}, Chapters={1}", listToPlay.Name, chapters.Length);

                // create the chosen file path (playlist)
                filePath = Path.Combine(bluray.DirectoryPLAYLIST.FullName, listToPlay.Name);

                #region Refresh Rate Changer

                // Because g_player reads the framerate from the iniating media path we need to
                // do a re-check of the framerate after the user has chosen the playlist. We do
                // this by grabbing the framerate from the first video stream in the playlist as
                // this data was already scanned.
                using (Settings xmlreader = new MPSettings())
                {
                    bool enabled = xmlreader.GetValueAsBool("general", "autochangerefreshrate", false);
                    if (enabled)
                    {
                        TSFrameRate framerate = listToPlay.VideoStreams[0].FrameRate;
                        if (framerate != TSFrameRate.Unknown)
                        {
                            double fps = 0;
                            switch (framerate)
                            {
                                case TSFrameRate.FRAMERATE_59_94:
                                    fps = 59.94;
                                    break;
                                case TSFrameRate.FRAMERATE_50:
                                    fps = 50;
                                    break;
                                case TSFrameRate.FRAMERATE_29_97:
                                    fps = 29.97;
                                    break;
                                case TSFrameRate.FRAMERATE_25:
                                    fps = 25;
                                    break;
                                case TSFrameRate.FRAMERATE_24:
                                    fps = 24;
                                    break;
                                case TSFrameRate.FRAMERATE_23_976:
                                    fps = 23.976;
                                    break;
                            }

                            BDHandlerCore.LogDebug("Initiating refresh rate change: {0}", fps);
                            RefreshRateChanger.SetRefreshRateBasedOnFPS(fps, filePath, RefreshRateChanger.MediaType.Video);
                        }
                    }
                }

                #endregion

                return true;
            }
            catch (Exception e)
            {
                BDHandlerCore.LogError("Exception while reading bluray structure {0} {1}", e.Message, e.StackTrace);
                return true;
            }
        }

    }
}
