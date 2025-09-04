
using System;
using System.Reflection;

namespace WpfAutoUpdater.Helpers
{
    public static class VersionHelper
    {
        public static string GetCurrentVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            //var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            //if (!string.IsNullOrWhiteSpace(info)) return info;
            var version = asm.GetName().Version?.ToString();
            return version ?? "1.1.9";
        }
    }
}
