﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Engine.Collections;
using Engine.ComponentSystem.Common.Components;
using Engine.ComponentSystem.Common.Messages;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Common.Systems
{
    /// <summary>
    /// This class represents a simple index structure for nearest neighbor
    /// queries. It uses a grid structure for indexing, and will return lists
    /// of entities in cells near a query point.
    /// </summary>
    public sealed class IndexSystem : AbstractComponentSystem<Index>
    {
        #region Type ID

        /// <summary>
        /// The unique type ID for this system, by which it is referred to in the manager.
        /// </summary>
        public static readonly int TypeId = ComponentSystem.Manager.GetSystemTypeId(typeof(IndexSystem));

        #endregion

        #region Group number distribution

        /// <summary>
        /// Next group index dealt out.
        /// </summary>
        private static byte _nextGroup = 1;

        /// <summary>
        /// Reserves a group number for use.
        /// </summary>
        /// <returns>The reserved group number.</returns>
        public static byte GetGroup()
        {
            return GetGroups(1);
        }

        /// <summary>
        /// Reserves multiple group numbers for use.
        /// </summary>
        /// <param name="range">The number of group numbers to reserve.</param>
        /// <returns>The start of the range of reserved group numbers.</returns>
        public static byte GetGroups(byte range)
        {
            if (range + _nextGroup > 0xFF)
            {
                throw new InvalidOperationException("No more index groups available.");
            }
            var result = _nextGroup;
            _nextGroup += range;
            return result;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Total number of index structures currently in use.
        /// </summary>
        public int NumIndexes
        {
            get
            {
                var count = 0;
                foreach (var index in _trees)
                {
                    if (index != null)
                    {
                        ++count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Total number of entries over all index structures.
        /// </summary>
        public int Count
        {
            get
            {
                var count = 0;
                foreach (var index in _trees)
                {
                    if (index != null)
                    {
                        count += index.Count;
                    }
                }
                return count;
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// The number of items in a single cell allowed before we try splitting it.
        /// </summary>
        private int _maxEntriesPerNode;

        /// <summary>
        /// The minimum bounds size of a node along an axis, used to stop splitting
        /// at a defined accuracy.
        /// </summary>
        private int _minNodeBounds;

        /// <summary>
        /// The actual indexes we're using, mapping entity positions to the
        /// entities, allowing faster range queries.
        /// </summary>
        private IIndex<int>[] _trees = new IIndex<int>[sizeof(ulong) * 8];

        #endregion

        #region Single-Allocation

        /// <summary>
        /// Reused for iteration.
        /// </summary>
        private List<IIndex<int>> _reusableTreeList = new List<IIndex<int>>();

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new index system using the specified constraints for indexes.
        /// </summary>
        /// <param name="maxEntriesPerNode">The maximum number of entries per
        /// node before the node will be split.</param>
        /// <param name="minNodeBounds">The minimum bounds size of a node, i.e.
        /// nodes of this size or smaller won't be split regardless of the
        /// number of entries in them.</param>
        public IndexSystem(int maxEntriesPerNode, int minNodeBounds)
        {
            _maxEntriesPerNode = maxEntriesPerNode;
            _minNodeBounds = minNodeBounds;
        }

        #endregion

        #region Logic

        public override void Update(long frame)
        {
            base.Update(frame);

            ResetQueryCount();
        }

        #endregion

        #region Entity lookup

        /// <summary>
        /// Get all entities in the specified range of the query point.
        /// </summary>
        /// <param name="query">The point to use as a query point.</param>
        /// <param name="range">The distance up to which to get neighbors.</param>
        /// <param name="list">The list to use for storing the results.</param>
        /// <param name="groups">The bitmask representing the groups to check in.</param>
        /// <returns>All entities in range.</returns>
        public void Find(Vector2 query, float range, ref ICollection<int> list, ulong groups)
        {
            foreach (var tree in TreesForGroups(groups))
            {
                IncrementQueryCount();
                tree.Find(query, range, ref list);
            }
        }

        /// <summary>
        /// Get all entities contained in the specified rectangle.
        /// </summary>
        /// <param name="query">The query rectangle.</param>
        /// <param name="list">The list to use for storing the results.</param>
        /// <param name="groups">The bitmask representing the groups to check in.</param>
        /// <returns>All entities in range.</returns>
        public void Find(ref Rectangle query, ref ICollection<int> list, ulong groups)
        {
            foreach (var tree in TreesForGroups(groups))
            {
                IncrementQueryCount();
                tree.Find(ref query, ref list);
            }
        }

        #endregion

        #region Utility methods

        /// <summary>
        /// Utility method used to create indexes flagged in the specified bit mask
        /// if they don't already exist.
        /// </summary>
        /// <param name="groups">The groups to create index structures for.</param>
        private void EnsureIndexesExist(ulong groups)
        {
            var index = 0;
            while (groups > 0)
            {
                if ((groups & 1) == 1 && _trees[index] == null)
                {
                    _trees[index] = new QuadTree<int>(_maxEntriesPerNode, _minNodeBounds);
                }
                groups = groups >> 1;
                ++index;
            }
        }

        /// <summary>
        /// Utility method that returns a list of all trees flagged in the
        /// specified bit mask. Calling this a second time invalidates the
        /// reference to a list returned by the previous call.
        /// </summary>
        /// <param name="groups">The groups to get the indexes for.</param>
        /// <returns>A list of the specified indexes.</returns>
        private IEnumerable<IIndex<int>> TreesForGroups(ulong groups)
        {
            _reusableTreeList.Clear();
            byte index = 0;
            while (groups > 0)
            {
                if ((groups & 1) == 1 && _trees[index] != null)
                {
                    _reusableTreeList.Add(_trees[index]);
                }
                groups = groups >> 1;
                ++index;
            }
            return _reusableTreeList;
        }

        /// <summary>
        /// Adds the specified entity to all indexes specified in groups.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <param name="groups">The indexes to add to.</param>
        private void AddEntity(int entity, ulong groups)
        {
            // Make sure the indexes exists.
            EnsureIndexesExist(groups);

            // Compute the bounds for the indexable as well as possible.
            var bounds = new Rectangle();
            var collidable = ((Collidable)Manager.GetComponent(entity, Collidable.TypeId));
            if (collidable != null)
            {
                bounds = collidable.ComputeBounds();
            }
            var transform = ((Transform)Manager.GetComponent(entity, Transform.TypeId));
            if (transform != null)
            {
                bounds.X = (int)transform.Translation.X - bounds.Width / 2;
                bounds.Y = (int)transform.Translation.Y - bounds.Height / 2;
            }

            // Add the entity to all its indexes.
            foreach (var tree in TreesForGroups(groups))
            {
                // Add to each group.
                tree.Add(ref bounds, entity);
            }
        }

        #endregion

        #region Component removal handling

        /// <summary>
        /// Adds entities that got an index component to all their indexes.
        /// </summary>
        /// <param name="component">The component that was added.</param>
        protected override void OnComponentAdded(Index component)
        {
            AddEntity(component.Entity, component.IndexGroupsMask);
        }

        /// <summary>
        /// Remove entities that had their index component removed from all
        /// indexes.
        /// </summary>
        /// <param name="component">The component.</param>
        protected override void OnComponentRemoved(Index component)
        {
            // Remove from any indexes the entity was part of.
            foreach (var tree in TreesForGroups(component.IndexGroupsMask))
            {
                tree.Remove(component.Entity);
            }
        }

        #endregion

        #region Messaging

        /// <summary>
        /// Handles position changes of indexed components.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">The message.</param>
        public override void Receive<T>(ref T message)
        {
            base.Receive(ref message);

            if (message is IndexGroupsChanged)
            {
                var changedMessage = (IndexGroupsChanged)(ValueType)message;

                // Do we have new groups?
                if (changedMessage.AddedIndexGroups != 0)
                {
                    AddEntity(changedMessage.Entity, changedMessage.AddedIndexGroups);
                }

                // Do we have deprecated groups?
                if (changedMessage.RemovedIndexGroups != 0)
                {
                    // Remove from each old group.
                    foreach (var tree in TreesForGroups(changedMessage.RemovedIndexGroups))
                    {
                        tree.Remove(changedMessage.Entity);
                    }
                }
            }
            else if (message is IndexBoundsChanged)
            {
                var changedMessage = (IndexBoundsChanged)(ValueType)message;
                
                // Check if the entity is indexable.
                var index = ((Index)Manager.GetComponent(changedMessage.Entity, Index.TypeId));
                if (index == null)
                {
                    return;
                }

                var bounds = changedMessage.Bounds;
                var transform = ((Transform)Manager.GetComponent(changedMessage.Entity, Transform.TypeId));
                if (transform != null)
                {
                    bounds.X = (int)transform.Translation.X - bounds.Width / 2;
                    bounds.Y = (int)transform.Translation.Y - bounds.Height / 2;
                }

                // Update all indexes the entity is part of.
                foreach (var tree in TreesForGroups(index.IndexGroupsMask))
                {
                    tree.Update(ref bounds, changedMessage.Entity);
                }
            }
            else if (message is TranslationChanged)
            {
                var changedMessage = (TranslationChanged)(ValueType)message;

                // Check if the entity is indexable.
                var index = ((Index)Manager.GetComponent(changedMessage.Entity, Index.TypeId));
                if (index == null)
                {
                    return;
                }

                // Update all indexes the component is part of.
                foreach (var tree in TreesForGroups(index.IndexGroupsMask))
                {
                    tree.Move(changedMessage.CurrentPosition, changedMessage.Entity);
                }
            }
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
        public override Packet Packetize(Packet packet)
        {
            base.Packetize(packet);

            for (var i = 0; i < _trees.Length; ++i)
            {
                var tree = _trees[i];
                if (tree == null)
                {
                    packet.Write(0);
                    continue;
                }

                packet.Write(tree.Count);
                foreach (var tuple in tree)
                {
                    packet.Write(tuple.Item1);
                    packet.Write(tuple.Item2);
                }
            }

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
        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            for (var i = 0; i < _trees.Length; ++i)
            {
                if (_trees[i] != null)
                {
                    _trees[i].Clear();
                }
                var count = packet.ReadInt32();
                if (count <= 0)
                {
                    continue;
                }
                if (_trees[i] == null)
                {
                    _trees[i] = new QuadTree<int>(_maxEntriesPerNode, _minNodeBounds);
                }
                for (var j = 0; j < count; ++j)
                {
                    var bounds = packet.ReadRectangle();
                    var entity = packet.ReadInt32();
                    _trees[i].Add(ref bounds, entity);
                }
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

            hasher.Put(_maxEntriesPerNode);
            hasher.Put(_minNodeBounds);

            foreach (var tree in _trees)
            {
                hasher.Put(tree == null ? 0 : tree.Count);
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
            var copy = (IndexSystem)base.NewInstance();

            copy._trees = new IIndex<int>[sizeof(ulong) * 8];
            copy._reusableTreeList = new List<IIndex<int>>();

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

            var copy = (IndexSystem)into;

            copy._maxEntriesPerNode = _maxEntriesPerNode;
            copy._minNodeBounds = _minNodeBounds;

            foreach (var tree in copy._trees)
            {
                if (tree != null)
                {
                    tree.Clear();
                }
            }

            for (var i = 0; i < _trees.Length; i++)
            {
                if (_trees[i] == null)
                {
                    continue;
                }
                if (copy._trees[i] == null)
                {
                    copy._trees[i] = new QuadTree<int>(copy._maxEntriesPerNode, copy._minNodeBounds);
                }
                foreach (var entry in _trees[i])
                {
                    var bounds = entry.Item1;
                    copy._trees[i].Add(ref bounds, entry.Item2);
                }
            }
        }

        #endregion

        #region Debug stuff

        /// <summary>
        /// Total number of queries over all index structures since the
        /// last update. This will always be zero when not running in
        /// debug mode.
        /// </summary>
        public int NumQueriesSinceLastUpdate { get; private set; }

        /// <summary>
        /// Renders all index structures matching the specified index group bit mask
        /// using the specified shape at the specified translation.
        /// </summary>
        /// <param name="groups">Bit mask determining which indexes to draw.</param>
        /// <param name="shape">Shape to use for drawing.</param>
        /// <param name="translation">Translation to apply when drawing.</param>
        [Conditional("DEBUG")]
        public void DrawIndex(ulong groups, Graphics.AbstractShape shape, Vector2 translation)
        {
            foreach (var tree in TreesForGroups(groups))
            {
                var quadTree = tree as QuadTree<int>;
                if (quadTree != null)
                {
                    quadTree.Draw(shape, translation);
                }
            }
        }

        /// <summary>
        /// Increments the number of queries performed.
        /// </summary>
        [Conditional("DEBUG")]
        private void IncrementQueryCount()
        {
            ++NumQueriesSinceLastUpdate;
        }

        /// <summary>
        /// Resets the number of queries performed.
        /// </summary>
        [Conditional("DEBUG")]
        private void ResetQueryCount()
        {
            NumQueriesSinceLastUpdate = 0;
        }

        #endregion
    }
}
