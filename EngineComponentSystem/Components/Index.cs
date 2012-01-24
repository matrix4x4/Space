﻿using System;
using Engine.ComponentSystem.Messages;
using Engine.ComponentSystem.Parameterizations;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Components
{
    /// <summary>
    /// Allows tracking a component with a position in the <c>IndexSystem</c>
    /// for quick nearest neighbor queries.
    /// </summary>
    public sealed class Index : AbstractComponent
    {
        #region Fields

        /// <summary>
        /// The bit mask of the index group this component will belong to.
        /// There are a total of 64 separate groups, via the 64 bits in a
        /// ulong.
        /// </summary>
        public ulong IndexGroups;

        /// <summary>
        /// Whether the position of our entity changed since the last update.
        /// </summary>
        public bool PositionChanged;
        
        /// <summary>
        /// The position we had before a position change. This corresponds to
        /// the position at which this entity is stored at in the index.
        /// </summary>
        public Vector2 PreviousPosition;

        /// <summary>
        /// The cell id we were in at our previous position.
        /// </summary>
        private ulong _previousCellId;

        /// <summary>
        /// Whether the actual index cell we're in has changed since the last
        /// update.
        /// </summary>
        private bool _cellIdChanged;

        #endregion

        #region Constructor

        public Index(ulong groups)
        {
            this.IndexGroups = groups;
        }

        public Index()
            : this(IndexSystem.DefaultIndexGroup)
        {
        }

        #endregion

        #region Logic

        /// <summary>
        /// Tells the index system whether our position changed.
        /// </summary>
        /// <param name="parameterization">The parameterization to use.</param>
        public override void Update(object parameterization)
        {
            var args = (IndexParameterization)parameterization;

            if (_cellIdChanged)
            {
                args.IndexGroups = IndexGroups;
                args.PositionChanged = true;
                args.PreviousPosition = PreviousPosition;

                PositionChanged = false;
                _cellIdChanged = false;

                // Remember the finest index cell we might now be in.
                var position = Entity.GetComponent<Transform>().Translation;
                _previousCellId = CoordinateIds.Combine(
                    (int)position.X >> IndexSystem.MinimumNodeSizeShift,
                    (int)position.Y >> IndexSystem.MinimumNodeSizeShift);
            }
        }

        /// <summary>
        /// Supports <c>IndexParameterization</c>.
        /// </summary>
        /// <param name="parameterizationType">The type to check.</param>
        /// <returns>Whether it's supported or not.</returns>
        public override bool SupportsUpdateParameterization(Type parameterizationType)
        {
            return parameterizationType == typeof(IndexParameterization);
        }

        /// <summary>
        /// Uses <c>TranslationChanged</c> messages to set our changed flag.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        public override void HandleMessage<T>(ref T message)
        {
            if (message is TranslationChanged)
            {
                // Position changed. If this is the first, remember this as
                // our previous position.
                if (!PositionChanged)
                {
                    PreviousPosition = ((TranslationChanged)(ValueType)message).PreviousPosition;
                }
                PositionChanged = true;

                // Check if the actual index cell we're in might have changed.
                var position = Entity.GetComponent<Transform>().Translation;
                var cellId = CoordinateIds.Combine(
                    (int)position.X >> IndexSystem.MinimumNodeSizeShift,
                    (int)position.Y >> IndexSystem.MinimumNodeSizeShift);
                if (cellId != _previousCellId)
                {
                    // Actual cell we might be in in the index has changed.
                    _cellIdChanged = true;
                }
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
            base.Packetize(packet);

            packet.Write(IndexGroups);
            packet.Write(PositionChanged);
            packet.Write(PreviousPosition);
            packet.Write(_cellIdChanged);
            packet.Write(_previousCellId);

            return packet;
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            IndexGroups = packet.ReadUInt64();
            PositionChanged = packet.ReadBoolean();
            PreviousPosition = packet.ReadVector2();
            _cellIdChanged = packet.ReadBoolean();
            _previousCellId = packet.ReadUInt64();
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);

            hasher.Put(BitConverter.GetBytes(IndexGroups));
            hasher.Put(BitConverter.GetBytes(PositionChanged));
            hasher.Put(BitConverter.GetBytes(PreviousPosition.X));
            hasher.Put(BitConverter.GetBytes(PreviousPosition.Y));
        }

        #endregion

        #region Copying

        /// <summary>
        /// Creates a deep copy of this instance by reusing the specified
        /// instance, if possible.
        /// </summary>
        /// <param name="into"></param>
        /// <returns>
        /// An independent (deep) clone of this instance.
        /// </returns>
        public override AbstractComponent DeepCopy(AbstractComponent into)
        {
            var copy = (Index)base.DeepCopy(into);

            if (copy == into)
            {
                copy.IndexGroups = IndexGroups;
                copy.PositionChanged = PositionChanged;
                copy.PreviousPosition = PreviousPosition;
                copy._cellIdChanged = _cellIdChanged;
                copy._previousCellId = _previousCellId;
            }

            return copy;
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
            return base.ToString() + ", IndexGroups = " + IndexGroups.ToString() + ", PositionChanged = " + PositionChanged.ToString() + ", PreviousPosition = " + PreviousPosition.ToString() + ", CellIdChanged = " + _cellIdChanged.ToString() + ", PreviousCellId = " + _previousCellId.ToString();
        }

        #endregion
    }
}
