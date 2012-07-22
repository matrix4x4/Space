﻿using Engine.ComponentSystem.RPG.Components;
using Engine.ComponentSystem.Systems;

namespace Engine.ComponentSystem.RPG.Systems
{
    /// <summary>
    /// Handles updating status effects to make them expire, by removing the
    /// status effect component once it has run out.
    /// </summary>
    public sealed class StatusEffectSystem : AbstractComponentSystem<StatusEffect>
    {
        protected override void UpdateComponent(long frame, StatusEffect component)
        {
            if (component.Remaining > 0)
            {
                // Still running.
                --component.Remaining;
            }
            else
            {
                // Expired, remove self.
                Manager.RemoveComponent(component);
            }
        }
    }
}
