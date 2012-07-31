﻿using System.Collections.Generic;
using Engine.ComponentSystem.Common.Components;
using Engine.ComponentSystem.Common.Systems;
using Engine.ComponentSystem.Systems;
using Engine.Math;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;
using Space.ComponentSystem.Messages;

namespace Space.ComponentSystem.Systems
{
    /// <summary>
    /// Keeps track of a global grid of cells which can be either alive or
    /// dead, depending on whether a player avatar is inside the cell or one
    /// of its neighboring cells, or not.
    /// 
    /// <para>
    /// Also keeps a more fine grained index over transform components to allow
    /// relatively quick requests for nearby transform components (nearest
    /// neighbor search).
    /// </para>
    /// </summary>
    public sealed class CellSystem : AbstractSystem
    {
        #region Type ID

        /// <summary>
        /// The unique type ID for this system, by which it is referred to in the manager.
        /// </summary>
        public static readonly int TypeId = Engine.ComponentSystem.Manager.GetSystemTypeId(typeof(CellSystem));

        #endregion

        #region Constants

        /// <summary>
        /// Dictates the size of cells, where the actual cell size is 2 to the
        /// power of this value.
        /// </summary>
        public const int CellSizeShiftAmount = 17;

        /// <summary>
        /// The size of a single cell in world units (normally: pixels).
        /// </summary>
        public const int CellSize = 1 << CellSizeShiftAmount;

        /// <summary>
        /// Index used to track entities that should automatically be removed
        /// when a cell dies, and they are in that cell.
        /// </summary>
        public static readonly ulong CellDeathAutoRemoveIndexGroupMask = 1ul << IndexSystem.GetGroup();

        /// <summary>
        /// The time to wait before actually killing of a cell after it has
        /// gotten out of reach. This is to avoid reallocating cells over
        /// and over again, if a player flies along a cell border or keeps
        /// flying back and forth over it. Unit is in game frames.
        /// </summary>
        private const int CellDeathDelay = 312; // ~5 seconds

        #endregion

        #region Properties

        /// <summary>
        /// A list of the IDs of all cells that are currently active.
        /// </summary>
        public IEnumerator<ulong> ActiveCells { get { return _livingCells.GetEnumerator(); } }

        #endregion

        #region Fields

        /// <summary>
        /// List of cells that are currently marked as alive.
        /// </summary>
        private HashSet<ulong> _livingCells = new HashSet<ulong>();

        /// <summary>
        /// Cells awaiting cleanup, with the time when they became invalid.
        /// </summary>
        private Dictionary<ulong, long> _pendingCells = new Dictionary<ulong, long>();

        #endregion

        #region Single-Allocation

        /// <summary>
        /// Reused each update, avoids memory re-allocation.
        /// </summary>
        private HashSet<ulong> _reusableNewCellIds = new HashSet<ulong>();

        /// <summary>
        /// Reused each update, avoids memory re-allocation.
        /// </summary>
        private HashSet<ulong> _reusableBornCellsIds = new HashSet<ulong>();

        /// <summary>
        /// Reused each update, avoids memory re-allocation.
        /// </summary>
        private HashSet<ulong> _reusableDeceasedCellsIds = new HashSet<ulong>();

        /// <summary>
        /// Reused each update, avoids memory re-allocation.
        /// </summary>
        private List<ulong> _reusablePendingList = new List<ulong>();

        /// <summary>
        /// Reused for querying entities contained in a dying cell.
        /// </summary>
        private HashSet<int> _reusableEntityList = new HashSet<int>();

        #endregion

        #region Accessors

        /// <summary>
        /// Tests if the specified cell is currently active.
        /// </summary>
        /// <param name="cellId">The id of the cell to check/</param>
        /// <returns>Whether the cell is active or not.</returns>
        public bool IsCellActive(ulong cellId)
        {
            return _livingCells.Contains(cellId);
        }

        /// <summary>
        /// Gets the cell id for a given coordinate pair.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <returns>The id of the cell containing that coordinate pair.</returns>
        public static ulong GetCellIdFromCoordinates(int x, int y)
        {
            return CoordinateIds.Combine(x >> CellSizeShiftAmount, y >> CellSizeShiftAmount);
        }

        /// <summary>
        /// Gets the cell id for a given position.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The id of the cell containing that position.</returns>
        public static ulong GetCellIdFromCoordinates(ref Vector2 position)
        {
            return GetCellIdFromCoordinates((int)position.X, (int)position.Y);
        }

        #endregion

        #region Logic

        /// <summary>
        /// Checks all players' positions to determine which cells are active
        /// and which are not. Sends messages if a cell's state changes.
        /// </summary>
        /// <param name="frame">The frame the update applies to.</param>
        public override void Update(long frame)
        {
            // Check the positions of all avatars to check which cells
            // should live, and which should die / stay dead.
            var avatarSystem = (AvatarSystem)Manager.GetSystem(AvatarSystem.TypeId);
            foreach (var avatar in avatarSystem.Avatars)
            {
                var transform = ((Transform)Manager.GetComponent(avatar, Transform.TypeId));
                var x = ((int)transform.Translation.X) >> CellSizeShiftAmount;
                var y = ((int)transform.Translation.Y) >> CellSizeShiftAmount;
                AddCellAndNeighbors(x, y, _reusableNewCellIds);
            }

            // Get the cells that became alive, notify systems and components.
            _reusableBornCellsIds.UnionWith(_reusableNewCellIds);
            _reusableBornCellsIds.ExceptWith(_livingCells);
            CellStateChanged changedMessage;
            foreach (var cellId in _reusableBornCellsIds)
            {
                // If its in there, remove it from the pending list.
                if (!_pendingCells.Remove(cellId))
                {
                    // Notify if cell wasn't alive already.
                    var xy = CoordinateIds.Split(cellId);
                    changedMessage.Id = cellId;
                    changedMessage.X = xy.Item1;
                    changedMessage.Y = xy.Item2;
                    changedMessage.State = true;
                    Manager.SendMessage(ref changedMessage);
                }
            }
            _reusableBornCellsIds.Clear();

            // Check pending list, kill off old cells, notify systems etc.
            _reusablePendingList.AddRange(_pendingCells.Keys);
            foreach (var cellId in _reusablePendingList)
            {
                // Are we still delaying?
                if (frame - _pendingCells[cellId] <= CellDeathDelay)
                {
                    continue;
                }

                // Timed out, kill it for good.
                _pendingCells.Remove(cellId);

                // Notify.
                var xy = CoordinateIds.Split(cellId);
                changedMessage.Id = cellId;
                changedMessage.X = xy.Item1;
                changedMessage.Y = xy.Item2;
                changedMessage.State = false;
                Manager.SendMessage(ref changedMessage);

                // Kill any remaining entities in the area covered by the
                // cell that just died.
                RectangleF cellBounds;
                cellBounds.X = xy.Item1 * CellSize;
                cellBounds.Y = xy.Item2 * CellSize;
                cellBounds.Width = CellSize;
                cellBounds.Height = CellSize;
                ICollection<int> neighbors = _reusableEntityList;
                ((IndexSystem)Manager.GetSystem(IndexSystem.TypeId)).Find(ref cellBounds, ref neighbors, CellDeathAutoRemoveIndexGroupMask);
                foreach (var neighbor in neighbors)
                {
                    Manager.RemoveEntity(neighbor);
                }
                _reusableEntityList.Clear();
            }
            _reusablePendingList.Clear();

            // Get the cells that died, put to pending list.
            _reusableDeceasedCellsIds.UnionWith(_livingCells);
            _reusableDeceasedCellsIds.ExceptWith(_reusableNewCellIds);
            foreach (var cellId in _reusableDeceasedCellsIds)
            {
                // Add it to the pending list.
                _pendingCells.Add(cellId, frame);
            }
            _reusableDeceasedCellsIds.Clear();

            _livingCells.Clear();
            _livingCells.UnionWith(_reusableNewCellIds);

            _reusableNewCellIds.Clear();
        }

        #endregion

        #region Utility methods

        /// <summary>
        /// Adds the combined coordinates for all neighboring cells and the
        /// specified cell itself to the specified set of cells.
        /// </summary>
        /// <param name="x">The x coordinate of the main cell.</param>
        /// <param name="y">The y coordinate of the main cell.</param>
        /// <param name="cells">The set of cells to add to.</param>
        private static void AddCellAndNeighbors(int x, int y, ISet<ulong> cells)
        {
            for (var ny = y - 1; ny <= y + 1; ny++)
            {
                for (var nx = x - 1; nx <= x + 1; nx++)
                {
                    cells.Add(CoordinateIds.Combine(nx, ny));
                }
            }
        }

        #endregion

        #region Serialization / Hashing

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>
        /// The packet after writing.
        /// </returns>
        public override Packet Packetize(Packet packet)
        {
            packet.Write(_livingCells.Count);
            foreach (var cellId in _livingCells)
            {
                packet.Write(cellId);
            }
            packet.Write(_pendingCells.Count);
            foreach (var pending in _pendingCells)
            {
                packet.Write(pending.Key);
                packet.Write(pending.Value);
            }

            return packet;
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            _livingCells.Clear();
            var numLiving = packet.ReadInt32();
            for (var i = 0; i < numLiving; i++)
            {
                _livingCells.Add(packet.ReadUInt64());
            }
            _pendingCells.Clear();
            var numPending = packet.ReadInt32();
            for (var i = 0; i < numPending; i++)
            {
                var cell = packet.ReadUInt64();
                var frame = packet.ReadInt64();
                _pendingCells.Add(cell, frame);
            }
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);

            foreach (var cellId in _livingCells)
            {
                hasher.Put(cellId);
            }
            foreach (var cellId in _pendingCells.Keys)
            {
                hasher.Put(cellId);
            }
        }

        #endregion

        #region Copying

        /// <summary>
        /// Servers as a copy constructor that returns a new instance of the same
        /// type that is freshly initialized.
        /// 
        /// <para>
        /// This takes care of duplicating reference types to a new copy of that
        /// type (e.g. collections).
        /// </para>
        /// </summary>
        /// <returns>A cleared copy of this system.</returns>
        public override AbstractSystem NewInstance()
        {
            var copy = (CellSystem)base.NewInstance();

            copy._livingCells = new HashSet<ulong>();
            copy._pendingCells = new Dictionary<ulong, long>();
            copy._reusableNewCellIds = new HashSet<ulong>();
            copy._reusableBornCellsIds = new HashSet<ulong>();
            copy._reusableDeceasedCellsIds = new HashSet<ulong>();
            copy._reusablePendingList = new List<ulong>();
            copy._reusableEntityList = new HashSet<int>();

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
        public override void CopyInto(AbstractSystem into)
        {
            base.CopyInto(into);

            var copy = (CellSystem)into;

            copy._livingCells.Clear();
            copy._livingCells.UnionWith(_livingCells);
            copy._pendingCells.Clear();
            foreach (var item in _pendingCells)
            {
                copy._pendingCells.Add(item.Key, item.Value);
            }
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
            return base.ToString() + ", LivingCells=[" + string.Join(", ", _livingCells) + "], PendingCells=[" + string.Join(", ", _pendingCells) + "]";
        }

        #endregion
    }
}
