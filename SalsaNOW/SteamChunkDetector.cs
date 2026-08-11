using System;
using System.IO;
using System.Linq;

namespace SalsaNOW
{
    internal static class SteamChunkDetector
    {
        private static readonly string STEAM_UI_DIR = @"C:\Program Files (x86)\Steam\steamui";

        public static string DetectChunk()
        {
            if (!Directory.Exists(STEAM_UI_DIR))
                return null;

            var chunks = Directory.GetFiles(STEAM_UI_DIR, "chunk~*.js")
                .OrderByDescending(f => new FileInfo(f).Length)
                .ToList();

            if (chunks.Count == 0)
            {
                var allJs = Directory.GetFiles(STEAM_UI_DIR, "*.js")
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .ToList();

                if (allJs.Count > 0)
                    return Path.GetFileName(allJs[0]);
                return null;
            }

            string knownChunk = "chunk~2dcc5aaf7.js";
            string knownPath = Path.Combine(STEAM_UI_DIR, knownChunk);
            if (File.Exists(knownPath))
                return knownChunk;

            foreach (var chunk in chunks)
            {
                try
                {
                    string content = File.ReadAllText(chunk);
                    if (content.Contains("geforce") ||
                        content.Contains("GeForce") ||
                        content.Contains("GFN") ||
                        content.Contains("cloudgaming") ||
                        (content.Contains("filter") && content.Contains("library")))
                    {
                        return Path.GetFileName(chunk);
                    }
                }
                catch { }
            }

            if (chunks.Count > 0)
                return Path.GetFileName(chunks[0]);

            return null;
        }

        public static string GetRandomMasqueradeName()
        {
            string[] templates = {
                "winlogsrv", "dcomlaunch", "svcrouter", "wininitexe",
                "wbemcore", "svcshare", "wincfg", "syshealth",
                "winsrvmgr", "taskbridge", "winsysmon", "winaux",
            };

            var rnd = new Random(Guid.NewGuid().GetHashCode());
            return templates[rnd.Next(templates.Length)] + ".exe";
        }

        public static string GetBackupDirName()
        {
            return "steamui.bak";
        }
    }
}
