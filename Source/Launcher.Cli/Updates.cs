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
        private const string Releases = "https://api.github.com/repos/agustinluzardo/Open-Rails-Linux-Fork/releases?per_page=100";
        private const string ArchiveName = "riel-linux-x64.zip";
        // Last release on the FTS history, before main moved to native Open Rails.
        private const string LegacyMainTip = "c19a19e2b5d5230e0acb460e1cbf0a63a4863edc";

        internal static async Task<int> Run(bool checkOnly, CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            if (!File.Exists(Path.Combine(root, "riel")) || !File.Exists(Path.Combine(root, "app", "riel")))
                throw new LauncherException("Updates require the extracted portable riel-linux-x64 folder.");

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Riel-Updater/1.0");
            using JsonDocument releases = JsonDocument.Parse(await client.GetStringAsync(Releases, cancellationToken).ConfigureAwait(false));
            // GitHub's release list can use the tag's creation time, which need
            // not match publishing order for commits authored outside GitHub.
            JsonElement release = releases.RootElement.EnumerateArray()
                .Where(item => !item.GetProperty("draft").GetBoolean() &&
                    item.GetProperty("tag_name").GetString().StartsWith("main-", StringComparison.Ordinal))
                .OrderByDescending(item => item.GetProperty("published_at").GetDateTimeOffset())
                .FirstOrDefault();
            if (release.ValueKind == JsonValueKind.Undefined)
                throw new LauncherException("No tested main release is available yet.");

            string tag = release.GetProperty("tag_name").GetString();
            string commit = tag.Substring("main-".Length);
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
                string status = comparison.RootElement.GetProperty("status").GetString();
                bool legacyMigration = false;
                if (status == "diverged")
                {
                    // The upstream rebase made every installed FTS build diverge
                    // from native main. Only migrate versions on that old history;
                    // a different local or newer native build must not be downgraded.
                    string legacyUrl = "https://api.github.com/repos/agustinluzardo/Open-Rails-Linux-Fork/compare/" +
                        VersionInfo.CodeVersion + "..." + LegacyMainTip;
                    using JsonDocument legacy = JsonDocument.Parse(await client.GetStringAsync(legacyUrl, cancellationToken).ConfigureAwait(false));
                    string legacyStatus = legacy.RootElement.GetProperty("status").GetString();
                    legacyMigration = legacyStatus == "ahead" || legacyStatus == "identical";
                }
                if (status != "ahead" && !legacyMigration)
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
                foreach (string executable in new[] { "riel", "riel-gui", "RunActivity", "MultiPlayer.Hub" })
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
