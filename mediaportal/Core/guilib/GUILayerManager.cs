#region Copyright (C) 2005-2011 Team MediaPortal

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
using MediaPortal.Player;

namespace MediaPortal.GUI.Library
{
  public class GUILayerManager
  {
    public enum LayerType : int
    {
      Gui = 0,
      MusicOverlay,
      VideoOverlay,
      TvOverlay,
      Video,
      WeatherOverlay,
      TopOverlay,
      Osd,
      Topbar1,
      Topbar2,
      Dialog,
      MiniEPG,
      Volume
    }

    // layers:
    //      [GUI] - [PREVIEW] - [VIDEO] - [OSD] - [TOPBAR1] - [TOPBAR2] - [DIALOG] - [VOLUME]

    private const int MAX_LAYERS = 15;

    private static readonly IRenderLayer[] _layers = new IRenderLayer[MAX_LAYERS];

    public static void RegisterLayer(IRenderLayer renderer, LayerType zOrder)
    {
      _layers[(int)zOrder] = renderer;
    }

    public static void UnRegisterLayer(IRenderLayer renderer)
    {
      for (int i = 0; i < MAX_LAYERS; ++i)
      {
        if (_layers[i] == renderer)
        {
          _layers[i] = null;
        }
      }
    }

    public static IRenderLayer GetLayer(LayerType zOrder)
    {
      return _layers[(int)zOrder];
    }

    public static bool Render(float timePassed, GUILayers layers)
    {
      bool uiVisible = false;

      if (GUIGraphicsContext.BlankScreen)
      {
        return false;
      }
      int videoLayer = (int) LayerType.Video;
      if (GUIGraphicsContext.ShowBackground == false)
      {
        if (_layers[videoLayer] != null)
        {
          if (_layers[videoLayer].ShouldRenderLayer())
          {
            _layers[videoLayer].RenderLayer(timePassed);
            GUIFontManager.Present();
          }
        }
      }

      int startLayer = 0;
      int endLayer = MAX_LAYERS;

      for (int i = startLayer; i < endLayer; ++i)
      {
        if (_layers[i] != null)
        {
          //// madVR pass GUI rendering when video is played
          if (GUIGraphicsContext.VideoRenderer == GUIGraphicsContext.VideoRendererType.madVR && GUIGraphicsContext.Vmr9Active)
          {
            if ((i == (int)LayerType.Gui || i == (int)LayerType.TopOverlay || i == (int)LayerType.VideoOverlay || i == (int)LayerType.Video || i == (int)LayerType.Gui ) && (layers == GUILayers.over|| layers == GUILayers.all))
            {
              continue;
            }
          }
          if (_layers[i].ShouldRenderLayer())
          {
            if (GUIGraphicsContext.ShowBackground == false && i == videoLayer)
            {
              continue;
            }
            // For madVR, inform that we have UI displaying
            if (i == (int) LayerType.Gui || i == (int) LayerType.Osd || i == (int) LayerType.Topbar2 ||
                i == (int) LayerType.Dialog || i == (int) LayerType.Topbar1 || i == (int) LayerType.MiniEPG ||
                i == (int) LayerType.Volume)
            {
              uiVisible = true;
            }
            _layers[i].RenderLayer(timePassed);
            GUIFontManager.Present();
          }
        }
      }
      //// madVR inform that MP frame is done (workaround to avoid flickering)
      //if (GUIGraphicsContext.Vmr9Active && VMR9Util.g_vmr9 != null)
      //{
      //  VMR9Util.g_vmr9.MadVrRepeatFrame();
      //}
      Log.Error("uiVisible {0}", uiVisible);
      return uiVisible;
    }
  }
}