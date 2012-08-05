﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using Engine.Diagnostics;
using Engine.Serialization;
using Engine.Util;

namespace Engine.ComponentSystem.Systems
{
    /// <summary>
    /// Base class for systems, implementing default basic functionality.
    /// </summary>
    [DebuggerTypeProxy(typeof(FlattenHierarchyProxy))]
    public abstract class AbstractSystem : ICopyable<AbstractSystem>, IPacketizable, IHashable
    {
        #region Type ID

        /// <summary>
        /// Gets the component type id for the calling currently-being-initialized
        /// component type class. This will create a new ID if necessary.
        /// </summary>
        /// <returns>The type id for that component.</returns>
        /// <remarks>
        /// Utility method for subclasses, this just redirects to the same method in
        /// the component system manager. Uses execution stack to determine calling
        /// type.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static int CreateTypeId()
        {
            return ComponentSystem.Manager.GetSystemTypeId(new StackFrame(1, false).GetMethod().DeclaringType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// The component system manager this system is part of.
        /// </summary>
        public IManager Manager { get; set; }

        #endregion

        #region Logic

        /// <summary>
        /// Default implementation does nothing.
        /// </summary>
        /// <param name="frame">The frame in which the update is applied.</param>
        public virtual void Update(long frame)
        {
        }

        /// <summary>
        /// Default implementation does nothing.
        /// </summary>
        /// <param name="frame">The frame that should be rendered.</param>
        public virtual void Draw(long frame)
        {
        }

        #endregion

        #region Messaging

        /// <summary>
        /// Handle a message of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="message">The message.</param>
        public virtual void Receive<T>(ref T message) where T : struct
        {
        }

        #endregion

        #region Serialization / Hashing

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <remarks>
        /// Must be overridden in subclasses setting <c>ShouldSynchronize</c>
        /// to true.
        /// </remarks>
        /// <returns>
        /// The packet after writing.
        /// </returns>
        public virtual Packet Packetize(Packet packet)
        {
            return packet;
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <remarks>
        /// Must be overridden in subclasses setting <c>ShouldSynchronize</c>
        /// to true.
        /// </remarks>
        /// <param name="packet">The packet to read from.</param>
        public virtual void Depacketize(Packet packet)
        {
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public virtual void Hash(Hasher hasher)
        {
        }

        #endregion

        #region Copying

        /// <summary>
        /// Creates a new copy of the object, that shares no mutable
        /// references with this instance.
        /// </summary>
        /// <returns>The copy.</returns>
        public virtual AbstractSystem NewInstance()
        {
            var copy = (AbstractSystem)MemberwiseClone();

            copy.Manager = null;

            return copy;
        }

        /// <summary>
        /// Creates a deep copy of the system. The passed system must be of the
        /// same type.
        /// 
        /// <para>
        /// This clones any contained data types to return an instance that
        /// represents a complete copy of the one passed in.
        /// </para>
        /// </summary>
        /// <remarks>The manager for the system to copy into must be set to the
        /// manager into which the system is being copied.</remarks>
        /// <returns>A deep copy, with a fully cloned state of this one.</returns>
        public virtual void CopyInto(AbstractSystem into)
        {
            Debug.Assert(into.GetType().TypeHandle.Equals(GetType().TypeHandle));
            Debug.Assert(into != this);

            // Manager must be re-set to new owner before copying.
            Debug.Assert(into.Manager != null);
            Debug.Assert(into.Manager != Manager);
        }

        #endregion

        #region ToString

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var hasher = new Hasher();
            Hash(hasher);
            return GetType().Name + ": Hash=" + hasher.Value;
        }

        #endregion
    }
}
