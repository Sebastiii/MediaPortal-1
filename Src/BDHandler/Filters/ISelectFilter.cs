namespace MediaPortal.Plugins.BDHandler.Filters
{
    /// <summary>
    /// Common interface for stream select filters
    /// </summary>
    public interface ISelectFilter : IFilter
    {
        /// <summary>
        /// Parses the subtitle language.
        /// </summary>
        /// <param name="input">raw string returned by the filter</param>
        /// <returns>language string in usable format</returns>
        string ParseSubtitleLanguage(string input);

        /// <summary>
        /// Parses the name of the subtitle.
        /// </summary>
        /// <param name="input">raw string returned by the filter</param>
        /// <returns>name of of the subtitle</returns>
        string ParseSubtitleName(string input);

        /// <summary>
        /// Parses the type of the audio.
        /// </summary>
        /// <param name="input">raw string returned by the filter</param>
        /// <returns>audio type string in usable format</returns>
        string ParseAudioType(string input);

        /// <summary>
        /// Parses the audio language.
        /// </summary>
        /// <param name="input">raw string returned by the filter</param>
        /// <returns>language string in usable format</returns>
        string ParseAudioLanguage(string input);

    }
}
