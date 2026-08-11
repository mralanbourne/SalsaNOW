using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalsaNOW
{
    internal class AutoPersist
    {
        public static async Task BackupDesktopRegistry(CancellationToken token, string globalDirectory)
        {
            string jsonUrlContent = await SalsaMirror.DownloadStringAsync("/ExplorerContents/jsons/RegistryList.json");

            string backupDir = Path.Combine(globalDirectory, "RegistryBackups");
            Directory.CreateDirectory(backupDir);

            using (WebClient client = new WebClient())
            {
                // Download config once
                string json = jsonUrlContent;

                RegistryBackupConfig config =
                    JsonConvert.DeserializeObject<RegistryBackupConfig>(json);

                if (config == null)
                    return;

                // Restore existing backups ONCE
                foreach (RegistryEntry entry in config.RegistryKeys)
                {
                    string file = Path.Combine(backupDir, entry.File);

                    if (!File.Exists(file))
                        continue;

                    try
                    {
                        SalsaLogger.Info("Restoring registry: " + entry.Key);

                        using (Process process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = $"import \"{file}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }))
                        {
                            process?.WaitForExit();
                        }
                    }
                    catch (Exception ex)
                    {
                        SalsaLogger.Error("Failed to restore " + entry.Key + ": " + ex.Message);
                    }
                }

                SalsaLogger.Info("Initial registry restore complete.");

                // Backup loop
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        foreach (RegistryEntry entry in config.RegistryKeys)
                        {
                            try
                            {
                                string file = Path.Combine(backupDir, entry.File);

                                using (Process process = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "reg.exe",
                                    Arguments = $"export \"{entry.Key}\" \"{file}\" /y",
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    WindowStyle = ProcessWindowStyle.Hidden
                                }))
                                {
                                    process?.WaitForExit();
                                }
                            }
                            catch (Exception ex)
                            {
                                SalsaLogger.Error("Failed to back up " + entry.Key + ": " + ex.Message);
                            }
                        }

                        await Task.Delay(
                            TimeSpan.FromSeconds(config.BackupIntervalSeconds),
                            token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        SalsaLogger.Error("Registry backup loop failed: " + ex.Message);

                        await Task.Delay(TimeSpan.FromSeconds(5), token);
                    }
                }
            }
        }

        public static async Task ApplyCustomRegistryFiles(string globalDirectory)
        {
            string json = await SalsaMirror.DownloadStringAsync("/ExplorerContents/jsons/RegistryFiles.json");

            string downloadDir = Path.Combine(globalDirectory, "RegistryFiles");
            Directory.CreateDirectory(downloadDir);
                var files =
                    JsonConvert.DeserializeObject<List<RegistryFile>>(json);

                if (files == null)
                    return;

                foreach (RegistryFile entry in files)
                {
                    string regFile = Path.Combine(
                        downloadDir,
                        Path.GetFileName(entry.File));

                    string mirrorPath = entry.Url;
                    int schemeIdx = mirrorPath.IndexOf("://");
                    if (schemeIdx >= 0) {
                        int pathIdx = mirrorPath.IndexOf("/", schemeIdx + 3);
                        if (pathIdx >= 0) mirrorPath = mirrorPath.Substring(pathIdx);
                    }
                    await SalsaMirror.DownloadFileAsync(mirrorPath, regFile);

                    using (Process process = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "reg.exe",
                            Arguments = "import \"" + regFile + "\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }))
                    {
                        process?.WaitForExit();
                    }
                }
        }
    }
}
