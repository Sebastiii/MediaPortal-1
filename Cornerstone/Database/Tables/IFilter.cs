﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Cornerstone.Database.Tables {
    public interface IFilter<T> where T:DatabaseTable {
        event FilterUpdatedDelegate<T> Updated;
        List<T> Filter(List<T> input);
        bool Active { get; }
    }

    public delegate void FilterUpdatedDelegate<T>(IFilter<T> obj) where T:DatabaseTable;
}
