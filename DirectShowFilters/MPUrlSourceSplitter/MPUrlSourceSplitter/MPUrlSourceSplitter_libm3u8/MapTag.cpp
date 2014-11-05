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

#include "MapTag.h"
#include "ItemCollection.h"
#include "PlaylistItemCollection.h"
#include "PlaylistItem.h"
#include "DiscontinuityTag.h"

CMapTag::CMapTag(HRESULT *result)
  : CTag(result)
{
}

CMapTag::~CMapTag(void)
{
}

/* get methods */

/* set methods */

/* other methods */

bool CMapTag::IsMediaPlaylistItem(unsigned int version)
{
  return (version == PLAYLIST_VERSION_05);
}

bool CMapTag::IsMasterPlaylistItem(unsigned int version)
{
  return false;
}

bool CMapTag::IsPlaylistItemTag(void)
{
  return true;
}

bool CMapTag::ApplyTagToPlaylistItems(unsigned int version, CItemCollection *notProcessedItems, CPlaylistItemCollection *processedPlaylistItems)
{
  if (version == PLAYLIST_VERSION_05)
  {
    // it is applied to all playlist items after this tag until next discontinuity tag or end of playlist
    bool applied = this->ParseAttributes(version);

    for (unsigned int i = 1; (applied & (i < notProcessedItems->Count())); i++)
    {
      CPlaylistItem *playlistItem = dynamic_cast<CPlaylistItem *>(notProcessedItems->GetItem(i));
      CDiscontinuityTag *discontinuityTag = dynamic_cast<CDiscontinuityTag *>(notProcessedItems->GetItem(i));

      if (playlistItem != NULL)
      {
        CTag *clone = (CTag *)this->Clone();
        applied &= (clone != NULL);

        CHECK_CONDITION_EXECUTE(applied, applied &= playlistItem->GetTags()->Add(clone));

        CHECK_CONDITION_EXECUTE(!applied, FREE_MEM_CLASS(clone));
      }

      if (discontinuityTag != NULL)
      {
        break;
      }
    }

    return applied;
  }
  else
  {
    // unknown playlist version
    return false;
  }
}

bool CMapTag::ParseTag(unsigned int version)
{
  bool result = __super::ParseTag(version);
  result &= (version == PLAYLIST_VERSION_05);

  if (result)
  {
    // successful parsing of tag
    // compare it to our tag
    result &= (wcscmp(this->tag, TAG_MAP) == 0);
  }

  return result;
}

/* protected methods */

CItem *CMapTag::CreateItem(void)
{
  HRESULT result = S_OK;
  CMapTag *item = new CMapTag(&result);
  CHECK_POINTER_HRESULT(result, item, result, E_OUTOFMEMORY);

  CHECK_CONDITION_EXECUTE(FAILED(result), FREE_MEM_CLASS(item));
  return item;
}

bool CMapTag::CloneInternal(CItem *item)
{
  bool result = __super::CloneInternal(item);
  CMapTag *tag = dynamic_cast<CMapTag *>(item);
  result &= (tag != NULL);

  return result;
}