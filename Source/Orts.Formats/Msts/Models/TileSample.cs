// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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
using FreeTrainSimulator.Common.Native;
using System.Collections;
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts.Files;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// Represents a single MSTS tile stored on disk, of whatever size (2KM, 4KM, 16KM or 32KM sqaure).
    /// </summary>
    [DebuggerDisplay("TileX = {Tile.X}, TileZ = {Tile.Z}, Size = {Size}")]
    public class TileSample: ITileCoordinate
    {
        private readonly Terrain terrain;
        private readonly BitArray terrainFlags;
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
        private readonly ushort[,] terrainAltitude;
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional

        private readonly Tile tile;
        public ref readonly Tile Tile => ref tile;

        public int Size { get; }

        public bool Valid { get { return terrain != null && terrainAltitude != null; } }
        public float Floor { get { return terrain.Samples.SampleFloor; } }  // in meters
        public float Resolution { get { return terrain.Samples.SampleScale; } }  // in meters per( number in Y-file )
        public int SampleCount { get; }//{ get { return terrain.Samples.SampleCount; } }
        public float SampleSize { get { return terrain.Samples.SampleSize; } }
        public int PatchCount { get { return terrain.Patchsets[0].PatchSize; } }
#pragma warning disable CA1819 // Properties should not return arrays
        public Shader[] Shaders { get { return terrain.Shaders; } }
#pragma warning restore CA1819 // Properties should not return arrays
        public float WaterNE { get { return terrain.WaterLevelOffset.NE != 0 ? terrain.WaterLevelOffset.NE : terrain.WaterLevelOffset.SW; } } // in meters
        public float WaterNW { get { return terrain.WaterLevelOffset.NW != 0 ? terrain.WaterLevelOffset.NW : terrain.WaterLevelOffset.SW; } }
        public float WaterSE { get { return terrain.WaterLevelOffset.SE != 0 ? terrain.WaterLevelOffset.SE : terrain.WaterLevelOffset.SW; } }
        public float WaterSW { get { return terrain.WaterLevelOffset.SW != 0 ? terrain.WaterLevelOffset.SW : terrain.WaterLevelOffset.SW; } }

        public bool ContainsWater
        {
            get
            {
                if (terrain.WaterLevelOffset != null)
                    foreach (var patchset in terrain.Patchsets)
                        foreach (var patch in patchset.Patches)
                            if (patch.WaterEnabled)
                                return true;
                return false;
            }
        }

        public Patch GetPatch(int x, int z)
        {
            return terrain.Patchsets[0].Patches[z * PatchCount + x];
        }

        public TileSample(string filePath, in Tile tile, TileHelper.TileZoom zoom, bool visible)
        {
            if (!ContentIO.DirectoryExists(filePath))
                return;

            this.tile = tile;
            Size = 1 << (15 - (int)zoom);

            string filePattern = TileHelper.TileFileName(tile, zoom);

            string terrainFileName = null;
            string terrainAltitudeFileName = null;
            string terrainFlagsFileName = null;

            // Enumerating a Linux filesystem does not guarantee the order in which the files
            // appear. The altitude and flags buffers need SampleCount from the .t file, so first
            // discover all files and then load them in dependency order.
            foreach (string fileName in ContentIO.EnumerateFiles(filePath, filePattern + "??.*"))
            {
                if (fileName.EndsWith(".t", StringComparison.OrdinalIgnoreCase))
                    terrainFileName = fileName;
                else if (fileName.EndsWith("_y.raw", StringComparison.OrdinalIgnoreCase))
                    terrainAltitudeFileName = fileName;
                else if (fileName.EndsWith("_f.raw", StringComparison.OrdinalIgnoreCase))
                    terrainFlagsFileName = fileName;
            }

            if (terrainFileName != null)
            {
                try
                {
                    terrain = TerrainFile.LoadTerrainFile(terrainFileName);
                    SampleCount = terrain.Samples.SampleCount;
                }
                catch (IOException exception)
                {
                    Trace.WriteLine(new FileLoadException(terrainFileName, exception));
                }
            }

            if (SampleCount > 0 && terrainAltitudeFileName != null)
            {
                try
                {
                    terrainAltitude = TerrainAltitudeFile.LoadTerrainAltitudeFile(terrainAltitudeFileName, SampleCount);
                }
                catch (IOException exception)
                {
                    Trace.WriteLine(new FileLoadException(terrainAltitudeFileName, exception));
                }
            }

            if (SampleCount > 0 && terrainFlagsFileName != null)
            {
                try
                {
                    terrainFlags = TerrainFlagsFile.LoadTerrainFlagsFile(terrainFlagsFileName, SampleCount);
                }
                catch (IOException exception)
                {
                    Trace.WriteLine(new FileLoadException(terrainFlagsFileName, exception));
                }
            }
            // T and Y files are expected to exist; F files are optional.
            if (null == terrain && visible)
                Trace.TraceWarning("Ignoring missing tile {0}.t", filePattern);
        }

        public float GetElevation(int ux, int uz)
        {
            return terrainAltitude[ux, uz] * Resolution + Floor;
        }

        public bool IsVertexHidden(int x, int z)
        {
            return terrainFlags?[x * SampleCount + z] ?? false;
        }

        public bool InsideTile(int x, int z)
        {
            return x >= 0 && x < SampleCount && z >= 0 && z < SampleCount;
        }
    }
}
