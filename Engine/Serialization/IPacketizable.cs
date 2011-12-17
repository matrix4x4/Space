﻿namespace Engine.Serialization
{
    /// <summary>
    /// Custom serialization using packets, with the intent of making
    /// it easier to send and receive data between network participants,
    /// or to store game data.
    /// </summary>
    public interface IPacketizable<TPlayerData>
        where TPlayerData : IPacketizable<TPlayerData>
    {
        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">the packet to write the data to.</param>
        void Packetize(Packet packet);

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">the packet to read from.</param>
        /// <param name="context">contextual information provided by the <c>Packetizer</c>.</param>
        void Depacketize(Packet packet, IPacketizerContext<TPlayerData> context);
    }
}
