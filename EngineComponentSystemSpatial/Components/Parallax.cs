﻿using Engine.ComponentSystem.Components;

namespace Engine.ComponentSystem.Spatial.Components
{
    /// <summary>This component defines in which layer to render an entity in a parallax render system.</summary>
    public sealed class Parallax : Component
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

        #region Fields

        /// <summary>The "layer" the component is in.</summary>
        /// <remarks>
        ///     This directly translates to the offset used when rendering it, where <c>1.0f</c>
        ///     means 1:1 mapping of coordinate to screen space. Lower values make objects "move slower"/appear further back,
        ///     higher values do the opposite.
        /// </remarks>
        public float Layer = 1.0f;

        #endregion

        #region Initialization

        /// <summary>Initialize with the specified layer.</summary>
        /// <param name="layer">The layer.</param>
        public Parallax Initialize(float layer)
        {
            Layer = layer;

            return this;
        }

        /// <summary>Reset the component to its initial state, so that it may be reused without side effects.</summary>
        public override void Reset()
        {
            base.Reset();

            Layer = 1.0f;
        }

        #endregion
    }
}