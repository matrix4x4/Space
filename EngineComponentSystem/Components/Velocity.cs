﻿using System;
using Engine.Math;
using Engine.Serialization;
using Engine.Util;

namespace Engine.ComponentSystem.Components
{
    /// <summary>
    /// Represents the velocity of an object.
    /// </summary>
    public sealed class Velocity : AbstractComponent
    {
        #region Properties

        /// <summary>
        /// The directed speed of the object.
        /// </summary>
        public FPoint Value { get; set; }

        #endregion

        #region Serialization / Hashing

        public override Packet Packetize(Packet packet)
        {
            return base.Packetize(packet)
                .Write(Value);
        }

        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);
            
            Value = packet.ReadFPoint();
        }

        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);
            
            hasher.Put(BitConverter.GetBytes(Value.X.RawValue));
            hasher.Put(BitConverter.GetBytes(Value.Y.RawValue));
        }

        #endregion
    }
}
