﻿using Engine.ComponentSystem.Components;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;

namespace Space.ComponentSystem.Components
{
    /// <summary>
    /// Represents sun visuals.
    /// </summary>
    public sealed class SunRenderer : Component
    {
        #region Fields

        /// <summary>
        /// The size of the sun.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Surface rotation of the sun.
        /// </summary>
        public Vector2 SurfaceRotation;

        /// <summary>
        /// Rotational direction of primary surface turbulence.
        /// </summary>
        public Vector2 PrimaryTurbulenceRotation;

        /// <summary>
        /// Rotational direction of secondary surface turbulence.
        /// </summary>
        public Vector2 SecondaryTurbulenceRotation;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the component by using another instance of its type.
        /// </summary>
        /// <param name="other">The component to copy the values from.</param>
        public override Component Initialize(Component other)
        {
            base.Initialize(other);

            var otherSun = (SunRenderer)other;
            Radius = otherSun.Radius;
            SurfaceRotation = otherSun.SurfaceRotation;
            PrimaryTurbulenceRotation = otherSun.PrimaryTurbulenceRotation;
            SecondaryTurbulenceRotation = otherSun.SecondaryTurbulenceRotation;

            return this;
        }

        /// <summary>
        /// Initialize with the specified radius.
        /// </summary>
        /// <param name="radius">The radius of the sun.</param>
        /// <param name="surfaceRotation">Surface rotation of the sun.</param>
        /// <param name="primaryTurbulenceRotation">Rotational direction of primary surface turbulence.</param>
        /// <param name="secondaryTurbulenceRotation">Rotational direction of secondary surface turbulence.</param>
        /// <returns></returns>
        public SunRenderer Initialize(float radius, Vector2 surfaceRotation, Vector2 primaryTurbulenceRotation, Vector2 secondaryTurbulenceRotation)
        {
            Radius = radius;
            SurfaceRotation = surfaceRotation;
            PrimaryTurbulenceRotation = primaryTurbulenceRotation;
            SecondaryTurbulenceRotation = secondaryTurbulenceRotation;

            return this;
        }

        /// <summary>
        /// Reset the component to its initial state, so that it may be reused
        /// without side effects.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            Radius = 0;
            SurfaceRotation = Vector2.Zero;
            PrimaryTurbulenceRotation = Vector2.Zero;
            SecondaryTurbulenceRotation = Vector2.Zero;
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>
        /// The packet after writing.
        /// </returns>
        public override Packet Packetize(Packet packet)
        {
            return base.Packetize(packet)
                .Write(Radius)
                .Write(SurfaceRotation)
                .Write(PrimaryTurbulenceRotation)
                .Write(SecondaryTurbulenceRotation);
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            Radius = packet.ReadInt32();
            SurfaceRotation = packet.ReadVector2();
            PrimaryTurbulenceRotation = packet.ReadVector2();
            SecondaryTurbulenceRotation = packet.ReadVector2();
        }

#if DEBUG
        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);

            hasher.Put(Radius);
            hasher.Put(SurfaceRotation);
            hasher.Put(PrimaryTurbulenceRotation);
            hasher.Put(SecondaryTurbulenceRotation);
        }
#endif

        #endregion
    }
}
