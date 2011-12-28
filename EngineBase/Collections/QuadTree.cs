﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace Engine.Collections
{
    /// <summary>
    /// A quad tree that can dynamically grow as needed.
    /// 
    /// <para>
    /// A special restriction is that all nodes will be sized at some power of
    /// two, where every level that power increases:<br/>
    /// <c>node size := minBucketSize &lt;&lt; level</c>.
    /// </para>
    /// 
    /// <para>
    /// All nodes can quickly iterate over all entries stored in all of their
    /// child nodes. The actual entries are stored in a linked list, which is
    /// sorted in a way that allows unambiguous mapping of a section of that
    /// linked list to a subtree.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of the values stored in this tree.</typeparam>
    [DebuggerDisplay("Count = {Count}")]
    public sealed class QuadTree<T>
    {
        #region Properties

        /// <summary>
        /// The number of values stored in this tree.
        /// </summary>
        public int Count { get { return _entries.Count; } }

        /// <summary>
        /// The current overall bounds of the tree.
        /// </summary>
        public Rectangle Bounds { get { return new Rectangle(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height); } }

        #endregion

        #region Fields

        /// <summary>
        /// The number of items in a single cell allowed before splitting the cell.
        /// </summary>
        private readonly int _maxEntriesPerNode;

        /// <summary>
        /// The minimum size of a grid cell, used to stop splitting at a
        /// defined accuracy.
        /// </summary>
        private readonly int _minBucketSize;

        /// <summary>
        /// The current bounds of the tree. This is a dynamic value, adjusted
        /// based on elements added to the tree.
        /// </summary>
        private Rectangle _bounds = Rectangle.Empty;

        /// <summary>
        /// The root node of the tree.
        /// </summary>
        private Node _root = new Node(null);

        /// <summary>
        /// A list of all entries in the tree. The linked list allows simply
        /// adding an entry to a leaf node, keeping the pointers to the segment
        /// of an inner intact.
        /// </summary>
        private LinkedList<Entry> _entries = new LinkedList<Entry>();

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new, empty quad tree, with the specified parameters.
        /// </summary>
        /// <param name="maxEntriesPerNode">The maximum number of entries per
        /// node before the node will be split.</param>
        /// <param name="minBucketSize">The minimum size of a bucket, i.e.
        /// nodes of this size or smaller won't be split regardless of the
        /// number of entries in them.</param>
        public QuadTree(int maxEntriesPerNode, int minBucketSize)
        {
            if (maxEntriesPerNode < 1)
            {
                throw new ArgumentException("Split count must be larger than zero.", "maxEntriesPerNode");
            }
            if (minBucketSize < 1)
            {
                throw new ArgumentException("Bucket size must be larger than zero.", "minBucketSize");
            }
            _maxEntriesPerNode = maxEntriesPerNode;
            _minBucketSize = minBucketSize;

            _bounds.X = _bounds.Y = -_minBucketSize;
            _bounds.Width = _bounds.Height = _minBucketSize << 1;
        }

        #endregion

        /// <summary>
        /// Add a new entry to the tree, at the specified position, with the
        /// specified associated value.
        /// </summary>
        /// <param name="point">The point at which to store the entry.</param>
        /// <param name="value">The value associated with the point.</param>
        /// <exception cref="ArgumentException">This pair of point and value
        /// are already stored in the tree.</exception>
        public void Add(Vector2 point, T value)
        {
            // Create the entry to add.
            var entry = new Entry(point, value);

            // Handle dynamic growth.
            EnsureCapacity(point);

            // Get the node to insert in.
            int nodeX, nodeY, nodeSize;
            var insertionNode = FindNode(point, out nodeX, out nodeY, out nodeSize);

            // If it's not a leaf node, create the leaf node for the new entry.
            // Also get the node in the linked list to insert after.
            LinkedListNode<Entry> insertAfter;
            if (!insertionNode.IsLeaf)
            {
                var cell = ComputeCell(nodeX, nodeY, nodeSize >> 1, point);
                insertionNode.Children[cell] = new Node(insertionNode);
                insertionNode = insertionNode.Children[cell];
                insertAfter = insertionNode.Parent.HighEntry;
            }
            else
            {
                // Got a leaf, check if we already have that point.
                foreach (var existingEntry in insertionNode.Entries)
                {
                    if (entry.Equals(existingEntry))
                    {
                        throw new ArgumentException("Entry is already in the tree at the specified point.", "value");
                    }
                }
                // Not yet in the tree.
                insertAfter = insertionNode.LowEntry;
            }

            // Add the data, get the newly created list entry.
            var insertedEntry = insertAfter != null
                ? _entries.AddAfter(insertAfter, entry)
                : _entries.AddFirst(entry);

            var node = insertionNode;
            while (node != null)
            {
                if (node.LowEntry == node.HighEntry)
                {
                    // Only one node yet, or empty.
                    node.LowEntry = node.LowEntry ?? insertedEntry;
                    node.HighEntry = insertedEntry;
                }
                else if (node.HighEntry == insertAfter)
                {
                    // Inserted after high node, adjust accordingly.
                    node.HighEntry = insertedEntry;
                }

                // Remember we have one more entry.
                ++node.EntryCount;

                // Continue checking in our parent.
                node = node.Parent;
            }

            // We need to split the node.
            SplitNodeIfNecessary(nodeX, nodeY, nodeSize, insertionNode);
        }

        /// <summary>
        /// Ensures the tree can contain the given point.
        /// </summary>
        /// <param name="point">The point to ensure tree size for.</param>
        private void EnsureCapacity(Vector2 point)
        {
            if (!_bounds.Contains((int)point.X, (int)point.Y))
            {
                // Point is outside our current tree bounds. Expand it to allow
                // fitting in the new point.
                uint neededSizeX = GetNextHighestPowerOfTwo(
                    (uint)System.Math.Max(0, System.Math.Abs(point.X) - 1));
                uint neededSizeY = GetNextHighestPowerOfTwo(
                    (uint)System.Math.Max(0, System.Math.Abs(point.Y) - 1));
                int neededSize = (int)System.Math.Max(neededSizeX, neededSizeY);

                // Avoid possible issues when adding the first point at (0, 0).
                if (neededSize == 0)
                {
                    neededSize = _minBucketSize;
                }

                // Already got a root node. Push as many levels above it as
                // we need for the new entry. This ensures there will be a
                // node at the point we're trying to insert.
                while (_bounds.X > -neededSize)
                {
                    InsertLevel();
                }
            }
        }

        /// <summary>
        /// Update a single entry by changing its position.
        /// </summary>
        /// <param name="oldPoint">The old position of the entry.</param>
        /// <param name="newPoint">The new position of the entry.</param>
        /// <param name="value">The value of the entry.</param>
        /// <returns><c>true</c> if the update was successful, <c>false</c>
        /// if there is no such entry in the tree.</returns>
        public bool Update(Vector2 oldPoint, Vector2 newPoint, T value)
        {
            // The entry we wish to update.
            var entry = new Entry(oldPoint, value);

            // Get the node the entry would be in.
            int nodeX, nodeY, nodeSize;
            var oldNode = FindNode(oldPoint, out nodeX, out nodeY, out nodeSize);
            
            // Is the node a leaf node? If not we don't have that entry.
            if (oldNode.IsLeaf)
            {
                // Check if we have that entry.
                foreach (var nodeEntry in oldNode.Entries)
                {
                    if (nodeEntry.Value.Equals(entry))
                    {
                        // Found it! See if the new point falls into the same
                        // node, otherwise re-insert.
                        var newNode = FindNode(newPoint, out nodeX, out nodeY, out nodeSize);
                        if (oldNode == newNode)
                        {
                            // Same node, just update the entry.
                            nodeEntry.Value.Point = newPoint;
                        }
                        else
                        {
                            // Different node, re-insert.
                            Remove(oldPoint, value);
                            Add(newPoint, value);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Remove the specified value at the specified point from the tree.
        /// </summary>
        /// <param name="point">The position to remove the value at.</param>
        /// <param name="value">The value to remove.</param>
        /// <returns><c>true</c> if the specified pair of point and value was
        /// in the tree, <c>false</c> otherwise.</returns>
        public bool Remove(Vector2 point, T value)
        {
            // The entry we wish to remove.
            var removalEntry = new Entry(point, value);

            // Get the node the entry would be in.
            int nodeX, nodeY, nodeSize;
            var removalNode = FindNode(point, out nodeX, out nodeY, out nodeSize);

            // Is the node a leaf node? If not we don't have that entry.
            if (removalNode.IsLeaf)
            {
                // Check if we have that entry.
                foreach (var nodeEntry in removalNode.Entries)
                {
                    if (nodeEntry.Value.Equals(removalEntry))
                    {
                        // Found it! If it's our low or high state adjust them
                        // accordingly.
                        var node = removalNode;
                        while (node != null)
                        {
                            if (node.LowEntry == node.HighEntry)
                            {
                                // Only one left, clear the node.
                                node.LowEntry = null;
                                node.HighEntry = null;
                            }
                            else if (node.LowEntry == nodeEntry)
                            {
                                // It's the low node, adjust accordingly.
                                node.LowEntry = node.LowEntry.Next;
                            }
                            else if (node.HighEntry == nodeEntry)
                            {
                                // It's the high node, adjust accordingly.
                                node.HighEntry = node.HighEntry.Previous;
                            }

                            // Adjust entry count.
                            --node.EntryCount;

                            // Continue checking in our parent.
                            node = node.Parent;
                        }

                        // Remove the entry from the list of entries.
                        _entries.Remove(nodeEntry);

                        // See if we can compact the node's parent. This has to
                        // be done in a post-processing step because the entry
                        // has to be removed first (to update entry counts).
                        CleanNode(removalNode);

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Perform a range query on this tree. This will return all entries
        /// in the tree that are in the specified range to the specified point,
        /// using a euclidean distance.
        /// </summary>
        /// <param name="point">The query point near which to get entries.</param>
        /// <param name="range">The maximum distance an entry may be away
        /// from the query point to be returned.</param>
        /// <returns></returns>
        public List<T> RangeQuery(Vector2 point, float range)
        {
            var result = new List<T>();

            // Recurse through the tree, starting at the root node, to find
            // nodes intersecting with the range query.
            Accumulate(_bounds.X, _bounds.Y, _bounds.Width, _root,
                point, range * range, result);

            return result;
        }

        #region Internal functionality

        /// <summary>
        /// Find a node at the given query point. If possible, this will return
        /// a leaf node. If there is no leaf node at the query point, it will
        /// return the inner node that would contain the leaf node that would
        /// hold that point.
        /// </summary>
        /// <param name="point">The point to get the leaf node for.</param>
        /// <param name="nodeX">Will be the x position of the node.</param>
        /// <param name="nodeY">Will be the y position of the node.</param>
        /// <param name="nodeSize">Will be the size of the node.</param>
        /// <returns>The node for the specified query point.</returns>
        private Node FindNode(Vector2 point, out int nodeX, out int nodeY, out int nodeSize)
        {
            var node = _root;
            nodeX = _bounds.X;
            nodeY = _bounds.Y;
            nodeSize = _bounds.Width;

            while (!node.IsLeaf)
            {
                // Get current child size.
                var childSize = nodeSize >> 1;

                // Into which child node would we descend?
                var cell = ComputeCell(nodeX, nodeY, childSize, point);

                // Do we have to create that child?
                if (node.Children[cell] != null)
                {
                    // Yes, descend into that node.
                    node = node.Children[cell];
                    nodeX += (((cell & 1) == 0) ? 0 : childSize);
                    nodeY += (((cell & 2) == 0) ? 0 : childSize);
                    nodeSize = childSize;
                }
                else
                {
                    // No. Return the current inner node instead.
                    return node;
                }
            }

            return node;
        }

        #region Restructuring

        /// <summary>
        /// Check if a node needs to be split, and split it if allowed to.
        /// </summary>
        /// <param name="x">The x position of the node.</param>
        /// <param name="y">The y position of the node.</param>
        /// <param name="size">The size of the node.</param>
        /// <param name="node">The actual node to split.</param>
        private void SplitNodeIfNecessary(int x, int y, int size, Node node)
        {
            // Should we split?
            if (!node.IsLeaf || node.EntryCount <= _maxEntriesPerNode || size <= _minBucketSize)
            {
                // No.
                return;
            }

            // Precompute child size, used several times.
            var childSize = size >> 1;

            // Used to keep track of the new high entry due to possible
            // resorting.
            LinkedListNode<Entry> highEntry = null;

            // Check each entry to which new cell it'll belong.
            foreach (var entry in new List<LinkedListNode<Entry>>(node.Entries))
            {
                // In which child node would we insert?
                int cell = ComputeCell(x, y, childSize, entry.Value.Point);

                // Do we have to create that child?
                if (node.Children[cell] == null)
                {
                    // Yes.
                    node.Children[cell] = new Node(node);
                    node.Children[cell].LowEntry = entry;

                    // No shuffling, mark this as the last entry.
                    highEntry = entry;
                }
                else if (node.Children[cell].HighEntry.Next != entry)
                {
                    // Out of order. Sort the sublist to represent in up to
                    // four intervals the entries of the child nodes.
                    _entries.Remove(entry);
                    _entries.AddAfter(node.Children[cell].HighEntry, entry);
                }
                else
                {
                    // No shuffling, mark this as the last entry.
                    highEntry = entry;
                }

                // Either way, one more node.
                ++node.Children[cell].EntryCount;

                // List is now in order, so we set the highest to this entry.
                node.Children[cell].HighEntry = entry;
            }

            // Adjust parent high node if it changed due to sorting.
            if (node.HighEntry != highEntry)
            {
                // Need to adjust parents who had our high entry (including
                // the node that was split).
                var oldHighEntry = node.HighEntry;
                var parent = node;
                while (parent != null)
                {
                    if (parent.HighEntry == oldHighEntry)
                    {
                        parent.HighEntry = highEntry;
                        parent = parent.Parent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Do this recursively if the split resulted in another bucket that
            // is too large.
            for (int i = 0; i < 4; ++i)
            {
                if (node.Children[i] != null)
                {
                    SplitNodeIfNecessary(
                        x + (((i & 1) == 0) ? 0 : childSize),
                        y + (((i & 2) == 0) ? 0 : childSize),
                        childSize, node.Children[i]);
                }
            }
        }

        /// <summary>
        /// Try to clean up a node and its parents. This walks the tree towards
        /// the root, removing child nodes where possible.
        /// </summary>
        /// <param name="node">The node to start cleaning at.</param>
        private void CleanNode(Node node)
        {
            // Do nothing for leaf nodes or when passing the root node.
            if (node == null)
            {
                return;
            }

            // Check if child nodes are unnecessary for this node.
            if (node.EntryCount <= _maxEntriesPerNode)
            {
                // We can prune the child nodes.
                node.Children[0] = null;
                node.Children[1] = null;
                node.Children[2] = null;
                node.Children[3] = null;
            }
            else
            {
                // Check if we have empty child nodes.
                for (int i = 0; i < 4; i++)
                {
                    // If so, remove them.
                    if (node.Children[i] != null && node.Children[i].EntryCount == 0)
                    {
                        node.Children[i] = null;
                    }
                }
            }

            // Check parent.
            CleanNode(node.Parent);
        }

        /// <summary>
        /// Inserts a new level on top of the root node, making it the new root
        /// node. Will reattach all of the root node's child nodes to the
        /// appropriate child nodes of the new root node.
        /// </summary>
        private void InsertLevel()
        {
            // Create the new root node.
            var node = new Node(null);

            // Copy list start and end (which will just be the first and last
            // elements in the list of all entries).
            node.EntryCount = _root.EntryCount;
            node.LowEntry = _root.LowEntry;
            node.HighEntry = _root.HighEntry;

            // Check top left sector, add it as top left sectors lower right
            // node, if it is set.
            if (_root.Children[0] != null)
            {
                node.Children[0] = new Node(node);
                node.Children[0].Children[3] = _root.Children[0];
                node.Children[0].Children[3].Parent = node.Children[0];

                node.Children[0].EntryCount = _root.Children[0].EntryCount;
                node.Children[0].LowEntry = _root.Children[0].LowEntry;
                node.Children[0].HighEntry = _root.Children[0].HighEntry;
            }

            // Check top right sector, add it as top right sectors lower left
            // node, if it is set.
            if (_root.Children[1] != null)
            {
                node.Children[1] = new Node(node);
                node.Children[1].Children[2] = _root.Children[1];
                node.Children[1].Children[2].Parent = node.Children[1];

                node.Children[1].EntryCount = _root.Children[1].EntryCount;
                node.Children[1].LowEntry = _root.Children[1].LowEntry;
                node.Children[1].HighEntry = _root.Children[1].HighEntry;
            }

            // Check bottom left sector, add it as bottom left sectors top
            // right node, if it is set.
            if (_root.Children[2] != null)
            {
                node.Children[2] = new Node(node);
                node.Children[2].Children[1] = _root.Children[2];
                node.Children[2].Children[1].Parent = node.Children[2];

                node.Children[2].EntryCount = _root.Children[2].EntryCount;
                node.Children[2].LowEntry = _root.Children[2].LowEntry;
                node.Children[2].HighEntry = _root.Children[2].HighEntry;
            }

            // Check bottom right sector, add it as bottom right sectors top
            // left node, if it is set.
            if (_root.Children[3] != null)
            {
                node.Children[3] = new Node(node);
                node.Children[3].Children[0] = _root.Children[3];
                node.Children[3].Children[0].Parent = node.Children[3];

                node.Children[3].EntryCount = _root.Children[3].EntryCount;
                node.Children[3].LowEntry = _root.Children[3].LowEntry;
                node.Children[3].HighEntry = _root.Children[3].HighEntry;
            }

            // Set the new root node, adjust the overall tree bounds.
            _root = node;
            _bounds.X = _bounds.X << 1;
            _bounds.Y = _bounds.Y << 1;
            _bounds.Width = _bounds.Width << 1;
            _bounds.Height = _bounds.Height << 1;
        }

        #endregion

        #endregion

        #region Utility methods

        /// <summary>
        /// Gets the next higher power of two for a given number. Used when
        /// inserting new nodes into the tree, to check if our bounds suffice.
        /// </summary>
        /// <remarks>
        /// If a power of two is given, the next higher one will be returned,
        /// not the given one. When zero is given, zero is returned.
        /// </remarks>
        /// <see cref="http://jeffreystedfast.blogspot.com/2008/06/calculating-nearest-power-of-2.html"/>
        /// <param name="i">The number to get the next higher power of two
        /// for.</param>
        /// <returns>The next higher power of two.</returns>
        private static uint GetNextHighestPowerOfTwo(uint i)
        {
            uint j, k;
            if ((j = i & 0xFFFF0000) == 0) j = i;
            if ((k = j & 0xFF00FF00) == 0) k = j;
            if ((j = k & 0xF0F0F0F0) == 0) j = k;
            if ((k = j & 0xCCCCCCCC) == 0) k = j;
            if ((j = k & 0xAAAAAAAA) == 0) j = k;
            return j << 1;
        }

        /// <summary>
        /// Computes the cell of a node with the specified position and child
        /// node size the specified point falls into.
        /// </summary>
        /// <param name="x">The x coordinate of the node.</param>
        /// <param name="y">The y coordinate of the node.</param>
        /// <param name="childSize">The size of the nodes child nodes.</param>
        /// <param name="point">The point to check for.</param>
        /// <returns>The cell number the point falls into.</returns>
        private static int ComputeCell(int x, int y, int childSize, Vector2 point)
        {
            var cell = 0;
            if ((int)point.X > x + childSize)
            {
                // Right half.
                cell |= 1;
            }
            if ((int)point.Y > y + childSize)
            {
                // Lower half.
                cell |= 2;
            }
            return cell;
        }

        /// <summary>
        /// Accumulate all entries in range of a circular range query to the
        /// given list. This recurses the tree down inner nodes that intersect
        /// the query, until it finds a leaf node. Then adds all entries in the
        /// leaf that are in range.
        /// </summary>
        /// <param name="x">The x position of the current node.</param>
        /// <param name="y">The y position of the current node.</param>
        /// <param name="size">The size of the current node.</param>
        /// <param name="node">The current node.</param>
        /// <param name="point">The query point.</param>
        /// <param name="rangeSquared">The squared query range.</param>
        /// <param name="list">The result list.</param>
        private void Accumulate(int x, int y, int size, Node node, Vector2 point, float rangeSquared, List<T> list)
        {
            if (Intersect(point, rangeSquared, x, y, size))
            {
                // Node intersects with the query.
                if (node.IsLeaf)
                {
                    // Add all entries in this node that are in range.
                    foreach (var entry in node.Entries)
                    {
                        if (Vector2.DistanceSquared(point, entry.Value.Point) < rangeSquared)
                        {
                            list.Add(entry.Value.Value);
                        }
                    }
                }
                else
                {
                    // Recurse into child nodes.
                    var childSize = size >> 1;
                    for (int i = 0; i < 4; i++)
                    {
                        if (node.Children[i] != null)
                        {
                            Accumulate(
                                x + (((i & 1) == 0) ? 0 : childSize),
                                y + (((i & 2) == 0) ? 0 : childSize),
                                childSize, node.Children[i],
                                point, rangeSquared, list);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Circle / Box intersection test.
        /// </summary>
        /// <param name="center">The center of the circle.</param>
        /// <param name="radiusSquared">The squared radius of the circle.</param>
        /// <param name="x">The x position of the box.</param>
        /// <param name="y">The y position of the box.</param>
        /// <param name="size">The size of the box.</param>
        /// <returns>Whether the two intersect or not.</returns>
        private static bool Intersect(Vector2 center, float radiusSquared, int x, int y, int size)
        {
            Vector2 closest = center;
            if (center.X < x)
            {
                closest.X = x;
            }
            else if (center.X > x + size)
            {
                closest.X = x + size;
            }
            if (center.Y < y)
            {
                closest.Y = y;
            }
            else if (center.Y > y + size)
            {
                closest.Y = y + size;
            }
            return Vector2.DistanceSquared(closest, center) <= radiusSquared;
        }

        #endregion

        #region Types

        /// <summary>
        /// A node in the tree, which can either be a leaf or an inner node.
        /// 
        /// <para>
        /// Leaf nodes only hold a list of entities, whereas inner nodes also
        /// reference to more specific child nodes.
        /// </para>
        /// </summary>
        [DebuggerDisplay("Count = {EntryCount}, Children = {GetChildrenCount()}")]
        private class Node
        {
            #region Properties
            
            /// <summary>
            /// Whether this node is a leaf node.
            /// </summary>
            public bool IsLeaf { get { return GetChildrenCount() == 0; } }

            /// <summary>
            /// Returns an iterator for the entries stored in this node.
            /// </summary>
            public IEnumerable<LinkedListNode<Entry>> Entries
            {
                get
                {
                    for (var entry = LowEntry; HighEntry != null && entry != HighEntry.Next; entry = entry.Next)
                    {
                        yield return entry;
                    }
                }
            }

            #endregion

            #region Fields
            
            /// <summary>
            /// The parent of this node.
            /// </summary>
            public Node Parent;

            /// <summary>
            /// The low entry in the entity list (low end of the interval).
            /// </summary>
            public LinkedListNode<Entry> LowEntry;

            /// <summary>
            /// The high entry in the entity list (high end of the interval).
            /// </summary>
            public LinkedListNode<Entry> HighEntry;

            /// <summary>
            /// Number of entries in this node.
            /// </summary>
            public int EntryCount;

            /// <summary>
            /// The children this node points to.
            /// </summary>
            public readonly Node[] Children = new Node[4];

            #endregion

            #region Constructor

            /// <summary>
            /// Creates a new tree node with the specified parent node.
            /// </summary>
            /// <param name="parent">The parent of this node.</param>
            public Node(Node parent)
            {
                this.Parent = parent;
            }

            #endregion

            #region Accessors
            
            /// <summary>
            /// Compute the number of entries stored in this node.
            /// </summary>
            /// <returns>The number of entries stored in this node.</returns>
            //public int GetCount()
            //{
            //    int count = 0;
            //    foreach (var entry in Entries)
            //    {
            //        ++count;
            //    }
            //    return count;
            //}

            /// <summary>
            /// Get the number of child nodes this node references.
            /// </summary>
            /// <returns>The number of child nodes of this node.</returns>
            public int GetChildrenCount()
            {
                return ((Children[0] == null) ? 0 : 1) +
                       ((Children[1] == null) ? 0 : 1) +
                       ((Children[2] == null) ? 0 : 1) +
                       ((Children[3] == null) ? 0 : 1);
            }

            #endregion
        }

        /// <summary>
        /// A single entry in the tree, uniquely identified by its position
        /// and value.
        /// </summary>
        [DebuggerDisplay("Point = {Point}, Value = {Value}")]
        private class Entry
        {
            #region Fields
            
            /// <summary>
            /// The point at which the entry is stored.
            /// </summary>
            public Vector2 Point;

            /// <summary>
            /// The value stored in this entry.
            /// </summary>
            public readonly T Value;
            
            #endregion

            #region Constructor

            /// <summary>
            /// Creates a new entry with the specified parameters.
            /// </summary>
            /// <param name="point">The point of the entry.</param>
            /// <param name="value">The value of the entry.</param>
            public Entry(Vector2 point, T value)
            {
                this.Point = point;
                this.Value = value;
            }

            #endregion

            #region Overrides

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                Entry e = obj as Entry;
                if (e == null)
                {
                    return false;
                }

                return (Point.Equals(e.Point)) && (Value.Equals(e.Value));
            }

            public bool Equals(Entry e)
            {
                if (e == null)
                {
                    return false;
                }

                return (Point.Equals(e.Point)) && (Value.Equals(e.Value));
            }

            public override int GetHashCode()
            {
                return Point.GetHashCode() ^ Value.GetHashCode();
            }

            #endregion
        }

        #endregion
    }
}
