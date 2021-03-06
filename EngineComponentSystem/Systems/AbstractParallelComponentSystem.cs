﻿using System.Threading.Tasks;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Messages;

namespace Engine.ComponentSystem.Systems
{
    /// <summary>
    ///     Base class for component systems that support parallelized updates, i.e. the update for each component is
    ///     thread safe.
    /// </summary>
    /// <typeparam name="TComponent">The type of component handled in this system.</typeparam>
    public abstract class AbstractParallelComponentSystem<TComponent> : AbstractUpdatingComponentSystem<TComponent>
        where TComponent : IComponent
    {
        #region Logic

        /// <summary>
        ///     Loops over all components and calls <c>UpdateComponent()</c>.
        /// </summary>
        public override void OnUpdate(Update message)
        {
            // We can use the components collection directly, because we must not
            // change the manager in parallel mode, anyway.
            Parallel.ForEach(
                Components,
                component =>
                {
                    if (component.Enabled)
                    {
                        UpdateComponent(message.Frame, component);
                    }
                });
        }

        #endregion
    }
}