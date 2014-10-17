/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#pragma once

#ifndef __MSHS_MANIFEST_SMOOTH_STREAMING_MEDIA_BOX_DEFINED
#define __MSHS_MANIFEST_SMOOTH_STREAMING_MEDIA_BOX_DEFINED

#include "MshsManifestProtectionBoxCollection.h"
#include "MshsManifestStreamBoxCollection.h"
#include "Box.h"

#define MSHS_MANIFEST_SMOOTH_STREAMING_MEDIA_BOX_TYPE                 L"mssm"

#define MSHS_MANIFEST_SMOOTH_STREAMING_MEDIA_BOX_FLAG_NONE            BOX_FLAG_NONE

#define MSHS_MANIFEST_SMOOTH_STREAMING_MEDIA_BOX_FLAG_LAST            (BOX_FLAG_LAST + 0)

#define MANIFEST_MAJOR_VERSION                                        2
#define MANIFEST_MINOR_VERSION                                        0
#define MANIFEST_TIMESCALE_DEFAULT                                    10000000

class CMshsManifestSmoothStreamingMediaBox : public CBox
{
public:
  // creates new instance of CMshsManifestSmoothStreamingMediaBox class
  CMshsManifestSmoothStreamingMediaBox(HRESULT *result);
  // destructor
  virtual ~CMshsManifestSmoothStreamingMediaBox(void);

  /* get methods */

  // gets manifest major version (must be 2)
  // @return : major version
  uint32_t GetMajorVersion(void);

  // gets manifest minor version (must be 0)
  // @return : minor version
  uint32_t GetMinorVersion(void);

  // gets time scale of the duration, specified as the number of increments in one second
  // the default value is MANIFEST_TIMESCALE_DEFAULT
  // @return : time scale of duration
  uint64_t GetTimeScale(void);

  // gets duration of the presentation, specified as the number of time increments indicated by the value of the time scale
  // @return : duration of presentation
  uint64_t GetDuration(void);

  // gets protections applicable to streams
  // @return : protections
  CMshsManifestProtectionBoxCollection *GetProtections(void);

  // gets streams
  // @return : streams
  CMshsManifestStreamBoxCollection *GetStreams(void);

  /* set methods */

  // sets major version
  // @param majorVersion : major version to set
  void SetMajorVersion(uint32_t majorVersion);

  // sets minor version
  // @param minorVersion : minor version to set
  void SetMinorVersion(uint32_t minorVersion);

  // sets time scale
  // @param timeScale : time scale to set
  void SetTimeScale(uint64_t timeScale);

  // sets duration
  // @param duration : duration to set
  void SetDuration(uint64_t duration);

  /* other methods*/

  // tests if media is protected
  // @return : true if protected, false otherwise
  bool IsProtected(void);

  // gets box data in human readable format
  // @param indent : string to insert before each line
  // @return : box data in human readable format or NULL if error
  virtual wchar_t *GetParsedHumanReadable(const wchar_t *indent);

private:
  uint32_t majorVersion;

  uint32_t minorVersion;

  uint64_t timeScale;

  uint64_t duration;

  CMshsManifestProtectionBoxCollection *protections;

  CMshsManifestStreamBoxCollection *streams;

  /* methods */

  // gets whole box size
  // method is called to determine whole box size for storing box into buffer
  // @return : size of box 
  virtual uint64_t GetBoxSize(void);

  // parses data in buffer
  // @param buffer : buffer with box data for parsing
  // @param length : the length of data in buffer
  // @param processAdditionalBoxes : specifies if additional boxes have to be processed
  // @return : true if parsed successfully, false otherwise
  virtual bool ParseInternal(const unsigned char *buffer, uint32_t length, bool processAdditionalBoxes);

  // gets whole box into buffer (buffer must be allocated before)
  // @param buffer : the buffer for box data
  // @param length : the length of buffer for data
  // @param processAdditionalBoxes : specifies if additional boxes have to be processed (added to buffer)
  // @return : number of bytes stored into buffer, 0 if error
  virtual uint32_t GetBoxInternal(uint8_t *buffer, uint32_t length, bool processAdditionalBoxes);
};

#endif