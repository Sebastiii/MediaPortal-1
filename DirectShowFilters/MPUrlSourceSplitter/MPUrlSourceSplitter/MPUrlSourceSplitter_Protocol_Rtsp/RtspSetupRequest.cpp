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

#include "StdAfx.h"

#include "RtspSetupRequest.h"

CRtspSetupRequest::CRtspSetupRequest(void)
  : CRtspRequest()
{
  CRtspTransportRequestHeader *header = new CRtspTransportRequestHeader();
  if (header != NULL)
  {
    if (!this->requestHeaders->Add(header))
    {
      FREE_MEM_CLASS(header);
    }
  }
}

CRtspSetupRequest::CRtspSetupRequest(bool createDefaultHeaders)
  : CRtspRequest(createDefaultHeaders)
{
  if (createDefaultHeaders)
  {
    CRtspTransportRequestHeader *header = new CRtspTransportRequestHeader();
    if (header != NULL)
    {
      if (!this->requestHeaders->Add(header))
      {
        FREE_MEM_CLASS(header);
      }
    }
  }
}

CRtspSetupRequest::~CRtspSetupRequest(void)
{
}

/* get methods */

const wchar_t *CRtspSetupRequest::GetMethod(void)
{
  return RTSP_SETUP_METHOD;
}

CRtspTransportRequestHeader *CRtspSetupRequest::GetTransportRequestHeader(void)
{
  return dynamic_cast<CRtspTransportRequestHeader *>(this->requestHeaders->GetRtspHeader(RTSP_TRANSPORT_REQUEST_HEADER_NAME, false));
}

/* set methods */

/* other methods */

CRtspSetupRequest *CRtspSetupRequest::Clone(void)
{
  return (CRtspSetupRequest *)__super::Clone();
}

bool CRtspSetupRequest::CloneInternal(CRtspRequest *clonedRequest)
{
  return __super::CloneInternal(clonedRequest);
}

CRtspRequest *CRtspSetupRequest::GetNewRequest(void)
{
  return new CRtspSetupRequest(false);
}