// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Riel is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Riel.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Globalization;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;

namespace Riel.Launcher.Gui
{
    /// <summary>A route as the list shows it: its name, and which folder it came from.</summary>
    public sealed class RouteItem
    {
        public RouteItem(FolderModel folder, RouteModelHeader route)
        {
            Folder = folder;
            Route = route;
        }

        public FolderModel Folder { get; }
        public RouteModelHeader Route { get; }
        public string Name => string.IsNullOrWhiteSpace(Route.Name) ? Route.Id : Route.Name;
        public string FolderName => Folder.Name;
    }

    /// <summary>An activity, with the facts that decide whether to play it.</summary>
    public sealed class ActivityItem
    {
        public ActivityItem(ActivityModelHeader activity)
        {
            Activity = activity;
        }

        public ActivityModelHeader Activity { get; }
        public string Name => string.IsNullOrWhiteSpace(Activity.Name) ? Activity.Id : Activity.Name;

        /// <summary>"08:30 · Summer · Clear · Easy · 1 h 20 min"</summary>
        public string Summary
        {
            get
            {
                string summary = string.Join("  ·  ",
                    Activity.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    Names.Season(Activity.Season),
                    Names.Weather(Activity.Weather),
                    Names.Difficulty(Activity.Difficulty));
                return Activity.Duration > TimeSpan.Zero ? $"{summary}  ·  {Names.Duration(Activity.Duration)}" : summary;
            }
        }

        public string Description
        {
            get
            {
                string description = Activity.Description?.Trim();
                string briefing = Activity.Briefing?.Trim();
                if (string.IsNullOrEmpty(briefing) || string.Equals(briefing, description, StringComparison.Ordinal))
                    return description ?? string.Empty;
                return string.IsNullOrEmpty(description) ? briefing : $"{description}\n\n{briefing}";
            }
        }
    }

    /// <summary>A path to explore along: its name and where it runs.</summary>
    public sealed class PathItem
    {
        public PathItem(PathModelHeader path)
        {
            Path = path;
        }

        public PathModelHeader Path { get; }

        public string Name
        {
            get
            {
                string name = string.IsNullOrWhiteSpace(Path.Name) ? Path.Id : Path.Name;
                return string.IsNullOrWhiteSpace(Path.Start) || string.IsNullOrWhiteSpace(Path.End)
                    ? name
                    : $"{name}   ({Path.Start} → {Path.End})";
            }
        }

        public override string ToString() => Name;
    }

    /// <summary>A consist to explore with.</summary>
    public sealed class ConsistItem
    {
        public ConsistItem(WagonSetModel consist)
        {
            Consist = consist;
        }

        public WagonSetModel Consist { get; }
        public string Name => string.IsNullOrWhiteSpace(Consist.Name) ? Consist.Id : Consist.Name;
        public string LocomotiveReference => Consist.Locomotive?.Reference;
        public string LocomotiveName => string.IsNullOrWhiteSpace(Consist.Locomotive?.Name)
            ? Consist.Locomotive?.Reference
            : Consist.Locomotive.Name;
        public string Detail => Translation.Count(Consist.TrainCars.Length, "{0} vehicle", "{0} vehicles");
    }

    /// <summary>A choice in a drop down that stands for an enum value.</summary>
    public sealed class TimetableItem
    {
        public TimetableItem(TimetableModel model) => Model = model;
        public TimetableModel Model { get; }
        public override string ToString() => string.IsNullOrWhiteSpace(Model.Name) ? Model.Id : Model.Name;
    }

    public sealed class TimetableTrainItem
    {
        public TimetableTrainItem(TimetableTrainModel model) => Model = model;
        public TimetableTrainModel Model { get; }
        public string Name => Model.Name;
        public string Summary => $"{Model.Group} · {Model.StartTime:HH:mm} · {Model.WagonSet} · {Model.Path}";
    }

    public sealed class WeatherFileItem
    {
        public WeatherFileItem(WeatherModelHeader model) => Model = model;
        public WeatherModelHeader Model { get; }
        public override string ToString() => Model == null ? Translation.T("Default weather") :
            string.IsNullOrWhiteSpace(Model.Name) ? Model.Id : Model.Name;
    }

    /// <summary>A choice in a drop down that stands for an enum value.</summary>
    public sealed class Choice<T>
    {
        public Choice(T value, string name)
        {
            Value = value;
            Name = name;
        }

        public T Value { get; }
        public string Name { get; }
        public override string ToString() => Name;
    }

    /// <summary>The words for the enums the launcher shows, in the user's language.</summary>
    internal static class Names
    {
        public static string Season(SeasonType season) => season switch
        {
            SeasonType.Spring => Translation.T("Spring"),
            SeasonType.Summer => Translation.T("Summer"),
            SeasonType.Autumn => Translation.T("Autumn"),
            SeasonType.Winter => Translation.T("Winter"),
            _ => season.ToString(),
        };

        public static string Weather(WeatherType weather) => weather switch
        {
            WeatherType.Clear => Translation.T("Clear"),
            WeatherType.Rain => Translation.T("Rain"),
            WeatherType.Snow => Translation.T("Snow"),
            _ => weather.ToString(),
        };

        public static string Difficulty(Difficulty difficulty) => difficulty switch
        {
            FreeTrainSimulator.Common.Difficulty.Easy => Translation.T("Easy"),
            FreeTrainSimulator.Common.Difficulty.Medium => Translation.T("Medium"),
            FreeTrainSimulator.Common.Difficulty.Hard => Translation.T("Hard"),
            _ => difficulty.ToString(),
        };

        public static string Duration(TimeSpan duration)
        {
            return duration.TotalHours >= 1
                ? Translation.F("{0} h {1} min", (int)duration.TotalHours, duration.Minutes)
                : Translation.F("{0} min", (int)Math.Round(duration.TotalMinutes));
        }
    }
}
