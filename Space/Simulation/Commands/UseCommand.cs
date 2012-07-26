﻿using Engine.Serialization;
using Engine.Simulation.Commands;

namespace Space.Simulation.Commands
{
    /// <summary>
    /// Makes a player use an item from his inventory.
    /// </summary>
    internal sealed class UseCommand : FrameCommand
    {
        #region Fields

        /// <summary>
        /// The index in the inventory of the item to be used.
        /// </summary>
        public int InventoryIndex;

        #endregion

        #region Constructor

        public UseCommand(int inventoryIndex)
            : base(SpaceCommandType.UseItem)
        {
            InventoryIndex = inventoryIndex;
        }

        /// <summary>
        /// For deserialization.
        /// </summary>
        public UseCommand()
            : this(-1)
        {
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>
        /// The packet after writing.
        /// </returns>
        public override Packet Packetize(Packet packet)
        {
            return base.Packetize(packet)
                .Write(InventoryIndex);
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            InventoryIndex = packet.ReadInt32();
        }

        #endregion
    }
}
