﻿using System;
using System.Globalization;
using Engine.ComponentSystem.Components;
using Engine.Serialization;
using Engine.XnaExtensions;
using Microsoft.Xna.Framework;

namespace Space.ComponentSystem.Components
{
    /// <summary>
    /// Handles control of a single ship, represented by its relevant components.
    /// 
    /// <para>
    /// Requires: <c>Transform</c>, <c>Spin</c>, <c>Acceleration</c>, <c>MovementProperties</c>.
    /// </para>
    /// </summary>
    public sealed class ShipControl : Component
    {
        #region Type ID

        /// <summary>
        /// The unique type ID for this object, by which it is referred to in the manager.
        /// </summary>
        public static readonly int TypeId = CreateTypeId();

        /// <summary>
        /// The type id unique to the entity/component system in the current program.
        /// </summary>
        public override int GetTypeId()
        {
            return TypeId;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Whether we're currently stabilizing our position or not.
        /// </summary>
        public bool Stabilizing;

        /// <summary>
        /// Whether to shoot or not.
        /// </summary>
        public bool Shooting;

        /// <summary>
        /// The current acceleration of the ship.
        /// </summary>
        internal Vector2 DirectedAcceleration;

        /// <summary>
        /// The current target rotation (used to check if the public one
        /// changed since the last update).
        /// </summary>
        internal float TargetRotation;

        /// <summary>
        /// Flag whether the rotation changed since the last update.
        /// </summary>
        internal bool TargetRotationChanged;

        /// <summary>
        /// The rotation we had in the previous update.
        /// </summary>
        internal float PreviousRotation;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the component by using another instance of its type.
        /// </summary>
        /// <param name="other">The component to copy the values from.</param>
        public override Component Initialize(Component other)
        {
            base.Initialize(other);

            var otherControl = (ShipControl)other;
            Stabilizing = otherControl.Stabilizing;
            Shooting = otherControl.Shooting;
            DirectedAcceleration = otherControl.DirectedAcceleration;
            TargetRotation = otherControl.TargetRotation;
            TargetRotationChanged = otherControl.TargetRotationChanged;
            PreviousRotation = otherControl.PreviousRotation;

            return this;
        }

        /// <summary>
        /// Reset the component to its initial state, so that it may be reused
        /// without side effects.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            Stabilizing = false;
            Shooting = false;
            DirectedAcceleration = Vector2.Zero;
            TargetRotation = 0;
            TargetRotationChanged = false;
            PreviousRotation = 0;
        }

        #endregion

        #region Utility Accessors

        /// <summary>
        /// Begin or continue accelerating in the specified direction, or stop
        /// accelerating if <c>Vector2.Zero</c> is given.
        /// </summary>
        /// <param name="direction">The new directed acceleration.</param>
        public void SetAcceleration(Vector2 direction)
        {
            DirectedAcceleration = direction;
            if (DirectedAcceleration != Vector2.Zero)
            {
                // Make sure we have a unit vector of our direction.
                if (DirectedAcceleration.LengthSquared() > 1)
                {
                    DirectedAcceleration.Normalize();
                }
            }
        }

        /// <summary>
        /// Set a new target rotation for this ship.
        /// </summary>
        /// <param name="rotation">The rotation to rotate to.</param>
        public void SetTargetRotation(float rotation)
        {
            if (Math.Abs(TargetRotation - rotation) > 0.001f)
            {
                TargetRotation = rotation;
                TargetRotationChanged = true;
            }
        }

        #endregion

        #region Serialization / Hashing

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
                .Write(Stabilizing)
                .Write(Shooting)
                .Write(DirectedAcceleration)
                .Write(TargetRotation)
                .Write(TargetRotationChanged)
                .Write(PreviousRotation);
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            Stabilizing = packet.ReadBoolean();
            Shooting = packet.ReadBoolean();
            DirectedAcceleration = packet.ReadVector2();
            TargetRotation = packet.ReadSingle();
            TargetRotationChanged = packet.ReadBoolean();
            PreviousRotation = packet.ReadSingle();
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);

            hasher.Put(Stabilizing);
            hasher.Put(Shooting);
            hasher.Put(DirectedAcceleration);
            hasher.Put(TargetRotation);
            hasher.Put(TargetRotationChanged);
            hasher.Put(PreviousRotation);
        }

        #endregion

        #region ToString

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return base.ToString() + ", Stabilizing=" + Stabilizing + ", Shooting=" + Shooting + ", DirectedAcceleration=" + DirectedAcceleration.X.ToString(CultureInfo.InvariantCulture) + ":" + DirectedAcceleration.Y.ToString(CultureInfo.InvariantCulture) + ", TargetRotation=" + TargetRotation.ToString(CultureInfo.InvariantCulture) + ", TargetRotationChanged=" + TargetRotationChanged + ", PreviousRotation=" + PreviousRotation.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }
}