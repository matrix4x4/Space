﻿using System;
using Engine.ComponentSystem.Parameterizations;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Components
{
    /// <summary>
    /// Base class for components that implement collision logic.
    /// 
    /// <para>
    /// Requires: <c>Transform</c>.
    /// </para>
    /// </summary>
    public abstract class AbstractCollidable : AbstractComponent
    {
        #region Fields

        /// <summary>
        /// This bit mask representing the collision groups this component is
        /// part of. Components sharing at least one group will not be tested
        /// against each other.
        /// </summary>
        public uint CollisionGroups;

        /// <summary>
        /// Previous position of the underlying physics component (for sweep tests).
        /// </summary>
        protected Vector2 _previousPosition;

        #endregion

        #region Constructor

        protected AbstractCollidable(uint groups)
        {
            this.CollisionGroups = groups;
        }

        protected AbstractCollidable()
            : this(0)
        {
        }

        #endregion

        #region Intersection

        /// <summary>
        /// Test if this collidable collides with the specified one.
        /// </summary>
        /// <param name="collidable">The other collidable to test against.</param>
        /// <returns>Whether the two collide or not.</returns>
        public abstract bool Intersects(AbstractCollidable collidable);

        internal abstract bool Intersects(ref Vector2 extents, ref Vector2 previousPosition, ref Vector2 position);

        internal abstract bool Intersects(float radius, ref Vector2 previousPosition, ref Vector2 position);

        #endregion

        #region Logic
        
        /// <summary>
        /// Checks for collisions between this component and others given in the parameterization.
        /// </summary>
        /// <param name="parameterization">the parameterization to use.</param>
        public override void Update(object parameterization)
        {
            // Update our previous position.
            var transform = Entity.GetComponent<Transform>();
            if (transform != null)
            {
                _previousPosition = transform.Translation;
            }
        }

        /// <summary>
        /// Accepts <c>CollisionParameterization</c>s.
        /// </summary>
        /// <param name="parameterizationType">the type to check.</param>
        /// <returns>whether the type's supported or not.</returns>
        public override bool SupportsUpdateParameterization(Type parameterizationType)
        {
            return parameterizationType == typeof(CollisionParameterization);
        }

        #endregion

        #region Serialization / Hashing

        public override Packet Packetize(Packet packet)
        {
            return base.Packetize(packet)
                .Write(CollisionGroups)
                .Write(_previousPosition);
        }

        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            CollisionGroups = packet.ReadUInt32();
            _previousPosition = packet.ReadVector2();
        }

        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);

            hasher.Put(BitConverter.GetBytes(CollisionGroups));
            hasher.Put(BitConverter.GetBytes(_previousPosition.X));
            hasher.Put(BitConverter.GetBytes(_previousPosition.Y));
        }

        #endregion

        #region Copying

        protected override void CopyFields(AbstractComponent into, bool isShallowCopy)
        {
            base.CopyFields(into, isShallowCopy);

            if (!isShallowCopy)
            {
                var copy = (AbstractCollidable)into;

                copy.CollisionGroups = CollisionGroups;
                copy._previousPosition = _previousPosition;
            }
        }

        #endregion
    }
}
