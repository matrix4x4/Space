﻿using System;
using Engine.ComponentSystem.Entities;
using Engine.ComponentSystem.Parameterizations;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Components
{
    /// <summary>
    /// When attached to a component with a transform, this will automatically
    /// position the component to follow an ellipsoid path around a specified
    /// center entity.
    /// </summary>
    public sealed class EllipsePath : AbstractComponent
    {
        #region Fields

        /// <summary>
        /// The id of the entity the entity this component belongs to
        /// rotates around.
        /// </summary>
        public int CenterEntityId;

        /// <summary>
        /// The radius of the ellipse along the major axis.
        /// </summary>
        public float MajorRadius;

        /// <summary>
        /// The radius of the ellipse along the minor axis.
        /// </summary>
        public float MinorRadius;

        /// <summary>
        /// The angle of the ellipse's major axis to the global x axis.
        /// </summary>
        public float Angle;

        /// <summary>
        /// The time in frames it takes for the component to perform a full
        /// rotation around its center.
        /// </summary>
        public float Period;

        /// <summary>
        /// Starting offset of our period (otherwise all objects with the same
        /// period will always be at the same angle...)
        /// </summary>
        public float PeriodOffset;

        /// <summary>
        /// Actual value of the angle.
        /// </summary>
        private float _angle;

        /// <summary>
        /// Precomputed sine of the angle.
        /// </summary>
        private float _sinPhi;

        /// <summary>
        /// Precomputed cosine of the angle.
        /// </summary>
        private float _cosPhi = 1;

        #endregion

        #region Constructor

        public EllipsePath(int centerEntityId, float majorRadius, float minorRadius, float angle, float period, float periodOffset)
        {
            this.CenterEntityId = centerEntityId;
            if (majorRadius < minorRadius)
            {
                this.MajorRadius = minorRadius;
                this.MinorRadius = majorRadius;
            }
            else
            {
                this.MajorRadius = majorRadius;
                this.MinorRadius = minorRadius;
            }
            this.Angle = angle;
            this.Period = period;
            this.PeriodOffset = periodOffset;
        }

        public EllipsePath()
        {
        }

        #endregion

        #region Logic

        /// <summary>
        /// Updates an objects position based on this velocity.
        /// </summary>
        /// <param name="parameterization">The parameterization to use.</param>
        public override void Update(object parameterization)
        {
            var transform = Entity.GetComponent<Transform>();

            // Only if a transform is set.
            if (transform != null)
            {
                var args = (DefaultLogicParameterization)parameterization;

                // Try to get the center of the entity we're rotating around.
                Vector2 center = Vector2.Zero;
                var centerEntity = Entity.Manager.GetEntity(CenterEntityId);
                if (centerEntity != null)
                {
                    var centerTransform = centerEntity.GetComponent<Transform>();
                    if (centerTransform != null)
                    {
                        center = centerTransform.Translation;
                    }
                }

                // Get the angle based on the time passed.
                double t = PeriodOffset + System.Math.PI * args.Frame / Period;
                float sinT = (float)System.Math.Sin(t);
                float cosT = (float)System.Math.Cos(t);

                var f = (float)System.Math.Sqrt(System.Math.Abs(MinorRadius * MinorRadius - MajorRadius * MajorRadius));

                // If our angle changed, recompute our sine and cosine.
                if (_angle != Angle)
                {
                    _angle = Angle;
                    _sinPhi = (float)System.Math.Sin(_angle);
                    _cosPhi = (float)System.Math.Cos(_angle);
                }

                // Compute the current position and set it.
                transform.SetTranslation(
                    center.X + f * _cosPhi + MajorRadius * cosT * _cosPhi - MinorRadius * sinT * _sinPhi,
                    center.Y + f * _sinPhi + MajorRadius * cosT * _sinPhi + MinorRadius * sinT * _cosPhi
                );
            }
        }

        /// <summary>
        /// Accepts <c>DefaultLogicParameterization</c>s.
        /// </summary>
        /// <param name="parameterizationType">the type to check.</param>
        /// <returns>whether the type's supported or not.</returns>
        public override bool SupportsUpdateParameterization(Type parameterizationType)
        {
            return parameterizationType == typeof(DefaultLogicParameterization);
        }

        #endregion

        #region Serialization / Hashing

        public override Packet Packetize(Packet packet)
        {
            return base.Packetize(packet)
                .Write(CenterEntityId)
                .Write(MajorRadius)
                .Write(MinorRadius)
                .Write(Angle)
                .Write(Period)
                .Write(PeriodOffset);
        }

        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);
            CenterEntityId = packet.ReadInt32();
            MajorRadius = packet.ReadSingle();
            MinorRadius = packet.ReadSingle();
            Angle = packet.ReadSingle();
            Period = packet.ReadSingle();
            PeriodOffset = packet.ReadSingle();
        }

        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);
            hasher.Put(BitConverter.GetBytes(CenterEntityId));
            hasher.Put(BitConverter.GetBytes(MajorRadius));
            hasher.Put(BitConverter.GetBytes(MinorRadius));
            hasher.Put(BitConverter.GetBytes(Angle));
            hasher.Put(BitConverter.GetBytes(Period));
            hasher.Put(BitConverter.GetBytes(PeriodOffset));
        }

        #endregion

        #region Copying

        protected override bool ValidateType(AbstractComponent instance)
        {
            return instance is EllipsePath;
        }

        protected override void CopyFields(AbstractComponent into, bool isShallowCopy)
        {
            base.CopyFields(into, isShallowCopy);

            if (!isShallowCopy)
            {
                var copy = (EllipsePath)into;

                copy.CenterEntityId = CenterEntityId;
                copy.MajorRadius = MajorRadius;
                copy.MinorRadius = MinorRadius;
                copy.Angle = Angle;
                copy.Period = Period;
                copy.PeriodOffset = PeriodOffset;

                copy._angle = _angle;
                copy._sinPhi = _sinPhi;
                copy._cosPhi = _cosPhi;
            }
        }

        #endregion

        #region ToString

        public override string ToString()
        {
            return GetType().Name + ": " + CenterEntityId + ", " + MajorRadius + ", " + MinorRadius + ", " + Angle + ", " + Period + ", " + PeriodOffset;
        }

        #endregion
    }
}
