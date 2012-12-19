﻿using System;
using Engine.ComponentSystem.Common.Components;
using Engine.ComponentSystem.Systems;
using Microsoft.Xna.Framework;
using Space.ComponentSystem.Components;
using Space.ComponentSystem.Messages;
using Space.Data;

namespace Space.ComponentSystem.Systems
{
    /// <summary>
    /// This system interprets messages and triggers combat floating text
    /// accordingly.
    /// </summary>
    public sealed class CombatTextSystem : AbstractSystem, IUpdatingSystem, IMessagingSystem
    {
        #region Fields

        /// <summary>
        /// The faction of the local player.
        /// </summary>
        private Factions _localPlayerFaction;

        #endregion

        #region Logic

        /// <summary>
        /// Updates the system.
        /// </summary>
        /// <param name="frame">The frame in which the update is applied.</param>
        public void Update(long frame)
        {
            var avatar = ((LocalPlayerSystem)Manager.GetSystem(LocalPlayerSystem.TypeId)).LocalPlayerAvatar;
            _localPlayerFaction = avatar > 0
                ? ((Faction)Manager.GetComponent(avatar, Faction.TypeId)).Value
                : Factions.None;
        }

        /// <summary>
        /// Handle a message of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="message">The message.</param>
        public void Receive<T>(T message) where T : struct
        {
            {
                var cm = message as DamageApplied?;
                if (cm != null)
                {
                    var position = ((Transform)Manager.GetComponent(cm.Value.Entity, Transform.TypeId)).Translation;
                    var value = (int)Math.Round(cm.Value.Amount);
                    var scale = cm.Value.IsCriticalHit ? 1f : 0.5f;
                    var isLocalPlayerFaction = (_localPlayerFaction & ((Faction)Manager.GetComponent(cm.Value.Entity, Faction.TypeId)).Value) != Factions.None;
                    if (value > 0)
                    {
                        // Normal damage.

                        var color = isLocalPlayerFaction ? Color.Red : (cm.Value.IsCriticalHit ? Color.Yellow : Color.White);
                        ((FloatingTextSystem)Manager.GetSystem(FloatingTextSystem.TypeId))
                            .Display(value, position, color, scale);
                    }
                    else
                    {
                        value = (int)Math.Round(cm.Value.ShieldedAmount);
                        if (value > 0)
                        {
                            // Shield damage.
                            var color = isLocalPlayerFaction ? Color.Red : (cm.Value.IsCriticalHit ? Color.Yellow : Color.LightBlue);
                            ((FloatingTextSystem)Manager.GetSystem(FloatingTextSystem.TypeId))
                                .Display(value, position, color, scale);
                        }
                        else
                        {
                            // No damage.
                            var color = isLocalPlayerFaction ? Color.LightBlue : (cm.Value.IsCriticalHit ? Color.Yellow : Color.Purple);
                            ((FloatingTextSystem)Manager.GetSystem(FloatingTextSystem.TypeId))
                                .Display("Absorbed", position, color, scale);
                        }
                    }
                }
            }
            {
                var cm = message as DamageBlocked?;
                if (cm != null)
                {
                    var position = ((Transform)Manager.GetComponent(cm.Value.Entity, Transform.TypeId)).Translation;
                    ((FloatingTextSystem)Manager.GetSystem(FloatingTextSystem.TypeId))
                        .Display("Blocked", position, Color.LightBlue, 0.5f);
                }
            }
        }

        #endregion
    }
}