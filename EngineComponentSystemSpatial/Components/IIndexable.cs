﻿using Engine.ComponentSystem.Components;

#if FARMATH
using WorldBounds = Engine.FarMath.FarRectangle;
#else
using WorldBounds = Engine.Math.RectangleF;
#endif

namespace Engine.ComponentSystem.Spatial.Components
{
    /// <summary>
    ///     Interface for components that can be tracked via the <see cref="Systems.IndexSystem"/>.
    /// </summary>
    public interface IIndexable : IComponent
    {
        /// <summary>The index group mask determining which indexes the component will be tracked by.</summary>
        ulong IndexGroupsMask { get; set; }

        /// <summary>Computes the current world bounds of the component, to allow adding it to indexes.</summary>
        /// <returns>The current world bounds of the component.</returns>
        WorldBounds ComputeWorldBounds();
    }
}