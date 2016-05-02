using System;

namespace MediaPortal.Plugins.BDHandler.Filters
{
    /// <summary>
    /// Common interface for filters
    /// </summary>
    public interface IFilter
    {
        /// <summary>
        /// Gets the friendly name of the filter.
        /// </summary>
        /// <value>a string representing the friendly name.</value>
        string Name { get; }

        /// <summary>
        /// Gets the ClassID of the filter.
        /// </summary>
        /// <value>a GUID representing the ClassID.</value>
        Guid ClassID { get; }

        /// <summary>
        /// Gets the recommended build number for this filter.
        /// </summary>
        /// <value>an integer representing the recommended build number.</value>
        int RecommendedBuildNumber { get; }

    }
}
