﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Collections;
using Engine.ComponentSystem.Messages;
using Engine.ComponentSystem.Spatial.Components;
using Engine.ComponentSystem.Spatial.Messages;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using Engine.Util;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

#if FARMATH
using WorldPoint = Engine.FarMath.FarPosition;
using WorldBounds = Engine.FarMath.FarRectangle;
#else
using WorldPoint = Microsoft.Xna.Framework.Vector2;
using WorldBounds = Engine.Math.RectangleF;
#endif

namespace Engine.ComponentSystem.Spatial.Systems
{
    /// <summary>
    ///     This class represents a simple index structure for nearest neighbor queries.
    /// </summary>
    public sealed class IndexSystem : AbstractSystem
    {
        #region Type ID

        /// <summary>The unique type ID for this system, by which it is referred to in the manager.</summary>
        public static readonly int TypeId = CreateTypeId();

        #endregion

        #region Group number distribution

        /// <summary>Next group index dealt out.</summary>
        private static int _nextIndexId;

        /// <summary>Reserves an index ID for use.</summary>
        /// <returns>The reserved ID.</returns>
        public static int GetIndexId()
        {
            return _nextIndexId++;
        }

        #endregion

        #region Properties

        /// <summary>Total number of index structures currently in use.</summary>
        [PublicAPI]
        public int IndexCount
        {
            get { return _trees.Count(index => index != null); }
        }

        /// <summary>Total number of entries over all index structures.</summary>
        [PublicAPI]
        public int Count
        {
            get { return _trees.Where(index => index != null).Sum(index => index.Count); }
        }

        /// <summary>Gets the index of the specified ID.</summary>
        [PublicAPI]
        public IIndex<int, WorldBounds, WorldPoint> this[int index]
        {
            get
            {
                if (_trees[index] == null)
                {
#if FARMATH
                    _trees[index] = new FarCollections.SpatialHashedQuadTree<int>(_maxEntriesPerNode, _minNodeBounds, 0.1f, 2f, (p, v) => p.Write(v), p => p.ReadInt32());
#else
                    _trees[index] = new DynamicQuadTree<int>(_maxEntriesPerNode, _minNodeBounds, 0.1f, 2f, (p, v) => p.Write(v), p => p.ReadInt32());
#endif
                    _changed[index] = new HashSet<int>();
                }
                return _trees[index];
            }
        }

        #endregion

        #region Fields

        /// <summary>The number of items in a single cell allowed before we try splitting it.</summary>
        private readonly int _maxEntriesPerNode;

        /// <summary>The minimum bounds size of a node along an axis, used to stop splitting at a defined accuracy.</summary>
        private readonly float _minNodeBounds;

        /// <summary>The actual indexes we're using, mapping component positions to the components, allowing faster range queries.</summary>
        [CopyIgnore, PacketizeIgnore]
#if FARMATH
        private SparseArray<FarCollections.SpatialHashedQuadTree<int>> _trees = new SparseArray<FarCollections.SpatialHashedQuadTree<int>>();
#else
        private SparseArray<DynamicQuadTree<int>> _trees = new SparseArray<DynamicQuadTree<int>>();
#endif

        /// <summary>List of components for which the index entry changed in the last update.</summary>
        [CopyIgnore, PacketizeIgnore]
        private SparseArray<HashSet<int>> _changed = new SparseArray<HashSet<int>>();

        #endregion

        #region Constructor

        /// <summary>Creates a new index system using the specified constraints for indexes.</summary>
        /// <param name="maxEntriesPerNode">The maximum number of entries per node before the node will be split.</param>
        /// <param name="minNodeBounds">
        ///     The minimum bounds size of a node, i.e. nodes of this size or smaller won't be split
        ///     regardless of the number of entries in them.
        /// </param>
        public IndexSystem(int maxEntriesPerNode, float minNodeBounds)
        {
            _maxEntriesPerNode = maxEntriesPerNode;
            _minNodeBounds = minNodeBounds;
        }

        #endregion

        #region Logic

        /// <summary>Adds index components to all their indexes.</summary>
        /// <param name="message"></param>
        [MessageCallback]
        public void OnComponentAdded(ComponentAdded message)
        {
            var index = message.Component as IIndexable;
            if (index != null)
            {
                AddIndex(index, index.IndexId);
            }
        }

        /// <summary>Remove index components from all indexes.</summary>
        /// <param name="message"></param>
        [MessageCallback]
        public void OnComponentRemoved(ComponentRemoved message)
        {
            var index = message.Component as IIndexable;
            if (index != null)
            {
                // Remove from any indexes the component was part of.
                RemoveIndex(index, index.IndexId);
            }
        }

        /// <summary>Handle group changes (moving components from one index group to another).</summary>
        [MessageCallback]
        public void OnIndexGroupsChanged(IndexGroupsChanged message)
        {
            RemoveIndex(message.Component, message.OldIndexId);
            AddIndex(message.Component, message.NewIndexId);
        }

        /// <summary>Handle bound changes (size of actual bounds of simple index components).</summary>
        [MessageCallback]
        public void OnIndexBoundsChanged(IndexBoundsChanged message)
        {
            var bounds = message.Bounds;
            var delta = message.Delta;
            var component = message.Component;

            // Update all indexes the component is part of.
            if (component.IndexId >= 0 &&
                _trees[component.IndexId].Update(bounds, delta, component.Id))
            {
                // Mark as changed.
                _changed[component.IndexId].Add(component.Id);
            }
        }
        
        /// <summary>Prefetch for performance.</summary>
        private static readonly int IndexableTypeId = ComponentSystem.Manager.GetComponentTypeId<IIndexable>();

        /// <summary>Handle position changes (moving components around in the world).</summary>
        [MessageCallback]
        public void OnTranslationChanged(TranslationChanged message)
        {
            var component = message.Component;
            var velocity = (IVelocity) Manager.GetComponent(component.Entity, ComponentSystem.Manager.GetComponentTypeId<IVelocity>());
            var delta = velocity != null ? velocity.LinearVelocity : Vector2.Zero;
            foreach (IIndexable indexable in Manager.GetComponents(component.Entity, IndexableTypeId))
            {
                var bounds = indexable.ComputeWorldBounds();
                if(_trees[indexable.IndexId].Update(bounds, delta, indexable.Id))
                {
                    // Mark as changed.
                    _changed[indexable.IndexId].Add(component.Id);
                }
            }
        }

        #endregion

        #region Component lookup

        /// <summary>Gets the list of components for which the index entry changed.</summary>
        public IEnumerable<int> GetChanged(int index)
        {
            return _changed[index];
        }

        /// <summary>Marks the component as changed in the specified groups.</summary>
        /// <param name="component">The component.</param>
        /// <param name="index">The index.</param>
        public void Touch(int component, int index)
        {
            _changed[index].Add(component);
        }
        
        /// <summary>Marks the component as unchanged in the specified groups.</summary>
        /// <param name="component">The component.</param>
        /// <param name="index">The index.</param>
        public void Untouch(int component, int index)
        {
            _changed[index].Remove(component);
        }

        /// <summary>Clears the list of changed components for the specified groups.</summary>
        /// <param name="index">The index.</param>
        public void ClearTouched(int index)
        {
            _changed[index].Clear();
        }

        #endregion

        #region Utility methods

        /// <summary>Adds the specified component to the specified index.</summary>
        /// <param name="component">The component to add.</param>
        /// <param name="index">The index.</param>
        private void AddIndex(IIndexable component, int index)
        {
            // Ignore invalid ids, which can be used to disable components.
            if (index < 0)
            {
                return;
            }

            this[index].Add(component.ComputeWorldBounds(), component.Id);
            _changed[index].Add(component.Id);
        }

        /// <summary>Removes the specified component from the specified index.</summary>
        /// <param name="component">The component to remove.</param>
        /// <param name="index">The index to remove from.</param>
        private void RemoveIndex(IIndexable component, int index)
        {
            // Ignore invalid ids, which can be used to disable components.
            if (index < 0)
            {
                return;
            }

            this[index].Remove(component.Id);
            _changed[index].Remove(component.Id);
        }

        #endregion

        #region Serialization / Hashing

        [OnPacketize]
        public IWritablePacket Packetize(IWritablePacket packet)
        {
            packet.Write(IndexCount);
            for (var index = 0; index < _nextIndexId; ++index)
            {
                // Skip non-existing and empty trees.
                if (_trees[index] == null || _trees[index].Count == 0)
                {
                    System.Diagnostics.Debug.Assert(_changed[index] == null || _changed[index].Count == 0);
                    continue;
                }
                packet.Write(index);

                packet.Write(_trees[index]);

                packet.Write(_changed[index].Count);
                foreach (var component in _changed[index])
                {
                    packet.Write(component);
                }
            }

            return packet;
        }

        [OnPostDepacketize]
        public void Depacketize(IReadablePacket packet)
        {
            foreach (var tree in _trees.Where(tree => tree != null))
            {
                tree.Clear();
            }
            foreach (var changed in _changed.Where(changed => changed != null))
            {
                changed.Clear();
            }

            var indexCount = packet.ReadInt32();
            for (var i = 0; i < indexCount; ++i)
            {
                var index = packet.ReadInt32();

                if (_trees[index] == null)
                {
#if FARMATH
                    _trees[index] = new FarCollections.SpatialHashedQuadTree<int>(_maxEntriesPerNode, _minNodeBounds, 0.1f, 2f, (p, v) => p.Write(v), p => p.ReadInt32());
#else
                    _trees[index] = new DynamicQuadTree<int>(_maxEntriesPerNode, _minNodeBounds, 0.1f, 2f, (p, v) => p.Write(v), p => p.ReadInt32());
#endif
                }
                if (_changed[index] == null)
                {
                    _changed[index] = new HashSet<int>();
                }

                packet.ReadPacketizableInto(_trees[index]);

                var changedCount = packet.ReadInt32();
                for (var j = 0; j < changedCount; ++j)
                {
                    _changed[index].Add(packet.ReadInt32());
                }
            }
        }

        [OnStringify]
        public StreamWriter Dump(StreamWriter w, int indent)
        {
            w.AppendIndent(indent).Write("Trees = {");
            for (var i = 0; i < _nextIndexId; ++i)
            {
                var tree = _trees[i];
                if (tree == null)
                {
                    continue;
                }
                w.AppendIndent(indent + 1).Write(i);
                w.Write(" = ");
                w.Dump(tree, indent + 1);
            }
            w.AppendIndent(indent).Write("}");

            w.AppendIndent(indent).Write("Changed = {");
            var first = true;
            foreach (var component in _changed)
            {
                if (!first)
                {
                    w.Write(", ");
                }
                first = false;
                w.Write(component);
            }
            w.Write("}");

            return w;
        }

        #endregion

        #region Copying

        /// <summary>
        ///     Servers as a copy constructor that returns a new instance of the same type that is freshly initialized.
        ///     <para>This takes care of duplicating reference types to a new copy of that type (e.g. collections).</para>
        /// </summary>
        /// <returns>A cleared copy of this system.</returns>
        public override AbstractSystem NewInstance()
        {
            var copy = (IndexSystem) base.NewInstance();

#if FARMATH
            copy._trees = new SparseArray<FarCollections.SpatialHashedQuadTree<int>>();
#else
            copy._trees = new SparseArray<DynamicQuadTree<int>>();
#endif
            copy._changed = new SparseArray<HashSet<int>>();

            return copy;
        }

        /// <summary>
        ///     Creates a deep copy of the system. The passed system must be of the same type.
        ///     <para>
        ///         This clones any contained data types to return an instance that represents a complete copy of the one passed
        ///         in.
        ///     </para>
        /// </summary>
        /// <remarks>The manager for the system to copy into must be set to the manager into which the system is being copied.</remarks>
        /// <returns>A deep copy, with a fully cloned state of this one.</returns>
        public override void CopyInto(AbstractSystem into)
        {
            base.CopyInto(into);

            var copy = (IndexSystem) into;

            foreach (var tree in copy._trees.Where(tree => tree != null))
            {
                tree.Clear();
            }
            foreach (var changed in copy._changed.Where(changed => changed != null))
            {
                changed.Clear();
            }

            for (var index = 0; index < _nextIndexId; ++index)
            {
                if (_trees[index] == null)
                {
                    continue;
                }

                if (copy._trees[index] == null)
                {
                    copy._trees[index] = _trees[index].NewInstance();
                }
                if (copy._changed[index] == null)
                {
                    copy._changed[index] = new HashSet<int>();
                }

                _trees[index].CopyInto(copy._trees[index]);
                copy._changed[index].UnionWith(_changed[index]);
            }
        }

        #endregion
    }
}