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
            string path = strFile.ToLower();

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
            return (CurrentFile.ToLower().EndsWith(".mpls"));
        }

        /// <summary>
        /// Renders a graph that can playback mpls files
        /// </summary>
        /// <returns></returns>
        protected override bool RenderCustomGraph()
        {
            try
            {
                graphBuilder = (IGraphBuilder)new FilterGraph();
                _rotEntry = new DsROTEntry((IFilterGraph)graphBuilder);
                List<string> filters = new List<string>();

                BDHandlerCore.LogDebug("Player is active.");

                GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_SWITCH_FULL_WINDOWED, 0, 0, 0, 1, 0, null);
                GUIWindowManager.SendMessage(msg);

                Vmr9 = new VMR9Util();
                Vmr9.AddVMR9(graphBuilder);
                Vmr9.Enable(false);

                // load the source filter                
                IBaseFilter source = DirectShowUtil.AddFilterToGraph(graphBuilder, this.sourceFilter.Name);

                // check if it's available
                if (source == null)
                {
                    Error.SetError("Unable to load source filter", "Please register filter: " + this.sourceFilter.Name);
                    BDHandlerCore.LogError("Unable to load DirectShowFilter: {0}", this.sourceFilter.Name);
                    return false;
                }

                // load the file
                int result = ((IFileSourceFilter)source).Load(CurrentFile, null);
                if (result != 0) return false;

                // add filters and audio renderer
                using (Settings settings = new Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
                {
                    // Get the minimal settings required
                    bool useAutoDecoderSettings = settings.GetValueAsBool("movieplayer", "autodecodersettings", false);
                    string filterAudioRenderer = settings.GetValueAsString("movieplayer", "audiorenderer", "Default DirectSound Device");

                    // if "Auto Decoder Settings" is unchecked we add the filters specified in the codec configuration
                    // otherwise the DirectShow merit system is used (except for renderer and source filter)
                    if (!useAutoDecoderSettings)
                    {
                        // Get the Video Codec configuration settings
                        
                        string filterVideoMpeg2 = settings.GetValueAsString("movieplayer", "mpeg2videocodec", "");
                        string filterVideoH264 = settings.GetValueAsString("movieplayer", "h264videocodec", "");
                        string filterAudioMpeg2 = settings.GetValueAsString("movieplayer", "mpeg2audiocodec", "");
                        string filterAudioAAC = settings.GetValueAsString("movieplayer", "aacaudiocodec", "");

                        // Get the custom filters that apply
                        int i = 0;
                        while (true)
                        {
                            string filter = settings.GetValueAsString("movieplayer", string.Format("filter{0}", i), null);
                            if (filter == null)
                            {
                                break;
                            }

                            if (settings.GetValueAsBool("movieplayer", string.Format("usefilter{0}", i), false))
                            {
                                // we found a filter so we add it to the filter collection
                                filters.Add(filter);
                            }

                            i++;
                        }
                        
                        if (!string.IsNullOrEmpty(filterVideoH264))
                            DirectShowUtil.AddFilterToGraph(graphBuilder, filterVideoH264);
                        
                        //if (!string.IsNullOrEmpty(strVideoCodec) && strVideoCodec != strH264VideoCodec)
                        //    DirectShowUtil.AddFilterToGraph(graphBuilder, strVideoCodec);
                        
                        if (!string.IsNullOrEmpty(filterAudioMpeg2))
                            DirectShowUtil.AddFilterToGraph(graphBuilder, filterAudioMpeg2);
                        if (!string.IsNullOrEmpty(filterAudioAAC) && filterAudioMpeg2 != filterAudioAAC)
                            DirectShowUtil.AddFilterToGraph(graphBuilder, filterAudioAAC);
                    }

                    DirectShowUtil.AddAudioRendererToGraph(graphBuilder, filterAudioRenderer, false);

                    // Add custom filter collection to graph 
                    foreach (string filter in filters)
                    {
                        DirectShowUtil.AddFilterToGraph(graphBuilder, filter);
                    }                   

                }

                DirectShowUtil.RenderUnconnectedOutputPins(graphBuilder, source);
                DirectShowUtil.ReleaseComObject(source); source = null;
                DirectShowUtil.RemoveUnusedFiltersFromGraph(graphBuilder);

                SubEngine.GetInstance().LoadSubtitles(graphBuilder, m_strCurrentFile);

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

                // this will be the title of the dialog, we strip the dialog of weird characters that might wreck the font engine.
                string heading = (bluray.Title != string.Empty) ? Regex.Replace(bluray.Title, @"[^\w\s\*\%\$\+\,\.\-\:\!\?\(\)]", "").Trim() : "Bluray: Select Feature";

                GUIWaitCursor.Hide();

                // Feature selection logic 
                TSPlaylistFile listToPlay = null;
                if (allPlayLists.Count == 0)
                {
                    BDHandlerCore.LogInfo("No playlists found, bypassing dialog.", allPlayLists.Count);
                    return true;
                }
                else if (allPlayLists.Count == 1)
                {
                    // if we have only one playlist to show just move on
                    BDHandlerCore.LogInfo("Found one valid playlist, bypassing dialog.", filePath);
                    listToPlay = allPlayLists[0];
                }
                else
                {
                    // Show selection dialog
                    BDHandlerCore.LogInfo("Found {0} playlists, showing selection dialog.", allPlayLists.Count);

                    // first make an educated guess about what the real features are (more than one chapter, no loops and longer than one hour)
                    // todo: make a better filter on the playlists containing the real features
                    List<TSPlaylistFile> playLists = allPlayLists.Where(p => (p.Chapters.Count > 1 || p.TotalLength >= MinimalFullFeatureLength) && !p.HasLoops).ToList();

                    // if the filter yields zero results just list all playlists 
                    if (playLists.Count == 0)
                    {
                        playLists = allPlayLists;
                    }

                    IDialogbox dialog = (IDialogbox)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
                    while (true)
                    {
                        dialog.Reset();
                        dialog.SetHeading(heading);

                        int count = 1;

                        for (int i = 0; i < playLists.Count; i++)
                        {
                            TSPlaylistFile playList = playLists[i];
                            TimeSpan lengthSpan = new TimeSpan((long)(playList.TotalLength * 10000000));
                            string length = string.Format("{0:D2}:{1:D2}:{2:D2}", lengthSpan.Hours, lengthSpan.Minutes, lengthSpan.Seconds);
                            // todo: translation
                            string feature = string.Format("Feature #{0}, {2} Chapter{3} ({1})", count, length, playList.Chapters.Count, (playList.Chapters.Count > 1) ? "s" : string.Empty);
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
                            return false;
                        }

                        // end dialog
                        break;
                    }

                    listToPlay = playLists[dialog.SelectedId - 1];
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
