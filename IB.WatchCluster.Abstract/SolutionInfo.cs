using System;
using System.Reflection;

namespace IB.WatchCluster.Abstract
{
    /// <summary>
    /// Helper class to get assembly and solution level info
    /// </summary>
    public static class SolutionInfo
    {
        private static readonly Lazy<string> _version = new(
            () => typeof(SolutionInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        /// <summary>
        /// Application version info
        /// </summary>
        public static string Version => _version.Value;

        /// <summary>
        /// Assembly name
        /// </summary>
        public static string Name => Assembly.GetEntryAssembly()?.GetName().Name;

        /// <summary>
        /// Assembly itself
        /// </summary>
        public static readonly Assembly Assembly = typeof(SolutionInfo).Assembly;
    }
}
