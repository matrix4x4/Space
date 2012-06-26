﻿using Engine.ComponentSystem.Components;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Systems
{
    /// <summary>
    /// Handles expiring components by removing them from the simulation when
    /// they expire.
    /// </summary>
    public sealed class ExpirationSystem : AbstractComponentSystem<Expiration>
    {
        protected override void UpdateComponent(GameTime gameTime, long frame, Expiration component)
        {
            if (component.TimeToLive > 0)
            {
                --component.TimeToLive;
            }
            else
            {
                Manager.RemoveEntity(component.Entity);
            }
        }
    }
}