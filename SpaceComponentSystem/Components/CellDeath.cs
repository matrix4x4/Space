﻿using Engine.ComponentSystem.Components;

namespace Space.ComponentSystem.Components
{
    /// <summary>This component does nothing, it just marks an entity for removal on cell death.</summary>
    public sealed class CellDeath : Component
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

        public bool IsForSubCell { get; private set; }

        #endregion

        #region Initialization

        public CellDeath Initialize(bool isForSubCell)
        {
            IsForSubCell = isForSubCell;

            return this;
        }

        public override void Reset()
        {
            base.Reset();

            IsForSubCell = false;
        }

        #endregion
    }
}