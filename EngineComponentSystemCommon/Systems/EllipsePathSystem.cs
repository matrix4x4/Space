﻿using System;
using Engine.ComponentSystem.Common.Components;
using Engine.ComponentSystem.Systems;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Common.Systems
{
    /// <summary>
    /// Makes an entity move along an ellipsoid path.
    /// </summary>
    public sealed class EllipsePathSystem : AbstractComponentSystem<EllipsePath>
    {
        #region Logic

        protected override void UpdateComponent(long frame, EllipsePath component)
        {
            // Get the center, the position of the entity we're rotating around.
            var center = ((Transform)Manager.GetComponent(component.CenterEntityId, Transform.TypeId)).Translation;

            // Get the angle based on the time passed.
            var t = component.PeriodOffset + MathHelper.Pi * frame / component.Period;
            var sinT = (float)Math.Sin(t);
            var cosT = (float)Math.Cos(t);

            // Compute the current position and set it.
            var transform = ((Transform)Manager.GetComponent(component.Entity, Transform.TypeId));
            transform.SetTranslation(
                center.X + component.PrecomputedA + component.PrecomputedB * cosT - component.PrecomputedC * sinT,
                center.Y + component.PrecomputedD + component.PrecomputedE * cosT + component.PrecomputedF * sinT
                );
        }

        #endregion
    }
}
