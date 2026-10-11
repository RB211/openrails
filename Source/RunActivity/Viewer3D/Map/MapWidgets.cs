// COPYRIGHT 2023 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;
using ORTS.Common;
using PointF = System.Drawing.PointF;

namespace Orts.Viewer3D.Debugging
{
    #region SignalWidget
    /// <summary>
    /// Defines a signal being drawn in a 2D view.
    /// </summary>
    public class SignalWidget : ItemWidget
    {
        public TrItem Item;
        /// <summary>
        /// The underlying signal object as referenced by the TrItem.
        /// </summary>
        public SignalObject Signal;

        public PointF Dir;
        public bool hasDir;
        /// <summary>
        /// For now, returns true if any of the signal heads shows any "clear" aspect.
        /// This obviously needs some refinement.
        /// </summary>
        public int IsProceed
        {
            get
            {
                int returnValue = 2;

                foreach (var head in Signal.SignalHeads.Where(x => x.Function == SignalFunction.NORMAL))
                {
                    if (head.state == MstsSignalAspect.CLEAR_1
                        || head.state == MstsSignalAspect.CLEAR_2)
                    {
                        returnValue = 0;
                    }
                    if (head.state == MstsSignalAspect.APPROACH_1
                        || head.state == MstsSignalAspect.APPROACH_2
                        || head.state == MstsSignalAspect.APPROACH_3)
                    {
                        returnValue = 1;
                    }
                }

                return returnValue;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public SignalWidget(SignalItem item, SignalObject signal)
        {
            Item = item;
            Signal = signal;
            hasDir = false;
            Location.X = (item.TileX * 2048) + item.X;
            Location.Y = (item.TileZ * 2048) + item.Z;
            var node = Program.Simulator.TDB.TrackDB.TrackNodes?[signal.trackNode];
            Vector2 v2;
            if (node?.TrVectorNode != null)
            {
                var ts = node.TrVectorNode.TrVectorSections?.FirstOrDefault();
                if (ts == null)
                    return;
                v2 = new Vector2((ts.TileX * 2048) + ts.X, (ts.TileZ * 2048) + ts.Z);
            }
            else if (node?.TrJunctionNode != null)
            {
                var ts = node?.UiD;
                if (ts == null)
                    return;
                v2 = new Vector2((ts.TileX * 2048) + ts.X, (ts.TileZ * 2048) + ts.Z);
            }
            else
            {
                return;
            }
            var v1 = new Vector2(Location.X, Location.Y);
            var v3 = v1 - v2;
            v3.Normalize();
            void copyTo(Vector2 input, ref PointF output)
            {
                output.X = input.X;
                output.Y = input.Y;
            }
            copyTo(v1 - Vector2.Multiply(v3, signal.direction == 0 ? 12f : -12f), ref Dir);
            //shift signal along the dir for 2m, so signals will not be overlapped
            copyTo(v1 - Vector2.Multiply(v3, signal.direction == 0 ? 1.5f : -1.5f), ref Location);
            hasDir = true;
        }
    }
    #endregion

    #region SwitchWidget
    /// <summary>
    /// Defines a signal being drawn in a 2D view.
    /// </summary>
    public class SwitchWidget : ItemWidget
    {
        public TrackNode Item;
        public uint main;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public SwitchWidget(TrackNode item)
        {
            Item = item;
            var TS = Program.Simulator.TSectionDat.TrackShapes.Get(item.TrJunctionNode.ShapeIndex);  // TSECTION.DAT tells us which is the main route

            main = TS != null ? TS.MainRoute : 0;

            Location.X = (Item.UiD.TileX * 2048) + Item.UiD.X; Location.Y = (Item.UiD.TileZ * 2048) + Item.UiD.Z;
        }
    }

    #endregion

    #region BufferWidget
    public class BufferWidget : ItemWidget
    {
        public TrackNode Item;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public BufferWidget(TrackNode item)
        {
            Item = item;

            Location.X = (Item.UiD.TileX * 2048) + Item.UiD.X; Location.Y = (Item.UiD.TileZ * 2048) + Item.UiD.Z;
        }
    }
    #endregion

    #region ItemWidget
    public class ItemWidget
    {
        public PointF Location;
        public PointF Location2D;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public ItemWidget()
        {

            Location = new PointF(float.NegativeInfinity, float.NegativeInfinity);
            Location2D = new PointF(float.NegativeInfinity, float.NegativeInfinity);
        }
    }
    #endregion

    #region TrainWidget
    public class TrainWidget : ItemWidget
    {
        public Train Train;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        public TrainWidget(Train t)
        {
            Train = t;
        }
    }
    #endregion

    #region LineSegment
    /// <summary>
    /// Defines a geometric line segment.
    /// </summary>
    public class LineSegment
    {
        public dVector A;
        public dVector B;
        public dVector C;
        //public float radius;
        public bool isCurved;

        public float angle1, angle2;

        public LineSegment(dVector A, dVector B, bool Occupied, TrVectorSection Section)
        {
            this.A = A;
            this.B = B;

            isCurved = false;
            if (Section == null) return;
            //MySection = Section;
            uint k = Section.SectionIndex;
            TrackSection ts = Program.Simulator.TSectionDat.TrackSections.Get(k);
            if (ts != null)
            {
                if (ts.SectionCurve != null)
                {
                    float diff = (float)(ts.SectionCurve.Radius * (1 - Math.Cos(ts.SectionCurve.Angle * 3.14f / 360)));
                    if (diff < 3) return; //not need to worry, curve too small
                                          //curve = ts.SectionCurve;
                    Vector3 v = new Vector3((float)(((B.TileX - A.TileX) * 2048) + B.X - A.X), 0, (float)(((B.TileZ - A.TileZ) * 2048) + B.Z - A.Z));
                    isCurved = true;
                    Vector3 v2 = Vector3.Cross(Vector3.Up, v); v2.Normalize();
                    v /= 2; v.X += (A.TileX * 2048) + (float)A.X; v.Z += (A.TileZ * 2048) + (float)A.Z;
                    v = ts.SectionCurve.Angle > 0 ? (v2 * -diff) + v : (v2 * diff) + v;
                    C = new dVector(0, v.X, 0, v.Z);
                }
            }
        }
    }
    #endregion

    #region SidingWidget

    /// <summary>
    /// Defines a siding name being drawn in a 2D view.
    /// </summary>
    public struct SidingWidget
    {
        public uint Id;
        public PointF Location;
        public string Name;
        public uint LinkId;

        /// <summary>
        /// The underlying track item.
        /// </summary>
        public SidingItem Item;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public SidingWidget(SidingItem item)
        {
            Id = item.TrItemId;
            LinkId = item.LinkedSidingId;
            Item = item;
            Name = item.ItemName;
            Location = new PointF((item.TileX * 2048) + item.X, (item.TileZ * 2048) + item.Z);
        }
    }
    #endregion

    public struct PlatformWidget
    {
        public uint Id;
        public PointF Location;
        public string Name;
        public PointF Extent1;
        public PointF Extent2;
        public uint LinkId;
        public string Station;

        /// <summary>
        /// The underlying track item.
        /// </summary>
        public PlatformItem Item;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="signal"></param>
        public PlatformWidget(PlatformItem item)
        {
            Id = item.TrItemId;
            LinkId = item.LinkedPlatformItemId;
            Item = item;
            Name = item.ItemName;
            Station = item.Station;
            Location = new PointF((item.TileX * 2048) + item.X, (item.TileZ * 2048) + item.Z);
            Extent1 = default;
            Extent2 = default;
        }
    }

    public class dVector
    {
        public int TileX, TileZ;
        public double X, Z;

        public dVector(int tilex1, double x1, int tilez1, double z1)
        {
            TileX = tilex1;
            TileZ = tilez1;
            X = x1;
            Z = z1;
        }

        public static double DistanceSqr(dVector v1, dVector v2)
        {
            return Math.Pow(((v1.TileX - v2.TileX) * 2048) + v1.X - v2.X, 2)
                + Math.Pow(((v1.TileZ - v2.TileZ) * 2048) + v1.Z - v2.Z, 2);
        }
    }
}
