﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Engine.Serialization;
using Engine.Util;

// Adjust these as necessary, they just have to share a compatible
// interface with the XNA types.
#if FARMATH
using Engine.Collections;
using Engine.FarMath;
using TPoint = Engine.FarMath.FarPosition;
using TSingle = Engine.FarMath.FarValue;
using TRectangle = Engine.FarMath.FarRectangle;
#else
using Engine.Math;
using TPoint = Microsoft.Xna.Framework.Vector2;
using TRectangle = Engine.Math.RectangleF;
#endif

#if FARMATH
namespace Engine.FarCollections
#else
namespace Engine.Collections
#endif
{
    /// <summary>
    ///     A <see href="http://en.wikipedia.org/wiki/Quadtree">QuadTree</see> that can dynamically grow as needed.
    ///     <para/>
    ///     The tree bounds will always be a multiple of the minimal node bounds, as the bound size is doubled whenever
    ///     the tree has to grow to contain it's entries.
    ///     <para/>
    ///     All nodes can quickly iterate over all entries stored in all of their child nodes. The actual entries are
    ///     stored in a linked list, which is sorted in a way that allows unambiguous mapping of a section of that linked
    ///     list to a subtree.
    /// </summary>
    /// <typeparam name="T">The type of the values stored in the index.</typeparam>
    /// <remarks>
    ///     When querying the tree, a cache is generated per node, for the entries fetched from that node. The cache gets
    ///     invalidated when the subtree the node is root of changes. This allows for faster iteration when repeatedly querying
    ///     the same area of the tree (as it'll just be an iteration over an array, instead of a walk through the linked list,
    ///     dereferencing the pointer to the next entry for each entry).
    ///     <para/>
    ///     The minimum node size can be specified as an arbitrary value larger than zero.
    /// </remarks>
    [Packetizable, DebuggerDisplay("Count = {Count}")]
    public sealed class DynamicQuadTree<T> : IIndex<T, TRectangle, TPoint>, ICopyable<DynamicQuadTree<T>>
    {
        #region Properties

        /// <summary>The number of values stored in this tree.</summary>
        public int Count
        {
            get { return _values.Count; }
        }

        /// <summary>Returns the maximum depth of this tree.</summary>
        public int Depth
        {
            get { return NodeDepth(_root); }
        }

        /// <summary>Utility method for computing tree depth.</summary>
        private static int NodeDepth(Node node)
        {
            return node == null ? 0 : (1 + node.Children.Max((Func<Node, int>) NodeDepth));
        }

        #endregion

        #region Fields

        /// <summary>A callback that can be used to write an object stored in the tree to a packet for serialization.</summary>
        [PacketizeIgnore]
        private readonly Action<IWritablePacket, T> _packetizer;

        /// <summary>A callback that can be used to read an object stored in the tree from a packet for deserialization.</summary>
        [PacketizeIgnore]
        private readonly Func<IReadablePacket, T> _depacketizer;

        /// <summary>The number of items in a single cell allowed before we try splitting it.</summary>
        private readonly int _maxEntriesPerNode;

        /// <summary>The minimum bounds size of a node along an axis, used to stop splitting at a defined accuracy.</summary>
        private readonly float _minNodeBounds;

        /// <summary>
        ///     Amount by which to oversize entry bounds to allow for small movement the item without having to update the
        ///     tree. Idea taken from Box2D.
        /// </summary>
        private readonly float _boundExtension;

        /// <summary>
        ///     Amount by which to oversize entry bounds in the direction they moved during an update, to predict future
        ///     movement. Idea taken from Box2D.
        /// </summary>
        private readonly float _movingBoundMultiplier;

        /// <summary>
        ///     The current bounds of the tree. This is a dynamic value, adjusted based on elements added and removed to the
        ///     tree (it shrinks, too).
        /// </summary>
        private TRectangle _bounds;

        /// <summary>The root node of the tree.</summary>
        [CopyIgnore, PacketizeIgnore]
        private Node _root;

        /// <summary>Mapping back from value to entry, for faster value to entry lookup when removing or updating items.</summary>
        [CopyIgnore, PacketizeIgnore]
        private readonly Dictionary<T, Entry> _values = new Dictionary<T, Entry>();

        #endregion

        #region Constructor

        /// <summary>Creates a new, empty quad tree, with the specified split and stop criteria.</summary>
        /// <param name="maxEntriesPerNode">The maximum number of entries per node before the node will be split.</param>
        /// <param name="minNodeBounds">
        ///     The minimum bounds size of a node, i.e. nodes of this size or smaller won't be split
        ///     regardless of the number of entries in them. See class remarks.
        /// </param>
        /// <param name="boundExtension">The amount by which to inflate bounds.</param>
        /// <param name="movingBoundMultiplier">The multiplier for moving bound displacement used for predictive bound inflation.</param>
        /// <param name="packetizer">A function that can be used to packetize the type stored in the tree.</param>
        /// <param name="depacketizer">A function that can be used to depacketize the type stored in the tree.</param>
        /// <exception cref="T:System.ArgumentException">
        ///     One or both of the specified parameters are invalid (must be larger than
        ///     zero).
        /// </exception>
        public DynamicQuadTree(
            int maxEntriesPerNode,
            float minNodeBounds,
            float boundExtension = 0.1f,
            float movingBoundMultiplier = 2f,
            Action<IWritablePacket, T> packetizer = null,
            Func<IReadablePacket, T> depacketizer = null)
        {
            if (maxEntriesPerNode < 1)
            {
                throw new ArgumentException("Split count must be larger than zero.", "maxEntriesPerNode");
            }
            if (minNodeBounds <= 0f)
            {
                throw new ArgumentException("Bucket size must be larger than zero.", "minNodeBounds");
            }
            _maxEntriesPerNode = maxEntriesPerNode;
            _minNodeBounds = minNodeBounds;
            _boundExtension = boundExtension;
            _movingBoundMultiplier = movingBoundMultiplier;
            _packetizer = packetizer;
            _depacketizer = depacketizer;

            Clear();
        }

        #endregion

        #region Accessors

        /// <summary>Add a new item to the index, with the specified bounds.</summary>
        /// <param name="bounds">The bounds of the item.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="T:System.ArgumentException">The item is already stored in the index.</exception>
        public void Add(TRectangle bounds, T item)
        {
#if FARMATH
            Debug.Assert(!FarValue.IsNaN(bounds.X));
            Debug.Assert(!FarValue.IsNaN(bounds.Y));
            Debug.Assert(!FarValue.IsNaN(bounds.Width));
            Debug.Assert(!FarValue.IsNaN(bounds.Height));
#else
            Debug.Assert(!float.IsNaN(bounds.X));
            Debug.Assert(!float.IsNaN(bounds.Y));
            Debug.Assert(!float.IsNaN(bounds.Width));
            Debug.Assert(!float.IsNaN(bounds.Height));
#endif

            if (Contains(item))
            {
                throw new ArgumentException("Item is already in the index.", "item");
            }

            // Extend bounds.
            bounds.Inflate(_boundExtension, _boundExtension);

            // Create the entry to add.
            var entry = new Entry {Bounds = bounds, Value = item};

            // Handle dynamic growth.
            EnsureCapacity(ref bounds);

            // Get the node to insert in.
            var nodeBounds = _bounds;
            var node = FindNode(ref bounds, _root, ref nodeBounds);

            // Add the entry to that node.
            AddToNode(node, ref nodeBounds, entry);

            // Store the entry in the value lookup.
            _values.Add(entry.Value, entry);
        }

        /// <summary>
        ///     Update an entry by changing its bounds. If the item is not stored in the index, this will return <code>false</code>
        ///     .
        /// </summary>
        /// <param name="newBounds">The new bounds of the item.</param>
        /// <param name="delta">The amount by which the object moved.</param>
        /// <param name="item">The item for which to update the bounds.</param>
        /// <returns>
        ///     <c>true</c> if the update was successful; <c>false</c> otherwise.
        /// </returns>
        public bool Update(TRectangle newBounds, Microsoft.Xna.Framework.Vector2 delta, T item)
        {
#if FARMATH
            Debug.Assert(!FarValue.IsNaN(newBounds.X));
            Debug.Assert(!FarValue.IsNaN(newBounds.Y));
            Debug.Assert(!FarValue.IsNaN(newBounds.Width));
            Debug.Assert(!FarValue.IsNaN(newBounds.Height));
            
#else
            Debug.Assert(!float.IsNaN(newBounds.X));
            Debug.Assert(!float.IsNaN(newBounds.Y));
            Debug.Assert(!float.IsNaN(newBounds.Width));
            Debug.Assert(!float.IsNaN(newBounds.Height));
#endif
            Debug.Assert(!float.IsNaN(delta.X));
            Debug.Assert(!float.IsNaN(delta.Y));

            // Check if we have that item.
            if (!Contains(item))
            {
                // No, nothing to do, then.
                return false;
            }

            // Get the old bounds.
            var entry = _values[item];

            // Nothing to do if our approximation in the tree still contains the item.
            if (entry.Bounds.Contains(newBounds))
            {
                return false;
            }

            // Estimate movement by bounds delta to predict position and
            // extend the bounds accordingly, to avoid tree updates.
            delta.X *= _movingBoundMultiplier;
            delta.Y *= _movingBoundMultiplier;
            var absDeltaX = delta.X < 0 ? -delta.X : delta.X;
            var absDeltaY = delta.Y < 0 ? -delta.Y : delta.Y;
            newBounds.Width += (int) absDeltaX;
            if (delta.X < 0)
            {
                newBounds.X += (int) delta.X;
            }
            newBounds.Height += (int) absDeltaY;
            if (delta.Y < 0)
            {
                newBounds.Y += (int) delta.Y;
            }

            // Extend bounds.
            newBounds.Inflate(_boundExtension, _boundExtension);

            // Update tree.
            UpdateBounds(ref newBounds, entry);

            // We had the entry, so return true.
            return true;
        }

        /// <summary>
        ///     Remove the specified item from the index. If the item is not stored in the index, this will return
        ///     <code>false</code>.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>
        ///     <c>true</c> if the item was removed; <c>false</c> otherwise.
        /// </returns>
        public bool Remove(T item)
        {
            // See if we have that entry.
            if (!Contains(item))
            {
                // No, nothing to do, then.
                return false;
            }

            // Get the existing entry.
            var entry = _values[item];

            // Get the node the entry should be in (if it isn't something
            // went terribly, terribly wrong).
            var nodeBounds = _bounds;
            var node = FindNode(ref entry.Bounds, _root, ref nodeBounds);

            // Remove the entry from that node.
            RemoveFromNode(node, entry);

            // Remove the entry from the value lookup.
            _values.Remove(entry.Value);

            // If the tree is empty, restore the bounds to their defaults.
            if (Count == 0)
            {
                _bounds.X = _bounds.Y = -_minNodeBounds;
                _bounds.Width = _bounds.Height = _minNodeBounds + _minNodeBounds;
            }

            // We had the entry, so return true.
            return true;
        }

        /// <summary>Test whether this index contains the specified item.</summary>
        /// <param name="item">The item to check.</param>
        /// <returns>
        ///     <c>true</c> if the index contains the item; <c>false</c> otherwise.
        /// </returns>
        public bool Contains(T item)
        {
            // Use the reverse look up for faster checking.
            return _values.ContainsKey(item);
        }

        /// <summary>Removes all items from the index.</summary>
        public void Clear()
        {
            // Reset the root node.
            _root = new Node();

            // And the bounds.
            _bounds.X = _bounds.Y = -_minNodeBounds;
            _bounds.Width = _bounds.Height = _minNodeBounds + _minNodeBounds;

            // And clear the reverse look up.
            _values.Clear();
        }

        /// <summary>Get the bounds at which the specified item is currently stored.</summary>
        public TRectangle this[T item]
        {
            get { return _values[item].Bounds; }
        }

        /// <summary>
        ///     Perform a circular query on this index. This will return all entries in the index that are in the specified range
        ///     of the specified point, using the euclidean distance function (i.e. <c>sqrt(x*x+y*y)</c>).
        /// </summary>
        /// <param name="center">The query point near which to get entries.</param>
        /// <param name="radius">The maximum distance an entry may be away from the query point to be returned.</param>
        /// <param name="results"> </param>
        /// <remarks>
        ///     This checks for intersections of the query circle and the bounds of the entries in the index. Intersections
        ///     (i.e. bounds not fully contained in the circle) will be returned, too.
        /// </remarks>
        public void Find(TPoint center, float radius, ISet<T> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException("results");
            }

            Accumulate(
                _root,
                _bounds,
                IntersectionExtensions.BoundsFor(center, radius),
                center,
                radius,
                results);
        }

        /// <summary>
        ///     Perform a circular query on this index. This will return all entries in the index that are in the specified range
        ///     of the specified point, using the euclidean distance function (i.e. <c>sqrt(x*x+y*y)</c>).
        /// </summary>
        /// <param name="center">The query point near which to get entries.</param>
        /// <param name="radius">The maximum distance an entry may be away from the query point to be returned.</param>
        /// <param name="callback">The method to call for each found hit.</param>
        /// <returns></returns>
        /// <remarks>
        ///     This checks for intersections of the query circle and the bounds of the entries in the index. Intersections
        ///     (i.e. bounds not fully contained in the circle) will be returned, too.
        /// </remarks>
        public bool Find(TPoint center, float radius, SimpleQueryCallback<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            return Accumulate(
                _root,
                _bounds,
                IntersectionExtensions.BoundsFor(center, radius),
                center,
                radius,
                callback);
        }

        /// <summary>
        ///     Perform an area query on this index. This will return all entries in the tree that are contained in or
        ///     intersecting with the specified query rectangle.
        /// </summary>
        /// <param name="rectangle">The query rectangle.</param>
        /// <param name="results"> </param>
        public void Find(TRectangle rectangle, ISet<T> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException("results");
            }

            Accumulate(_root, _bounds, rectangle, results);
        }

        /// <summary>
        ///     Perform an area query on this index. This will return all entries in the tree that are contained in or
        ///     intersecting with the specified query rectangle.
        /// </summary>
        /// <param name="rectangle">The query rectangle.</param>
        /// <param name="callback">The method to call for each found hit.</param>
        /// <returns></returns>
        public bool Find(TRectangle rectangle, SimpleQueryCallback<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            return Accumulate(_root, _bounds, rectangle, callback);
        }

        /// <summary>
        ///     Perform a line query on this index. This will return all entries in the index that are intersecting with the
        ///     specified query line.
        /// </summary>
        /// <param name="start">The start point.</param>
        /// <param name="end">The end point.</param>
        /// <param name="t">The fraction along the line to consider.</param>
        /// <param name="results">The list to put the results into.</param>
        /// <returns></returns>
        public void Find(TPoint start, TPoint end, float t, ISet<T> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException("results");
            }

            Accumulate(
                _root,
                _bounds,
                IntersectionExtensions.BoundsFor(start, end, t),
                start,
                end,
                t,
                results);
        }

        /// <summary>
        ///     Perform a line query on this index. This will return all entries in the index that are intersecting with the
        ///     specified query line.
        ///     <para>
        ///         Note that the callback will be passed the fraction along the line that the hit occurred at, and may return
        ///         the new maximum fraction up to which the search will run. If the returned fraction is exactly zero the search
        ///         will be stopped. If the returned fraction is negative the hit will be ignored, that is the max fraction will
        ///         not change.
        ///     </para>
        /// </summary>
        /// <param name="start">The start of the line.</param>
        /// <param name="end">The end of the line.</param>
        /// <param name="t">The fraction along the line to consider.</param>
        /// <param name="callback">The method to call for each found hit.</param>
        /// <returns></returns>
        public bool Find(TPoint start, TPoint end, float t, LineQueryCallback<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }

            // Pass bounds and t as ref because they may change during the query.
            var queryBounds = IntersectionExtensions.BoundsFor(start, end, t);
            return Accumulate(_root, _bounds, ref queryBounds, start, end, ref t, callback);
        }

        #endregion

        #region Enumerable

        /// <summary>Get an enumerator over the items in this tree, together with the bounds they ares stored at.</summary>
        /// <returns>An enumerator of all items in this index, with their bounds.</returns>
        public IEnumerator<Tuple<TRectangle, T>> GetEnumerator()
        {
            foreach (var entry in _values)
            {
                yield return Tuple.Create(entry.Value.Bounds, entry.Key);
            }
        }

        /// <summary>Get a non-generic enumerator over the entries in this tree.</summary>
        /// <returns>An enumerator of all items in this index, with their bounds.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        ///     A utility enumerator allowing the iteration over all nodes in the tree. This yields the bounds for each node
        ///     and an enumerator over all entries in it.
        /// </summary>
        /// <returns>An enumerator over all nodes in the tree.</returns>
        /// <remarks>This is mainly intended for debugging purposes, to allow rendering the node bounds.</remarks>
        public IEnumerable<Tuple<TRectangle, IEnumerable<T>>> GetNodeEnumerable()
        {
            // Keep local stack of nodes so we don't create a load of enumerators.
            var nodes = new Stack<Tuple<Node, TRectangle>>(32);

            // Push root node, if it exists.
            if (_root != null)
            {
                nodes.Push(Tuple.Create(_root, _bounds));
            }

            // Keep going while there are nodes.
            while (nodes.Count > 0)
            {
                // Get node to process.
                var entry = nodes.Pop();
                var node = entry.Item1;
                var bounds = entry.Item2;

                // Push child nodes for next iteration.
                var childBounds = new TRectangle {Width = bounds.Width / 2, Height = bounds.Height / 2};
                if (node.Children[0] != null)
                {
                    childBounds.X = bounds.X;
                    childBounds.Y = bounds.Y;
                    nodes.Push(Tuple.Create(node.Children[0], childBounds));
                }
                if (node.Children[1] != null)
                {
                    childBounds.X = bounds.X + childBounds.Width;
                    childBounds.Y = bounds.Y;
                    nodes.Push(Tuple.Create(node.Children[1], childBounds));
                }
                if (node.Children[2] != null)
                {
                    childBounds.X = bounds.X;
                    childBounds.Y = bounds.Y + childBounds.Height;
                    nodes.Push(Tuple.Create(node.Children[2], childBounds));
                }
                if (node.Children[3] != null)
                {
                    childBounds.X = bounds.X + childBounds.Width;
                    childBounds.Y = bounds.Y + childBounds.Height;
                    nodes.Push(Tuple.Create(node.Children[3], childBounds));
                }

                // Return data for this node.
                yield return Tuple.Create(bounds, node.GetEntryEnumerable());
            }
        }

        #endregion

        #region Serialization

        /// <summary>Write the object's state to the given packet.</summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>The packet after writing.</returns>
        [OnPacketize]
        public IWritablePacket Packetize(IWritablePacket packet)
        {
            if (_packetizer == null)
            {
                throw new InvalidOperationException("No serializer specified.");
            }

            // Get all entries as a list. We will use the list index as a unique
            // id when storing references inside the tree.
            var entries = _values.Values.ToList();

            // Write all entries. Write the links in a second step for deserialization.
            packet.Write(entries.Count);
            foreach (var entry in entries)
            {
                _packetizer(packet, entry.Value);
                packet.Write(entry.Bounds);
            }
            foreach (var entry in entries)
            {
                packet.Write(entries.IndexOf(entry.Next));
                packet.Write(entries.IndexOf(entry.Previous));
            }

            // Write tree. The tree is very unlikely to ever get so deep that we
            // get stack issues here, so we can just do it recursively.
            PacketizeNode(packet, _root, entries);

            return packet;
        }

        /// <summary>
        ///     Bring the object to the state in the given packet. This is called after automatic depacketization has been
        ///     performed.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        [OnPostDepacketize]
        public void Depacketize(IReadablePacket packet)
        {
            if (_depacketizer == null)
            {
                throw new InvalidOperationException("No deserializer specified.");
            }

            // Read plain list of entries. Store them in a list to allow restoring
            // links between entries in a second pass.
            var entryCount = packet.ReadInt32();
            var entries = new List<Entry>(entryCount);
            _values.Clear();
            for (var i = 0; i < entryCount; ++i)
            {
                var entry = new Entry {Value = _depacketizer(packet)};
                packet.Read(out entry.Bounds);
                entries.Add(entry);
                _values[entry.Value] = entry;
            }
            for (var i = 0; i < entryCount; ++i)
            {
                var next = packet.ReadInt32();
                if (next >= 0)
                {
                    entries[i].Next = entries[next];
                }
                var previous = packet.ReadInt32();
                if (previous >= 0)
                {
                    entries[i].Previous = entries[previous];
                }
            }

            DepacketizeNode(packet, out _root, entries);
        }
        
        /// <summary>Utility method for writing data from a single node.</summary>
        private static void PacketizeNode(IWritablePacket packet, Node node, IList<Entry> entries)
        {
            packet.Write(node.EntryCount);

            packet.Write(entries.IndexOf(node.FirstChildEntry));
            packet.Write(entries.IndexOf(node.LastChildEntry));
            packet.Write(entries.IndexOf(node.FirstEntry));
            packet.Write(entries.IndexOf(node.LastEntry));

            for (var i = 0; i < 4; ++i)
            {
                if (node.Children[i] != null)
                {
                    packet.Write(true);
                    PacketizeNode(packet, node.Children[i], entries);
                }
                else
                {
                    packet.Write(false);
                }
            }
        }

        /// <summary>Utility method for parsing data from a single node.</summary>
        private static void DepacketizeNode(IReadablePacket packet, out Node node, IList<Entry> entries)
        {
            node = new Node {EntryCount = packet.ReadInt32()};

            var firstChildEntry = packet.ReadInt32();
            if (firstChildEntry >= 0)
            {
                node.FirstChildEntry = entries[firstChildEntry];
            }
            var lastChildEntry = packet.ReadInt32();
            if (lastChildEntry >= 0)
            {
                node.LastChildEntry = entries[lastChildEntry];
            }
            var firstEntry = packet.ReadInt32();
            if (firstEntry >= 0)
            {
                node.FirstEntry = entries[firstEntry];
            }
            var lastEntry = packet.ReadInt32();
            if (lastEntry >= 0)
            {
                node.LastEntry = entries[lastEntry];
            }

            for (var i = 0; i < 4; ++i)
            {
                if (packet.ReadBoolean())
                {
                    DepacketizeNode(packet, out node.Children[i], entries);
                    node.Children[i].Parent = node;
                }
            }
        }

        [OnStringify]
        public StreamWriter Dump(StreamWriter sb, int indent)
        {
            var entries = _values.Values.ToList();

            sb.AppendIndent(indent).Write("ValueCount = ");
            sb.Write(_values.Count);
            sb.AppendIndent(indent).Write("Values = {");
            for (var i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                sb.AppendIndent(indent + 1).Write("#");
                sb.Write(i);
                sb.Write(" = {");
                sb.AppendIndent(indent + 2).Write("Value = ");
                sb.Write(entry.Value);
                sb.AppendIndent(indent + 2).Write("Bounds = ");
                sb.Write(entry.Bounds);
                sb.AppendIndent(indent + 2).Write("Previous = ");
                {
                    var index = entries.IndexOf(entry.Previous);
                    if (index >= 0)
                    {
                        sb.Write("#");
                        sb.Write(index);
                    }
                    else
                    {
                        sb.Write("null");
                    }
                }
                sb.AppendIndent(indent + 2).Write("Next = ");
                {
                    var index = entries.IndexOf(entry.Next);
                    if (index >= 0)
                    {
                        sb.Write("#");
                        sb.Write(index);
                    }
                    else
                    {
                        sb.Write("null");
                    }
                }
                sb.AppendIndent(indent + 1).Write("}");
            }
            sb.AppendIndent(indent).Write("}");

            sb.AppendIndent(indent).Write("Nodes = {");
            DumpNode(sb, indent + 1, _root, entries);
            sb.AppendIndent(indent).Write("}");

            return sb;
        }

        private static void DumpNode(StreamWriter sb, int indent, Node node, IList<Entry> entries)
        {
            sb.AppendIndent(indent).Write("EntryCount = ");
            sb.Write(node.EntryCount);
            sb.AppendIndent(indent).Write("FirstChildEntry = ");
            {
                var index = entries.IndexOf(node.FirstChildEntry);
                if (index >= 0)
                {
                    sb.Write("#");
                    sb.Write(index);
                }
                else
                {
                    sb.Write("null");
                }
            }
            sb.AppendIndent(indent).Write("LastChildEntry = ");
            {
                var index = entries.IndexOf(node.LastChildEntry);
                if (index >= 0)
                {
                    sb.Write("#");
                    sb.Write(index);
                }
                else
                {
                    sb.Write("null");
                }
            }
            sb.AppendIndent(indent).Write("FirstEntry = ");
            {
                var index = entries.IndexOf(node.FirstEntry);
                if (index >= 0)
                {
                    sb.Write("#");
                    sb.Write(index);
                }
                else
                {
                    sb.Write("null");
                }
            }
            sb.AppendIndent(indent).Write("LastEntry = ");
            {
                var index = entries.IndexOf(node.LastEntry);
                if (index >= 0)
                {
                    sb.Write("#");
                    sb.Write(index);
                }
                else
                {
                    sb.Write("null");
                }
            }

            sb.AppendIndent(indent).Write("Children = {");
            for (var i = 0; i < 4; ++i)
            {
                sb.AppendIndent(indent + 1).Write(i);
                sb.Write(" = ");
                if (node.Children[i] != null)
                {
                    sb.Write("{");
                    DumpNode(sb, indent + 2, node.Children[i], entries);
                    sb.AppendIndent(indent + 1).Write("}");
                }
                else
                {
                    sb.Write("null");
                }
            }
            sb.AppendIndent(indent).Write("}");
        }

        #endregion

        #region Copying

        /// <summary>Creates a new copy of the object, that shares no mutable references with this instance.</summary>
        /// <returns>The copy.</returns>
        public DynamicQuadTree<T> NewInstance()
        {
            return new DynamicQuadTree<T>(
                _maxEntriesPerNode,
                _minNodeBounds,
                _boundExtension,
                _movingBoundMultiplier,
                _packetizer,
                _depacketizer);
        }

        /// <summary>Creates a deep copy of the object, reusing the given object.</summary>
        /// <param name="into">The object to copy into.</param>
        /// <returns>The copy.</returns>
        public void CopyInto(DynamicQuadTree<T> into)
        {
            Copyable.CopyInto(this, into);

            // Create a shallow copy in the first pass, link stuff in the second.
            into._values.Clear();
            foreach (var entry in _values)
            {
                into._values.Add(
                    entry.Key,
                    new Entry
                    {
                        Bounds = entry.Value.Bounds,
                        Value = entry.Value.Value
                    });
            }
            foreach (var entry in _values)
            {
                if (entry.Value.Next != null)
                {
                    into._values[entry.Key].Next = into._values[entry.Value.Next.Value];
                }
                if (entry.Value.Previous != null)
                {
                    into._values[entry.Key].Previous = into._values[entry.Value.Previous.Value];
                }
            }

            // Now copy the actual tree. We keep a stack of nodes we still have to copy,
            // together with the already created copy of the node in the new tree that
            // we need to initialize.
            var stack = new Stack<Tuple<Node, Node>>();
            into._root = new Node();
            stack.Push(Tuple.Create(_root, into._root));
            while (stack.Count > 0)
            {
                var pair = stack.Pop();
                var source = pair.Item1;
                var target = pair.Item2;

                target.EntryCount = source.EntryCount;
                if (source.FirstChildEntry != null)
                {
                    target.FirstChildEntry = into._values[source.FirstChildEntry.Value];
                }
                if (source.LastChildEntry != null)
                {
                    target.LastChildEntry = into._values[source.LastChildEntry.Value];
                }
                if (source.FirstEntry != null)
                {
                    target.FirstEntry = into._values[source.FirstEntry.Value];
                }
                if (source.LastEntry != null)
                {
                    target.LastEntry = into._values[source.LastEntry.Value];
                }

                for (var i = 0; i < 4; ++i)
                {
                    if (source.Children[i] != null)
                    {
                        target.Children[i] = new Node {Parent = target};
                        stack.Push(Tuple.Create(source.Children[i], target.Children[i]));
                    }
                }
            }
        }

        #endregion

        #region Restructuring

        /// <summary>Ensures the tree can contain the specified bounds.</summary>
        /// <param name="bounds">The bounds to ensure the tree size for.</param>
        private void EnsureCapacity(ref TRectangle bounds)
        {
            // Inserts a new level below the root node, making it the new children
            // of root the node (which in turn have the old root's children as
            // their own children).
            // This is repeated until the tree bounds completely contain the
            // specified entry bounds.
            while (_bounds.X >= bounds.X ||
                   _bounds.Y >= bounds.Y ||
                   _bounds.X + _bounds.Width <= bounds.X + bounds.Width ||
                   _bounds.Y + _bounds.Height <= bounds.Y + bounds.Height)
            {
                // Check sectors for relocations. For existing child nodes
                // of root (0, 1, 2, null)
                // +---+---+
                // | 0 | 1 |
                // +---+---+
                // | 2 |
                // +---+
                // this results in the wrappers (a, b, c, d)
                // +-------+-------+
                // |  a    |    b  |
                // |   +---+---+   |
                // |   | 0 | 1 |   |
                // +---+---+---+---+
                // |   | 2 |
                // |   +---+
                // |  c    |
                // +-------+
                // If there was no node before, there won't be one afterwards, either.
                // If the node was a leaf, it will stay as it was (but size will
                // implicitly larger).
                for (var childNumber = 0; childNumber < 4; childNumber++)
                {
                    // Get the old child node that will be attached to the new node.
                    var child = _root.Children[childNumber];

                    // Skip unset ones and leaf nodes (leaf nodes would just have to
                    // be collapsed into the new wrapper anyway, and because nodes
                    // are not explicitly aware of their size, just via their position
                    // in the tree, we can keep it).
                    if (child == null || child.IsLeaf)
                    {
                        continue;
                    }

                    // Allocate new node.
                    var wrapper = new Node
                    {
                        // Its parent is the root node.
                        Parent = _root,
                        // Its child entries are the child entries of the
                        // previous child at this position, including that
                        // child own entries.
                        FirstChildEntry = child.FirstChildEntry ?? child.FirstEntry,
                        LastChildEntry = child.LastEntry ?? child.LastChildEntry,
                        EntryCount = child.EntryCount
                    };

                    // Set opposing corner inside that node to old node in that corner.
                    // The (3 - x) will always yield the diagonally opposite cell to x.
                    wrapper.Children[3 - childNumber] = child;

                    // Replace old child in the root node, and set the old child node's
                    // parent to the inserted node.
                    _root.Children[childNumber] = wrapper;
                    child.Parent = wrapper;
                }

                // Adjust the overall tree bounds.
                _bounds.X += _bounds.X;
                _bounds.Y += _bounds.Y;
                _bounds.Width += _bounds.Width;
                _bounds.Height += _bounds.Height;
            }
        }

        /// <summary>Adds an entry to a node and handles overflow as necessary.</summary>
        /// <param name="node">The node to insert in.</param>
        /// <param name="nodeBounds">The bounds of the node.</param>
        /// <param name="entry">The entry to insert.</param>
        private void AddToNode(Node node, ref TRectangle nodeBounds, Entry entry)
        {
            // In the following we'll get the node in the linked list to insert after.
            Entry insertAfter;

            // Check what type of tree node we have.
            if (node.IsLeaf)
            {
                // Got a leaf, insert in it. We're guaranteed to already have
                // at least one entry in this leaf if we come here, because
                // the first entry comes directly with the creation of the node.
                // And if it were to become empty due to removal, it would be
                // trimmed from the tree. Meaning FirstEntry cannot be null.
                insertAfter = node.FirstEntry;
            }
            else
            {
                // Inner node, see if we can create a child node for that entry (there
                // is none yet, because otherwise we would have received that as the
                // parameter).
                var cell = ComputeCell(ref nodeBounds, ref entry.Bounds);
                if (cell < 0)
                {
                    // No, we must insert into this node. We're in an internal node,
                    // so we're guaranteed to have a list of child entries. So we add
                    // this entry either after the first entry of local nodes, if we
                    // have any, or after the last child entry. This guarantees us to
                    // get a non-null reference.
                    insertAfter = node.FirstEntry ?? node.LastChildEntry;
                }
                else
                {
                    // Yes, we can push it to a non-existent child node. Allocate it.
                    node.Children[cell] = new Node {Parent = node};

                    // Insert at the end of the child entry segment of the parent node,
                    // to begin a new segment for this node (as to not interfere with
                    // segments of other child nodes, if they exist). This must be not
                    // null unless the entire tree is empty, which will be handled
                    // below, exactly by checking whether insertAfter is null or not.
                    insertAfter = node.LastChildEntry;

                    // Mark the new node as the current one (the one we're adding to).
                    // This is necessary to allow proper propagation of possible
                    // linked list segment end changes up to the root node. This will
                    // make the following split do nothing, too, as we'll have too few
                    // entries to split (one). That is also why we don't have to update
                    // the node bounds -- they won't be used.
                    node = node.Children[cell];
                }
            }

            // Update the references in the node we inserted into.
            if (node.FirstEntry == node.LastEntry)
            {
                // The node either had no local entries yet (null == null, empty tree or
                // empty inner node), or it had only one. In the first case we can set
                // both to the same, in the second we only need to set the last entry.
                // We do this by just updating the first node if it's null. The last one
                // has to be updated either way.
                node.FirstEntry = node.FirstEntry ?? entry;
                node.LastEntry = entry;
            }

            // Add the entry to the existing list if possible. If the insertion point
            // is null it means the tree is yet empty, so it will simply be the the
            // complete linked list for now.
            if (insertAfter != null)
            {
                // Insert into the existing list.
                entry.InsertAfter(insertAfter);
            }

            // Remember we have one more entry in this node.
            ++node.EntryCount;

            // Invalidate cache.
            node.LocalCache = null;

            // Update all parent nodes. We might have changed the ends of some segments
            // so we need to adjust those in the parent nodes.
            var parent = node.Parent;
            while (parent != null)
            {
                if (parent.FirstChildEntry == parent.LastChildEntry)
                {
                    // Same logic as for the local segment, just for the reference
                    // to child nodes. Can be null if we created the first child
                    // node for that parent, and can be one for obvious reasons.
                    parent.FirstChildEntry = parent.FirstChildEntry ?? entry;
                    parent.LastChildEntry = entry;
                }
                else if (parent.LastChildEntry == insertAfter)
                {
                    // In case it already had other entries, and we have altered the
                    // last entry (to create a new segment), adjust that reference
                    // accordingly.
                    parent.LastChildEntry = entry;
                }

                // Remember we have one more entry in this subtree.
                ++parent.EntryCount;

                // Invalidate cache.
                parent.ChildCache = null;

                // Continue checking in our parent.
                parent = parent.Parent;
            }

            // Check whether we need to split the node, and do it if necessary.
            TrySplitNode(node, ref nodeBounds);
        }

        /// <summary>Check if a node needs to be split, and split it if allowed to.</summary>
        /// <param name="node">The actual node to split.</param>
        /// <param name="nodeBounds">The bounds of the node.</param>
        private void TrySplitNode(Node node, ref TRectangle nodeBounds)
        {
            // Should we split?
            if (!node.IsLeaf || // Already is split.
                node.EntryCount <= _maxEntriesPerNode || // No reason to.
                nodeBounds.Width <= _minNodeBounds) // We can't (too small already).
            {
                return;
            }

            // Remember the previous start and end of the interval in this node, so that
            // we may afterwards check if we need to update the references in parent nodes
            // due to reshuffling some of the entries.
            var oldFirstEntry = node.FirstEntry;
            var oldLastEntry = node.LastEntry;

            // Check each entry to which new cell it'll belong. While doing this, we also
            // separate the entries into two main segments, that of entries moved into the
            // child nodes, and that of entries that had to remain in this node. The
            // remaining entries will be in the back, because the other will have been
            // moved to the front (or rather: to behind another child node entry).

            // We must keep track of the next node manually because the position of the
            // current entry might change due to shuffling (moving child entries to the
            // segment they belong to). But we only move entries "to the left" (i.e.
            // before other entries), so as long as we remember the next entry that's
            // not a problem.
            for (Entry entry = node.FirstEntry, next = entry.Next, end = node.LastEntry.Next;
                 entry != end;
                 entry = next, next = (next != end ? next.Next : end)) // This is essentially a test for null.
            {
                // In which child node would we insert?
                var cell = ComputeCell(ref nodeBounds, ref entry.Bounds);
                if (cell < 0)
                {
                    // We must keep that entry in the current node. See if it's the
                    // first one, because then we use it as a reference as to where
                    // to put entries that can be pushed to children that follow.
                    if (node.FirstEntry == null)
                    {
                        // This is the first remaining entry we found, track it.
                        node.FirstEntry = entry;
                    }
                }
                else
                {
                    // If this was the first entry we null the first local entry, to set
                    // it to the first remaining node as soon as we find it.
                    if (entry == node.FirstEntry)
                    {
                        node.FirstEntry = null;
                    }

                    // Do we have to create that child? (It might already exist because
                    // we created it in a previous iteration for another entry)
                    if (node.Children[cell] == null)
                    {
                        // Yes. If we already have nodes we need to keep, move this in
                        // front of that segment. Otherwise this position is fine.
                        if (node.FirstEntry != null)
                        {
                            // If this was the last local node, update that reference.
                            if (entry == node.LastEntry)
                            {
                                // Just point to the one before us. This won't go out
                                // of bounds (our local segment) because we already
                                // have at least one remaining local node.
                                node.LastEntry = entry.Previous;
                            }

                            // Move the entry to before the remaining local segment.
                            entry.Remove();
                            entry.InsertBefore(node.FirstEntry);
                        }

                        // Create the node and set the entry as the first child, and
                        // mark it as the last as well.
                        node.Children[cell] = new Node {Parent = node, FirstEntry = entry, LastEntry = entry};

                        // If it's the first entry moved to a child node (first new segment),
                        // mark it as the first child node.
                        node.FirstChildEntry = node.FirstChildEntry ?? entry;

                        // Mark this as the last child entry either way.
                        node.LastChildEntry = entry;
                    }
                    else
                    {
                        // The node exists, check if the next linked node is this one,
                        // because if it is we don't need to shuffle.
                        if (entry != node.Children[cell].LastEntry.Next)
                        {
                            // This means we have to sort the sub-list by moving this
                            // entry to the correct position.

                            // In case this is the last node we must update the reference
                            // to the last local node.
                            if (entry == node.LastEntry)
                            {
                                // Just point to the one before us. This won't go out of
                                // bounds (our local segment) because we already have a
                                // child node, meaning we're not the first.
                                node.LastEntry = entry.Previous;
                            }

                            // Then move the entry to the end of the segment of the node
                            // it goes into.
                            entry.Remove();
                            entry.InsertAfter(node.Children[cell].LastEntry);
                        }

                        // If the last entry in the node we inserted into was the last one
                        // in the child entry segment of our parent, update that pointer.
                        // (This means the node we insert into has the last segment).
                        if (node.Children[cell].LastEntry == node.LastChildEntry)
                        {
                            node.LastChildEntry = entry;
                        }

                        // We replaced the last entry, so set that in the node.
                        node.Children[cell].LastEntry = entry;
                    }

                    // Either way, one more entry in the child node.
                    ++node.Children[cell].EntryCount;
                }
            }

            // If the node is a leaf, still, this means not a single entry could be
            // moved to a child node, which means nothing changed, so we can stop.
            if (node.IsLeaf)
            {
                return;
            }

            // Invalidate cache, but only if something changed.
            node.LocalCache = node.ChildCache = null;

            // At this point the entries in in the segment that is delimited by the
            // node's first and last references is sorted into child node entries and
            // local entries. This allows us to test if any local entries remained:
            // there are none if the last child entry equals the last local entry.
            if (node.LastChildEntry == node.LastEntry)
            {
                // No more local entries.
                node.FirstEntry = null;
                node.LastEntry = null;
            }

            // Adjust parent nodes if references to the ends of the segment for this
            // node changed.
            var parent = node.Parent;
            while (parent != null)
            {
                // See if we need to update a reference to one of the segment bounds.
                var changed = false;
                if (parent.FirstChildEntry == oldFirstEntry)
                {
                    // Head reference changed. We're guaranteed to have some child
                    // entries at this point (else we'd have returned earlier).
                    parent.FirstChildEntry = node.FirstChildEntry;
                    changed = true;
                }
                if (parent.LastChildEntry == oldLastEntry)
                {
                    // Tail reference changed.
                    parent.LastChildEntry = node.LastEntry ?? node.LastChildEntry;
                    changed = true;
                }

                if (changed)
                {
                    // Continue with the next parent node.
                    parent = parent.Parent;
                }
                else
                {
                    // Stop if there were no more updates (all inner segments).
                    break;
                }
            }

            // Do this recursively if the split resulted in another node that
            // has too many entries.
            var childBounds = new TRectangle {Width = nodeBounds.Width / 2, Height = nodeBounds.Height / 2};

            if (node.Children[0] != null)
            {
                childBounds.X = nodeBounds.X;
                childBounds.Y = nodeBounds.Y;
                TrySplitNode(node.Children[0], ref childBounds);
            }
            if (node.Children[1] != null)
            {
                childBounds.X = nodeBounds.X + childBounds.Width;
                childBounds.Y = nodeBounds.Y;
                TrySplitNode(node.Children[1], ref childBounds);
            }
            if (node.Children[2] != null)
            {
                childBounds.X = nodeBounds.X;
                childBounds.Y = nodeBounds.Y + childBounds.Height;
                TrySplitNode(node.Children[2], ref childBounds);
                childBounds.Y -= childBounds.Height;
            }
            if (node.Children[3] != null)
            {
                childBounds.X = nodeBounds.X + childBounds.Width;
                childBounds.Y = nodeBounds.Y + childBounds.Height;
                TrySplitNode(node.Children[3], ref childBounds);
            }
        }

        /// <summary>Removes an entry from a node.</summary>
        /// <param name="node">The node to remove from.</param>
        /// <param name="entry">The entry to remove.</param>
        private void RemoveFromNode(Node node, Entry entry)
        {
            // Adjust parent nodes if necessary. If we remove from somewhere in the
            // middle we don't really care, as the parents won't reference that entry,
            // but we update the entry counts in this run, and invalidate caches, too.
            var parent = node.Parent;
            while (parent != null)
            {
                // Adjust the node itself. Based on where we
                if (parent.FirstChildEntry == parent.LastChildEntry)
                {
                    // Only one entry in this node, clear it out.
                    parent.FirstChildEntry = null;
                    parent.LastChildEntry = null;
                }
                else if (parent.FirstChildEntry == entry)
                {
                    // It's the low node, and we have more than one entry
                    // (otherwise we would be in the first case), so adjust
                    // the head reference accordingly.
                    parent.FirstChildEntry = parent.FirstChildEntry.Next;
                }
                else if (parent.LastChildEntry == entry)
                {
                    // It's the high node, and we have more than one entry
                    // (otherwise we would be in the first case), so adjust
                    // the tail reference accordingly.
                    parent.LastChildEntry = parent.LastChildEntry.Previous;
                }

                // Adjust entry count.
                --parent.EntryCount;

                // Invalidate cache.
                parent.ChildCache = null;

                // Continue checking in our parent.
                parent = parent.Parent;
            }

            // Adjust the node itself.
            if (node.FirstEntry == node.LastEntry)
            {
                // Only one entry in this node, clear it out.
                node.FirstEntry = null;
                node.LastEntry = null;
            }
            else if (node.FirstEntry == entry)
            {
                // It's the low node, and we have more than one entry
                // (otherwise we would be in the first case), so adjust
                // the head reference accordingly.
                node.FirstEntry = node.FirstEntry.Next;
            }
            else if (node.LastEntry == entry)
            {
                // It's the high node, and we have more than one entry
                // (otherwise we would be in the first case), so adjust
                // the tail reference accordingly.
                node.LastEntry = node.LastEntry.Previous;
            }

            // Adjust entry count.
            --node.EntryCount;

            // Invalidate cache.
            node.LocalCache = null;

            // Remove the entry from the linked list of entries.
            entry.Remove();

            // See if we can collapse the branch.
            CollapseBranch(node);
        }

        /// <summary>
        ///     Try to collapse a branch starting with the specified child node. This walks the tree towards the root,
        ///     removing child nodes while possible.
        /// </summary>
        /// <param name="node">The node to start cleaning at.</param>
        private void CollapseBranch(Node node)
        {
            // Move up the tree while there are nodes.
            do
            {
                // Skip leaf nodes.
                if (node.IsLeaf)
                {
                    continue;
                }

                // Check if child nodes are unnecessary for this node. This is the
                // case if there is a smaller number than the split count, of course.
                if (node.EntryCount <= _maxEntriesPerNode)
                {
                    // If we're empty we could use the else branch (null children), but
                    // that's kind of superfluous, because we'll get nulled ourselves
                    // in our parent, in that case, so just skip that.
                    if (node.EntryCount > 0 || node == _root)
                    {
                        // We can prune the child nodes.

                        // Make the first child node our first local node, thus adding the
                        // segments of child nodes to our local nodes. If no high node was
                        // set this means we had no local entries, so we want to set that
                        // if it was null. In case the last entry from our only child node
                        // was removed, the child entry pointer may be null even though
                        // we have local nodes, so make sure to keep that if we have no
                        // child entry referenced.
                        node.FirstEntry = node.FirstChildEntry ?? node.FirstEntry;
                        node.LastEntry = node.LastEntry ?? node.LastChildEntry;

                        // Remove references to child nodes.
                        node.Children[0] = null;
                        node.Children[1] = null;
                        node.Children[2] = null;
                        node.Children[3] = null;
                        node.FirstChildEntry = null;
                        node.LastChildEntry = null;

                        // Invalidate caches.
                        node.LocalCache = node.ChildCache = null;
                    }
                }
                else
                {
                    // The node needs to stay split. Check if we have empty child nodes.
                    // If so, remove them.
                    if (node.Children[0] != null && node.Children[0].EntryCount == 0)
                    {
                        node.Children[0] = null;
                    }
                    if (node.Children[1] != null && node.Children[1].EntryCount == 0)
                    {
                        node.Children[1] = null;
                    }
                    if (node.Children[2] != null && node.Children[2].EntryCount == 0)
                    {
                        node.Children[2] = null;
                    }
                    if (node.Children[3] != null && node.Children[3].EntryCount == 0)
                    {
                        node.Children[3] = null;
                    }

                    // If we still have children at this point, we could not merge nor
                    // completely empty this node, meaning there's nothing left for us
                    // to do further up the tree.
                    if (!node.IsLeaf)
                    {
                        return;
                    }
                }

                // Check parent.
            } while ((node = node.Parent) != null);
        }

        /// <summary>Updates the bounds for the specified entry, moving it to another tree node if necessary.</summary>
        /// <param name="newBounds">The new bounds.</param>
        /// <param name="entry">The entry.</param>
        private void UpdateBounds(ref TRectangle newBounds, Entry entry)
        {
            // Node may have changed. Get the node the entry is currently stored in.
            var nodeBounds = _bounds;
            var node = FindNode(ref entry.Bounds, _root, ref nodeBounds);

            // Update bounds of entry.
            entry.Bounds = newBounds;

            // Check if the entry should go to a different node now.
            if (nodeBounds.X >= newBounds.X ||
                nodeBounds.Y >= newBounds.Y ||
                nodeBounds.X + nodeBounds.Width <= newBounds.X + newBounds.Width ||
                nodeBounds.Y + nodeBounds.Height <= newBounds.Y + newBounds.Height ||
                (node.EntryCount > _maxEntriesPerNode && ComputeCell(ref nodeBounds, ref newBounds) > -1))
            {
                // Did not fit in node anymore, or we can push the entry into
                // a child node, remove from that node.
                RemoveFromNode(node, entry);

                // Handle dynamic growth.
                EnsureCapacity(ref newBounds);

                // Get the node to re-insert in.
                nodeBounds = _bounds;
                node = FindNode(ref newBounds, _root, ref nodeBounds);

                // Add the entry to that node.
                AddToNode(node, ref nodeBounds, entry);
            }
        }

        #endregion

        #region Tree traversal

        /// <summary>
        ///     Computes the cell of a node with the specified position and child node size the specified bounds falls into.
        ///     If there is no clear result, this will return -1, which means the bounds must be stored in the specified node
        ///     itself (assuming the node can contain the bounds).
        /// </summary>
        /// <param name="nodeBounds">The node bounds to check for.</param>
        /// <param name="entryBounds">The entry bounds to check for.</param>
        /// <returns>The cell number the bounds fall into.</returns>
        private static int ComputeCell(ref TRectangle nodeBounds, ref TRectangle entryBounds)
        {
            var halfNodeSize = nodeBounds.Width / 2;

            // Check if the bounds are on the splits.
            var midX = nodeBounds.X + halfNodeSize;
            if (midX >= entryBounds.X && midX <= entryBounds.X + entryBounds.Width)
            {
                // Y split runs through the bounds.
                return -1;
            }
            var midY = nodeBounds.Y + halfNodeSize;
            if (midY >= entryBounds.Y && midY <= entryBounds.Y + entryBounds.Height)
            {
                // X split runs through the bounds.
                return -1;
            }

            // Otherwise check which child node the bounds fall into.
            var cell = 0;
            if (entryBounds.X > midX)
            {
                // Right half.
                cell |= 1;
            }
            if (entryBounds.Y > midY)
            {
                // Lower half.
                cell |= 2;
            }
            return cell;
        }

        /// <summary>
        ///     Find the minimal node that can contain the specified bounds. If possible, this will return a leaf node. If
        ///     there is no leaf node that can contain the rectangle, it will return the smallest inner node that can contain the
        ///     bounds.
        /// </summary>
        /// <param name="bounds">The bounds to get the node for.</param>
        /// <param name="node">The node to start searching in.</param>
        /// <param name="nodeBounds">The bounds of the node we start in. Will hold the bounds of the resulting node.</param>
        /// <returns>The node containing the specified bounds.</returns>
        private static Node FindNode(ref TRectangle bounds, Node node, ref TRectangle nodeBounds)
        {
            // We're definitely done when we hit a leaf.
            while (!node.IsLeaf)
            {
                // Get current child size.
                var childSize = nodeBounds.Width / 2;

                // Into which child node would we descend?
                var cell = ComputeCell(ref nodeBounds, ref bounds);

                // Can we descend and do we have to create that child?
                if (cell < 0 || node.Children[cell] == null)
                {
                    // No, return the current node.
                    return node;
                }

                // Yes, descend into that node.
                node = node.Children[cell];
                nodeBounds.X += (((cell & 1) == 0) ? 0 : childSize);
                nodeBounds.Y += (((cell & 2) == 0) ? 0 : childSize);
                nodeBounds.Width = childSize;
                nodeBounds.Height = childSize;
            }

            // Return the best match for the bounds.
            return node;
        }

        #region Queries

        // --------------------------------------------------------------------
        // IMPORTANT: the following contains a lot of "duplicate code", which
        // isn't very nice from a design perspective. But the performance boost
        // beats design to a pulp, so that's how it is.
        //
        // ALSO: all of the following queries are implemented recursively.
        // This is faster than using an iterative approach (customly stacking
        // child nodes and processing them one after the other).
        // --------------------------------------------------------------------

        private static void Accumulate(
            Node node, TRectangle nodeBounds, TRectangle queryBounds, TPoint center, float radius, ISet<T> results)
        {
            // Check how to proceed.
            switch (ComputeIntersection(queryBounds, nodeBounds))
            {
                case IntersectionType.Contains:
                {
                    // Node completely contained in query, return all entries in it that
                    // intersect with the query no need to recurse further.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        if (entry.Bounds.Intersects(center, radius))
                        {
                            results.Add(entry.Value);
                        }
                    }

                    // Rebuild entry cache if necessary.
                    if (node.ChildCache == null)
                    {
                        node.RebuildChildCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.ChildCache.Length; i < count; i++)
                    {
                        var entry = node.ChildCache[i];
                        if (entry.Bounds.Intersects(center, radius))
                        {
                            results.Add(entry.Value);
                        }
                    }

                    break;
                }
                case IntersectionType.Intersects:
                {
                    // Add all local entries in this node that are in range, regardless
                    // of whether this is an inner or a leaf node.
                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        if (entry.Bounds.Intersects(center, radius))
                        {
                            results.Add(entry.Value);
                        }
                    }

                    // If it's not a leaf recurse into child nodes.
                    if (!node.IsLeaf)
                    {
                        // Build the bounds for each child in the following.
                        var childBounds = new TRectangle
                        {
                            Width = nodeBounds.Width / 2,
                            Height = nodeBounds.Height / 2
                        };

                        // Unrolled loop.
                        if (node.Children[0] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y;
                            Accumulate(node.Children[0], childBounds, queryBounds, center, radius, results);
                        }
                        if (node.Children[1] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y;
                            Accumulate(node.Children[1], childBounds, queryBounds, center, radius, results);
                        }
                        if (node.Children[2] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            Accumulate(node.Children[2], childBounds, queryBounds, center, radius, results);
                        }
                        if (node.Children[3] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            Accumulate(node.Children[3], childBounds, queryBounds, center, radius, results);
                        }
                    }

                    break;
                }
            }
        }

        private static bool Accumulate(
            Node node,
            TRectangle nodeBounds,
            TRectangle queryBounds,
            TPoint center,
            float radius,
            SimpleQueryCallback<T> callback)
        {
            // Check how to proceed.
            switch (ComputeIntersection(queryBounds, nodeBounds))
            {
                case IntersectionType.Contains:
                {
                    // Node completely contained in query, return all entries in it that
                    // intersect with the query no need to recurse further.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        if (entry.Bounds.Intersects(center, radius) && !callback(entry.Value))
                        {
                            return false;
                        }
                    }

                    // Rebuild entry cache if necessary.
                    if (node.ChildCache == null)
                    {
                        node.RebuildChildCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.ChildCache.Length; i < count; i++)
                    {
                        var entry = node.ChildCache[i];
                        if (entry.Bounds.Intersects(center, radius) && !callback(entry.Value))
                        {
                            return false;
                        }
                    }

                    break;
                }
                case IntersectionType.Intersects:
                {
                    // Add all local entries in this node that are in range, regardless
                    // of whether this is an inner or a leaf node.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        if (entry.Bounds.Intersects(center, radius))
                        {
                            if (!callback(entry.Value))
                            {
                                return false;
                            }
                        }
                    }

                    // If it's not a leaf recurse into child nodes.
                    if (!node.IsLeaf)
                    {
                        // Build the bounds for each child in the following.
                        var childBounds = new TRectangle
                        {
                            Width = nodeBounds.Width / 2,
                            Height = nodeBounds.Height / 2
                        };

                        // Unrolled loop.
                        if (node.Children[0] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y;
                            if (!Accumulate(node.Children[0], childBounds, queryBounds, center, radius, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[1] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y;
                            if (!Accumulate(node.Children[1], childBounds, queryBounds, center, radius, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[2] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            if (!Accumulate(node.Children[2], childBounds, queryBounds, center, radius, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[3] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            if (!Accumulate(node.Children[3], childBounds, queryBounds, center, radius, callback))
                            {
                                return false;
                            }
                        }
                    }

                    break;
                }
            }

            return true;
        }

        private static void Accumulate(Node node, TRectangle nodeBounds, TRectangle queryBounds, ISet<T> results)
        {
            // Check how to proceed.
            switch (ComputeIntersection(queryBounds, nodeBounds))
            {
                case IntersectionType.Contains:
                {
                    // Node completely contained in query, return all entries in it that
                    // intersect with the query no need to recurse further. In this case
                    // where we're dealing with rectangles, that will simply be all entries.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        results.Add(node.LocalCache[i].Value);
                    }

                    // Rebuild entry cache if necessary.
                    if (node.ChildCache == null)
                    {
                        node.RebuildChildCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.ChildCache.Length; i < count; i++)
                    {
                        results.Add(node.ChildCache[i].Value);
                    }

                    break;
                }
                case IntersectionType.Intersects:
                {
                    // Add all local entries in this node that are in range, regardless of
                    // whether this is an inner or a leaf node.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        if (IntersectionExtensions.Intersects(entry.Bounds, queryBounds))
                        {
                            results.Add(entry.Value);
                        }
                    }

                    if (!node.IsLeaf)
                    {
                        // Recurse into child nodes.
                        var childBounds = new TRectangle
                        {
                            Width = nodeBounds.Width / 2,
                            Height = nodeBounds.Height / 2
                        };

                        // Unrolled loop.
                        if (node.Children[0] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y;
                            Accumulate(node.Children[0], childBounds, queryBounds, results);
                        }
                        if (node.Children[1] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y;
                            Accumulate(node.Children[1], childBounds, queryBounds, results);
                        }
                        if (node.Children[2] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            Accumulate(node.Children[2], childBounds, queryBounds, results);
                        }
                        if (node.Children[3] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            Accumulate(node.Children[3], childBounds, queryBounds, results);
                        }
                    }

                    break;
                }
            }
        }

        private static bool Accumulate(
            Node node, TRectangle nodeBounds, TRectangle queryBounds, SimpleQueryCallback<T> callback)
        {
            // Check how to proceed.
            switch (ComputeIntersection(queryBounds, nodeBounds))
            {
                case IntersectionType.Contains:
                {
                    // Node completely contained in query, return all entries in it that
                    // intersect with the query no need to recurse further. In this case
                    // where we're dealing with rectangles, that will simply be all entries.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        if (!callback(node.LocalCache[i].Value))
                        {
                            return false;
                        }
                    }

                    // Rebuild entry cache if necessary.
                    if (node.ChildCache == null)
                    {
                        node.RebuildChildCache();
                    }

                    // Add all entries to the collection.
                    for (int i1 = 0, count = node.ChildCache.Length; i1 < count; i1++)
                    {
                        if (!callback(node.ChildCache[i1].Value))
                        {
                            return false;
                        }
                    }

                    break;
                }
                case IntersectionType.Intersects:
                {
                    // Add all local entries in this node that are in range, regardless of
                    // whether this is an inner or a leaf node.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        if (IntersectionExtensions.Intersects(entry.Bounds, queryBounds))
                        {
                            if (!callback(entry.Value))
                            {
                                return false;
                            }
                        }
                    }

                    if (!node.IsLeaf)
                    {
                        // Recurse into child nodes.
                        var childBounds = new TRectangle
                        {
                            Width = nodeBounds.Width / 2,
                            Height = nodeBounds.Height / 2
                        };

                        // Unrolled loop.
                        if (node.Children[0] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y;
                            if (!Accumulate(node.Children[0], childBounds, queryBounds, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[1] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y;
                            if (!Accumulate(node.Children[1], childBounds, queryBounds, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[2] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            if (!Accumulate(node.Children[2], childBounds, queryBounds, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[3] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            if (!Accumulate(node.Children[3], childBounds, queryBounds, callback))
                            {
                                return false;
                            }
                        }
                    }

                    break;
                }
            }

            return true;
        }

        private static void Accumulate(
            Node node, TRectangle nodeBounds, TRectangle queryBounds, TPoint start, TPoint end, float t, ISet<T> results)
        {
            // Check how to proceed.
            switch (ComputeIntersection(queryBounds, nodeBounds))
            {
                case IntersectionType.Contains:
                {
                    // Node completely contained in query, return all entries in it,
                    // no need to recurse further.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        results.Add(node.LocalCache[i].Value);
                    }

                    // Rebuild entry cache if necessary.
                    if (node.ChildCache == null)
                    {
                        node.RebuildChildCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.ChildCache.Length; i < count; i++)
                    {
                        results.Add(node.ChildCache[i].Value);
                    }

                    break;
                }
                case IntersectionType.Intersects:
                {
                    // Add all local entries in this node that are in range, regardless
                    // of whether this is an inner or a leaf node.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        float fraction;
                        if (entry.Bounds.Intersects(start, end, t, out fraction))
                        {
                            results.Add(entry.Value);
                        }
                    }

                    // If it's not a leaf recurse into child nodes.
                    if (!node.IsLeaf)
                    {
                        // Build the bounds for each child in the following.
                        var childBounds = new TRectangle
                        {
                            Width = nodeBounds.Width / 2,
                            Height = nodeBounds.Height / 2
                        };

                        // Unrolled loop.
                        if (node.Children[0] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y;
                            Accumulate(node.Children[0], childBounds, queryBounds, start, end, t, results);
                        }
                        if (node.Children[1] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y;
                            Accumulate(node.Children[1], childBounds, queryBounds, start, end, t, results);
                        }
                        if (node.Children[2] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            Accumulate(node.Children[2], childBounds, queryBounds, start, end, t, results);
                        }
                        if (node.Children[3] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            Accumulate(node.Children[3], childBounds, queryBounds, start, end, t, results);
                        }
                    }

                    break;
                }
            }
        }

        private static bool Accumulate(
            Node node,
            TRectangle nodeBounds,
            ref TRectangle queryBounds,
            TPoint start,
            TPoint end,
            ref float t,
            LineQueryCallback<T> callback)
        {
            // Check how to proceed.
            switch (ComputeIntersection(queryBounds, nodeBounds))
            {
                case IntersectionType.Contains:
                {
                    // Node completely contained in query, return all entries in it,
                    // no need to recurse further.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        float fraction;
                        if (entry.Bounds.Intersects(start, end, t, out fraction))
                        {
                            fraction = callback(entry.Value, fraction);
                            if (fraction > 0f)
                            {
                                t = fraction;
                                queryBounds = IntersectionExtensions.BoundsFor(start, end, t);
                            }
// ReSharper disable CompareOfFloatsByEqualityOperator Intentional, must be set to zero to trigger.
                            else if (fraction == 0f)
// ReSharper restore CompareOfFloatsByEqualityOperator
                            {
                                return false;
                            }
                        }
                    }

                    // Rebuild entry cache if necessary.
                    if (node.ChildCache == null)
                    {
                        node.RebuildChildCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.ChildCache.Length; i < count; i++)
                    {
                        var entry = node.ChildCache[i];
                        float fraction;
                        if (entry.Bounds.Intersects(start, end, t, out fraction))
                        {
                            fraction = callback(entry.Value, fraction);
                            if (fraction > 0f)
                            {
                                t = fraction;
                                queryBounds = IntersectionExtensions.BoundsFor(start, end, t);
                            }
// ReSharper disable CompareOfFloatsByEqualityOperator Intentional, must be set to zero to trigger.
                            else if (fraction == 0f)
// ReSharper restore CompareOfFloatsByEqualityOperator
                            {
                                return false;
                            }
                        }
                    }

                    break;
                }
                case IntersectionType.Intersects:
                {
                    // Add all local entries in this node that are in range, regardless
                    // of whether this is an inner or a leaf node.

                    // Rebuild entry cache if necessary.
                    if (node.LocalCache == null)
                    {
                        node.RebuildLocalCache();
                    }

                    // Add all entries to the collection.
                    for (int i = 0, count = node.LocalCache.Length; i < count; i++)
                    {
                        var entry = node.LocalCache[i];
                        float fraction;
                        if (entry.Bounds.Intersects(start, end, t, out fraction))
                        {
                            fraction = callback(entry.Value, fraction);
                            if (fraction > 0f)
                            {
                                t = fraction;
                                queryBounds = IntersectionExtensions.BoundsFor(start, end, t);
                            }
// ReSharper disable CompareOfFloatsByEqualityOperator Intentional, must be set to zero to trigger.
                            else if (fraction == 0f)
// ReSharper restore CompareOfFloatsByEqualityOperator
                            {
                                return false;
                            }
                        }
                    }

                    // If it's not a leaf recurse into child nodes.
                    if (!node.IsLeaf)
                    {
                        // Build the bounds for each child in the following.
                        var childBounds = new TRectangle
                        {
                            Width = nodeBounds.Width / 2,
                            Height = nodeBounds.Height / 2
                        };

                        // Unrolled loop.
                        if (node.Children[0] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y;
                            if (!Accumulate(node.Children[0], childBounds, ref queryBounds, start, end, ref t, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[1] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y;
                            if (!Accumulate(node.Children[1], childBounds, ref queryBounds, start, end, ref t, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[2] != null)
                        {
                            childBounds.X = nodeBounds.X;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            if (!Accumulate(node.Children[2], childBounds, ref queryBounds, start, end, ref t, callback))
                            {
                                return false;
                            }
                        }
                        if (node.Children[3] != null)
                        {
                            childBounds.X = nodeBounds.X + childBounds.Width;
                            childBounds.Y = nodeBounds.Y + childBounds.Height;
                            if (!Accumulate(node.Children[3], childBounds, ref queryBounds, start, end, ref t, callback))
                            {
                                return false;
                            }
                        }
                    }

                    break;
                }
            }

            return true;
        }

        #endregion

        #endregion

        #region Intersection testing

        /// <summary>Possible intersection types of geometric shapes.</summary>
        private enum IntersectionType
        {
            /// <summary>The shapes are cleanly separated from each other.</summary>
            Disjoint,

            /// <summary>The two shapes are overlapping each other.</summary>
            Intersects,

            /// <summary>One shape is completely contained within the other.</summary>
            Contains
        }

        /// <summary>Box / Box intersection test.</summary>
        /// <param name="rectangle">The first box.</param>
        /// <param name="bounds">The second box.</param>
        /// <returns>How the two intersect.</returns>
        private static IntersectionType ComputeIntersection(TRectangle rectangle, TRectangle bounds)
        {
            var rr = rectangle.X + rectangle.Width;
            var rb = rectangle.Y + rectangle.Height;
            var br = bounds.X + bounds.Width;
            var bb = bounds.Y + bounds.Height;
            if (rectangle.X > br ||
                rectangle.Y > bb ||
                bounds.X > rr ||
                bounds.Y > rb)
            {
                return IntersectionType.Disjoint;
            }

            if (bounds.X >= rectangle.X &&
                bounds.Y >= rectangle.Y &&
                br <= rr &&
                bb <= rb)
            {
                return IntersectionType.Contains;
            }

            return IntersectionType.Intersects;
        }

        #endregion

        #region Types

        /// <summary>
        ///     A node in the tree, which can either be a leaf or an inner node.
        ///     <para>
        ///         Leaf nodes only hold a list of entries, whereas inner nodes also reference to more specific child nodes (in
        ///         addition to local entries in case they cannot be put in a child node because they lie on a split).
        ///     </para>
        /// </summary>
        [DebuggerDisplay("Count = {EntryCount}, Leaf = {IsLeaf}")]
        private sealed class Node
        {
            #region Properties

            /// <summary>Whether this node is a leaf node or not.</summary>
            /// <value>
            ///     <c>true</c> if this instance is leaf node; otherwise, <c>false</c>.
            /// </value>
            public bool IsLeaf
            {
                get
                {
                    return (Children[0] == null) &&
                           (Children[1] == null) &&
                           (Children[2] == null) &&
                           (Children[3] == null);
                }
            }

            #endregion

            #region Fields

            /// <summary>The parent of this node.</summary>
            public Node Parent;

            /// <summary>The children this node points to.</summary>
            public readonly Node[] Children = new Node[4];

            /// <summary>The first entry in the child entity list.</summary>
            public Entry FirstChildEntry;

            /// <summary>The last entry in the child entity list.</summary>
            public Entry LastChildEntry;

            /// <summary>The first entry in the local entity list.</summary>
            public Entry FirstEntry;

            /// <summary>The last entry in the local entity list.</summary>
            public Entry LastEntry;

            /// <summary>Number of entries in this node and all its children combined.</summary>
            public int EntryCount;

            /// <summary>Cache of entries contained in this node by itself (leaf node or internal node with entries on split).</summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public Entry[] LocalCache;

            /// <summary>Used to synchronize access to the local cache.</summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly object _localCacheLock = new object();

            /// <summary>Cache of entries in child nodes.</summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public Entry[] ChildCache;

            /// <summary>Used to synchronize access to the child cache.</summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly object _childCacheLock = new object();

            #endregion

            #region Cache rebuilding

            /// <summary>Rebuilds the local cache.</summary>
            public void RebuildLocalCache()
            {
                lock (_localCacheLock)
                {
                    // Test again after locking, because the cache might have
                    // actually already been built by another thread between the
                    // outer check and getting here.
                    if (LocalCache != null)
                    {
                        return;
                    }

                    if (FirstEntry != null)
                    {
                        // Keep in local variable until fully built, otherwise
                        // other threads might see the not fully initialized array.
                        var cache = new Entry[EntryCount -
                                              (Children[0] == null ? 0 : Children[0].EntryCount) -
                                              (Children[1] == null ? 0 : Children[1].EntryCount) -
                                              (Children[2] == null ? 0 : Children[2].EntryCount) -
                                              (Children[3] == null ? 0 : Children[3].EntryCount)];
                        var i = 0;
                        for (Entry entry = FirstEntry, end = LastEntry.Next;
                             entry != end;
                             entry = entry.Next)
                        {
                            cache[i++] = entry;
                        }

                        // Done, set the new cache.
                        LocalCache = cache;
                    }
                    else
                    {
                        // Nothing here, just set it to an empty array so we don't
                        // try to rebuild it over and over.
                        LocalCache = new Entry[0];
                    }
                }
            }

            /// <summary>Rebuilds the child cache.</summary>
            public void RebuildChildCache()
            {
                lock (_childCacheLock)
                {
                    // Test again after locking, because the cache might have
                    // actually already been built by another thread between the
                    // outer check and getting here.
                    if (ChildCache != null)
                    {
                        return;
                    }

                    if (FirstChildEntry != null)
                    {
                        // Keep in local variable until fully built, otherwise
                        // other threads might see the not fully initialized array.
                        var cache = new Entry[
                            (Children[0] == null ? 0 : Children[0].EntryCount) +
                            (Children[1] == null ? 0 : Children[1].EntryCount) +
                            (Children[2] == null ? 0 : Children[2].EntryCount) +
                            (Children[3] == null ? 0 : Children[3].EntryCount)];
                        var i = 0;
                        for (Entry entry = FirstChildEntry, end = LastChildEntry.Next;
                             entry != end;
                             entry = entry.Next)
                        {
                            cache[i++] = entry;
                        }

                        // Done, set the new cache.
                        ChildCache = cache;
                    }
                    else
                    {
                        // Nothing here, just set it to an empty array so we don't
                        // try to rebuild it over and over.
                        ChildCache = new Entry[0];
                    }
                }
            }

            #endregion

            #region Enumerator

            /// <summary>Enumerates all entries stored directly in this node. It is used by the node iterator.</summary>
            /// <returns>An enumerator for all entries in this node.</returns>
            public IEnumerable<T> GetEntryEnumerable()
            {
                // Rebuild entry cache if necessary.
                RebuildLocalCache();

                // Yield all entries to the collection.
                for (int i = 0, j = LocalCache.Length; i < j; i++)
                {
                    yield return LocalCache[i].Value;
                }
            }

            #endregion
        }

        /// <summary>A single entry in the tree, uniquely identified by its value.</summary>
        [DebuggerDisplay("Bounds = {Bounds}, Value = {Value}")]
        private sealed class Entry
        {
            #region Fields

            /// <summary>Next entry in the linked list.</summary>
            public Entry Next;

            /// <summary>Previous entry in the linked list.</summary>
            public Entry Previous;

            /// <summary>The point at which the entry is stored.</summary>
            public TRectangle Bounds;

            /// <summary>The value stored in this entry.</summary>
            public T Value;

            #endregion

            #region Methods

            /// <summary>Remove this entry from the linked list.</summary>
            public void Remove()
            {
                if (Previous != null)
                {
                    Previous.Next = Next;
                }
                if (Next != null)
                {
                    Next.Previous = Previous;
                }

                Next = null;
                Previous = null;
            }

            /// <summary>Insert this node into the linked list, after the specified entry.</summary>
            /// <param name="entry">The entry to insert after.</param>
            public void InsertAfter(Entry entry)
            {
                // Adjust other nodes' values.
                var insertBefore = entry.Next;
                entry.Next = this;
                if (insertBefore != null)
                {
                    insertBefore.Previous = this;
                }

                // Adjust own values.
                Previous = entry;
                Next = insertBefore;
            }

            /// <summary>Insert this node into the linked list, before the specified entry.</summary>
            /// <param name="entry">The entry to insert before.</param>
            public void InsertBefore(Entry entry)
            {
                // Adjust other nodes' values.
                var insertAfter = entry.Previous;
                entry.Previous = this;
                if (insertAfter != null)
                {
                    insertAfter.Next = this;
                }

                // Adjust own values.
                Previous = insertAfter;
                Next = entry;
            }

            #endregion
        }

        #endregion
    }
}