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

#ifndef __RTSP_LOCATION_RESPONSE_HEADER_DEFINED
#define __RTSP_LOCATION_RESPONSE_HEADER_DEFINED

#include "RtspResponseHeader.h"

#define RTSP_LOCATION_RESPONSE_HEADER_TYPE                            L"Location"

class CRtspLocationResponseHeader : public CRtspResponseHeader
{
public:
  CRtspLocationResponseHeader(void);
  virtual ~CRtspLocationResponseHeader(void);

  /* get methods */

  // gets location header URI
  // @return : location header URI or NULL if not specified
  virtual const wchar_t *GetUri(void);

  /* set methods */

  /* other methods */

  // deep clones of current instance
  // @return : deep clone of current instance or NULL if error
  virtual CRtspLocationResponseHeader *Clone(void);

  // parses header and stores name and value to internal variables
  // @param header : header to parse
  // @param length : the length of header
  // @return : true if successful, false otherwise
  virtual bool Parse(const wchar_t *header, unsigned int length);

protected:

  wchar_t *uri;

  // deeply clones current instance to cloned header
  // @param  clonedHeader : cloned header to hold clone of current instance
  // @return : true if successful, false otherwise
  virtual bool CloneInternal(CHttpHeader *clonedHeader);

  // returns new header object to be used in cloning
  // @return : header object or NULL if error
  virtual CHttpHeader *GetNewHeader(void);
};

#endif