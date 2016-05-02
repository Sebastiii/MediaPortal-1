using System;
using System.Text.RegularExpressions;

namespace MediaPortal.Plugins.BDHandler.Filters
{
    /// <summary>
    /// MPC - Mpeg Source (Gabest) Filter Class
    /// </summary>
    public class MpcMpegSourceFilter : ISelectFilter
    {
        static Regex audioStreamTextExpr = new Regex(@"^audio - (?<lang>[^,]+),(?<type>.+)(?<other>\(.+\))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex subtitleStreamTextExpr = new Regex(@"^subtitle - (?<lang>[^,]+),(?<name>.+)(?<other>\(.+\))$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        string IFilter.Name
        {
            get { return "MPC - Mpeg Source (Gabest)"; }
        }

        Guid IFilter.ClassID
        {
            get { return new Guid("{1365BE7A-C86A-473C-9A41-C0A6E82C9FA3}"); }
        }

        int IFilter.RecommendedBuildNumber
        {
            get { return 1287; }
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
