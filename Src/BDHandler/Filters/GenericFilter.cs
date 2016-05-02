using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.BDHandler.Filters
{
    public class GenericFilter : ISelectFilter
    {
        string IFilter.Name
        {
            get { throw new NotImplementedException(); }
        }

        Guid IFilter.ClassID
        {
            get { throw new NotImplementedException(); }
        }

        int IFilter.RecommendedBuildNumber
        {
            get { throw new NotImplementedException(); }
        }        
        
        string ISelectFilter.ParseSubtitleLanguage(string input)
        {
            throw new NotImplementedException();
        }

        string ISelectFilter.ParseSubtitleName(string input)
        {
            throw new NotImplementedException();
        }

        string ISelectFilter.ParseAudioType(string input)
        {
            throw new NotImplementedException();
        }

        string ISelectFilter.ParseAudioLanguage(string input)
        {
            throw new NotImplementedException();
        }

       
    }
}
