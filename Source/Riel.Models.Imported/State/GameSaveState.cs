using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Riel.Common;
using Riel.Common.Api;
using Riel.Common.Position;
using Riel.Models.Settings;

using MemoryPack;

namespace Riel.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class GameSaveState : SaveStateBase
    {
        private const string HeaderEofMarker = "9zZi9WX51VvAH25Lgi0t";

        public string GameVersion { get; set; }
        public string Route { get; set; }
        public string Path { get; set; }
        public bool MultiplayerGame { get; set; }
        public double GameTime { get; set; }
        public DateTime RealSaveTime { get; set; }
        public WorldLocation InitialLocation { get; set; }
        public WorldLocation PlayerLocation { get; set; }
        public SimulatorSaveState SimulatorSaveState { get; set; }
        public ViewerSaveState ViewerSaveState { get; set; }
        public ActivityEvaluationState ActivityEvaluationState { get; set; }
        public ProfileSelectionsModel ProfileSelections { get; set; }
        [MemoryPackInclude]
        private string finalMarker = HeaderEofMarker;

        [MemoryPackIgnore]
        public bool? Valid { get; private set; }

        [MemoryPackOnDeserialized]
        public void OnDeserialized()
        {
            Valid = finalMarker == HeaderEofMarker;
        }
    }
}
