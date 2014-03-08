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

#ifndef __COLLECTION_DEFINED
#define __COLLECTION_DEFINED

template <class TItem> class CCollection
{
public:
  // create new instance of CCollection class
  CCollection();

  virtual ~CCollection(void);

  // add item to collection
  // @param item : the reference to item to add
  // @return : true if successful, false otherwise
  virtual bool Add(TItem *item);

  // insert item to collection
  // @param position : zero-based position to insert new item
  // @param item : item to insert
  // @result : true if successful, false otherwise
  virtual bool Insert(unsigned int position, TItem *item);

  // append collection of items
  // @param collection : the reference to collection to add
  // @return : true if all items added, false otherwise
  virtual bool Append(CCollection<TItem> *collection);

  // clear collection of items
  virtual void Clear(void);

  // get the item from collection with specified index
  // @param index : the index of item to find
  // @return : the reference to item or NULL if not find
  virtual TItem *GetItem(unsigned int index);

  // get count of items in collection
  // @return : count of items in collection
  virtual unsigned int Count(void);

  // remove item with specified index from collection
  // @param index : the index of item to remove
  // @return : true if removed, false otherwise
  virtual bool Remove(unsigned int index);

  // ensures that in internal buffer is enough space
  // if in internal buffer is not enough space, method tries to allocate enough space
  // @param requestedCount : the requested count of items
  // @return : true if in internal buffer is enough space, false otherwise
  virtual bool EnsureEnoughSpace(unsigned int requestedCount);

protected:
  // pointer to array of pointers to items
  TItem **items;

  // count of items in collection
  unsigned int itemCount;

  // maximum count of items to store in collection
  unsigned int itemMaximumCount;

  // clones specified item
  // @param item : the item to clone
  // @return : deep clone of item or NULL if not implemented
  virtual TItem *Clone(TItem *item) = 0;
};

// implementation

template <class TItem> CCollection<TItem>::CCollection()
{
  this->itemCount = 0;
  this->itemMaximumCount = 16;
  this->items = ALLOC_MEM_SET(this->items, TItem *, this->itemMaximumCount, 0);
}

template <class TItem> CCollection<TItem>::~CCollection(void)
{
  this->Clear();

  FREE_MEM(this->items);
}

template <class TItem> void CCollection<TItem>::Clear(void)
{
  // call destructors of all items
  for(unsigned int i = 0; i < this->itemCount; i++)
  {
    FREE_MEM_CLASS((*(this->items + i)));
  }

  // set used items to 0
  this->itemCount = 0;
}

template <class TItem> bool CCollection<TItem>::EnsureEnoughSpace(unsigned int requestedCount)
{
  if (requestedCount >= this->itemMaximumCount)
  {
    // there is need to enlarge array of items
    TItem **itemArray = REALLOC_MEM(this->items, TItem *, requestedCount);

    if (itemArray == NULL)
    {
      return false;
    }

    this->items = itemArray;
    this->itemMaximumCount = requestedCount;
  }

  return true;
}

template <class TItem> bool CCollection<TItem>::Add(TItem *item)
{
  if (item == NULL)
  {
    return false;
  }

  if (!this->EnsureEnoughSpace(this->Count() + 1))
  {
    return false;
  }

  *(this->items + this->itemCount++) = item;
  return true;
}

template <class TItem> bool CCollection<TItem>::Insert(unsigned int position, TItem *item)
{
  bool result = false;

  if ((position >= 0) && (position <= this->itemCount))
  {
    // ensure that enough space is in collection
    result = this->EnsureEnoughSpace(this->itemCount + 1);

    if (result)
    {
      // move everything after insert position

      for (unsigned int i = position; i < this->itemCount; i++)
      {
        *(this->items + this->itemCount - i + position) = *(this->items + this->itemCount - 1 - i + position);
      }

      *(this->items + position) = item;
      this->itemCount++;
    }
  }

  return result;
}

template <class TItem> bool CCollection<TItem>::Append(CCollection<TItem> *collection)
{
  bool result = true;
  if (collection != NULL)
  {
    unsigned int count = collection->Count();
    for (unsigned int i = 0; i < count; i++)
    {
      result &= this->Add(this->Clone(collection->GetItem(i)));
    }
  }
  return result;
}

template <class TItem> unsigned int CCollection<TItem>::Count(void)
{
  return this->itemCount;
}

template <class TItem> TItem *CCollection<TItem>::GetItem(unsigned int index)
{
  TItem *result = NULL;
  if (index < this->itemCount)
  {
    result = *(this->items + index);
  }
  return result;
}

template <class TItem> bool CCollection<TItem>::Remove(unsigned int index)
{
  bool result = false;

  if ((index >= 0) && (index < this->itemCount))
  {
    // delete item on specified index
    FREE_MEM_CLASS((*(this->items + index)));
    // move rest of items
    for (unsigned int i = (index + 1); i < this->itemCount; i++)
    {
      *(this->items + i - 1) = *(this->items + i);
    }

    this->itemCount--;
    result = true;
  }

  return result;
}

#endif