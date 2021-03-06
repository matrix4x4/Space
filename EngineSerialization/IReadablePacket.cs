using System;
using JetBrains.Annotations;

namespace Engine.Serialization
{
    /// <summary>Interface for packets/streams that can be read from.</summary>
    [PublicAPI]
    public interface IReadablePacket : IDisposable
    {
        #region Properties

        /// <summary>The number of bytes available for reading.</summary>
        [PublicAPI]
        int Available { get; }

        /// <summary>The number of used bytes in the buffer.</summary>
        [PublicAPI]
        int Length { get; }

        #endregion

        #region Buffer

        /// <summary>Reset set the read position, to read from the beginning once more.</summary>
        [PublicAPI]
        void Reset();

        #endregion

        #region Reading

        /// <summary>Reads a boolean value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        bool ReadBoolean();

        /// <summary>Reads a byte value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        byte ReadByte();

        /// <summary>Reads a single value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        float ReadSingle();

        /// <summary>Reads a double value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        double ReadDouble();

        /// <summary>Reads an int16 value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        short ReadInt16();

        /// <summary>Reads an int32 value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        int ReadInt32();

        /// <summary>Reads an int64 value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        long ReadInt64();

        /// <summary>Reads a uint16 value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        ushort ReadUInt16();

        /// <summary>Reads a uint32 value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        uint ReadUInt32();

        /// <summary>Reads a uint64 value.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        ulong ReadUInt64();

        /// <summary>Reads a byte array.</summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="offset">The offset to start writing at.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The number of bytes read.</returns>
        [PublicAPI]
        int ReadByteArray(byte[] buffer, int offset, int count);

        /// <summary>
        ///     Reads a byte array.
        ///     <para>
        ///         May return <c>null</c>.
        ///     </para>
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        byte[] ReadByteArray();

        #endregion

        #region Peeking

        /// <summary>Reads a boolean value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        bool PeekBoolean();

        /// <summary>Reads a byte value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        byte PeekByte();

        /// <summary>Reads a single value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        float PeekSingle();

        /// <summary>Reads a double value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        double PeekDouble();

        /// <summary>Reads an int16 value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        short PeekInt16();

        /// <summary>Reads an int32 value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        int PeekInt32();

        /// <summary>Reads an int64 value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        long PeekInt64();

        /// <summary>Reads a uint16 value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        ushort PeekUInt16();

        /// <summary>Reads a uint32 value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        uint PeekUInt32();

        /// <summary>Reads a uint64 value without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        ulong PeekUInt64();

        /// <summary>
        ///     Reads a byte array without moving ahead the read pointer.
        ///     <para>
        ///         May return <c>null</c>.
        ///     </para>
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        byte[] PeekByteArray();

        /// <summary>Reads a string value using UTF8 encoding without moving ahead the read pointer.</summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough available data for the read operation.</exception>
        [PublicAPI]
        string PeekString();

        #endregion

        #region Checking

        /// <summary>Determines whether enough data is available to read a boolean value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasBoolean();

        /// <summary>Determines whether enough data is available to read a byte value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasByte();

        /// <summary>Determines whether enough data is available to read a single value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasSingle();

        /// <summary>Determines whether enough data is available to read a double value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasDouble();

        /// <summary>Determines whether enough data is available to read an int16 value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasInt16();

        /// <summary>Determines whether enough data is available to read an in32 value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasInt32();

        /// <summary>Determines whether enough data is available to read an int64 value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasInt64();

        /// <summary>Determines whether enough data is available to read a uint16 value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasUInt16();

        /// <summary>Determines whether enough data is available to read a uint32 value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasUInt32();

        /// <summary>Determines whether enough data is available to read a uint64 value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasUInt64();

        /// <summary>Determines whether enough data is available to read a byte array.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasByteArray();

        /// <summary>Determines whether enough data is available to read a string value.</summary>
        /// <returns>
        ///     <c>true</c> if there is enough data; otherwise, <c>false</c>.
        /// </returns>
        [PublicAPI]
        bool HasString();

        #endregion
    }
}