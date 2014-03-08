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
using System.Runtime.InteropServices;
using DirectShowLib;
using TvLibrary.Implementations.Helper;
using TvLibrary.Interfaces;

namespace TvLibrary.Implementations.DVB
{
  /// <summary>
  /// DVB IP class
  /// </summary>
  public class TvCardDVBIPBuiltIn : TvCardDVBIP
  {
    /// <summary>
    /// CLSID_MPIPTVSource
    /// </summary>
    [ComImport, Guid("D3DD4C59-D3A7-4b82-9727-7B9203EB67C0")]
    public class MPIPTVSource {}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="device"></param>
    /// <param name="sequence"></param>
    public TvCardDVBIPBuiltIn(DsDevice device, int sequence)
      : base(device, sequence)
    {
      _defaultUrl = "udp://@0.0.0.0:1234";
    }

    protected AMMediaType mpeg2ProgramStream = new AMMediaType();

    /// <summary>
    /// AddStreamSourceFilter
    /// </summary>
    /// <param name="url"></param>
    protected override void AddStreamSourceFilter(string url)
    {
      Log.Log.WriteFile("dvbip:Add MediaPortal IPTV Source Filter");
      _filterStreamSource = FilterGraphTools.FindFilterByClsid(_graphBuilder, typeof(MPIPTVSource).GUID);

      if (_filterStreamSource == null)
      {
        _filterStreamSource = FilterGraphTools.AddFilterFromClsid(_graphBuilder, typeof (MPIPTVSource).GUID,
                                                                  "MediaPortal IPTV Source Filter");
      }

      AMMediaType mpeg2ProgramStream = new AMMediaType();
      mpeg2ProgramStream.majorType = MediaType.Stream;
      mpeg2ProgramStream.subType = MediaSubType.Mpeg2Transport;
      mpeg2ProgramStream.unkPtr = IntPtr.Zero;
      mpeg2ProgramStream.sampleSize = 0;
      mpeg2ProgramStream.temporalCompression = false;
      mpeg2ProgramStream.fixedSizeSamples = true;
      mpeg2ProgramStream.formatType = FormatType.None;
      mpeg2ProgramStream.formatSize = 0;
      mpeg2ProgramStream.formatPtr = IntPtr.Zero;
      ((IFileSourceFilter)_filterStreamSource).Load(url, mpeg2ProgramStream);
      //connect the [stream source] -> [inf tee]
      Log.Log.WriteFile("dvb:  Render [source]->[inftee]");
      int hr = _capBuilder.RenderStream(null, null, _filterStreamSource, null, _infTeeMain);
      if (hr != 0)
      {
        Log.Log.Error("dvb:Add source returns:0x{0:X}", hr);
        throw new TvException("Unable to add  source filter");
      }
    }

    public static bool DisconnectAllPins(IGraphBuilder graphBuilder, IBaseFilter filter)
    {
      IEnumPins pinEnum;
      int hr = filter.EnumPins(out pinEnum);
      if (hr != 0 || pinEnum == null)
      {
        return false;
      }
      FilterInfo info;
      filter.QueryFilterInfo(out info);
      Log.Log.Info("Disconnecting all pins from filter {0}", info.achName);
      Release.ComObject(info.pGraph);
      bool allDisconnected = true;
      for (; ; )
      {
        IPin[] pins = new IPin[1];
        int fetched;
        hr = pinEnum.Next(1, pins, out fetched);
        if (hr != 0 || fetched == 0)
        {
          break;
        }
        PinInfo pinInfo;
        pins[0].QueryPinInfo(out pinInfo);
        DsUtils.FreePinInfo(pinInfo);
        if (pinInfo.dir == PinDirection.Output)
        {
          if (!DisconnectPin(graphBuilder, pins[0]))
          {
            allDisconnected = false;
          }
        }
        Release.ComObject(pins[0]);
      }
      Release.ComObject(pinEnum);
      return allDisconnected;
    }

    public static bool DisconnectPin(IGraphBuilder graphBuilder, IPin pin)
    {
      IPin other;
      int hr = pin.ConnectedTo(out other);
      bool allDisconnected = true;
      PinInfo info;
      pin.QueryPinInfo(out info);
      DsUtils.FreePinInfo(info);
      Log.Log.Info("Disconnecting pin {0}", info.name);
      if (hr == 0 && other != null)
      {
        other.QueryPinInfo(out info);
        if (!DisconnectAllPins(graphBuilder, info.filter))
        {
          allDisconnected = false;
        }
        hr = pin.Disconnect();
        if (hr != 0)
        {
          allDisconnected = false;
          Log.Log.Error("Error disconnecting: {0:x}", hr);
        }
        hr = other.Disconnect();
        if (hr != 0)
        {
          allDisconnected = false;
          Log.Log.Error("Error disconnecting other: {0:x}", hr);
        }
        DsUtils.FreePinInfo(info);
        Release.ComObject(other);
      }
      else
      {
        Log.Log.Info("  Not connected");
      }
      return allDisconnected;
    }

    /// <summary>
    /// RemoveStreamSourceFilter
    /// </summary>
    protected override void RemoveStreamSourceFilter()
    {
      if (_filterStreamSource != null)
      {
        //DisconnectAllPins(_graphBuilder, _filterStreamSource);
        _graphBuilder.RemoveFilter(_filterStreamSource);
        Release.ComObject("MediaPortal IPTV Source Filter", _filterStreamSource);
        _filterStreamSource = null;
        if (mpeg2ProgramStream != null)
        {
          DsUtils.FreeAMMediaType(mpeg2ProgramStream);
          mpeg2ProgramStream = null;
        }
      }
    }

    /// <summary>
    /// RunGraph
    /// </summary>
    /// <param name="subChannel"></param>
    /// <param name="url"></param>
    protected override void RunGraph(int subChannel, string url)
    {
      int hr;
      FilterState state;
      (_graphBuilder as IMediaControl).GetState(10, out state);
      if (state == FilterState.Running)
      {
        hr = (_graphBuilder as IMediaControl).StopWhenReady();
        if (hr < 0 || hr > 1)
        {
          Log.Log.WriteFile("dvb:  StopGraph returns: 0x{0:X}", hr);
          throw new TvException("Unable to stop graph");
        }
        if (_mapSubChannels.ContainsKey(subChannel))
        {
          _mapSubChannels[subChannel].OnGraphStopped();
        }
      }
      if (_mapSubChannels.ContainsKey(subChannel))
      {
        _mapSubChannels[subChannel].AfterTuneEvent -= new BaseSubChannel.OnAfterTuneDelegate(OnAfterTuneEvent);
        _mapSubChannels[subChannel].AfterTuneEvent += new BaseSubChannel.OnAfterTuneDelegate(OnAfterTuneEvent);
        _mapSubChannels[subChannel].OnGraphStart();
      }
      RemoveStreamSourceFilter();
      AddStreamSourceFilter(url);

      Log.Log.Info("dvb:  RunGraph");
      hr = (_graphBuilder as IMediaControl).Run();
      if (hr < 0 || hr > 1)
      {
        Log.Log.WriteFile("dvb:  RunGraph returns: 0x{0:X}", hr);
        throw new TvException("Unable to start graph");
      }
      //GetTunerSignalStatistics();
      _epgGrabbing = false;
      if (_mapSubChannels.ContainsKey(subChannel) && (url != _defaultUrl))
      {
        _mapSubChannels[subChannel].AfterTuneEvent -= new BaseSubChannel.OnAfterTuneDelegate(OnAfterTuneEvent);
        _mapSubChannels[subChannel].AfterTuneEvent += new BaseSubChannel.OnAfterTuneDelegate(OnAfterTuneEvent);
        _mapSubChannels[subChannel].OnGraphStarted();
      }
    }
  }
}