﻿using System;
using Engine.ComponentSystem.Entities;
using Engine.Serialization;
using Engine.Util;

namespace Engine.ComponentSystem.Components
{
    /// <summary>
    /// Utility base class for components adding default behavior.
    /// 
    /// <para>
    /// Implementing classes must take note while cloning: they must not
    /// hold references to other components, or if they do (caching) they
    /// must invalidate these references when cloning.
    /// </para>
    /// </summary>
    public abstract class AbstractComponent : ICopyable<AbstractComponent>, IPacketizable, IHashable
    {
        #region Fields

        /// <summary>
        /// Unique ID in the context of its entity. This means there can be
        /// multiple components with the same id, but no two components with
        /// the same id attached to the same entity.
        /// </summary>
        public int UID = -1;

        /// <summary>
        /// This determines in which order this component will be rendered.
        /// Components with higher values will be drawn later.
        /// </summary>
        public int UpdateOrder;

        /// <summary>
        /// This determines in which order this component will be rendered.
        /// Components with higher values will be drawn later.
        /// </summary>
        public int DrawOrder;

        /// <summary>
        /// Gets the entity this component belongs to.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Whether the component is enabled or not. Disabled components will
        /// not have their <c>Update()</c> method called.
        /// </summary>
        public bool Enabled = true;

        #endregion

        #region Logic

        /// <summary>
        /// Does nothing on update. In debug mode, checks if the parameterization is valid.
        /// </summary>
        /// <param name="parameterization">The parameterization to use for this update.</param>
        public virtual void Update(object parameterization)
        {
        }

        /// <summary>
        /// Does nothing on draw. In debug mode, checks if the parameterization is valid.
        /// </summary>
        /// <param name="parameterization">The parameterization to use for this update.</param>
        public virtual void Draw(object parameterization)
        {
        }

        /// <summary>
        /// Does not support any parameterization per default.
        /// </summary>
        /// <param name="parameterizationType">The type of parameterization to check.</param>
        /// <returns>Whether the parameterization is supported.</returns>
        public virtual bool SupportsUpdateParameterization(Type parameterizationType)
        {
            return false;
        }

        /// <summary>
        /// Does not support any parameterization per default.
        /// </summary>
        /// <param name="parameterizationType">The type of parameterization to check.</param>
        /// <returns>Whether the parameterization is supported.</returns>
        public virtual bool SupportsDrawParameterization(Type parameterizationType)
        {
            return false;
        }

        /// <summary>
        /// Inform a component of a message that was sent by a component of
        /// the entity the component belongs to.
        /// 
        /// <para>
        /// Note that components will also receive the messages they send themselves.
        /// </para>
        /// </summary>
        /// <param name="message">The sent message.</param>
        public virtual void HandleMessage<T>(ref T message) where T : struct
        {
        }

        #endregion

        #region Serialization / Hashing

        /// <summary>
        /// To be implemented by subclasses.
        /// </summary>
        public virtual Packet Packetize(Packet packet)
        {
            return packet.Write(UID)
                .Write(UpdateOrder)
                .Write(DrawOrder)
                .Write(Enabled);
        }

        /// <summary>
        /// To be implemented by subclasses.
        /// </summary>
        public virtual void Depacketize(Packet packet)
        {
            UID = packet.ReadInt32();
            UpdateOrder = packet.ReadInt32();
            DrawOrder = packet.ReadInt32();
            Enabled = packet.ReadBoolean();
        }

        /// <summary>
        /// To be implemented by subclasses.
        /// </summary>
        public virtual void Hash(Hasher hasher)
        {
            hasher.Put(BitConverter.GetBytes(UID));
            hasher.Put(BitConverter.GetBytes(UpdateOrder));
            hasher.Put(BitConverter.GetBytes(Enabled));
        }

        #endregion

        #region Copying

        /// <summary>
        /// Creates a member-wise clone of this instance. Subclasses may
        /// override this method to perform further adjustments to the
        /// cloned instance, such as overwriting reference values.
        /// </summary>
        /// <returns>An independent (deep) clone of this instance.</returns>
        public AbstractComponent DeepCopy()
        {
            return DeepCopy(null);
        }

        /// <summary>
        /// Creates a member-wise clone of this instance. Subclasses may
        /// override this method to perform further adjustments to the
        /// cloned instance, such as overwriting reference values.
        /// </summary>
        /// <returns>An independent (deep) clone of this instance.</returns>
        public AbstractComponent DeepCopy(AbstractComponent into)
        {
            var copy = ValidateType(into) ? into : (AbstractComponent)MemberwiseClone();
            CopyFields(copy, copy != into);
            return copy;
        }

        /// <summary>
        /// Used for <c>DeepCopy(AbstractComponent)</c> and must be implemented
        /// in subclasses like this:
        /// <code>
        /// return instance is MyClass;
        /// </code>
        /// </summary>
        /// <param name="instance">The instance to check.</param>
        /// <returns>Whether it's the same type as the implementing type.</returns>
        protected abstract bool ValidateType(AbstractComponent instance);

        /// <summary>
        /// This is how subclasses should implement deep copying. Implement
        /// all copying of fields etc. in this method. This includes value
        /// fields!
        /// </summary>
        /// <param name="into">The instance to copy into.</param>
        /// <param name="isShallowCopy">Whether it is a shallow copy of this
        /// instance, meaning that reference fields have to be deep copied
        /// completely, or may be tried to be deep copied into.</param>
        protected virtual void CopyFields(AbstractComponent into, bool isShallowCopy)
        {
            into.Entity = null;
        }

        #endregion
    }
}
