using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace MediaPortal.Plugins.BDHandler
{
    public static class Extensions
    {
        /// <summary>
        /// Determines whether the specified string is the same culture as the current System.Globalization.CultureInfo.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="lang">language string</param>
        /// <returns></returns>
        public static bool Matches(this CultureInfo self, string lang) {
            StringComparison comparison = StringComparison.OrdinalIgnoreCase;
            return (self.EnglishName.Equals(lang, comparison) || self.TwoLetterISOLanguageName.Equals(lang, comparison) || self.ThreeLetterISOLanguageName.Equals(lang, comparison) || self.ThreeLetterWindowsLanguageName.Equals(lang, comparison));
        }
    }
}
