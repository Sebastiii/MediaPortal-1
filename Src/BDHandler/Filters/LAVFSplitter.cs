using System;
using System.Text.RegularExpressions;

namespace MediaPortal.Plugins.BDHandler.Filters
{
    /// <summary>
    /// LAVFSplitter Filter Class
    /// </summary>
    public class LAVFSplitter : ISelectFilter
    {
      static Regex audioStreamTextExpr = new Regex(@"(?:A:\s)(?<lang>.+?)(?:\s*\[(?<lang>[^\]]*?)\])?(?:\s*\((?<type>[^\)]*?)\))?(?:\s*\[(?<Default>[^\]]*?)\])?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
      static Regex subtitleStreamTextExpr = new Regex(@"(?:S:\s)(?<lang>.+?)(?:\s*\[(?<lang>[^\]]*?)\])?(?:\s*\((?<name>[^\)]*?)\))?(?:\s*\[(?<Default>[^\]]*?)\])?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
      static Regex subtitleForcedStreamTextExpr = new Regex(@"(?:S:\s)(?<forced>.+?)(?:\s*\[(?<forced>[^\]]*?)\])?(?:\s*\((?<name>[^\)]*?)\))?(?:\s*\[(?<Default>[^\]]*?)\])?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        string IFilter.Name
        {
          get { return "LAV Splitter Source"; }
        }

        Guid IFilter.ClassID
        {
          get { return new Guid("{B98D13E7-55DB-4385-A33D-09FD1BA26338}"); }
        }

        int IFilter.RecommendedBuildNumber
        {
            get { return 20; }
        }

        string ISelectFilter.ParseSubtitleLanguage(string input)
        {
            string language = subtitleStreamTextExpr.Replace(input, "${lang}");
            return language.Trim();
        }

        string ISelectFilter.ParseSubtitleName(string input)
        {
            string name = subtitleStreamTextExpr.Replace(input, "${name}");
            return name.Trim();
        }

        string ISelectFilter.ParseSubtitleForced(string input)
        {
          string language = subtitleForcedStreamTextExpr.Replace(input, "${forced}");
          return language.Trim();
        }

        string ISelectFilter.ParseAudioType(string input)
        {
            string type = audioStreamTextExpr.Replace(input, "${type}");
            return type.Trim();
        }

        string ISelectFilter.ParseAudioLanguage(string input)
        {
            string language = audioStreamTextExpr.Replace(input, "${lang}");
            return language.Trim();
        }

    }
}
