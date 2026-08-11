using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace SalsaNOW
{
    internal static class SalsaMirror
    {
        private static readonly string[] BOOTSTRAP_MIRRORS = new string[]
        {
            "https://salsanowfiles.work",
            "https://raw.githubusercontent.com/dpadGuy/SalsaNOW/main/mirror",
        };

        private static readonly byte[] REGISTRY_KEY = System.Text.Encoding.UTF8.GetBytes("SalsaNOW2026!");

        private static List<string> _activeMirrors = null;
        private static DateTime _lastRegistryFetch = DateTime.MinValue;
        private static readonly TimeSpan REGISTRY_TTL = TimeSpan.FromHours(6);

        private static readonly string[] USER_AGENTS = new string[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36",
        };

        private static readonly Random _rnd = new Random();

        private static string RandomUA()
        {
            return USER_AGENTS[_rnd.Next(USER_AGENTS.Length)];
        }

        public static async Task<List<string>> GetMirrorsAsync()
        {
            if (_activeMirrors != null && DateTime.Now - _lastRegistryFetch < REGISTRY_TTL)
                return _activeMirrors;

            foreach (var mirror in BOOTSTRAP_MIRRORS)
            {
                try
                {
                    var registry = await TryFetchRegistry(mirror);
                    if (registry != null && registry.Count > 0)
                    {
                        _activeMirrors = registry;
                        _lastRegistryFetch = DateTime.Now;
                        return _activeMirrors;
                    }
                }
                catch { }
            }

            _activeMirrors = BOOTSTRAP_MIRRORS.ToList();
            _lastRegistryFetch = DateTime.Now;
            return _activeMirrors;
        }

        private static async Task<List<string>> TryFetchRegistry(string mirror)
        {
            string url = mirror.TrimEnd('/') + "/SalsaRegistry.dat";
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = RandomUA();
                ServicePointManager.ServerCertificateValidationCallback = (s, c, h, e) => true;

                byte[] encoded;
                try { encoded = await wc.DownloadDataTaskAsync(new Uri(url)); }
                catch { return null; }

                byte[] decoded = new byte[encoded.Length];
                for (int i = 0; i < encoded.Length; i++)
                    decoded[i] = (byte)(encoded[i] ^ REGISTRY_KEY[i % REGISTRY_KEY.Length]);

                string json = System.Text.Encoding.UTF8.GetString(decoded);
                var mirrors = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);
                if (mirrors != null && mirrors.Count > 0)
                    return mirrors;
            }
            return null;
        }

        public static async Task<string> DownloadStringAsync(string path)
        {
            var mirrors = await GetMirrorsAsync();
            Exception lastError = null;

            foreach (var mirror in mirrors)
            {
                try
                {
                    string url = mirror.TrimEnd('/') + path;
                    using (var wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = RandomUA();
                        ServicePointManager.ServerCertificateValidationCallback = (s, c, h, e) => true;
                        return await wc.DownloadStringTaskAsync(new Uri(url));
                    }
                }
                catch (Exception ex) { lastError = ex; }
            }

            throw new Exception($"Failed: {path}: {lastError?.Message}");
        }

        public static async Task DownloadFileAsync(string path, string localPath)
        {
            var mirrors = await GetMirrorsAsync();
            Exception lastError = null;

            foreach (var mirror in mirrors)
            {
                try
                {
                    string url = mirror.TrimEnd('/') + path;
                    using (var wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = RandomUA();
                        ServicePointManager.ServerCertificateValidationCallback = (s, c, h, e) => true;
                        await wc.DownloadFileTaskAsync(new Uri(url), localPath);
                        return;
                    }
                }
                catch (Exception ex) { lastError = ex; }
            }

            throw new Exception($"Failed: {path}: {lastError?.Message}");
        }

        public static byte[] EncodeRegistry(string[] mirrors)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(mirrors);
            byte[] raw = System.Text.Encoding.UTF8.GetBytes(json);
            byte[] encoded = new byte[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                encoded[i] = (byte)(raw[i] ^ REGISTRY_KEY[i % REGISTRY_KEY.Length]);
            return encoded;
        }
    }
}
