using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.BDHandler
{
    /// <summary>
    /// Generic container to apply the singleton pattern to types
    /// </summary>
    /// <typeparam name="T">class type</typeparam>
    public class SingletonProvider<T> where T : new()
    {

        SingletonProvider() { }

        /// <summary>
        /// Gets the singleton instance of &lt;T&gt;.
        /// </summary>
        /// <value>The instance.</value>
        public static T Instance
        {
            get { return SingletonCreator.instance; }
        }

        /// <summary>
        /// Same as <see cref="SingletonProvider&lt;T&gt;.Instance"/>
        /// </summary>
        /// <returns></returns>
        public static T GetInstance()
        {
            return Instance;
        }

        class SingletonCreator
        {
            static SingletonCreator() { }

            internal static readonly T instance = new T();
        }
    }
}
