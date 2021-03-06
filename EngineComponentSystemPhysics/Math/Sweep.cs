﻿using Engine.Serialization;
using Engine.XnaExtensions;
using Microsoft.Xna.Framework;

#if FARMATH
using Engine.FarMath; //< For Serializer and Packetizer extension methods.
using LocalPoint = Microsoft.Xna.Framework.Vector2;
using WorldPoint = Engine.FarMath.FarPosition;
#else
using LocalPoint = Microsoft.Xna.Framework.Vector2;
using WorldPoint = Microsoft.Xna.Framework.Vector2;
#endif

namespace Engine.ComponentSystem.Physics.Math
{
    /// <summary>
    ///     This describes the motion of a body/shape for TOI computation. Shapes are defined with respect to the body
    ///     origin, which may no coincide with the center of mass. However, to support dynamics we must interpolate the center
    ///     of mass position.
    /// </summary>
    internal struct Sweep
    {
        #region Fields

        /// <summary>Local center of mass position.</summary>
        public LocalPoint LocalCenter;

        /// <summary>Center world positions.</summary>
        public WorldPoint CenterOfMass0, CenterOfMass;

        /// <summary>World angles.</summary>
        public float Angle0, Angle;

        /// <summary>Fraction of the current time step in the range [0,1] c0 and a0 are the positions at alpha0.</summary>
        public float Alpha0;

        #endregion

        #region Accessors

        /// <summary>Get the interpolated transform at a specific time.</summary>
        /// <param name="xf">The transform at the specified time.</param>
        /// <param name="beta">is a factor in [0,1], where 0 indicates alpha0.</param>
        public void GetTransform(out WorldTransform xf, float beta)
        {
            var angle = (1.0f - beta) * Angle0 + beta * Angle;
            var sin = (float) System.Math.Sin(angle);
            var cos = (float) System.Math.Cos(angle);

            xf.Translation = (1.0f - beta) * CenterOfMass0 + beta * CenterOfMass;
            xf.Rotation.Sin = sin;
            xf.Rotation.Cos = cos;

            // Shift to origin.
            xf.Translation -= xf.Rotation * LocalCenter;
        }

        /// <summary>Advance the sweep forward, yielding a new initial state.</summary>
        /// <param name="alpha">the new initial time</param>
        public void Advance(float alpha)
        {
            System.Diagnostics.Debug.Assert(Alpha0 < 1.0f);

            var beta = (alpha - Alpha0) / (1.0f - Alpha0);
            CenterOfMass0 = (1.0f - beta) * CenterOfMass0 + beta * CenterOfMass;
            Angle0 = (1.0f - beta) * Angle0 + beta * Angle;
            Alpha0 = alpha;
        }

        /// <summary>Normalize the angles.</summary>
        public void Normalize()
        {
            var d = MathHelper.TwoPi * (float) System.Math.Floor(Angle0 / MathHelper.TwoPi);
            Angle0 -= d;
            Angle -= d;
        }

        #endregion

        #region ToString

        /// <summary>
        ///     Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        ///     A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return
                string.Format(
                    "{{LocalCenter:{0} CenterOfMass0:{1} CenterOfMass:{2} Angle0:{3} Angle:{4} Alpha0:{5}}}",
                    LocalCenter,
                    CenterOfMass0,
                    CenterOfMass,
                    Angle0,
                    Angle,
                    Alpha0);
        }

        #endregion
    }

    /// <summary>Packet write and read methods for math types.</summary>
    internal static class PacketSweepExtensions
    {
        /// <summary>Writes the specified sweep value.</summary>
        /// <param name="packet">The packet.</param>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public static IWritablePacket Write(this IWritablePacket packet, Sweep data)
        {
            return packet
                .Write(data.LocalCenter)
                .Write(data.CenterOfMass0)
                .Write(data.CenterOfMass)
                .Write(data.Angle0)
                .Write(data.Angle)
                .Write(data.Alpha0);
        }

        /// <summary>Reads a sweep value.</summary>
        /// <param name="packet">The packet.</param>
        /// <param name="data">The read value.</param>
        /// <returns>This packet, for call chaining.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        public static IReadablePacket Read(this IReadablePacket packet, out Sweep data)
        {
            data = packet.ReadSweep();
            return packet;
        }

        /// <summary>Reads a sweep value.</summary>
        /// <param name="packet">The packet.</param>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        public static Sweep ReadSweep(this IReadablePacket packet)
        {
            Sweep result;
            packet.Read(out result.LocalCenter);
            packet.Read(out result.CenterOfMass0);
            packet.Read(out result.CenterOfMass);
            packet.Read(out result.Angle0);
            packet.Read(out result.Angle);
            packet.Read(out result.Alpha0);
            return result;
        }
    }
}