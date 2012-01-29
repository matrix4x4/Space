﻿using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Systems;
using Engine.Session;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Space.ComponentSystem.Systems
{
    /// <summary>
    /// Defines a sound system which uses the local player's avatar to
    /// determine the listener position.
    /// </summary>
    public sealed class PlayerCenteredSoundSystem : SoundSystem
    {
        #region Fields

        /// <summary>
        /// The session this system belongs to, for fetching the local player.
        /// </summary>
        IClientSession _session;

        #endregion

        #region Constructor
        
        public PlayerCenteredSoundSystem(SoundBank soundbank, IClientSession session)
            : base(soundbank)
        {
            this._session = session;
        }

        #endregion

        #region Logic

        /// <summary>
        /// Returns the position of the local player's avatar.
        /// </summary>
        protected override Vector2 GetListenerPosition()
        {
            var avatar = Manager.GetSystem<AvatarSystem>().GetAvatar(_session.LocalPlayer.Number);
            if (avatar != null)
            {
                return avatar.GetComponent<Transform>().Translation;
            }
            return Vector2.Zero;
        }

        /// <summary>
        /// Returns the velocity of the local player's avatar.
        /// </summary>
        protected override Vector2 GetListenerVelocity()
        {
            var avatar = Manager.GetSystem<AvatarSystem>().GetAvatar(_session.LocalPlayer.Number);
            if (avatar != null)
            {
                return avatar.GetComponent<Velocity>().Value;
            }
            return Vector2.Zero;
        }

        #endregion
    }
}