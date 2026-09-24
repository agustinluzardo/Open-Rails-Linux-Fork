// COPYRIGHT 2012 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Orts.Simulation.Commanding
{
    /// <summary>
    /// User may specify an automatic pause in the replay at a time measured from the end of the replay.
    /// </summary>
    public enum ReplayPauseState
    {
        Before,
        Due,        // Set by CommandLog.Replay(), tested by Viewer.Update()
        During,
        Done
    };

    public class CommandLog
    {

        public Collection<ICommand> CommandList { get; private set; } = new Collection<ICommand>();
        public Simulator Simulator { get; set; }
        public bool ReplayComplete { get; set; }
        public double ReplayEndsAt { get; set; }
        public ReplayPauseState PauseState { get; set; }
        public bool CameraReplaySuspended { get; set; }

        private double completeTime;
        private DateTime? resumeTime;
        private const double completeDelayS = 2;

        /// <summary>
        /// Preferred constructor.
        /// </summary>
        public CommandLog(Simulator simulator)
        {
            Simulator = simulator;
        }

        /// <summary>
        /// When a command is created, it adds itself to the log.
        /// </summary>
        /// <param name="Command"></param>
        public void CommandAdd(ICommand command)
        {
            ArgumentNullException.ThrowIfNull(command, nameof(command));
            command.Time = Simulator.ClockTime; // Note time that command was issued
            CommandList.Add(command);
        }

        /// <summary>
        /// Replays any commands that have become due.
        /// Issues commands from the replayCommandList at the same time that they were originally issued.
        /// <para>
        /// Assumes replayCommandList is already sorted by time.
        /// </para>
        /// </summary>
        public void Update(Collection<ICommand> replayCommandList)
        {
            ArgumentNullException.ThrowIfNull(replayCommandList, nameof(replayCommandList));
            double elapsedTime = Simulator.ClockTime;

            if (PauseState == ReplayPauseState.Before)
            {
                if (elapsedTime > ReplayEndsAt - Simulator.UserSettings.ReplayPauseDuration)
                {
                    PauseState = ReplayPauseState.Due;  // For Viewer.Update() to detect and pause.
                }
            }

            if (replayCommandList.Count > 0)
            {
                var c = replayCommandList[0];
                // Without a small margin, an activity event can pause simulator just before the ResumeActicityCommand is due, 
                // so resume never happens.
                double margin = (Simulator.GamePaused) ? 0.5 : 0;   // margin of 0.5 seconds
                if (elapsedTime >= c.Time - margin)
                {
                    if (c is PausedCommand)
                    {
                        // Wait for the right duration and then action the command.
                        // ActivityCommands need dedicated code as the clock is no longer advancing.
                        if (resumeTime == null)
                        {
                            var resumeCommand = (PausedCommand)c;
                            resumeTime = DateTime.UtcNow.AddSeconds(resumeCommand.PauseDurationS);
                        }
                        else
                        {
                            if (DateTime.UtcNow >= resumeTime)
                            {
                                resumeTime = null;  // cancel trigger
                                ReplayCommand(elapsedTime, replayCommandList, c);
                            }
                        }
                    }
                    else
                    {
                        // When the player uses a camera command during replay, replay continues but any camera commands in the 
                        // replayCommandList are skipped until the player pauses and exit from the Quit Menu.
                        // This allows some editing of the camera during a replay.
                        if (!(c is CameraCommand && CameraReplaySuspended))
                        {
                            ReplayCommand(elapsedTime, replayCommandList, c);
                        }
                        completeTime = elapsedTime + completeDelayS;  // Postpone the time for "Replay complete" message
                    }
                }
            }
            else
            {
                if (completeTime != 0 && elapsedTime > completeTime)
                {
                    completeTime = 0;       // Reset trigger so this only happens once
                    ReplayComplete = true;  // Flag seen by Viewer3D which announces "Replay complete".
                }
            }
        }

        private void ReplayCommand(double elapsedTime, Collection<ICommand> replayCommandList, ICommand c)
        {
            c.Redo();                           // Action the command
            CommandList.Add(c);               // Add to the log of commands
            replayCommandList.RemoveAt(0);    // Remove it from the head of the replay list
        }

        private const int ReplayMagic = 0x524C5250; // "RLRP"
        private const int ReplayFormatVersion = 1;

        private static IReadOnlyList<FieldInfo> SerializableFields(Type type)
        {
            var fields = new List<FieldInfo>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                fields.AddRange(current
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(field => !field.IsStatic && !field.IsNotSerialized));
            }

            return fields
                .OrderBy(field => field.DeclaringType?.FullName, StringComparer.Ordinal)
                .ThenBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string FieldKey(FieldInfo field) => $"{field.DeclaringType?.FullName}|{field.Name}";

        /// <summary>
        /// Saves the command log using a small versioned replay format.
        /// BinaryFormatter was removed from modern .NET and must not be used for new replay files.
        /// </summary>
        public void SaveLog(string filePath)
        {
            string temporaryFile = filePath + ".tmp";
            try
            {
                // Re-sort based on time as tests show that some commands are deferred.
                CommandList = new Collection<ICommand>(CommandList.OrderBy(command => command.Time).ToList());

                using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ReplayMagic);
                    writer.Write(ReplayFormatVersion);
                    writer.Write(CommandList.Count);

                    foreach (ICommand command in CommandList)
                    {
                        Type commandType = command.GetType();
                        if (commandType.Assembly != typeof(Command).Assembly || !typeof(ICommand).IsAssignableFrom(commandType))
                            throw new InvalidDataException($"Unsupported replay command type {commandType.FullName}");

                        writer.Write(commandType.FullName ?? throw new InvalidDataException("Replay command has no type name"));

                        IReadOnlyList<FieldInfo> fields = SerializableFields(commandType);
                        writer.Write(fields.Count);
                        foreach (FieldInfo field in fields)
                        {
                            writer.Write(FieldKey(field));
                            object value = field.GetValue(command);
                            writer.Write(JsonSerializer.Serialize(value, field.FieldType));
                        }
                    }
                }

                File.Move(temporaryFile, filePath, true);
                Trace.WriteLine("\nList of commands to replay saved");
            }
            catch (Exception error)
            {
                Trace.TraceWarning($"SaveLog error writing command log {filePath}: {error.Message}");
                try
                {
                    if (File.Exists(temporaryFile))
                        File.Delete(temporaryFile);
                }
                catch (IOException)
                {
                    // The save-state itself is still valid; replay logging must never crash the game.
                }
            }
        }

        /// <summary>
        /// Loads a replay written by <see cref="SaveLog"/>.
        /// Legacy BinaryFormatter replay files are ignored safely; the associated save-state can still be resumed.
        /// </summary>
        public void LoadLog(string filePath)
        {
            CommandList = new Collection<ICommand>();

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);

                if (reader.ReadInt32() != ReplayMagic)
                    throw new InvalidDataException("legacy BinaryFormatter replay files are not supported by this build");

                int version = reader.ReadInt32();
                if (version != ReplayFormatVersion)
                    throw new InvalidDataException($"unsupported replay format version {version}");

                int commandCount = reader.ReadInt32();
                if (commandCount < 0 || commandCount > 1_000_000)
                    throw new InvalidDataException($"invalid replay command count {commandCount}");

                Assembly commandAssembly = typeof(Command).Assembly;
                for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
                {
                    string typeName = reader.ReadString();
                    int fieldCount = reader.ReadInt32();
                    if (fieldCount < 0 || fieldCount > 256)
                        throw new InvalidDataException($"invalid field count {fieldCount} for {typeName}");

                    Type commandType = commandAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                    bool supportedType = commandType != null &&
                        !commandType.IsAbstract &&
                        typeof(ICommand).IsAssignableFrom(commandType);

                    Dictionary<string, FieldInfo> fields = supportedType
                        ? SerializableFields(commandType).ToDictionary(FieldKey, StringComparer.Ordinal)
                        : null;
                    object commandObject = supportedType ? RuntimeHelpers.GetUninitializedObject(commandType) : null;

                    for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                    {
                        string fieldKey = reader.ReadString();
                        string json = reader.ReadString();
                        if (fields != null && fields.TryGetValue(fieldKey, out FieldInfo field))
                        {
                            object value = JsonSerializer.Deserialize(json, field.FieldType);
                            field.SetValue(commandObject, value);
                        }
                    }

                    if (commandObject is ICommand command)
                        CommandList.Add(command);
                    else
                        Trace.TraceWarning($"Skipped unknown replay command type {typeName}");
                }
            }
            catch (Exception error)
            {
                CommandList = new Collection<ICommand>();
                Trace.TraceWarning($"LoadLog error reading command log {filePath}: {error.Message}");
            }
        }

        public static void ReportReplayCommands(Collection<ICommand> list)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(list));
            Trace.WriteLine("\nList of commands to replay:");
            foreach (ICommand c in list)
            {
                c.Report();
            }

        }
    }
}
