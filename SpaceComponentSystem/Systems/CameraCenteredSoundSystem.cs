﻿using System;
using Engine.ComponentSystem.Common.Components;
using Engine.ComponentSystem.Common.Systems;
using Engine.ComponentSystem.Systems;
using Engine.FarMath;
using Engine.Session;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Space.ComponentSystem.Messages;

namespace Space.ComponentSystem.Systems
{
    /// <summary>
    /// Defines a sound system which uses the local player's avatar to
    /// determine the listener position.
    /// </summary>
    public sealed class CameraCenteredSoundSystem : SoundSystem, IMessagingSystem
    {
        #region Fields

        /// <summary>
        /// The session this system belongs to, for fetching the local player.
        /// </summary>
        private readonly IClientSession _session;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="CameraCenteredSoundSystem"/> class.
        /// </summary>
        /// <param name="soundbank">The soundbank.</param>
        /// <param name="session">The session.</param>
        public CameraCenteredSoundSystem(SoundBank soundbank, IClientSession session)
            : base(soundbank)
        {
            _session = session;
        }

        #endregion

        #region Logic

        /// <summary>
        /// Reacts to messages to fire sounds.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="message">The message.</param>
        public void Receive<T>(ref T message) where T : struct
        {
            if (message is WeaponFired)
            {
                var weaponMessage = (WeaponFired)(ValueType)message;
                Play(weaponMessage.Weapon.Sound, weaponMessage.ShipEntity);
            }
        }

        /// <summary>
        /// Returns the position of the local player's avatar.
        /// </summary>
        protected override FarPosition GetListenerPosition()
        {
            if (_session.ConnectionState == ClientState.Connected)
            {
                var avatar = ((AvatarSystem)Manager.GetSystem(AvatarSystem.TypeId)).GetAvatar(_session.LocalPlayer.Number);
                if (avatar.HasValue)
                {
                    return ((Transform)Manager.GetComponent(avatar.Value, Transform.TypeId)).Translation;
                }
            }
            return FarPosition.Zero;
        }

        /// <summary>
        /// Returns the velocity of the local player's avatar.
        /// </summary>
        protected override Vector2 GetListenerVelocity()
        {
            if (_session.ConnectionState == ClientState.Connected)
            {
                var avatar = ((AvatarSystem)Manager.GetSystem(AvatarSystem.TypeId)).GetAvatar(_session.LocalPlayer.Number);
                if (avatar.HasValue)
                {
                    return ((Velocity)Manager.GetComponent(avatar.Value, Velocity.TypeId)).Value;
                }
            }
            return Vector2.Zero;
        }

        #endregion
    }
}
