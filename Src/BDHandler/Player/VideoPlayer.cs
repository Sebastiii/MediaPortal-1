using System;
using System.Collections.Generic;
using System.Globalization;
using DirectShowLib;
using DShowNET.Helper;
using MediaPortal.Player;
using MediaPortal.Plugins.BDHandler.Filters;
using MediaPortal.Profile;

namespace MediaPortal.Plugins.BDHandler.Player
{
    /// <summary>
    /// Specialized video player class that extends the native VMR9 player
    /// </summary>
    public class VideoPlayer : VideoPlayerVMR9
    {
        #region Member variables

        /// <summary>
        /// Dictionary that holds all available filter definitions
        /// </summary>
        protected Dictionary<Guid, ISelectFilter> filters;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoPlayer"/> class.
        /// </summary>
        public VideoPlayer()
            : this(g_Player.MediaType.Video)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoPlayer"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        public VideoPlayer(g_Player.MediaType type)
            : base(type)
        {
            // load available filters
            // todo: available filters should be instantiated using reflection?
            filters = new Dictionary<Guid, ISelectFilter>();
            ISelectFilter filter = SingletonProvider<MpcMpegSourceFilter>.GetInstance();
            filters[filter.ClassID] = filter;
        }

        #endregion

        #region Overrided methods

        /// <summary>
        /// Gets the audio language from the stream
        /// </summary>
        /// <param name="iStream">stream index</param>
        /// <returns>the localized audio language</returns>
        public override string AudioLanguage(int iStream)
        {
            string englishName = GetAudioLanguageFromStream(iStream);
            return Util.Utils.TranslateLanguageString(englishName);
        }

        /// <summary>
        /// Gets the audio type from the stream
        /// </summary>
        /// <param name="iStream">stream index</param>
        /// <returns></returns>
        public override string AudioType(int iStream)
        {
            FilterStreamInfos info = FStreams.GetStreamInfos(StreamType.Audio, iStream);

            ISelectFilter filter = GetFilterByStreamInfo(info);
            if (filter != null)
            {
                string type = filter.ParseAudioType(info.Name);
                BDHandlerCore.LogDebug("AudioType() Filter: {0}, IN: {1} OUT: {2}", filter.Name, info.Name, type);
                return type;
            }

            return base.AudioType(iStream); ;
        }

        /// <summary>
        /// Gets the subtitle language from the stream
        /// </summary>
        /// <param name="iStream"></param>
        /// <returns>the localized subtitle language</returns>
        public override string SubtitleLanguage(int iStream)
        {
            string englishName = GetSubtitleLanguageFromStream(iStream);
            return Util.Utils.TranslateLanguageString(englishName);
        }

        /// <summary>
        /// Gets the subtitle name from the stream.
        /// </summary>
        /// <param name="iStream">the subtitle name (description)</param>
        /// <returns></returns>
        public override string SubtitleName(int iStream)
        {
            FilterStreamInfos info = FStreams.GetStreamInfos(StreamType.Subtitle, iStream);

            ISelectFilter filter = GetFilterByStreamInfo(info);
            if (filter != null)
            {
                string name = filter.ParseSubtitleName(info.Name);
                BDHandlerCore.LogDebug("SubtitleName() Filter: {0}, IN: {1} OUT: {2}", filter.Name, info.Name, name);
                return name;
            }

            return base.SubtitleName(iStream);
        }

        /// <summary>
        /// Override for VMR9 OnInitialized with improved Subtitle & Audio Selection
        /// </summary>
        protected override void OnInitialized()
        {
            // AUDIO
            CultureInfo audioCulture = GetCultureInfoFromSettings("movieplayer", "audiolanguage");
            string selectedAudioLanguage = string.Empty;
            int audioStreams = AudioStreams;
            for (int i = 0; i < audioStreams; i++)
            {
                string language = GetAudioLanguageFromStream(i);
                if (audioCulture.Matches(language))
                {
                    CurrentAudioStream = i;
                    selectedAudioLanguage = language;
                    BDHandlerCore.LogInfo("Selected active audio track language: {0} ({1})", audioCulture.EnglishName, i);
                    break;
                }
            }

            // SUBTITLES
            CultureInfo subtitleCulture = GetCultureInfoFromSettings("subtitles", "language");
            int subtitleStreams = SubtitleStreams;

            for (int i = 0; i < subtitleStreams; i++)
            {
                string language = GetSubtitleLanguageFromStream(i);
                if (subtitleCulture.Matches(language))
                {
                    // set the current subtitle stream
                    CurrentSubtitleStream = i;

                    if (selectedAudioLanguage != string.Empty && subtitleCulture.Matches(selectedAudioLanguage))
                    {
                        // the languages of both audio and subtitles are the same so there's no need to show the subtitle
                        // todo: evaluate through user experience
                        EnableSubtitle = false;
                        BDHandlerCore.LogDebug("Disabling subtitles because the audio language is the same.");
                    }
                    else
                    {
                        // enable the subtitles
                        BDHandlerCore.LogInfo("Selected active subtitle track language: {0} ({1})", subtitleCulture.EnglishName, i);
                        EnableSubtitle = true;
                    }
                    break;
                }
            }

            // call the base OnInitialized method
            base.OnInitialized();
        }

        /// <summary>
        /// Build graph
        /// </summary>
        /// <returns></returns>
        protected override bool GetInterfaces()
        {
            if (UseCustomGraph())
            {
                return this.RenderCustomGraph();
            }

            return base.GetInterfaces();
        }

        #endregion

        #region Extended methods

        /// <summary>
        /// Specifies if custom graph should be used.
        /// </summary>
        /// <returns></returns>
        protected virtual bool UseCustomGraph()
        {
            return false;
        }

        /// <summary>
        /// Renders a custom graph for this player
        /// </summary>
        /// <returns></returns>
        protected virtual bool RenderCustomGraph()
        {
            return false;
        }

        /// <summary>
        /// Gets the ISelectFilter interface (if existing) for this FilterStreamInfo object
        /// </summary>
        /// <param name="info">FilterStreamInfos struct</param>
        /// <returns></returns>
        protected virtual ISelectFilter GetFilterByStreamInfo(FilterStreamInfos info)
        {
            Guid guid = GetFilterGuidByStreamInfo(info);
            ISelectFilter filter = null;
            if (filters.TryGetValue(guid, out filter)) 
            {
                return filter;
            }
            
            return null;
        }

        /// <summary>
        /// Gets the filter GUID by stream info.
        /// </summary>
        /// <param name="info">FilterStreamInfos struct</param>
        /// <returns></returns>
        protected virtual Guid GetFilterGuidByStreamInfo(FilterStreamInfos info)
        {
            Guid guid = Guid.Empty;

            IBaseFilter foundfilter = DirectShowUtil.GetFilterByName(graphBuilder, info.Filter);
            if (foundfilter != null)
            {
                foundfilter.GetClassID(out guid);

                BDHandlerCore.LogDebug("GetFilterGuidByStreamInfo() Filter: {0}, GUID={1}", info.Filter, guid.ToString());

                // release object
                DirectShowUtil.ReleaseComObject(foundfilter);
            }

            return guid;
        }

        /// <summary>
        /// Gets the culture info from configurationsettings.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="entry">The entry name.</param>
        /// <returns></returns>
        protected virtual CultureInfo GetCultureInfoFromSettings(string section, string entry)
        {
            CultureInfo culture = null;
            using (Settings xmlreader = new MPSettings())
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(xmlreader.GetValueAsString(section, entry, defaultLanguageCulture));
                }
                catch (Exception ex)
                {
                    culture = CultureInfo.GetCultureInfo(defaultLanguageCulture);
                    BDHandlerCore.LogError("unable to build CultureInfo - {0}", ex);
                }
            }

            return culture;
        }

        /// <summary>
        /// Gets the audio language from the stream
        /// </summary>
        /// <param name="iStream">stream index</param>
        /// <returns>language string formatted as english name</returns>
        protected virtual string GetAudioLanguageFromStream(int iStream)
        {
            FilterStreamInfos info = FStreams.GetStreamInfos(StreamType.Audio, iStream);

            if (info.LCID > 0)
            {
                string lang = GetEnglishNameByLCID(info.LCID);
                BDHandlerCore.LogDebug("AudioLanguage() LCID: {0}, OUT: {1}", info.LCID, lang);
            }

            ISelectFilter filter = GetFilterByStreamInfo(info);
            if (filter != null)
            {
                string language = filter.ParseAudioLanguage(info.Name);
                BDHandlerCore.LogDebug("AudioLanguage() Filter: {0}, IN: {1} OUT: {2}", filter.Name, info.Name, language);
                return language;
            }

            // if we made it this far just do the native dance
            return base.AudioLanguage(iStream);
        }

        /// <summary>
        /// Gets the subtitle language from the stream
        /// </summary>
        /// <param name="iStream">the stream index</param>
        /// <returns>language string formatted as english name</returns>
        protected virtual string GetSubtitleLanguageFromStream(int iStream)
        {
            FilterStreamInfos info = FStreams.GetStreamInfos(StreamType.Subtitle, iStream);

            if (info.LCID > 0)
            {
                return GetEnglishNameByLCID(info.LCID);
            }

            ISelectFilter filter = GetFilterByStreamInfo(info);
            if (filter != null)
            {
                string language = filter.ParseSubtitleLanguage(info.Name);
                BDHandlerCore.LogDebug("SubtitleLanguage() Filter: {0}, IN: {1} OUT: {2}", filter.Name, info.Name, language);
                return language;
            }

            return base.SubtitleLanguage(iStream);
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Gets the english name by LCID.
        /// </summary>
        /// <param name="lcid">The lcid.</param>
        /// <returns></returns>
        public static string GetEnglishNameByLCID(int lcid)
        {
            return CultureInfo.GetCultureInfo(lcid).EnglishName.Split('(')[0].Trim();
        }

        #endregion
    }
}
