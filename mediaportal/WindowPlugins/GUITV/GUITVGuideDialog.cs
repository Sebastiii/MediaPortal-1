/* 
*	Copyright (C) 2005 Team MediaPortal
*	http://www.team-mediaportal.com
*
*  This Program is free software; you can redistribute it and/or modify
*  it under the terms of the GNU General Public License as published by
*  the Free Software Foundation; either version 2, or (at your option)
*  any later version.
*   
*  This Program is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
*  GNU General Public License for more details.
*   
*  You should have received a copy of the GNU General Public License
*  along with GNU Make; see the file COPYING.  If not, write to
*  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
*  http://www.gnu.org/copyleft/gpl.html
*
*/
#region usings
using System;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using MediaPortal.Dialogs;
using MediaPortal.Player;
using MediaPortal.TV.Recording;
using MediaPortal.TV.Database;
#endregion


namespace MediaPortal.GUI.TV
{
  /// <summary>
  /// 
  /// </summary>
  public class GUITVGuideDialog : GUITvGuideBase, IRenderLayer
  {
    #region Base Dialog Variables
    bool m_bRunning = false;
    #endregion

    public GUITVGuideDialog()
      : base()
    {
      GetID = (int)GUIWindow.Window.WINDOW_DIALOG_TVGUIDE;
    }

    public override bool Init()
    {
      Initialize();

      return Load(GUIGraphicsContext.Skin + @"\dialogTvGuide.xml");
    }
    protected override void OnPageDestroy(int new_windowId)
    {
      base.OnPageDestroy(new_windowId);
      m_bRunning = false;
    }
    #region Base Dialog Members
    public void DoModal(int dwParentId)
    {

      GUIWindow parentWindow = GUIWindowManager.GetWindow(dwParentId); ;
      if (null == parentWindow)
      {
        return;
      }
      bool wasRouted = GUIWindowManager.IsRouted;
      IRenderLayer prevLayer = GUILayerManager.GetLayer(GUILayerManager.LayerType.Dialog);

      GUIWindowManager.IsSwitchingToNewWindow = true;
      GUIWindowManager.RouteToWindow(GetID);


      // active this window...
      GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_WINDOW_INIT, GetID, 0, 0, -1, 0, null);
      OnMessage(msg);

      GUILayerManager.RegisterLayer(this, GUILayerManager.LayerType.Dialog);
      GUIWindowManager.IsSwitchingToNewWindow = false;
      m_bRunning = true;
      while (m_bRunning && GUIGraphicsContext.CurrentState == GUIGraphicsContext.State.RUNNING)
      {
        GUIWindowManager.Process();
      }

      GUIWindowManager.IsSwitchingToNewWindow = true;
      GUIWindowManager.UnRoute();
      GUIWindowManager.IsSwitchingToNewWindow = false;

      FreeResources();
      DeInitControls();
      GUILayerManager.UnRegisterLayer(this);
      if (wasRouted)
      {
        GUIWindowManager.RouteToWindow(dwParentId);
        GUILayerManager.RegisterLayer(prevLayer, GUILayerManager.LayerType.Dialog);
      }
    }
    #endregion
    public override bool OnMessage(GUIMessage message)
    {
      //      needRefresh = true;
      switch (message.Message)
      {
        case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT:
          {
            Log.Write("tvguidedlg:GUI_MSG_WINDOW_DEINIT");
            m_bRunning = false;
            return true;
          }
      }
      return base.OnMessage(message);
    }
    public override void OnAction(Action action)
    {
      switch (action.wID)
      {
        case Action.ActionType.ACTION_PREVIOUS_MENU:
          m_bRunning = false;
          return;
      }
      base.OnAction(action);
    }
    #region IRenderLayer
    public bool ShouldRenderLayer()
    {
      return true;
    }

    public void RenderLayer(float timePassed)
    {
      Render(timePassed);
    }
    #endregion
  }
}
