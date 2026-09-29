// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Riel.Launcher;

namespace Riel.Launcher.Gui
{
    /// <summary>
    /// Starts the native route editor with the same content root and route selected in Riel.
    /// The packaged editor is based on TSRE5vc and accepts --game-root and --route directly.
    /// </summary>
    internal static class RouteEditorLauncher
    {
        private const string OverrideEnvironmentVariable = "RIEL_ROUTE_EDITOR";
        private const string ExecutableName = "riel-route-editor";

        public static Process Start(RouteItem route)
        {
            ArgumentNullException.ThrowIfNull(route);

            string executable = FindExecutable()
                ?? throw new LauncherException(
                    "Riel Route Editor is not installed in this build. Update Riel or set RIEL_ROUTE_EDITOR to a native editor executable.");

            string contentRoot = Path.GetFullPath(route.Folder.ContentPath);
            if (!Directory.Exists(contentRoot))
                throw new LauncherException($"the content folder no longer exists: {contentRoot}");

            if (string.IsNullOrWhiteSpace(route.Route.Id))
                throw new LauncherException("the selected route has no route id");

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
            };
            start.ArgumentList.Add("--game-root");
            start.ArgumentList.Add(contentRoot);
            start.ArgumentList.Add("--route");
            start.ArgumentList.Add(route.Route.Id);
            // Keep editor preferences in the user's application-data profile instead of
            // writing settings into /opt/riel or the extracted portable directory.
            start.ArgumentList.Add("--appdata-profile");

            return Process.Start(start)
                ?? throw new LauncherException("Riel Route Editor could not be started");
        }

        private static string FindExecutable()
        {
            string overridePath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                string expanded = Path.GetFullPath(Environment.ExpandEnvironmentVariables(overridePath));
                if (File.Exists(expanded))
                    return expanded;
            }

            foreach (string candidate in PackagedCandidates())
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(directory, ExecutableName);
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // Ignore malformed PATH entries and continue looking.
                }
            }

            return null;
        }

        private static IEnumerable<string> PackagedCandidates()
        {
            yield return Path.Combine(AppContext.BaseDirectory, "route-editor", ExecutableName);
            yield return Path.Combine(AppContext.BaseDirectory, ExecutableName);
            yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "route-editor", ExecutableName));
        }
    }
}
