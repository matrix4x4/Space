﻿using System;
using Engine.ComponentSystem.Messages;
using Engine.ComponentSystem.Spatial.Components;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Space.ComponentSystem.Components;
using Space.ComponentSystem.Messages;
using Space.Data;

namespace Space.ComponentSystem.Systems
{
    /// <summary>This system interprets messages and triggers combat floating text accordingly.</summary>
    [Packetizable(false), PresentationOnlyAttribute]
    public sealed class CombatTextSystem : AbstractSystem
    {
        /// <summary>Determines whether this system is enabled, i.e. whether it should draw.</summary>
        /// <value>
        ///     <c>true</c> if this instance is enabled; otherwise, <c>false</c>.
        /// </value>
        [PublicAPI]
        public bool Enabled { get; set; }

        #region Fields

        /// <summary>The faction of the local player.</summary>
        private Factions _localPlayerFaction;

        #endregion

        #region Logic

        /// <summary>Draws the system.</summary>
        [MessageCallback]
        public void OnDraw(Draw message)
        {
            if (!Enabled)
            {
                return;
            }

            var avatar = ((LocalPlayerSystem) Manager.GetSystem(LocalPlayerSystem.TypeId)).LocalPlayerAvatar;
            _localPlayerFaction = avatar > 0
                                      ? ((Faction) Manager.GetComponent(avatar, Faction.TypeId)).Value
                                      : Factions.None;
        }
        
        /// <summary>Store for performance.</summary>
        private static readonly int TransformTypeId = Engine.ComponentSystem.Manager.GetComponentTypeId<ITransform>();

        [MessageCallback]
        public void OnDamageApplied(DamageApplied message)
        {
            var position = ((ITransform) Manager.GetComponent(message.Entity, TransformTypeId)).Position;
            var value = (int) Math.Round(message.Amount);
            var scale = message.IsCriticalHit ? 1f : 0.5f;
            var faction = Manager.GetComponent(message.Entity, Faction.TypeId) as Faction;
            var isLocalPlayerFaction = faction != null && (_localPlayerFaction & faction.Value) != Factions.None;
            if (value > 0)
            {
                // Normal damage.
                var color = isLocalPlayerFaction
                                ? Color.Red
                                : (message.IsCriticalHit ? Color.Yellow : Color.White);
                ((FloatingTextSystem) Manager.GetSystem(FloatingTextSystem.TypeId))
                    .Display(value, position, color, scale);
            }
            else
            {
                value = (int) Math.Round(message.ShieldedAmount);
                if (value > 0)
                {
                    // Shield damage.
                    var color = isLocalPlayerFaction
                                    ? Color.Violet
                                    : (message.IsCriticalHit ? Color.Yellow : Color.LightBlue);
                    ((FloatingTextSystem) Manager.GetSystem(FloatingTextSystem.TypeId))
                        .Display(value, position, color, scale);
                }
                else
                {
                    // No damage.
                    var color = isLocalPlayerFaction
                                    ? Color.LightBlue
                                    : (message.IsCriticalHit ? Color.Yellow : Color.Purple);
                    ((FloatingTextSystem) Manager.GetSystem(FloatingTextSystem.TypeId))
                        .Display("Absorbed", position, color, scale);
                }
            }
        }

        [MessageCallback]
        public void OnDamageBlocked(DamageBlocked message)
        {
            var position = ((ITransform) Manager.GetComponent(message.Entity, TransformTypeId)).Position;
            ((FloatingTextSystem) Manager.GetSystem(FloatingTextSystem.TypeId))
                .Display("Blocked", position, Color.LightBlue, 0.5f);
        }

        #endregion
    }
}