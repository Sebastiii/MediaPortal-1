#region Copyright (C) 2005-2013 Team MediaPortal
// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Drawing;
using MediaPortal.GUI.Library;
using MediaPortal.MusicPlayer.BASS;
using MediaPortal.MusicPlayer.BASS;
using MediaPortal.TagReader;


namespace MediaPortal.Visualization
{
  public class WMPViz : VisualizationBase, IDisposable
  {
    #region Variables


    private BASSVIS_INFO _mediaInfo = null;
    private BASSVIS_EXEC visExec = null;
    private bool RenderStarted = false;
    private bool firstRun = true;
    private bool VizVisible = false;
    private MusicTag trackTag = null;
    private string _OldCurrentFile = "   ";

    #endregion



    public WMPViz(VisualizationInfo vizPluginInfo, VisualizationWindow vizCtrl)
      : base(vizPluginInfo, vizCtrl)
    {
    }





    public override bool Initialize()
    {
    {


      _mediaInfo = new BASSVIS_INFO("", "");
      try
      {
      {


        if (VizPluginInfo == null)
        {
          Log.Error("Visualization Manager: {0} visualization engine initialization failed! Reason:{1}",


      }


        firstRun = true;
        RenderStarted = false;
        bool result = SetOutputContext(VisualizationWindow.OutputContextType);
        _Initialized = result && _visParam.VisHandle != 0;
      }
      catch (Exception ex)
      {
        Log.Error(
        Log.Error(
          "Visualization Manager: WMP visualization engine initialization failed with the following exception {0}",
        return false;
      }


    }




    #region Private Methods

    private void PlaybackStateChanged(object sender, BassAudioEngine.PlayState oldState,
                                      BassAudioEngine.PlayState newState)
    {
      if (_visParam.VisHandle != 0)
      {
        Log.Debug("WMPViz: BassPlayer_PlaybackStateChanged from {0} to {1}", oldState.ToString(), newState.ToString());
        if (newState == BassAudioEngine.PlayState.Playing)
        {
          RenderStarted = false;
          trackTag = TagReader.TagReader.ReadTag(Bass.CurrentFile);
          if (trackTag != null)
          {
            _songTitle = String.Format("{0} - {1}", trackTag.Artist, trackTag.Title);
          }
          else
          {
      }


          _mediaInfo.SongTitle = _songTitle;


    }
        }
    {
        {
      }
        }
      {
        {
          BassVis.BASSVIS_SetPlayState(_visParam, BASSVIS_PLAYSTATE.Stop);
      }
    }
    }




    #region <Base class> Overloads

      {
    {
      base.InitializePreview();
      }


    public override void Dispose()
    {
      base.Dispose();
    }


    {
    {
      {
      {
        if (VisualizationWindow == null || !VisualizationWindow.Visible || _visParam.VisHandle == 0)
        {
      }


      {
        {
          if (Bass.CurrentFile != _OldCurrentFile && !Bass.IsRadio)
          {
            trackTag = TagReader.TagReader.ReadTag(Bass.CurrentFile);
            if (trackTag != null)
            {
              _songTitle = String.Format("{0} - {1}", trackTag.Artist, trackTag.Title);
      }
            }
      {
            {
      }
    }


          // Set Song information, so that the plugin can display it
    {
          {
            _mediaInfo.SongTitle = _songTitle;
            _mediaInfo.SongFile = Bass.CurrentFile;
            _mediaInfo.Channels = 2;
            _mediaInfo.SampleRate = 44100;
          }
      {
          {
        {
            {
              // Change TrackTag to StreamTag for Radio
              trackTag = Bass.GetStreamTags();
        {
              {
                // Artist and Title show better i think
                _songTitle = trackTag.Artist + ": " + trackTag.Title;
        }
        else
        {
              {
        }
              }
            }
          }


        {
        {
          _mediaInfo.SongTitle = "Mediaportal Preview";
        }
        BassVis.BASSVIS_SetInfo(_visParam, _mediaInfo);

          {
        {
          return 1;




            {
        {
            }


        // ckeck is playing
        int nReturn = BassVis.BASSVIS_SetPlayState(_visParam, BASSVIS_PLAYSTATE.IsPlaying);
            {
        {
          if (stream != 0)
          {
            // Do not Render without playing
            if (MusicPlayer.BASS.Config.MusicPlayer == AudioPlayer.WasApi)
            {
            }
            else
            {
            {
            }
          }
          }
        }

          catch (Exception)
          {
          }
      }

        }


      {
    {
      Bass.PlaybackStateChanged -= new BassAudioEngine.PlaybackStateChangedDelegate(PlaybackStateChanged);
        {
      {
          }
      }
      return false;


          {

      return true;
    }


    {
    {
      return true;


      {
    {
      base.WindowChanged(vizWindow);
      return true;


        {
    {
      // If width or height are 0 the call to CreateVis will fail.  
      // If width or height are 1 the window is in transition so we can ignore it.
      if (VisualizationWindow.Width <= 1 || VisualizationWindow.Height <= 1)
          return false;
        }


      // Do a move of the WMP Viz
        {
      {
        // Visible State hold
        VizVisible = VisualizationWindow.Visible;
        // Hide the Viswindow, so that we don't see it, while moving
        VisualizationWindow.Visible = false;
        BassVis.BASSVIS_Resize(_visParam, 0, 0, newSize.Width, newSize.Height);
        // reactivate old Visible state
        }
      }
      return true;
    }

        {
    {
      if (VisualizationWindow == null)
      {
        }
      }

      if (_Initialized && !firstRun)
      {
      }


      // If width or height are 0 the call to CreateVis will fail.  
      // If width or height are 1 the window is in transition so we can ignore it.
      {
      {
        return false;


        {
      {
        }

        try
        {
      {
        BassVis.BASSVIS_SetPlayState(_visParam, BASSVIS_PLAYSTATE.Play);

        visExec = new BASSVIS_EXEC(BASSVISKind.BASSVISKIND_WMP.ToString());
        visExec.WMP_PluginIndex = VizPluginInfo.PlgIndex -1;
        visExec.WMP_PresetIndex = VizPluginInfo.PresetIndex;
        visExec.WMP_SrcVisHandle = VisualizationWindow.Handle;
        visExec.Left = 0;
        visExec.Top = 0;
        visExec.Width = VisualizationWindow.Width;
        visExec.Height = VisualizationWindow.Height;

        BassVis.BASSVIS_ExecutePlugin(visExec, _visParam);
        BassVis.BASSVIS_SetModulePreset(_visParam, VizPluginInfo.PresetIndex);
        BassVis.BASSVIS_SetOption(_visParam, BASSVIS_CONFIGFLAGS.BASSVIS_CONFIG_FFTAMP, 128);

          {
        {
          // SetForegroundWindow
          }
        }
          }
        catch (Exception ex)
        {
      {
        Log.Error(
          "Visualization Manager: WMP visualization engine initialization failed with the following exception {0}",
        }
      }
      _Initialized = _visParam.VisHandle != 0;
      }


    }
}