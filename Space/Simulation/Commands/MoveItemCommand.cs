﻿using Engine.Simulation.Commands;

namespace Space.Simulation.Commands
{
    /// <summary>Command issued when a player wants to move an item in his inventory.</summary>
    internal sealed class MoveItemCommand : FrameCommand
    {
        #region Fields

        /// <summary>The first inventory slot involved.</summary>
        public readonly int FirstIndex;

        /// <summary>The second inventory slot involved.</summary>
        public readonly int SecondIndex;

        #endregion

        #region Constructor

        public MoveItemCommand(int firstIndex, int secondIndex)
            : base(SpaceCommandType.MoveItem)
        {
            FirstIndex = firstIndex;
            SecondIndex = secondIndex;
        }

        public MoveItemCommand()
            : this(-1, -1) {}

        #endregion
    }
}