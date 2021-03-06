﻿using Engine.ComponentSystem.Components;

namespace Engine.ComponentSystem.Common.Components
{
    /// <summary>
    ///     Entities with this component have an expiration date, after which they will be removed from the entity
    ///     manager.
    /// </summary>
    public sealed class Expiration : Component
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

        /// <summary>The number remaining updates the entity this component belongs to is allowed to live.</summary>
        public int TimeToLive;

        #endregion

        #region Initialization

        /// <summary>Initializes the component with the specified TTL.</summary>
        /// <param name="ttl">The time the object has to live.</param>
        public Expiration Initialize(int ttl)
        {
            TimeToLive = ttl;

            return this;
        }

        /// <summary>Reset the component to its initial state, so that it may be reused without side effects.</summary>
        public override void Reset()
        {
            base.Reset();

            TimeToLive = 0;
        }

        #endregion
    }
}