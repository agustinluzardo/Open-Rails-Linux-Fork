using System;

using Riel.Common.Position;
using Riel.Models.Track;

using Microsoft.Xna.Framework;

namespace Riel.Models.Shim
{
    /// <summary>Places MSTS track geometry using its yaw, pitch and roll.</summary>
    public static class TrackSectionExtensions
    {
        public static WorldLocation LocationAt(this TrackSection section, in WorldLocation start, in Vector3 direction, double distance)
        {
            Matrix orientation = Matrix.CreateFromYawPitchRoll(direction.Y, direction.X, direction.Z);
            return section.LocationAt(start, orientation, distance);
        }

        /// <summary>Uses a cached orientation when placing a section repeatedly during traversal.</summary>
        public static WorldLocation LocationAt(this TrackSection section, in WorldLocation start, in Matrix orientation, double distance)
        {
            Vector3 displacement;
            if (section.Curved)
            {
                if (section.Radius <= 0)
                    return start;
                // Equivalent to Open Rails Traveller.SetLocation: rotate around
                // the local curve centre, then apply the complete MSTS placement.
                double angle = distance / section.Radius;
                displacement = new Vector3(
                    (float)(Math.Sign(section.Angle) * section.Radius * (1 - Math.Cos(angle))),
                    0,
                    (float)(section.Radius * Math.Sin(angle)));
            }
            else
            {
                displacement = new Vector3(0, 0, (float)distance);
            }
            return new WorldLocation(start.Tile, start.Location + Vector3.Transform(displacement, orientation), true);
        }
    }
}
