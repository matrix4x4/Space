﻿using Engine.ComponentSystem.Components;

namespace Engine.ComponentSystem.Spatial.Components
{
    public sealed class Sound : Component
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

        /// <summary>The name of the sound</summary>
        public string SoundName;

        #endregion

        #region Initialization

        /// <summary>Initialize the Sound with the given sound name</summary>
        /// <param name="soundName">The name of the sound to be played</param>
        /// <returns></returns>
        public Sound Initialize(string soundName)
        {
            SoundName = soundName;

            return this;
        }

        /// <summary>Reset the component to its initial state, so that it may be reused without side effects.</summary>
        public override void Reset()
        {
            base.Reset();

            SoundName = null;
        }

        #endregion
    }
}