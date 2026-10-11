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
//
// Copy of the Tile class from RunActivity/Viewer3D/Tiles.cs. TrackViewer used to reference the whole RunActivity
// project for this (and for its GDI-based fonts); that dependency has been removed so TrackViewer does not depend
// on the (Windows-centric) game executable. File names are resolved case-insensitively.

using System;
using System.Diagnostics;
using System.IO;
using Orts.Formats.Msts;

namespace ORTS.TrackViewer.Drawing
{
    /// <summary>
    /// The terrain data (.t, _y.raw and _f.raw files) of a single tile.
    /// </summary>
    [DebuggerDisplay("TileX = {TileX}, TileZ = {TileZ}, Size = {Size}")]
    public class Tile
    {
#pragma warning disable 1591
        public readonly int TileX, TileZ, Size;

        public bool Loaded { get { return TFile != null && YFile != null; } }
        public float Floor { get { return TFile.terrain.terrain_samples.terrain_sample_floor; } }  // in meters
        public float Resolution { get { return TFile.terrain.terrain_samples.terrain_sample_scale; } }  // in meters per( number in Y-file )
        public int SampleCount { get { return TFile.terrain.terrain_samples.terrain_nsamples; } }
        public float SampleSize { get { return TFile.terrain.terrain_samples.terrain_sample_size; } }
        public int PatchCount { get { return TFile.terrain.terrain_patchsets[0].terrain_patchset_npatches; } }
        public terrain_shader[] Shaders { get { return TFile.terrain.terrain_shaders; } }

        public terrain_patchset_patch GetPatch(int x, int z)
        {
            return TFile.terrain.terrain_patchsets[0].terrain_patchset_patches[z * PatchCount + x];
        }

        readonly TerrainFile TFile;
        readonly TerrainAltitudeFile YFile;
        readonly TerrainFlagsFile FFile;

        public Tile(string filePath, int tileX, int tileZ, TileName.Zoom zoom, bool visible)
        {
            TileX = tileX;
            TileZ = tileZ;
            Size = 1 << (15 - (int)zoom);

            var fileName = filePath + TileName.FromTileXZ(tileX, tileZ, zoom);
            string tFileName = CaseInsensitivePath.Resolve(fileName + ".t");
            if (!File.Exists(tFileName))
            {
                // Many tiles adjacent to the visible tile may not be modelled, so a warning is not helpful;
                // ignore a missing .t file unless it is the currently visible tile.
                if (visible)
                    Trace.TraceWarning("Ignoring missing tile {0}.t", fileName);
                return;
            }

            // T and Y files are expected to exist; F files are optional.
            try
            {
                TFile = new TerrainFile(tFileName);
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException(fileName + ".t", error));
            }
            try
            {
                YFile = new TerrainAltitudeFile(CaseInsensitivePath.Resolve(fileName + "_y.raw"), SampleCount);
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException(fileName + "_y.raw", error));
            }
            try
            {
                string fFileName = CaseInsensitivePath.Resolve(fileName + "_f.raw");
                if (File.Exists(fFileName))
                    FFile = new TerrainFlagsFile(fFileName, SampleCount);
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException(fileName + "_f.raw", error));
            }
        }

        internal float GetElevation(int ux, int uz)
        {
            return (float)YFile.GetElevation(ux, uz) * Resolution + Floor;
        }

        internal bool IsVertexHidden(int ux, int uz)
        {
            return FFile == null ? false : FFile.IsVertexHidden(ux, uz);
        }
#pragma warning restore 1591
    }
}
