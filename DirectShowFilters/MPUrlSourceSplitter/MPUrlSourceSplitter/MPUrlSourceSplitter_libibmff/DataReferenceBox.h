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

#ifndef __DATA_REFERENCE_BOX_DEFINED
#define __DATA_REFERENCE_BOX_DEFINED

#include "FullBox.h"
#include "DataEntryBoxCollection.h"
#include "DataEntryBox.h"
#include "DataEntryUrnBox.h"
#include "DataEntryUrlBox.h"

#define DATA_REFERENCE_BOX_TYPE                                       L"dref"

#define DATA_REFERENCE_BOX_FLAG_NONE                                  FULL_BOX_FLAG_NONE

#define DATA_REFERENCE_BOX_FLAG_LAST                                  (FULL_BOX_FLAG_LAST + 0)

class CDataReferenceBox :
  public CFullBox
{
public:
  // initializes a new instance of CDataReferenceBox class
  CDataReferenceBox(HRESULT *result);

  // destructor
  virtual ~CDataReferenceBox(void);

  /* get methods */

  // gets whole box into buffer (buffer must be allocated before)
  // @param buffer : the buffer for box data
  // @param length : the length of buffer for data
  // @return : true if all data were successfully stored into buffer, false otherwise
  virtual bool GetBox(uint8_t *buffer, uint32_t length);

  // gets data entry box collection stored in data reference box
  // @return : data entry box collection
  virtual CDataEntryBoxCollection *GetDataEntryBoxCollection(void);

  /* set methods */

  /* other methods */

  // gets box data in human readable format
  // @param indent : string to insert before each line
  // @return : box data in human readable format or NULL if error
  virtual wchar_t *GetParsedHumanReadable(const wchar_t *indent);

protected:

  // stores data entry boxes stored in this data reference box
  CDataEntryBoxCollection *dataEntryBoxCollection;

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