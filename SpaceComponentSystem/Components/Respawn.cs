﻿using System;
using System.Collections.Generic;
using System.IO;
using Engine.ComponentSystem.Components;
using Engine.FarMath;
using Engine.Serialization;
using Engine.Util;

namespace Space.ComponentSystem.Components
{
    /// <summary>Allows a timed death for entities, meaning they will respawn automatically after a specified timeout.</summary>
    public sealed class Respawn : Component
    {
        #region Type ID

        /// <summary>The unique type ID for this object, by which it is referred to in the manager.</summary>
        public static readonly int TypeId = CreateTypeId();

        /// <summary>The type id unique to the entity/component system in the current program.</summary>
        public override int GetTypeId()
        {
            return TypeId;
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Returns whether the component is currently in respawn mode, i.e. the entity is to be considered dead, and
        ///     we're waiting to respawn it.
        /// </summary>
        public bool IsRespawning
        {
            get { return TimeToRespawn > 0; }
        }

        #endregion

        #region Fields

        /// <summary>A list of components which should be disabled while dead.</summary>
        [CopyIgnore, PacketizeIgnore]
        public readonly List<int> ComponentsToDisable = new List<int>();

        /// <summary>The number of ticks to wait before respawning the entity.</summary>
        public int Delay;

        /// <summary>The position at which to respawn the entity.</summary>
        public FarPosition Position;

        /// <summary>The relative amount of its maximum health the entity should have after respawning.</summary>
        public float RelativeHealth;

        /// <summary>The relative amount of its maximum energy the entity should have after respawning.</summary>
        public float RelativeEnergy;

        /// <summary>The remaining time in ticks until to respawn the entity.</summary>
        internal int TimeToRespawn;

        #endregion

        #region Initialization

        /// <summary>Initialize the component by using another instance of its type.</summary>
        /// <param name="other">The component to copy the values from.</param>
        public override Component Initialize(Component other)
        {
            base.Initialize(other);

            ComponentsToDisable.AddRange(((Respawn) other).ComponentsToDisable);

            return this;
        }

        /// <summary>Initialize with the specified parameters.</summary>
        /// <param name="delay">The delay.</param>
        /// <param name="disableComponents">The disable components.</param>
        /// <param name="position">The position.</param>
        /// <param name="relativeHealth">The relative health.</param>
        /// <param name="relativeEnergy">The relative energy.</param>
        public Respawn Initialize(
            int delay,
            IEnumerable<Type> disableComponents,
            FarPosition position,
            float relativeHealth = 1f,
            float relativeEnergy = 1f)
        {
            Delay = delay;
            Position = position;
            if (disableComponents != null)
            {
                foreach (var type in disableComponents)
                {
                    ComponentsToDisable.Add(Engine.ComponentSystem.Manager.GetComponentTypeId(type));
                }
            }
            RelativeHealth = relativeHealth;
            RelativeEnergy = relativeEnergy;

            return this;
        }

        /// <summary>Reset the component to its initial state, so that it may be reused without side effects.</summary>
        public override void Reset()
        {
            base.Reset();

            Delay = 0;
            Position = FarPosition.Zero;
            ComponentsToDisable.Clear();
            RelativeHealth = 1;
            RelativeEnergy = 1;
        }

        #endregion

        #region Serialization / Hashing

        [OnPacketize]
        public IWritablePacket Packetize(IWritablePacket packet)
        {
            packet.Write(ComponentsToDisable.Count);
            foreach (var componentType in ComponentsToDisable)
            {
                packet.Write(componentType);
            }

            return packet;
        }

        [OnPostDepacketize]
        public void Depacketize(IReadablePacket packet)
        {
            ComponentsToDisable.Clear();
            var componentCount = packet.ReadInt32();
            for (var i = 0; i < componentCount; i++)
            {
                ComponentsToDisable.Add(packet.ReadInt32());
            }
        }

        [OnStringify]
        public StreamWriter Dump(StreamWriter w, int indent)
        {
            w.AppendIndent(indent).Write("ComponentsToDisable = {");
            var first = true;
            foreach (var component in ComponentsToDisable)
            {
                if (!first)
                {
                    w.Write(", ");
                }
                first = false;
                w.Write(component);
            }
            w.Write("}");

            return w;
        }

        #endregion
    }
}