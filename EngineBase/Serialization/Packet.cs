﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;

namespace Engine.Serialization
{
    /// <summary>
    /// Serialization utility class, for packing basic types into a byte array
    /// and reading them back. Actual packet structure is implied by the
    /// program structure, i.e. the caller must know what the next thing to
    /// read should be.
    /// </summary>
    public sealed class Packet : IDisposable, IEquatable<Packet>
    {
        #region Properties

        /// <summary>
        /// The number of bytes available for reading.
        /// </summary>
        public int Available { get { return (int)(_stream.Length - _stream.Position); } }

        /// <summary>
        /// The number of used bytes in the buffer.
        /// </summary>
        public int Length { get { return (int)_stream.Length; } }

        #endregion

        #region Fields

        /// <summary>
        /// The underlying memory stream used for buffering.
        /// </summary>
        private MemoryStream _stream;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new, empty packet.
        /// </summary>
        public Packet()
        {
            _stream = new MemoryStream();
        }

        /// <summary>
        /// Create a new packet based on the given buffer, which will
        /// result in a read-only packet.
        /// </summary>
        /// <param name="data">The data to initialize the packet with.</param>
        public Packet(byte[] data)
        {
            _stream = new MemoryStream(data ?? new byte[0], false);
        }

        /// <summary>
        /// Disposes this packet, freeing any memory it occupies.
        /// </summary>
        public void Dispose()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            //GC.SuppressFinalize(this);
        }

        #endregion

        #region Buffer

        /// <summary>
        /// Writes the contents of this packet to an array and returns it.
        /// </summary>
        /// <returns>The raw contents of this packet as a <c>byte[]</c>.</returns>
        public byte[] GetBuffer()
        {
            return _stream.ToArray();
        }

        /// <summary>
        /// Reset set the read position, to read from the beginning once more.
        /// </summary>
        public void Reset()
        {
            _stream.Position = 0;
        }

        #endregion

        #region Writing

        /// <summary>
        /// Writes the specified boolean value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(bool data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified byte value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(byte data)
        {
            _stream.WriteByte(data);
            return this;
        }

        /// <summary>
        /// Writes the specified double value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(double data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified vector value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(Vector2 data)
        {
            return Write(data.X).Write(data.Y);
        }

        /// <summary>
        /// Writes the specified vector value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(Vector3 data)
        {
            return Write(data.X).Write(data.Y).Write(data.Z);
        }

        /// <summary>
        /// Writes the specified rectangle value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(Rectangle data)
        {
            return Write(data.X).Write(data.Y).Write(data.Width).Write(data.Height);
        }

        /// <summary>
        /// Writes the specified single value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(float data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }
        
        /// <summary>
        /// Writes the specified int32 value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(int data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified in64 value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(long data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified int16 value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(short data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified uint32 value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(uint data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified uint64 value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(ulong data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified uint16 value.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(ushort data)
        {
            var bytes = BitConverter.GetBytes(data);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Writes the specified byte array.
        /// 
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(byte[] data)
        {
            return data == null ? Write(-1) : Write(data, data.Length);
        }

        /// <summary>
        /// Writes the specified length from the specified byte array.
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <param name="length">The number of bytes to write.</param>
        /// <returns>
        /// This packet, for call chaining.
        /// </returns>
        public Packet Write(byte[] data, int length)
        {
            if (data == null)
            {
                return Write(-1);
            }
            else
            {
                Write(length);
                _stream.Write(data, 0, length);
                return this;
            }
        }

        /// <summary>
        /// Writes the specified string value using UTF8 encoding.
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(string data)
        {
            if (data == null)
            {
                return Write((byte[])null);
            }
            else
            {
                return Write(Encoding.UTF8.GetBytes(data));
            }
        }

        /// <summary>
        /// Writes the specified packet.
        /// 
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(Packet data)
        {
            return Write((byte[])data);
        }

        /// <summary>
        /// Writes the specified object.
        /// 
        /// <para>
        /// Must be read back using one of the <see cref="ReadPacketizable{T}"/>
        /// overloads.
        /// </para>
        /// 
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write(IPacketizable data)
        {
            if (data == null)
            {
                return Write((Packet)null);
            }
            else
            {
                using (var packet = new Packet())
                {
                    return Write(data.Packetize(packet));
                }
            }
        }

        /// <summary>
        /// Writes the specified object with its type info, meaning to
        /// know the actual underlying type is not necessary for reading.
        /// 
        /// <para>
        /// Must byte read back using <see cref="ReadPacketizableWithTypeInfo{T}"/>.
        /// </para>
        /// 
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet WriteWithTypeInfo(IPacketizable data)
        {
            return Write(data.GetType().AssemblyQualifiedName).Write(data);
        }

        /// <summary>
        /// Writes the specified collection of objects.
        /// 
        /// <para>
        /// Must byte read back using <see cref="ReadPacketizables{T}"/>.
        /// </para>
        /// 
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// </summary>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet Write<T>(ICollection<T> data)
            where T : IPacketizable
        {
            if (data == null)
            {
                return Write(-1);
            }
            else
            {
                Write(data.Count);
                foreach (var item in data)
                {
                    Write(item);
                }
                return this;
            }
        }

        /// <summary>
        /// Writes the specified collection of objects.
        /// </summary>
        /// 
        /// <para>
        /// Must byte read back using <see cref="ReadPacketizablesWithTypeInfo{T}"/>.
        /// </para>
        /// 
        /// <para>
        /// May be <c>null</c>.
        /// </para>
        /// <param name="data">The value to write.</param>
        /// <returns>This packet, for call chaining.</returns>
        public Packet WriteWithTypeInfo<T>(ICollection<T> data)
            where T : IPacketizable
        {
            if (data == null)
            {
                return Write(-1);
            }
            else
            {
                Write(data.Count);
                foreach (var item in data)
                {
                    WriteWithTypeInfo(item);
                }
                return this;
            }
        }

        #endregion

        #region Reading

        /// <summary>
        /// Reads a boolean value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public bool ReadBoolean()
        {
            if (!HasBoolean())
            {
                throw new PacketException("Cannot read boolean.");
            }
            var bytes = new byte[sizeof(bool)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToBoolean(bytes, 0);
        }

        /// <summary>
        /// Reads a byte value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public byte ReadByte()
        {
            if (!HasByte())
            {
                throw new PacketException("Cannot read byte.");
            }
            return (byte)_stream.ReadByte();
        }

        /// <summary>
        /// Reads a single value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public float ReadSingle()
        {
            if (!HasSingle())
            {
                throw new PacketException("Cannot read single.");
            }
            var bytes = new byte[sizeof(float)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Reads a double value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public double ReadDouble()
        {
            if (!HasDouble())
            {
                throw new PacketException("Cannot read double.");
            }
            var bytes = new byte[sizeof(double)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToDouble(bytes, 0);
        }

        /// <summary>
        /// Reads a vector value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public Vector2 ReadVector2()
        {
            Vector2 result;
            result.X = ReadSingle();
            result.Y = ReadSingle();
            return result;
        }

        /// <summary>
        /// Reads a vector value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public Vector3 ReadVector3()
        {
            Vector3 result;
            result.X = ReadSingle();
            result.Y = ReadSingle();
            result.Z = ReadSingle();
            return result;
        }

        /// <summary>
        /// Reads a rectangle value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public Rectangle ReadRectangle()
        {
            Rectangle result;
            result.X = ReadInt32();
            result.Y = ReadInt32();
            result.Width = ReadInt32();
            result.Height = ReadInt32();
            return result;
        }

        /// <summary>
        /// Reads an int16 value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public short ReadInt16()
        {
            if (!HasInt16())
            {
                throw new PacketException("Cannot read int16.");
            }
            var bytes = new byte[sizeof(short)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>
        /// Reads an int32 value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public int ReadInt32()
        {
            if (!HasInt32())
            {
                throw new PacketException("Cannot read int32.");
            }
            var bytes = new byte[sizeof(int)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// Reads an int64 value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public long ReadInt64()
        {
            if (!HasInt64())
            {
                throw new PacketException("Cannot read int64.");
            }
            var bytes = new byte[sizeof(long)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToInt64(bytes, 0);
        }

        /// <summary>
        /// Reads a uint16 value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public ushort ReadUInt16()
        {
            if (!HasUInt16())
            {
                throw new PacketException("Cannot read uint16.");
            }
            var bytes = new byte[sizeof(ushort)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <summary>
        /// Reads a uint32 value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public uint ReadUInt32()
        {
            if (!HasUInt32())
            {
                throw new PacketException("Cannot read uint32.");
            }
            var bytes = new byte[sizeof(uint)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Reads a uint64 value.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public ulong ReadUInt64()
        {
            if (!HasUInt64())
            {
                throw new PacketException("Cannot read uint64.");
            }
            var bytes = new byte[sizeof(ulong)];
            _stream.Read(bytes, 0, bytes.Length);
            return BitConverter.ToUInt64(bytes, 0);
        }

        /// <summary>
        /// Reads a byte array.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public byte[] ReadByteArray()
        {
            if (!HasByteArray())
            {
                throw new PacketException("Cannot read byte[].");
            }
            var length = ReadInt32();
            if (length < 0)
            {
                return null;
            }
            else
            {
                var bytes = new byte[length];
                _stream.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        /// <summary>
        /// Reads a string value using UTF8 encoding.
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public string ReadString()
        {
            if (!HasString())
            {
                throw new PacketException("Cannot read string.");
            }
            var data = ReadByteArray();
            if (data == null)
            {
                return null;
            }
            else
            {
                return Encoding.UTF8.GetString(data);
            }
        }

        /// <summary>
        /// Reads a packet.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public Packet ReadPacket()
        {
            if (!HasPacket())
            {
                throw new PacketException("Cannot read packet.");
            }
            return (Packet)ReadByteArray();
        }

        /// <summary>
        /// Reads an object into the specified existing instance.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of object being read.</typeparam>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public T ReadPacketizableInto<T>(T existingInstance)
            where T : IPacketizable
        {
            using (var packet = ReadPacket())
            {
                if (packet == null)
                {
                    return default(T);
                }
                else
                {
                    existingInstance.Depacketize(packet);
                    return existingInstance;
                }
            }
        }

        /// <summary>
        /// Reads an object value of the given type.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of the object to read.</typeparam>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public T ReadPacketizable<T>()
            where T : IPacketizable, new()
        {
            return ReadPacketizableInto(new T());
        }

        /// <summary>
        /// Reads an object value of an arbitrary type, which should be a
        /// subtype of the specified type parameter.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Supertype of the type actually being read.</typeparam>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public T ReadPacketizableWithTypeInfo<T>()
            where T : IPacketizable
        {
            var typeName = ReadString();
            var type = Type.GetType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException("Trying to read an unknown type '" + typeName + "'.");
            }
            return ReadPacketizableInto((T)Activator.CreateInstance(type));
        }

        /// <summary>
        /// Reads an object collections.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The type of the objects to read.</typeparam>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public T[] ReadPacketizables<T>()
            where T : IPacketizable, new()
        {
            return ReadPacketizables(ReadPacketizable<T>);
        }

        /// <summary>
        /// Reads an object collections.
        /// 
        /// <para>
        /// May return <c>null</c>.
        /// </para>
        /// </summary>
        /// <typeparam name="T">Supertype of the type of the objects actually
        /// being read.</typeparam>
        /// <returns>The read value.</returns>
        /// <exception cref="PacketException">The packet has not enough
        /// available data for the read operation.</exception>
        public T[] ReadPacketizablesWithTypeInfo<T>()
            where T : IPacketizable
        {
            return ReadPacketizables(ReadPacketizableWithTypeInfo<T>);
        }

        /// <summary>
        /// Internal method used to read object collections, using the
        /// specified method to read single objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        private T[] ReadPacketizables<T>(Func<T> reader)
            where T : IPacketizable
        {
            var numPacketizables = ReadInt32();
            if (numPacketizables < 0)
            {
                return null;
            }
            else
            {
                var result = new T[numPacketizables];
                for (int i = 0; i < numPacketizables; i++)
                {
                    result[i] = reader();
                }
                return result;
            }
        }

        #endregion

        #region Peeking

        public bool PeekBoolean()
        {
            var position = _stream.Position;
            var result = ReadBoolean();
            _stream.Position = position;
            return result;
        }

        public byte PeekByte()
        {
            var position = _stream.Position;
            var result = ReadByte();
            _stream.Position = position;
            return result;
        }

        public float PeekSingle()
        {
            var position = _stream.Position;
            var result = ReadSingle();
            _stream.Position = position;
            return result;
        }

        public double PeekDouble()
        {
            var position = _stream.Position;
            var result = ReadDouble();
            _stream.Position = position;
            return result;
        }

        public short PeekInt16()
        {
            var position = _stream.Position;
            var result = ReadInt16();
            _stream.Position = position;
            return result;
        }

        public int PeekInt32()
        {
            var position = _stream.Position;
            var result = ReadInt32();
            _stream.Position = position;
            return result;
        }

        public long PeekInt64()
        {
            var position = _stream.Position;
            var result = ReadInt64();
            _stream.Position = position;
            return result;
        }

        public ushort PeekUInt16()
        {
            long position = _stream.Position;
            var result = ReadUInt16();
            _stream.Position = position;
            return result;
        }

        public uint PeekUInt32()
        {
            var position = _stream.Position;
            var result = ReadUInt32();
            _stream.Position = position;
            return result;
        }

        public ulong PeekUInt64()
        {
            var position = _stream.Position;
            var result = ReadUInt64();
            _stream.Position = position;
            return result;
        }

        public byte[] PeekByteArray()
        {
            var position = _stream.Position;
            var result = ReadByteArray();
            _stream.Position = position;
            return result;
        }

        public Packet PeekPacket()
        {
            var position = _stream.Position;
            var result = ReadPacket();
            _stream.Position = position;
            return result;
        }

        public string PeekString()
        {
            var position = _stream.Position;
            var result = ReadString();
            _stream.Position = position;
            return result;
        }

        #endregion

        #region Checking

        public bool HasBoolean()
        {
            return Available >= sizeof(bool);
        }

        public bool HasByte()
        {
            return Available >= sizeof(byte);
        }

        public bool HasSingle()
        {
            return Available >= sizeof(float);
        }

        public bool HasDouble()
        {
            return Available >= sizeof(double);
        }

        public bool HasFixed()
        {
            return Available >= sizeof(long);
        }

        public bool HasInt16()
        {
            return Available >= sizeof(short);
        }

        public bool HasInt32()
        {
            return Available >= sizeof(int);
        }

        public bool HasInt64()
        {
            return Available >= sizeof(long);
        }

        public bool HasUInt16()
        {
            return Available >= sizeof(ushort);
        }

        public bool HasUInt32()
        {
            return Available >= sizeof(uint);
        }

        public bool HasUInt64()
        {
            return Available >= sizeof(ulong);
        }

        public bool HasByteArray()
        {
            if (HasInt32())
            {
                return Available >= sizeof(int) + Math.Max(0, PeekInt32());
            }
            return false;
        }

        public bool HasPacket()
        {
            return HasByteArray();
        }

        public bool HasString()
        {
            return HasByteArray();
        }

        #endregion

        #region Casting

        /// <summary>
        /// Casts a packet to a byte array.
        /// </summary>
        /// <param name="value">The packet to cast.</param>
        /// <returns>A byte array representing the packet's internal buffer,
        /// or <c>null</c> if the packet itself was <c>null</c>.</returns>
        public static explicit operator byte[](Packet value)
        {
            if (value == null)
            {
                return null;
            }
            else
            {
                return value.GetBuffer();
            }
        }

        /// <summary>
        /// Casts a byte array to a packet.
        /// 
        /// <para>
        /// The resulting packet will be read-only.
        /// </para>
        /// </summary>
        /// <param name="value">The byte array to cast.</param>
        /// <returns>A packt using the byte array as its internal buffer,
        /// or <c>null</c> if the packet itself was <c>null</c>.</returns>
        public static explicit operator Packet(byte[] value)
        {
            return value == null ? null : new Packet(value);
        }

        #endregion

        #region Equality

        /// <summary>
        /// Tests for equality with the specified object.
        /// </summary>
        /// <param name="other">The object to test for equality with.</param>
        /// <returns>Whether this and the specified object are equal.</returns>
        public bool Equals(Packet other)
        {
            return other != null &&
                other._stream.Length == _stream.Length &&
                SafeNativeMethods.memcmp(other._stream.GetBuffer(), _stream.GetBuffer(), _stream.Length) == 0;
        }

        internal class SafeNativeMethods
        {
            /// <summary>
            /// Compares two byte arrays.
            /// </summary>
            /// <param name="b1">The first array.</param>
            /// <param name="b2">The second array.</param>
            /// <param name="count">The number of bytes to check.</param>
            /// <returns>Zero if the two are equal.</returns>
            [DllImport("msvcrt.dll")]
            internal static extern int memcmp(byte[] b1, byte[] b2, long count);

            private SafeNativeMethods()
            {
            }
        }

        #endregion
    }
}
