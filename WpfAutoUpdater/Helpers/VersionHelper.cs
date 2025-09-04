
using System;
using System.Reflection;

namespace WpfAutoUpdater.Helpers
{
    public static class VersionHelper
    {
        public static string GetCurrentVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info)) return info;
            return asm.GetName().Version?.ToString() ?? "1.1.2";
        }
    }
}
