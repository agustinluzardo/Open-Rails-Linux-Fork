// COPYRIGHT 2026 by the Riel project. GPL-3.0-or-later.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;

namespace Riel.Launcher
{
    /// <summary>Installs only a tested, checksummed portable build from the main release channel.</summary>
    internal static class Updates
    {
        private const string MainBranch = "https://api.github.com/repos/agustinluzardo/Open-Rails-Linux-Fork/branches/main";
        private const string ReleaseByTag = "https://api.github.com/repos/agustinluzardo/Open-Rails-Linux-Fork/releases/tags/";
        private const string ArchiveName = "riel-linux-x64.zip";

        internal static async Task<int> Run(bool checkOnly, CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            if (!File.Exists(Path.Combine(root, "riel")) || !File.Exists(Path.Combine(root, "app", "riel")))
                throw new LauncherException("Updates require the extracted portable riel-linux-x64 folder.");

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Riel-Updater/1.0");
            // Follow the branch itself, not "most recently published" prereleases.
            // Rollbacks and rebuilt commits can make release timestamps differ from
            // main's actual history, which previously made the launcher offer a stale
            // build or miss the current one entirely.
            using JsonDocument branch = JsonDocument.Parse(await client.GetStringAsync(MainBranch, cancellationToken).ConfigureAwait(false));
            string mainCommit = branch.RootElement.GetProperty("commit").GetProperty("sha").GetString();
            if (string.IsNullOrWhiteSpace(mainCommit) || mainCommit.Length < 12)
                throw new LauncherException("GitHub did not return a valid main commit.");

            string commit = mainCommit.Substring(0, 12);
            string tag = "main-" + commit;
            using HttpResponseMessage releaseResponse = await client.GetAsync(ReleaseByTag + tag, cancellationToken).ConfigureAwait(false);
            if (releaseResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine("No tested Riel build has been published for current main (" + commit + ") yet.");
                return 0;
            }
            releaseResponse.EnsureSuccessStatusCode();
            using JsonDocument releaseDocument = JsonDocument.Parse(
                await releaseResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            JsonElement release = releaseDocument.RootElement;
            if (release.GetProperty("draft").GetBoolean())
                throw new LauncherException("The current main build is still a draft.");
            if (commit.StartsWith(VersionInfo.CodeVersion, StringComparison.OrdinalIgnoreCase))
            {
                string oldStage = Path.Combine(root, ".riel-update-stage");
                if (Directory.Exists(oldStage))
                    Directory.Delete(oldStage, recursive: true);
                Console.WriteLine("Riel is up to date (" + commit + ").");
                return 0;
            }
            // A just-built local main commit can be newer than the last published
            // release. GitHub's ancestry check prevents silently downgrading it.
            try
            {
                string compareUrl = "https://api.github.com/repos/agustinluzardo/Open-Rails-Linux-Fork/compare/" +
                    VersionInfo.CodeVersion + "..." + commit;
                using JsonDocument comparison = JsonDocument.Parse(await client.GetStringAsync(compareUrl, cancellationToken).ConfigureAwait(false));
                if (comparison.RootElement.GetProperty("status").GetString() != "ahead")
                {
                    Console.WriteLine("Riel is up to date (" + VersionInfo.CodeVersion + ").");
                    return 0;
                }
            }
            catch (HttpRequestException)
            {
                // A custom build may not exist in this repository; the release is
                // still offered explicitly, after verification, to its user.
            }
            Console.WriteLine("Available Riel main build: " + commit + " (installed: " + VersionInfo.CodeVersion + ").");
            if (checkOnly)
                return 10; // GUI uses this to distinguish available from up to date.

            JsonElement assets = release.GetProperty("assets");
            string archiveUrl = AssetUrl(assets, ArchiveName);
            string checksumUrl = AssetUrl(assets, ArchiveName + ".sha256");
            string checksum = (await client.GetStringAsync(checksumUrl, cancellationToken).ConfigureAwait(false)).Split(' ', '\n')[0];
            if (checksum.Length != 64 || !checksum.All(Uri.IsHexDigit))
                throw new LauncherException("Invalid SHA-256 checksum in release.");

            string stage = Path.Combine(root, ".riel-update-stage");
            if (Directory.Exists(stage))
                Directory.Delete(stage, recursive: true);
            Directory.CreateDirectory(stage);
            string archive = Path.Combine(stage, ArchiveName);
            try
            {
                using (HttpResponseMessage response = await client.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 400_000_000)
                        throw new LauncherException("Update archive exceeds 400 MB.");
                    await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using FileStream output = File.Create(archive);
                    byte[] buffer = new byte[131072];
                    long received = 0;
                    int lastPercent = -1;
                    int count;
                    while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        received += count;
                        if (received > 400_000_000)
                            throw new LauncherException("Update archive exceeds 400 MB.");
                        await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                        if (response.Content.Headers.ContentLength is long length && length > 0)
                        {
                            int percent = (int)(received * 100 / length);
                            if (percent / 5 != lastPercent / 5)
                            {
                                Console.WriteLine("PROGRESS " + percent);
                                lastPercent = percent;
                            }
                        }
                    }
                }
                await using (FileStream stream = File.OpenRead(archive))
                {
                    string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                    if (!actual.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                        throw new LauncherException("Downloaded update failed SHA-256 verification.");
                }
                ZipFile.ExtractToDirectory(archive, stage);
                if (!File.Exists(Path.Combine(stage, "riel-linux-x64", "app", "riel")))
                    throw new LauncherException("Update archive does not contain the Riel launcher.");
                foreach (string executable in new[] { "riel", "riel-gui", "ActivityRunner", "MultiPlayer.Hub" })
                {
                    string file = Path.Combine(stage, "riel-linux-x64", "app", executable);
                    if (File.Exists(file))
                        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                File.Delete(archive);
                Console.WriteLine("Verified update staged. Finishing installation...");
                return 0;
            }
            catch
            {
                Directory.Delete(stage, recursive: true);
                throw;
            }
        }

        private static string AssetUrl(JsonElement assets, string name)
        {
            foreach (JsonElement asset in assets.EnumerateArray())
                if (asset.GetProperty("name").GetString() == name)
                    return asset.GetProperty("browser_download_url").GetString();
            throw new LauncherException("Release is missing " + name + ".");
        }
    }
}
