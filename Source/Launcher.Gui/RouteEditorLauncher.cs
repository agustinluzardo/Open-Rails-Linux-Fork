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
using Riel.Models.Settings;

namespace Riel.Launcher.Gui
{
    internal enum RielEditorTool
    {
        RouteEditor,
        ConsistEditor,
        ShapeViewer,
        AceConverter,
    }

    internal static class RielEditorToolExtensions
    {
        public static bool RequiresContent(this RielEditorTool tool) => tool != RielEditorTool.AceConverter;

        public static string DisplayName(this RielEditorTool tool) => tool switch
        {
            RielEditorTool.RouteEditor => "Riel Route Editor",
            RielEditorTool.ConsistEditor => "Riel Consist Editor",
            RielEditorTool.ShapeViewer => "Riel Shape Viewer",
            RielEditorTool.AceConverter => "Riel ACE Converter",
            _ => "Riel Editor",
        };
    }

    internal static class RouteEditorLauncher
    {
        private const string OverrideEnvironmentVariable = "RIEL_ROUTE_EDITOR";
        private const string ExecutableName = "riel-route-editor";

        public static Process Start(RielEditorTool tool, RouteItem route, ProfileUserSettingsModel settings)
        {
            string executable = FindExecutable(out bool shellWrapper)
                ?? throw new LauncherException(
                    "Riel editor tools are not installed in this build. Update Riel or set RIEL_ROUTE_EDITOR to a native editor executable.");

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = shellWrapper ? "/bin/sh" : executable,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory,
            };
            if (shellWrapper)
                start.ArgumentList.Add(executable);

            if (settings?.TraceRouteEditorRendering == true)
                start.Environment["RIEL_EDITOR_RENDER_DIAGNOSTICS"] = "1";

            if (tool.RequiresContent())
            {
                if (route == null)
                    throw new LauncherException("choose a route before opening this editor");

                string contentRoot = Path.GetFullPath(route.Folder.ContentPath);
                if (!Directory.Exists(contentRoot))
                    throw new LauncherException("the content folder no longer exists: " + contentRoot);

                start.ArgumentList.Add("--game-root");
                start.ArgumentList.Add(contentRoot);

                if (tool == RielEditorTool.RouteEditor)
                {
                    if (string.IsNullOrWhiteSpace(route.Route.Id))
                        throw new LauncherException("the selected route has no route id");
                    start.ArgumentList.Add("--route");
                    start.ArgumentList.Add(route.Route.Id);
                }
            }

            switch (tool)
            {
                case RielEditorTool.RouteEditor:
                    break;
                case RielEditorTool.ConsistEditor:
                    start.ArgumentList.Add("--conedit");
                    break;
                case RielEditorTool.ShapeViewer:
                    start.ArgumentList.Add("--shapeview");
                    break;
                case RielEditorTool.AceConverter:
                    start.ArgumentList.Add("--aceconv");
                    break;
                default:
                    throw new LauncherException("unsupported Riel editor tool");
            }

            start.ArgumentList.Add("--appdata-profile");

            return Process.Start(start)
                ?? throw new LauncherException(tool.DisplayName() + " could not be started");
        }

        private static string FindExecutable(out bool shellWrapper)
        {
            shellWrapper = false;
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
                {
                    shellWrapper = OperatingSystem.IsLinux();
                    return candidate;
                }
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
