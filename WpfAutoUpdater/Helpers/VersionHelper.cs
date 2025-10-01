
using System;
using System.Reflection;

namespace WpfAutoUpdater.Helpers
{
    public static class VersionHelper
    {
        public static string GetCurrentVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = asm.GetName().Version?.ToString();
            return version ?? "Error!";
        }
    }
}

// To create a new version tag and push it to GitHub, use the following commands:
//git tag v1.2.1
//>> git push origin v1.2.1