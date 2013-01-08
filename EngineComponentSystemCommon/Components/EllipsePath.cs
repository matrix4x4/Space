﻿using Engine.ComponentSystem.Components;
using Engine.Serialization;

namespace Engine.ComponentSystem.Common.Components
{
    /// <summary>
    /// When attached to a component with a transform, this will automatically
    /// position the component to follow an ellipsoid path around a specified
    /// center entity.
    /// </summary>
    public sealed class EllipsePath : Component
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

        #region Properties

        /// <summary>
        /// The angle of the ellipse's major axis to the global x axis.
        /// </summary>
        public float Angle
        {
            get { return _angle; }
            set
            {
                _angle = value;
                Precompute();
            }
        }

        /// <summary>
        /// The radius of the ellipse along the major axis.
        /// </summary>
        public float MajorRadius
        {
            get { return _majorRadius; }
            set
            {
                _majorRadius = value;
                Precompute();
            }
        }

        /// <summary>
        /// The radius of the ellipse along the minor axis.
        /// </summary>
        public float MinorRadius
        {
            get { return _minorRadius; }
            set
            {
                _minorRadius = value;
                Precompute();
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// The id of the entity the entity this component belongs to
        /// rotates around.
        /// </summary>
        public int CenterEntityId;

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
        /// Precomputed for position calculation.
        /// </summary>
        /// <remarks>Do not change manually.</remarks>
        [PacketizerIgnore]
        internal float PrecomputedA;

        /// <summary>
        /// Precomputed for position calculation.
        /// </summary>
        /// <remarks>Do not change manually.</remarks>
        [PacketizerIgnore]
        internal float PrecomputedB;

        /// <summary>
        /// Precomputed for position calculation.
        /// </summary>
        /// <remarks>Do not change manually.</remarks>
        [PacketizerIgnore]
        internal float PrecomputedC;

        /// <summary>
        /// Precomputed for position calculation.
        /// </summary>
        /// <remarks>Do not change manually.</remarks>
        [PacketizerIgnore]
        internal float PrecomputedD;

        /// <summary>
        /// Precomputed for position calculation.
        /// </summary>
        /// <remarks>Do not change manually.</remarks>
        [PacketizerIgnore]
        internal float PrecomputedE;

        /// <summary>
        /// Precomputed for position calculation.
        /// </summary>
        /// <remarks>Do not change manually.</remarks>
        [PacketizerIgnore]
        internal float PrecomputedF;

        /// <summary>
        /// Actual value of the angle.
        /// </summary>
        private float _angle;

        /// <summary>
        /// Actual value of major radius.
        /// </summary>
        private float _majorRadius;

        /// <summary>
        /// Actual value of minor radius.
        /// </summary>
        private float _minorRadius;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the component by using another instance of its type.
        /// </summary>
        /// <param name="other">The component to copy the values from.</param>
        public override Component Initialize(Component other)
        {
            base.Initialize(other);

            var otherEllipsePath = (EllipsePath)other;
            Angle = otherEllipsePath._angle;
            MajorRadius = otherEllipsePath._majorRadius;
            MinorRadius = otherEllipsePath._minorRadius;
            CenterEntityId = otherEllipsePath.CenterEntityId;
            Period = otherEllipsePath.Period;
            PeriodOffset = otherEllipsePath.PeriodOffset;

            return this;
        }

        /// <summary>
        /// Initialize the component with the specified values.
        /// </summary>
        /// <param name="centerEntityId">The center entity's id.</param>
        /// <param name="majorRadius">The major radius.</param>
        /// <param name="minorRadius">The minor radius.</param>
        /// <param name="angle">The angle.</param>
        /// <param name="period">The period.</param>
        /// <param name="periodOffset">The period offset.</param>
        public EllipsePath Initialize(int centerEntityId, float majorRadius, float minorRadius,
            float angle, float period, float periodOffset)
        {
            CenterEntityId = centerEntityId;
            if (majorRadius < minorRadius)
            {
                MajorRadius = minorRadius;
                MinorRadius = majorRadius;
            }
            else
            {
                MajorRadius = majorRadius;
                MinorRadius = minorRadius;
            }
            Angle = angle;
            Period = period;
            PeriodOffset = periodOffset;

            return this;
        }

        /// <summary>
        /// Reset the component to its initial state, so that it may be reused
        /// without side effects.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            Angle = 0;
            MajorRadius = 0;
            MinorRadius = 0;
            CenterEntityId = 0;
            Period = 0;
            PeriodOffset = 0;
        }

        #endregion

        #region Precomputation

        /// <summary>
        /// Fills in precomputable values.
        /// </summary>
        private void Precompute()
        {
            // If our angle changed, recompute our sine and cosine.
            var sinPhi = (float)System.Math.Sin(_angle);
            var cosPhi = (float)System.Math.Cos(_angle);
            var f = (float)System.Math.Sqrt(System.Math.Abs(_minorRadius * _minorRadius - _majorRadius * _majorRadius));
            
            PrecomputedA = f * cosPhi;
            PrecomputedB = MajorRadius * cosPhi;
            PrecomputedC = MinorRadius * sinPhi;
            PrecomputedD = f * sinPhi;
            PrecomputedE = MajorRadius * sinPhi;
            PrecomputedF = MinorRadius * cosPhi;
        }

        #endregion

        #region Serialization / Hashing

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void PostDepacketize(IReadablePacket packet)
        {
            base.PostDepacketize(packet);

            Precompute();
        }

        #endregion
    }
}
