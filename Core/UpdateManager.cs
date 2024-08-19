using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System.IO.Compression;

namespace Core
{
    public class UpdateManager : IDisposable
    {
        private readonly HttpClient client;

        public Version NewVersion { get; private set; }
        public string UpdateUrl { get; private set; }

        public string MainAppDir { get; private set; }
        private string ScriptPath => Path.Combine(MainAppDir, "UpdateScript.ps1");

        public UpdateManager(string? installDir = null)
        {
            MainAppDir = installDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            client = new HttpClient();
        }

        public async Task<bool> CheckForUpdate(Version? currentVersion, string repoOwner, string repoName)
        {
            try
            {
                if (File.Exists(ScriptPath))
                    File.Delete(ScriptPath);

                using GithubManager githubManager = new();
                var (latestVersion, latestZipUrl) = await githubManager.GetLatestReleaseInfo(repoOwner, repoName);
                UpdateUrl = latestZipUrl;
                if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(latestZipUrl))
                {
                    return false;
                }

                var latest = Version.Parse(latestVersion);
                NewVersion = latest;
                if (currentVersion != null && latest <= currentVersion)
                {
                    return false;
                }

                
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public async Task DoUpdate(IProgress<double>? progressCallback = null, IEnumerable<string>? filesToIgnore = null)
        {
            string latestZipUrl = UpdateUrl;
            // Download the newest release of Aimmy to %temp%
            string tempPath = Path.GetTempPath();
            string localZipPath = Path.Combine(tempPath, "AimmyUpdate.zip");
            string extractPath = Path.Combine(tempPath, "AimmyUpdate");

            await DownloadZipAsync(latestZipUrl, localZipPath, progressCallback);
            await ExtractZipAsync(localZipPath, extractPath);


            //CreateUpdateBatchScript(mainAppDir, extractPath, localZipPath, filesToIgnore);
            CreateUpdatePowerShellScript(extractPath, localZipPath, filesToIgnore);
            StartUpdateProcess();
        }

        private async Task DownloadZipAsync(string url, string destinationPath, IProgress<double>? progressCallback)
        {
            using var response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            long totalRead = 0L;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                progressCallback?.Report((double)totalRead / totalBytes * 100);
            }
        }

        private async Task ExtractZipAsync(string zipPath, string destinationPath)
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, destinationPath, true));
        }

        private void CreateUpdatePowerShellScript(string extractPath, string zipPath, IEnumerable<string>? filesToIgnore)
        {
            using (StreamWriter sw = new(ScriptPath))
            {
                sw.WriteLine("Add-Type -AssemblyName PresentationFramework");
                sw.WriteLine("$window = New-Object system.windows.window");
                sw.WriteLine("$window.Title = 'Update in Progress'");
                sw.WriteLine("$window.Height = 150");
                sw.WriteLine("$window.Width = 400");
                sw.WriteLine("$window.WindowStartupLocation = 'CenterScreen'");
                sw.WriteLine("$window.Topmost = $true");

                sw.WriteLine("$grid = New-Object system.windows.controls.grid");
                sw.WriteLine("$window.Content = $grid");

                sw.WriteLine("$statusLabel = New-Object system.windows.controls.label");
                sw.WriteLine("$statusLabel.HorizontalAlignment = 'Center'");
                sw.WriteLine("$statusLabel.VerticalAlignment = 'Top'");
                sw.WriteLine("$statusLabel.Margin = '10'");
                sw.WriteLine("$grid.Children.Add($statusLabel)");

                sw.WriteLine("$progressBar = New-Object system.windows.controls.progressbar");
                sw.WriteLine("$progressBar.HorizontalAlignment = 'Stretch'");
                sw.WriteLine("$progressBar.VerticalAlignment = 'Top'");
                sw.WriteLine("$progressBar.Height = 20");
                sw.WriteLine("$progressBar.Margin = '10,40,10,10'");
                sw.WriteLine("$progressBar.Minimum = 0");
                sw.WriteLine("$progressBar.Maximum = 100");
                sw.WriteLine("$grid.Children.Add($progressBar)");

                sw.WriteLine("$window.Show()");

                // Dateien im Hauptverzeichnis löschen
                sw.WriteLine("$statusLabel.Content = 'Deleting old files...'");
                sw.WriteLine("[System.Windows.Forms.Application]::DoEvents()");
                sw.WriteLine($"Get-ChildItem -Path \"{MainAppDir}\" -File | ForEach-Object {{ Remove-Item $_.FullName -Force }}");

                string[] allFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
                int fileCount = allFiles.Length;

                sw.WriteLine($"$total = {fileCount}");
                sw.WriteLine("$counter = 0");

                foreach (string file in allFiles)
                {
                    string relativePath = file.Substring(extractPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    if (filesToIgnore == null || !ShouldIgnoreFile(relativePath, filesToIgnore))
                    {
                        string destinationFile = Path.Combine(MainAppDir, relativePath);
                        string destinationDir = Path.GetDirectoryName(destinationFile)!;
                        if (!Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        sw.WriteLine("$statusLabel.Content = 'Copying: " + relativePath.Replace("'", "''") + "'");
                        sw.WriteLine("[System.Windows.Forms.Application]::DoEvents()");

                        sw.WriteLine($"Copy-Item -Path \"{file}\" -Destination \"{destinationFile}\" -Force");

                        sw.WriteLine("$counter++");
                        sw.WriteLine("$progressBar.Value = ($counter / $total) * 100");
                    }
                }

                sw.WriteLine("$statusLabel.Content = 'Update Complete!'");
                sw.WriteLine("$progressBar.Value = 100");

                sw.WriteLine("Start-Sleep -Seconds 2");

                // Arbeitsverzeichnis setzen und Launcher.exe starten
                sw.WriteLine($"Set-Location \"{MainAppDir}\"");
                sw.WriteLine($"Start-Process -FilePath \"Launcher.exe\" -WorkingDirectory \"{MainAppDir}\"");

                sw.WriteLine($"Remove-Item -Force \"{zipPath}\"");
                sw.WriteLine($"Remove-Item -Recurse -Force \"{extractPath}\"");
                sw.WriteLine($"Remove-Item -Force \"{ScriptPath}\"");

                sw.WriteLine("$window.Close()");
            }
        }
        

        private bool ShouldIgnoreFile(string relativePath, IEnumerable<string> filesToIgnore)
        {
            foreach (string ignorePattern in filesToIgnore)
            {
                if (relativePath.Equals(ignorePattern.TrimStart('/'), StringComparison.OrdinalIgnoreCase) ||
                    relativePath.StartsWith(ignorePattern.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void StartUpdateProcess()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("powershell", $"-ExecutionPolicy Bypass -File \"{ScriptPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            Process.Start(startInfo);
            Environment.Exit(0);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
