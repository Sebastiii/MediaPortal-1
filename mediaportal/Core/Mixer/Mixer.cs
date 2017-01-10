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
using System.Linq;
using MediaPortal.ExtensionMethods;
using MediaPortal.GUI.Library;
using MediaPortal.Player;
using NAudio.CoreAudioApi;

namespace MediaPortal.Mixer
{
  public sealed class Mixer : IDisposable
  {
    #region Events

    #endregion Events

    #region Methods

    public void Close()
    {
      lock (this)
      {
        if (_handle == IntPtr.Zero)
        {
          return;
        }

        MixerNativeMethods.mixerClose(_handle);

        _handle = IntPtr.Zero;
      }
    }

    public void Dispose()
    {
      _mMdevice?.SafeDispose();
      Close();
    }

    public void Open()
    {
      Open(0, false);
    }

    public void Open(int mixerIndex, bool isDigital)
    {
      lock (this)
      {
        try
        {
          if (_mMdeviceEnumerator == null)
            _mMdeviceEnumerator = new MMDeviceEnumerator();

          _mMdevice = _mMdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
          if (_mMdevice != null)
          {
            Log.Info($"Mixer: default audio device: {_mMdevice.FriendlyName}");
            //volume = (int)Math.Round(_audioDefaultDevice.Volume * VolumeMaximum);
          }
        }
        catch (Exception)
        {
          _isMuted = false;
          _volume = VolumeMaximum;
        }
      }
    }

    public void ChangeAudioDevice(string deviceName, bool setToDefault)
    {
      try
      {
        if (_mMdeviceEnumerator == null)
          _mMdeviceEnumerator = new MMDeviceEnumerator();

        _mMdevice = _mMdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        if (setToDefault || deviceName == "Default DirectSound Device" || deviceName == "Default WaveOut Device" ||
            deviceName == "Mediaportal - Audio Renderer")
        {
          _mMdevice = _mMdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
          Log.Info($"Mixer: changed audio device to default : {_mMdevice.FriendlyName}");
          IsDefaultDevice = true;
          return;
        }

        var deviceFound = _mMdeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
          .FirstOrDefault(
            device => device.FriendlyName.Trim().ToLowerInvariant() == deviceName.Trim().ToLowerInvariant());

        if (deviceFound != null)
        {
          _mMdevice = deviceFound;
          IsDefaultDevice = false;
          Log.Info($"Mixer: changed audio device to : {deviceFound.FriendlyName}");
        }
        else
        {
          Log.Info($"Mixer: ChangeAudioDevice failed because device {deviceName} was not found, falling back to default");
          _mMdevice = _mMdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
          IsDefaultDevice = true;
        }
      }
      catch (Exception ex)
      {
        Log.Error($"Mixer: error occured in ChangeAudioDevice: {ex}");
      }
    }

    #endregion Methods

    #region Properties

    public bool IsMuted
    {
      get { lock (this) return _isMuted; }
      set
      {
        lock (this)
        {
          _mMdevice.AudioEndpointVolume.Mute = value;
          _isMuted = value;
        }
      }
    }


    public int Volume
    {
      get
      {
        lock (this)
        {
          return _volume;
        }
      }
      set
      {
        lock (this)
          try
          {
            // Check if default device is set and still valid for volume control
            if (IsDefaultDevice)
            {
              var mMdeviceCurrent = _mMdeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
              if (mMdeviceCurrent?.ID != _mMdevice?.ID)
                _mMdevice = mMdeviceCurrent;
            }

            _volume = value;
            int volumePercentage = (int) Math.Round((double) (100 * value) / VolumeMaximum);

            // Make sure we never go out of scope
            if (volumePercentage < 0)
              volumePercentage = 0;
            else if (volumePercentage > 100)
              volumePercentage = 100;

            if (_mMdevice != null)
            {
              switch (volumePercentage)
              {
                case 0:
                  IsMuted = true;
                  break;
                case 100:
                  _mMdevice.AudioEndpointVolume.MasterVolumeLevelScalar = 1;
                  IsMuted = false;
                  break;
                default:

                  float volume = volumePercentage / 100.0f;
                  _mMdevice.AudioEndpointVolume.MasterVolumeLevelScalar = volume;

                  IsMuted = false;
                  break;
              }
            }

            VolumeHandler.Instance.mixer_UpdateVolume();
          }
          catch (Exception ex)
          {
            Log.Error($"Mixer: error occured in Volume: {ex}");

          }
      }
    }

    public int VolumeMaximum => 65535;

    public int VolumeMinimum => 0;

    #endregion Properties

    #region Fields
    private IntPtr _handle;
    private bool _isMuted;
    public bool IsDefaultDevice;
    private int _volume;
    private MMDeviceEnumerator _mMdeviceEnumerator = new MMDeviceEnumerator();
    private MMDevice _mMdevice;

    #endregion Fields
  }
}