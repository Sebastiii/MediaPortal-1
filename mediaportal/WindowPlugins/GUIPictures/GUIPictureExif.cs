#region Copyright (C) 2005-2020 Team MediaPortal

// Copyright (C) 2005-2020 Team MediaPortal
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using MediaPortal.Picture.Database;
using MediaPortal.Util;

namespace MediaPortal.GUI.Pictures
{
  /// <summary>
  /// 
  /// </summary>
  public class GUIPicureExif : GUIInternalWindow, IRenderLayer
  {
    #region Skin controls

    [SkinControl(2)] protected GUIImage imgPicture = null;
    [SkinControl(3)] protected GUIListControl listExifProperties = null;

    #endregion

    #region Variables

    private string _currentPicture;
    private ExifMetadata.Metadata _currentMetaData ;
    private int _currentSelectedItem = -1;
    private string _histogramFilename = string.Empty;

    #endregion

    public GUIPicureExif()
    {
      GetID = (int)Window.WINDOW_PICTURE_EXIF;
    }

    #region Overrides

    public override bool Init()
    {
      return Load(GUIGraphicsContext.GetThemedSkinFile(@"\PictureExifInfo.xml"));
    }

    public override void PreInit() { }

    private void ReturnToPreviousWindow()
    {
      if (GUIWindowManager.HasPreviousWindow())
      {
        GUIWindowManager.ShowPreviousWindow();
      }
      else
      {
        GUIWindowManager.CloseCurrentWindow();
      }
    }

    protected override void OnPageLoad()
    {
      base.OnPageLoad();

      if (string.IsNullOrEmpty(_currentPicture) || !File.Exists(_currentPicture))
      {
        ReturnToPreviousWindow();
        return;
      }

      _currentMetaData = PictureDatabase.GetExifFromDB(_currentPicture);
      if (_currentMetaData.IsEmpty())
      {
        _currentMetaData = PictureDatabase.GetExifFromFile(_currentPicture);
      }
      if (_currentMetaData.IsEmpty())
      {
        ReturnToPreviousWindow();
        return;
      }

      GUIPropertyManager.SetProperty("#pictures.exif.images.vertical", string.Empty);
      GUIPropertyManager.SetProperty("#pictures.exif.images.horizontal", string.Empty);

      SetExifGUIListItems();
      Update();
      Refresh();
    }

    protected override void OnPageDestroy(int newWindowId)
    {
      if (File.Exists(_histogramFilename))
      {
        File.Delete(_histogramFilename);
      }
      ReleaseResources();
      GUIPropertyManager.SetProperty("#pictures.exif.picture", String.Empty);
      base.OnPageDestroy(newWindowId);
    }

    protected override void OnShowContextMenu()
    {
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);

      if (dlg == null)
      {
        return;
      }

      dlg.Reset();
      dlg.SetHeading(498); // Menu

      // Dialog items
      dlg.AddLocalizedString(2168); // Update Exif

      // Show dialog menu
      dlg.DoModal(GetID);

      if (dlg.SelectedId== -1)
      {
        return;
      }

      switch (dlg.SelectedId)
      {
        case 2168: // Update Exif
          Log.Debug("GUIPicturesExif: Update Exif {0}: {1}", PictureDatabase.UpdatePicture(_currentPicture, -1), _currentPicture);
          _currentMetaData = PictureDatabase.GetExifFromDB(_currentPicture);
          SetExifGUIListItems();
          Update();
          Refresh();
          break;
      }
    }

    public override bool OnMessage(GUIMessage message)
    {
      switch (message.Message)
      {
        case GUIMessage.MessageType.GUI_MSG_WINDOW_INIT_DONE:
        {
          Refresh();
        }
        break;
      }
      return base.OnMessage(message);
    }

    #endregion

    public string Picture
    {
      get { return _currentPicture; }
      set { _currentPicture = value; }
    }

    private void Update()
    {
      try
      {
        if (listExifProperties != null && !listExifProperties.IsVisible)
        {
          listExifProperties.IsVisible = true;

          if (!listExifProperties.IsEnabled)
          {
            GUIControl.EnableControl(GetID, listExifProperties.GetID);
          }

          GUIControl.SelectControl(GetID, listExifProperties.GetID);
          GUIControl.FocusControl(GetID, listExifProperties.GetID);
          GUIPropertyManager.SetProperty("#itemcount", Util.Utils.GetObjectCountLabel(listExifProperties.Count));
          listExifProperties.SelectedListItemIndex = _currentSelectedItem;
          SelectItem();
        }

        if (imgPicture != null)
        {
          imgPicture.Rotation = PictureDatabase.GetRotation(_currentPicture);
          imgPicture.Dispose();
          imgPicture.AllocResources();
        }

        GUIPropertyManager.SetProperty("#pictures.exif.picture", _currentPicture);
      }
      catch (Exception ex)
      {
        Log.Error("GUIPictureExif Update controls Error: {1}", ex.Message);
      }
    }

    private void SelectItem()
    {
      if (_currentSelectedItem >= 0 && listExifProperties != null)
      {
        GUIControl.SelectItemControl(GetID, listExifProperties.GetID, _currentSelectedItem);
        GUIControl.FocusItemControl(GetID, listExifProperties.GetID, _currentSelectedItem);
      }
    }

    private string GetMapURL(double lat, double lon, out string filename)
    {
      filename = string.Empty;
      string mapurl = GUILocalizeStrings.Get(9090);
      if (!Util.Utils.IsURL(mapurl))
      {
        return string.Empty;
      }

      try
      {
        mapurl = String.Format(mapurl, lat.ToMapString(), lon.ToMapString());
        filename = lat.ToFileName() + "-" + lon.ToFileName() + ".png";
        return mapurl;
      }
      catch
      {
        Log.Debug("GetMapURL: Wrong map URL {0}", GUILocalizeStrings.Get(9090));
      }
      return string.Empty;
    }

    private string GetAddressURL(double lat, double lon)
    {
      string addrurl = GUILocalizeStrings.Get(9091);
      if (!Util.Utils.IsURL(addrurl))
      {
        return string.Empty;
      }

      try
      {
        addrurl = String.Format(addrurl, lat.ToMapString(), lon.ToMapString(), GUILocalizeStrings.GetCultureName(GUILocalizeStrings.CurrentLanguage()));
        return addrurl;
      }
      catch
      {
        Log.Debug("GetAddressURL: Wrong Address URL {0}", GUILocalizeStrings.Get(9090));
      }
      return string.Empty;
    }

    private void MapDownload(string url, string filename, ref GUIListItem item)
    {
      string mFilename = Path.Combine(Thumbs.PicturesMaps, filename);
      if (!File.Exists(mFilename))
      {
        Util.Utils.DownLoadAndCacheImage(url, mFilename);
      }
      item.DVDLabel = mFilename;
    }

    private void GetAddress(string url)
    {
      string json = Util.Utils.DownLoadString(url);
      if (string.IsNullOrEmpty(json))
      {
        return;
      }

      Regex regex = new Regex(@"display_name.:.(.+?)\""");
      Match match = regex.Match(json);
      if (!match.Success)
      {
        return;
      }

      string address = match.Groups[1].Value;
      if (string.IsNullOrWhiteSpace(address))
      {
        return;
      }

      if (listExifProperties != null)
      {
        GUIListItem fileitem = new GUIListItem();
        fileitem.Label = address;
        fileitem.Label2 = GUILocalizeStrings.Get(9039);
        fileitem.IconImage = Thumbs.Pictures + @"\exif\data\address.png";
        fileitem.ThumbnailImage = fileitem.IconImage;
        fileitem.OnItemSelected += OnItemSelected;
        listExifProperties.Add(fileitem);
        GUIPropertyManager.SetProperty("#itemcount", listExifProperties.Count.ToString());
      }
    }

    private void GetHistogram()
    {
      if (string.IsNullOrEmpty(_currentPicture))
      {
        return;
      }

      _histogramFilename = Path.GetTempFileName();
      if (File.Exists(_histogramFilename))
      {
        File.Delete(_histogramFilename);
      }
      _histogramFilename = _histogramFilename + ".png";

      if (Util.Picture.GetHistogramImage(_currentPicture, _histogramFilename))
      {
        GUIListItem fileitem = new GUIListItem();
        fileitem.Label = GUILocalizeStrings.Get(9040);
        fileitem.Label2 = GUILocalizeStrings.Get(9040);
        fileitem.DVDLabel = _histogramFilename;
        fileitem.IconImage = Thumbs.Pictures + @"\exif\data\histogram.png";
        fileitem.ThumbnailImage = fileitem.IconImage;
        fileitem.OnItemSelected += OnItemSelected;
        listExifProperties.Add(fileitem);
        GUIPropertyManager.SetProperty("#itemcount", listExifProperties.Count.ToString());
      }
    }

    private void Refresh()
    {
      SetProperties();
    }

    private void SetProperties()
    {
      _currentMetaData.SetExifProperties();

      int width = 96;
      int height = 0;

      GUIPropertyManager.SetProperty("#pictures.exif.images.vertical", string.Empty);
      List<GUIOverlayImage> exifIconImages = _currentMetaData.GetExifInfoOverlayImage(ref width, ref height);
      if (exifIconImages != null && exifIconImages.Count > 0)
      {
        GUIPropertyManager.SetProperty("#pictures.exif.images.vertical", GUIImageAllocator.BuildConcatImage("Exif:Icons:V:", string.Empty, width, height, exifIconImages));
      }

      width = 0;
      height = 96;

      GUIPropertyManager.SetProperty("#pictures.exif.images.horizontal", string.Empty);
      exifIconImages = _currentMetaData.GetExifInfoOverlayImage(ref width, ref height);
      if (exifIconImages != null && exifIconImages.Count > 0)
      {
        GUIPropertyManager.SetProperty("#pictures.exif.images.horizontal", GUIImageAllocator.BuildConcatImage("Exif:Icons:H:", string.Empty, width, height, exifIconImages));
      }
    }

    private void OnItemSelected(GUIListItem item, GUIControl parent)
    {
      try
      {
        if (item != null)
        {
          GUIPropertyManager.SetProperty("#selecteditem", item.Label2 + ": " + item.Label);
          GUIPropertyManager.SetProperty("#pictures.exif.additional", item.DVDLabel);
        }
      }
      catch (Exception ex)
      {
        Log.Error("GUIPicturesExif OnItemSelected exception: {0}", ex.Message);
      }
    }

    private void SetExifGUIListItems()
    {
      try
      {
        if (listExifProperties != null)
        {
          listExifProperties.Clear();
        }
        else
        {
          return;
        }

        GUIListItem fileitem = new GUIListItem();
        fileitem.Label = Path.GetFileNameWithoutExtension(_currentPicture).ToUpperInvariant();
        fileitem.Label2 = GUILocalizeStrings.Get(863);
        fileitem.IconImage = Thumbs.Pictures + @"\exif\data\file.png";
        fileitem.ThumbnailImage = fileitem.IconImage;
        fileitem.OnItemSelected += OnItemSelected;
        listExifProperties.Add(fileitem);

        string addrurl = string.Empty;

        Type type = typeof(ExifMetadata.Metadata);
        foreach (FieldInfo prop in type.GetFields())
        {
          string value = string.Empty;
          string mapurl = string.Empty;
          string mapfile = string.Empty;
          string caption = prop.Name.ToCaption() ?? prop.Name;
          switch (prop.Name)
          {
            case nameof(ExifMetadata.Metadata.ImageDimensions):
              value = _currentMetaData.ImageDimensionsAsString();
              break;
            case nameof(ExifMetadata.Metadata.Resolution):
              value = _currentMetaData.ResolutionAsString();
              break;
            case nameof(ExifMetadata.Metadata.Location):
              if (_currentMetaData.Location != null)
              {
                string latitude = _currentMetaData.Location.Latitude.ToLatitudeString() ?? string.Empty;
                string longitude = _currentMetaData.Location.Longitude.ToLongitudeString() ?? string.Empty;
                if (!string.IsNullOrEmpty(latitude) && !string.IsNullOrEmpty(longitude))
                {
                  value = latitude + " / " + longitude;
                  mapurl = GetMapURL(_currentMetaData.Location.Latitude, _currentMetaData.Location.Longitude, out mapfile);
                  addrurl = GetAddressURL(_currentMetaData.Location.Latitude, _currentMetaData.Location.Longitude);
                }
              }
              break;
            case nameof(ExifMetadata.Metadata.Altitude):
              if (_currentMetaData.Location != null)
              {
                value = _currentMetaData.Altitude.ToAltitudeString();
              }
              break;
            case nameof(ExifMetadata.Metadata.HDR):
              continue;
            default:
              value = ((ExifMetadata.MetadataItem)prop.GetValue(_currentMetaData)).DisplayValue;
              break;
          }
          if (!string.IsNullOrEmpty(value))
          {
            GUIListItem item = new GUIListItem();
            item.Label = value.ToValue() ?? value;
            item.Label2 = caption;
            item.IconImage = Thumbs.Pictures + @"\exif\data\" + prop.Name + ".png";
            item.ThumbnailImage = item.IconImage;
            item.OnItemSelected += OnItemSelected;
            listExifProperties.Add(item);

            if (!string.IsNullOrEmpty(mapurl))
            {
              ThreadPool.QueueUserWorkItem(delegate { MapDownload(mapurl, mapfile, ref item); });
            }
          }
        }
        ThreadPool.QueueUserWorkItem(delegate
        {
          if (!string.IsNullOrEmpty(addrurl))
          {
            GetAddress(addrurl);
          }
          GetHistogram();
        });

        if (listExifProperties.Count > 0)
        {
          listExifProperties.SelectedListItemIndex = 0;
          _currentSelectedItem = 0;
          SelectItem();
        }
      }
      catch (Exception ex)
      {
        Log.Error("GUIPicturesExif exception SetExifGUIListItems: {0}", ex.Message);
      }
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

    public override void Render(float timePassed)
    {
      base.Render(timePassed);
    }

    #endregion

    private void ReleaseResources()
    {
      if (imgPicture != null)
      {
        imgPicture.Dispose();
      }
    }

  }
}