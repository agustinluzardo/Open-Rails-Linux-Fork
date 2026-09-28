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

/* AIPath
 * 
 * Contains a processed version of the MSTS PAT file.
 * The processing saves information needed for AI train dispatching and to align switches.
 * Could this be used for player trains also?
 * 
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Riel.Common;
using Riel.Common.Api;
using Riel.Common.Position;
using Riel.Models.Content;
using Riel.Models.Imported.State;
using Riel.Models.Track;
using Riel.Runtime;
using Riel.Runtime.Track;

namespace Orts.Simulation.AIs
{
    public class AIPath : ISaveStateApi<AiPathSaveState>
    {
        public AIPathNode FirstNode { get; }    // path starting node
        //public AIPathNode LastVisitedNode; not used anymore
        public Collection<AIPathNode> Nodes { get; } = new Collection<AIPathNode>();
        public string PathName { get; } //name of the path to be able to print it.

        public AIPath() { }

        /// <summary>
        /// Creates an AIPath from path model information.
        /// First creates all the nodes and then links them together into a main list
        /// with optional parallel siding list.
        /// </summary>
        public AIPath(PathModel pathModel, bool timetableMode)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            PathName = pathModel.Name;
            bool fatalerror = false;
            if (pathModel.PathNodes.Length <= 0)
            {
                Nodes = null;
                return;
            }

            foreach (Riel.Models.Content.PathNode pathNode in pathModel.PathNodes)
                Nodes.Add(new AIPathNode(pathNode, timetableMode));
            FirstNode = Nodes[0];
            //LastVisitedNode = FirstNode;            

            // Connect the various nodes to each other
            for (int i = 0; i < Nodes.Count; i++)
            {
                AIPathNode node = Nodes[i];
                node.Index = i;
                Riel.Models.Content.PathNode tpn = pathModel.PathNodes[i];

                // find TVNindex to next main node.
                if (tpn.NextMainNode > -1)
                {
                    node.NextMainNode = Nodes[tpn.NextMainNode];
                    node.NextMainTVNIndex = node.FindTVNIndex(node.NextMainNode, i == 0 ? -1 : Nodes[i - 1].NextMainTVNIndex);
                    if (node.JunctionIndex >= 0)
                        node.IsFacingPoint = TestFacingPoint(node.JunctionIndex, node.NextMainTVNIndex);
                    if (node.NextMainTVNIndex < 0)
                    {
                        node.NextMainNode = null;
                        Trace.TraceWarning("Cannot find main track for node {1} in path {0}", pathModel.Hierarchy(), i);
                        fatalerror = true;
                    }
                }

                // find TVNindex to next siding node
                if (tpn.NextSidingNode > -1)
                {
                    node.NextSidingNode = Nodes[tpn.NextSidingNode];
                    node.NextSidingTVNIndex = node.FindTVNIndex(node.NextSidingNode, i == 0 ? -1 : Nodes[i - 1].NextMainTVNIndex);
                    if (node.JunctionIndex >= 0)
                        node.IsFacingPoint = TestFacingPoint(node.JunctionIndex, node.NextSidingTVNIndex);
                    if (node.NextSidingTVNIndex < 0)
                    {
                        node.NextSidingNode = null;
                        Trace.TraceWarning("Cannot find siding track for node {1} in path {0}", pathModel.Hierarchy(), i);
                        fatalerror = true;
                    }
                }

                if (node.NextMainNode != null && node.NextSidingNode != null)
                    node.Type = TrainPathNodeType.SidingStart;
            }

            FindSidingEnds();

            // The candidate search walks the route for every path node. Run it only when
            // explicitly diagnosing track resolution, including during activity startup.
            if (DiagnosticTrace.AiRouteResolution && !fatalerror &&
                string.Equals(PathName, "Plaza C Via Circuito Dsc", StringComparison.OrdinalIgnoreCase))
            {
                Trace.TraceInformation("[AIPathResolve] path={0} nodes={1}", PathName, Nodes.Count);
                foreach (AIPathNode pathNode in Nodes)
                    pathNode.TraceTrackResolution(PathName);
            }

            if (fatalerror)
                Nodes = null; // invalid path - do not return any nodes
        }

        /// <summary>
        /// constructor out of other path
        /// </summary>
        /// <param name="otherPath"></param>

        public AIPath(AIPath otherPath)
        {
            ArgumentNullException.ThrowIfNull(otherPath);
            FirstNode = new AIPathNode(otherPath.FirstNode);
            foreach (AIPathNode otherNode in otherPath.Nodes)
            {
                Nodes.Add(new AIPathNode(otherNode));
            }

            // set correct node references

            for (int iNode = 0; iNode <= otherPath.Nodes.Count - 1; iNode++)
            {
                AIPathNode otherNode = otherPath.Nodes[iNode];
                if (otherNode.NextMainNode != null)
                {
                    Nodes[iNode].NextMainNode = Nodes[otherNode.NextMainNode.Index];
                }

                if (otherNode.NextSidingNode != null)
                {
                    Nodes[iNode].NextSidingNode = Nodes[otherNode.NextSidingNode.Index];
                }
            }

            if (otherPath.FirstNode.NextMainNode != null)
            {
                FirstNode.NextMainNode = Nodes[otherPath.FirstNode.NextMainNode.Index];
            }
            if (otherPath.FirstNode.NextSidingNode != null)
            {
                FirstNode.NextSidingNode = Nodes[otherPath.FirstNode.NextSidingNode.Index];
            }

            PathName = otherPath.PathName;
        }

        /// <summary>
        /// Find all nodes that are the end of a siding (so where main path and siding path come together again)
        /// </summary>
        private void FindSidingEnds()
        {
            Dictionary<int, AIPathNode> lastUse = new Dictionary<int, AIPathNode>();
            for (AIPathNode node1 = FirstNode; node1 != null; node1 = node1.NextMainNode)
            {
                if (node1.JunctionIndex >= 0)
                    lastUse[node1.JunctionIndex] = node1;
                AIPathNode node2 = node1.NextSidingNode;
                while (node2 != null && node2.NextSidingNode != null)
                {
                    if (node2.JunctionIndex >= 0)
                        lastUse[node2.JunctionIndex] = node2;
                    node2 = node2.NextSidingNode;
                }
                if (node2 != null)
                    node2.Type = TrainPathNodeType.SidingEnd;
            }
            //foreach (KeyValuePair<int, AIPathNode> kvp in lastUse)
            //    kvp.Value.IsLastSwitchUse = true;
        }

        /// <summary>
        /// returns true if the specified vector node is at the facing point end of
        /// the specified juction node, else false.
        /// </summary>
        private static bool TestFacingPoint(int junctionIndex, int vectorIndex)
        {
            if (junctionIndex < 0 || vectorIndex < 0)
                return false;
            TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
            if (trackDatabase.TrackNodes[junctionIndex] is not JunctionNode)
                return false;
            return trackDatabase.TrackNodeConnectors[junctionIndex].TrackNodeConnectors[0].Link != vectorIndex;
        }

        public async ValueTask<AiPathSaveState> Snapshot()
        {
            return new AiPathSaveState()
            {
                AiPathNodeSaveStates = await Nodes.SnapshotCollection<AiPathNodeSaveState, AIPathNode>().ConfigureAwait(false),
            };
        }

        public async ValueTask Restore(AiPathSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            await Nodes.RestoreCollectionCreateNewItems(saveState.AiPathNodeSaveStates).ConfigureAwait(false);

            foreach (AiPathNodeSaveState saveNode in saveState.AiPathNodeSaveStates)
            {
                AIPathNode result = Nodes.Where(node => node.Index == saveNode.Index).FirstOrDefault();
                result.NextMainNode = Nodes[saveNode.NextMainNodeIndex];
                result.NextSidingNode = Nodes[saveNode.NextSidingNodeIndex];
            }
        }
    }

    public class AIPathNode : ISaveStateApi<AiPathNodeSaveState>
    {
        public int Index { get; set; }
        public Riel.Common.TrainPathNodeType Type { get; set; } = Riel.Common.TrainPathNodeType.Other;
        public int WaitTimeS { get; private set; }               // number of seconds to wait after stopping at this node
        public int WaitUntil { get; private set; }               // clock time to wait until if not zero
        public AIPathNode NextMainNode { get; set; }     // next path node on main path
        public AIPathNode NextSidingNode { get; set; }   // next path node on siding path
        public int NextMainTVNIndex { get; set; } = -1;   // index of main vector node leaving this path node
        public int NextSidingTVNIndex { get; set; } = -1; // index of siding vector node leaving this path node
        public WorldLocation Location { get; set; }      // coordinates for this path node
        public int JunctionIndex { get; set; } = -1;      // index of junction node, -1 if none
        public bool IsFacingPoint { get; set; }          // true if this node entered from the facing point end

        public AIPathNode() { }

        /// <summary>
        /// Creates a single AIPathNode and initializes everything that do not depend on other nodes.
        /// The AIPath constructor will initialize the rest.
        /// </summary>
        public AIPathNode(Riel.Models.Content.PathNode pathNode, bool timetableMode)
        {
            ArgumentNullException.ThrowIfNull(pathNode);

            WaitTimeS = pathNode.WaitInfo?.WaitTime ?? 0;
            Location = pathNode.Location;

            // PathNodeType is a flags enum. Open Rails treats the PDP junction
            // property independently from the TrPathNode reversal/wait flags:
            // a reversal or waiting point may also sit on a junction. The old
            // switch made these mutually exclusive and silently lost the
            // JunctionIndex for combined nodes, which can corrupt every
            // subpath built after a reversal.
            if (pathNode.NodeType.Includes(PathNodeType.Junction))
            {
                JunctionIndex = FindJunctionOrEndIndex(Location, true);
            }

            if (pathNode.NodeType.Includes(PathNodeType.Reversal))
            {
                Type = TrainPathNodeType.Reverse;
            }
            else if (pathNode.NodeType.Includes(PathNodeType.Wait))
            {
                Type = pathNode.NodeType.Includes(PathNodeType.Invalid) && timetableMode
                    ? TrainPathNodeType.Invalid
                    : TrainPathNodeType.Stop;
            }
            else if (pathNode.NodeType.Includes(PathNodeType.Invalid) && timetableMode)
            {
                Type = TrainPathNodeType.Invalid;
            }
        }

        /// <summary>
        /// Constructor from other AIPathNode
        /// </summary>
        /// <param name="otherNode"></param>

        public AIPathNode(AIPathNode otherNode)
        {
            ArgumentNullException.ThrowIfNull(otherNode);
            Index = otherNode.Index;
            Type = otherNode.Type;
            WaitTimeS = otherNode.WaitTimeS;
            WaitUntil = otherNode.WaitUntil;
            NextMainNode = null; // set after completion of copying to get correct reference
            NextSidingNode = null; // set after completion of copying to get correct reference
            NextMainTVNIndex = otherNode.NextMainTVNIndex;
            NextSidingTVNIndex = otherNode.NextSidingTVNIndex;
            Location = otherNode.Location;
            JunctionIndex = otherNode.JunctionIndex;
            IsFacingPoint = otherNode.IsFacingPoint;
        }


        private readonly record struct PathTrackCandidate(int TrackNodeIndex, int VectorSectionIndex, double LateralDistance, double LongitudinalDistance);

        /// <summary>
        /// Diagnostic-only comparison between the current FTS nearest-section lookup and the
        /// capture semantics used by the legacy Open Rails Traveller for MSTS PAT points.
        /// Open Rails ignores elevation, accepts up to 2.5 m lateral centerline offset, and
        /// allows a 0.5 m longitudinal margin beyond each vector-section end.
        /// </summary>
        internal void TraceTrackResolution(string pathName)
        {
            WorldLocation planarLocation = Location.SetElevation(0);
            TrackTraveller? selected = TrackTraveller.InitializeTraveller(planarLocation);
            List<PathTrackCandidate> openRailsCandidates = FindOpenRailsPathCandidates(planarLocation);

            int openRailsNode = openRailsCandidates.Count > 0 ? openRailsCandidates[0].TrackNodeIndex : -1;
            int openRailsSection = openRailsCandidates.Count > 0 ? openRailsCandidates[0].VectorSectionIndex : -1;
            double openRailsDistance = openRailsCandidates.Count > 0 ? openRailsCandidates[0].LateralDistance : double.NaN;
            double selectedDistance = selected.HasValue
                ? Math.Sqrt(WorldLocation.GetDistanceSquared2D(planarLocation, selected.Value.Location))
                : double.NaN;

            string candidateSummary = openRailsCandidates.Count == 0
                ? "<none>"
                : string.Join(", ", openRailsCandidates.Take(3).Select(candidate =>
                    $"{candidate.TrackNodeIndex}/{candidate.VectorSectionIndex}:{candidate.LateralDistance:F3}m@{candidate.LongitudinalDistance:F2}m"));

            Trace.TraceInformation(
                "[AIPathResolve] path={0} node={1} location={2} type={3} junction={4} mainTVN={5} sidingTVN={6} facing={7} " +
                "ftsNode={8} ftsSection={9} ftsDistance={10:F3}m orNode={11} orSection={12} orDistance={13:F3}m candidates=[{14}]",
                pathName, Index, Location, Type, JunctionIndex, NextMainTVNIndex, NextSidingTVNIndex, IsFacingPoint,
                selected?.TrackNodeIndex ?? -1, selected?.SectionIndex ?? -1, selectedDistance,
                openRailsNode, openRailsSection, openRailsDistance, candidateSummary);
        }

        private static List<PathTrackCandidate> FindOpenRailsPathCandidates(in WorldLocation planarLocation)
        {
            const double maximumCenterlineOffset = 2.5;
            const double initErrorMargin = 0.5;

            TrackWorld trackWorld = RuntimeDataResolver.Instance.TrackWorld;
            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            List<PathTrackCandidate> candidates = new List<PathTrackCandidate>();

            // Legacy Open Rails walks track nodes in database order and keeps the first
            // qualifying vector section from each node. Order is therefore relevant for
            // equal-distance parallel tracks.
            foreach (VectorNode vectorNode in trackDatabase.VectorNodes.OrderBy(node => node.NodeIndex))
            {
                for (int sectionIndex = 0; sectionIndex < vectorNode.VectorSections.Length; sectionIndex++)
                {
                    VectorSectionNode section = vectorNode.VectorSections[sectionIndex];
                    if (!trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geometry) || !geometry.HasGeometry)
                        continue;

                    if (!TryOpenRailsPathCandidate(planarLocation, section, geometry, maximumCenterlineOffset, initErrorMargin,
                        out double lateralDistance, out double longitudinalDistance))
                        continue;

                    candidates.Add(new PathTrackCandidate(vectorNode.NodeIndex, sectionIndex, lateralDistance, longitudinalDistance));
                    break;
                }
            }

            return candidates
                .OrderBy(candidate => candidate.LateralDistance)
                .ThenBy(candidate => candidate.TrackNodeIndex)
                .ToList();
        }

        private static bool TryOpenRailsPathCandidate(in WorldLocation location, VectorSectionNode section, SectionGeometry geometry,
            double maximumCenterlineOffset, double initErrorMargin, out double lateralDistance, out double longitudinalDistance)
        {
            lateralDistance = double.MaxValue;
            longitudinalDistance = double.NaN;

            if (!geometry.Curved)
            {
                var segment = WorldLocation.GetDistanceVector(section.Location, section.EndLocation);
                var toPoint = WorldLocation.GetDistanceVector(section.Location, location);
                segment.Y = 0;
                toPoint.Y = 0;

                double segmentLength = Math.Sqrt((double)segment.X * segment.X + (double)segment.Z * segment.Z);
                if (segmentLength <= 1e-9)
                    return false;

                longitudinalDistance = ((double)toPoint.X * segment.X + (double)toPoint.Z * segment.Z) / segmentLength;
                lateralDistance = Math.Abs((double)toPoint.X * segment.Z - (double)toPoint.Z * segment.X) / segmentLength;

                return lateralDistance <= maximumCenterlineOffset
                    && longitudinalDistance >= -initErrorMargin
                    && longitudinalDistance <= geometry.Length + initErrorMargin;
            }

            if (geometry.Radius <= 0)
                return false;

            var centerToPoint = WorldLocation.GetDistanceVector(geometry.ArcCenter, location);
            centerToPoint.Y = 0;
            double radialDistance = Math.Sqrt((double)centerToPoint.X * centerToPoint.X + (double)centerToPoint.Z * centerToPoint.Z);
            lateralDistance = Math.Abs(radialDistance - geometry.Radius);
            if (lateralDistance > maximumCenterlineOffset)
                return false;

            double uLength = Math.Sqrt((double)geometry.U.X * geometry.U.X + (double)geometry.U.Z * geometry.U.Z);
            double vLength = Math.Sqrt((double)geometry.V.X * geometry.V.X + (double)geometry.V.Z * geometry.V.Z);
            if (uLength <= 1e-9 || vLength <= 1e-9)
                return false;

            double dotU = ((double)centerToPoint.X * geometry.U.X + (double)centerToPoint.Z * geometry.U.Z) / uLength;
            double dotV = ((double)centerToPoint.X * geometry.V.X + (double)centerToPoint.Z * geometry.V.Z) / vLength;
            double angle = Math.Atan2(dotV, dotU);
            longitudinalDistance = angle * geometry.Radius;

            return longitudinalDistance >= -initErrorMargin
                && longitudinalDistance <= geometry.Length + initErrorMargin;
        }

        public ValueTask<AiPathNodeSaveState> Snapshot()
        {
            return ValueTask.FromResult(new AiPathNodeSaveState()
            {
                Index = Index,
                NodeType = Type,
                WaitTime = WaitTimeS,
                WaitUntil = WaitUntil,
                NextMainNodeIndex = NextMainNode == null ? -1 : NextMainNode.Index,
                NextMainTrackVectorNodeIndex = NextMainTVNIndex,
                NextSidingNodeIndex = NextSidingNode == null ? -1 : NextSidingNode.Index,
                NextSidingTrackVectorNodeIndex = NextSidingTVNIndex,
                JunctionIndex = JunctionIndex,
                FacingJunction = IsFacingPoint,
                Location = Location,
            });
        }

        public ValueTask Restore(AiPathNodeSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            Index = saveState.Index;
            Type = saveState.NodeType;
            WaitTimeS = saveState.WaitTime;
            WaitUntil = saveState.WaitUntil;
            NextMainTVNIndex = saveState.NextMainTrackVectorNodeIndex;
            NextSidingTVNIndex = saveState.NextSidingTrackVectorNodeIndex;
            JunctionIndex = saveState.JunctionIndex;
            IsFacingPoint = saveState.FacingJunction;
            Location = saveState.Location;

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Returns the index of the vector node connection this path node to the (given) nextNode.
        /// </summary>
        public int FindTVNIndex(AIPathNode nextNode, int previousNextMainTVNIndex)
        {
            ArgumentNullException.ThrowIfNull(nextNode);

            int junctionIndexThis = JunctionIndex;
            int junctionIndexNext = nextNode.JunctionIndex;

            // if this is no junction, try to find the TVN index 
            if (junctionIndexThis < 0)
            {
                try
                {
                    return FindTrackNodeIndex(this);
                }
                catch (InvalidDataException)
                {
                    junctionIndexThis = FindJunctionOrEndIndex(this.Location, false);
                }
            }

            // this is a junction; if the next node is no junction, try that one.
            if (junctionIndexNext < 0)
            {
                try
                {
                    return FindTrackNodeIndex(nextNode);
                }
                catch (InvalidDataException)
                {
                    junctionIndexNext = FindJunctionOrEndIndex(nextNode.Location, false);
                }
            }

            //both this node and the next node are junctions: find the vector node connecting them.
            TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
            var iCand = -1;
            foreach (VectorNode vectorNode in trackDatabase.VectorNodes)
            {
                TrackNodeConnectorIndex connectors = trackDatabase.TrackNodeConnectors[vectorNode.NodeIndex];
                if (connectors.TrackNodeConnectors[0].Link == junctionIndexThis && connectors.TrackNodeConnectors[1].Link == junctionIndexNext)
                {
                    iCand = vectorNode.NodeIndex;
                    if (iCand != previousNextMainTVNIndex)
                        break;
                    Trace.TraceInformation("Managing rocket loop at trackNode {0}", iCand);
                }
                else if (connectors.TrackNodeConnectors[1].Link == junctionIndexThis && connectors.TrackNodeConnectors[0].Link == junctionIndexNext)
                {
                    iCand = vectorNode.NodeIndex;
                    if (iCand != previousNextMainTVNIndex)
                        break;
                    Trace.TraceInformation("Managing rocket loop at trackNode {0}", iCand);
                }
            }
            return iCand;
        }

        /// <summary>
        /// Try to find the tracknode corresponding to the given node's location.
        /// This will raise an exception if it cannot be found
        /// </summary>
        /// <param name="TDB"></param>
        /// <param name="tsectiondat"></param>
        /// <param name="node"></param>
        /// <returns>The track node index that has been found (or an exception)</returns>
        private static int FindTrackNodeIndex(AIPathNode node)
        {
            // Open Rails matches PAT path nodes to vector track in X/Z only.
            // PAT elevations can differ from the loaded track and otherwise
            // make an entire AI service pick the adjacent line or lose stops.
            return TrackTraveller.InitializeTraveller(node.Location.SetElevation(0))?.TrackNodeIndex
                ?? throw new InvalidDataException($"{node.Location} could not be found in the track database.");
        }

        /// <summary>
        /// Find the junctionNode or endNode closest to the given location
        /// </summary>
        /// <param name="location">Location for which we want to find the node</param>
        /// <param name="trackDB">track database containing the trackNodes</param>
        /// <param name="wantJunctionNode">true if a junctionNode is wanted, false for a endNode</param>
        /// <returns>tracknode index of the closes node</returns>
        public static int FindJunctionOrEndIndex(in WorldLocation location, bool wantJunctionNode)
        {
            TrackDatabase trackDatabase = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase;
            int bestIndex = -1;
            float bestDistance2 = 1e10f;
            for (int j = 0; j < trackDatabase.TrackNodes.Length; j++)
            {
                TrackNodeBase tn = trackDatabase.TrackNodes[j];
                if (tn == null)
                    continue;
                if (wantJunctionNode && tn is not JunctionNode)
                    continue;
                if (!wantJunctionNode && tn is not EndNode)
                    continue;
                if (tn.Location.Tile != location.Tile)
                    continue;

                float dx = tn.Location.Location.X - location.Location.X;
                dx += (tn.Location.TileX - location.TileX) * 2048;
                float dz = tn.Location.Location.Z - location.Location.Z;
                dz += (tn.Location.TileZ - location.TileZ) * 2048;
                float dy = tn.Location.Location.Y - location.Location.Y;
                float d = dx * dx + dy * dy + dz * dz;
                if (bestDistance2 > d)
                {
                    bestIndex = j;
                    bestDistance2 = d;
                }

            }
            return bestIndex;
        }
    }
}
