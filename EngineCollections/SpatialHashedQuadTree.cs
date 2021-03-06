﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.FarMath;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;

// Adjust these as necessary, they just have to share a compatible
// interface with the XNA types.
#if FARMATH
using Engine.Collections;
using TPoint = Engine.FarMath.FarPosition;
using TSingle = Engine.FarMath.FarValue;
using TRectangle = Engine.FarMath.FarRectangle;
#else
using Engine.Math;
using TPoint = Microsoft.Xna.Framework.Vector2;
using TSingle = System.Single;
using TRectangle = Engine.Math.RectangleF;
#endif

#if FARMATH
namespace Engine.FarCollections
#else
namespace Engine.Collections
#endif
{
    /// <summary>
    ///     This is a two level index structure, using a spatial hash as the primary structure and working with
    ///     <seealso cref="FarValue"/>s. It splits areas into those defined by the segment size of far values and indexes these
    ///     areas using quad trees. On that level the index works with normal float values for better performance.
    /// </summary>
    /// <typeparam name="T">The type to store in the index.</typeparam>
    [Packetizable]
    public sealed class SpatialHashedQuadTree<T> : IIndex<T, TRectangle, TPoint>, ICopyable<SpatialHashedQuadTree<T>>
    {
        #region Constants

        /// <summary>The size of a single hashed cell in this index (i.e. bounds of a quadtree).</summary>
#if FARMATH
        private static readonly int CellSize = FarValue.SegmentSize;
#else
        private const int CellSize = 65536;
#endif

        #endregion

        #region Properties

        /// <summary>The number of values stored in this index.</summary>
        public int Count
        {
            get { return _entryBounds.Count; }
        }

        /// <summary>Gets the maximum tree depth of the deepest sub-tree.</summary>
        public int Depth
        {
            get { return _cells.Values.Max(t => t.Depth); }
        }

        #endregion

        #region Fields

        /// <summary>A callback that can be used to write an object stored in the tree to a packet for serialization.</summary>
        [PacketizeIgnore]
        private readonly Action<IWritablePacket, T> _packetizer;

        /// <summary>A callback that can be used to read an object stored in the tree from a packet for deserialization.</summary>
        [PacketizeIgnore]
        private readonly Func<IReadablePacket, T> _depacketizer;

        /// <summary>The max entries per node in quad trees.</summary>
        private readonly int _maxEntriesPerNode;

        /// <summary>The min node bounds for nodes in quad trees.</summary>
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

        /// <summary>The buckets with the quad trees storing the actual entries.</summary>
        [CopyIgnore, PacketizeIgnore]
        private readonly Dictionary<ulong, Collections.DynamicQuadTree<T>> _cells =
            new Dictionary<ulong, Collections.DynamicQuadTree<T>>();

        /// <summary>Maps entries back to their bounds, for removal.</summary>
        [CopyIgnore, PacketizeIgnore]
        private readonly Dictionary<T, TRectangle> _entryBounds = new Dictionary<T, TRectangle>();

        #endregion

        #region Constructor

        /// <summary>
        ///     Initializes a new instance of the <see cref="SpatialHashedQuadTree{T}"/> class.
        /// </summary>
        /// <param name="maxEntriesPerNode">The max entries per node in quad trees.</param>
        /// <param name="minNodeBounds">The min node bounds for nodes in quad trees.</param>
        /// <param name="boundExtension">The amount by which to fatten bounds.</param>
        /// <param name="movingBoundMultiplier">The amount with which to multiply movement delta for fattening.</param>
        /// <param name="packetizer">A function that can be used to packetize the type stored in the tree.</param>
        /// <param name="depacketizer">A function that can be used to depacketize the type stored in the tree.</param>
        public SpatialHashedQuadTree(
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
        }

        #endregion

        #region Implementation of IIndex<T,FarRectangle,FarPosition>

        /// <summary>Add a new item to the index, with the specified bounds.</summary>
        /// <param name="bounds">The bounds of the item.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="T:System.ArgumentException">The item is already stored in the index.</exception>
        public void Add(TRectangle bounds, T item)
        {
            if (Contains(item))
            {
                throw new ArgumentException("Entry is already in the index.", "item");
            }

            // Extend bounds.
            bounds.Inflate(_boundExtension, _boundExtension);

            // Add to each cell the element's bounds intersect with.
            foreach (var cell in ComputeCells(bounds))
            {
                // Create the quad tree for that cell if it doesn't yet exist.
                if (!_cells.ContainsKey(cell.Item1))
                {
                    // No need to extend again, we already did.
                    _cells.Add(
                        cell.Item1,
                        new Collections.DynamicQuadTree<T>(
                            _maxEntriesPerNode,
                            _minNodeBounds,
                            0f,
                            0f,
                            _packetizer,
                            _depacketizer));
                }

                // Convert the item bounds to the tree's local coordinate space.
                var relativeBounds = bounds;
                relativeBounds.Offset(cell.Item2);

                // And add the item to the tree.
// ReSharper disable RedundantCast Necessary for FarCollections.
                _cells[cell.Item1].Add((Math.RectangleF) relativeBounds, item);
// ReSharper restore RedundantCast
            }

            // Store element itself for future retrieval (removals, item lookup).
            _entryBounds.Add(item, bounds);
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
        public bool Update(TRectangle newBounds, Vector2 delta, T item)
        {
            // Check if we have that entry, if not add it.
            if (!Contains(item))
            {
                return false;
            }

            // Get the old bounds.
            var oldBounds = _entryBounds[item];

            // Nothing to do if our approximation in the tree still contains the item.
            if (oldBounds.Contains(newBounds))
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

            // Figure out what changed (the delta in cells).

            // Because we already did the bound extensions the update method in the
            // related quad trees would just do superfluous work, so instead we can
            // just remove and re-insert the entries where necessary. This also makes
            // this function a lot simpler.

            /*
            
            var oldCells = new HashSet<Tuple<ulong, TPoint>>(ComputeCells(oldBounds));
            var newCells = new HashSet<Tuple<ulong, TPoint>>(ComputeCells(newBounds));

            // Get all cells that the entry no longer is in.
            var removedCells = new HashSet<Tuple<ulong, TPoint>>(oldCells);
            removedCells.ExceptWith(newCells);
            foreach (var cell in removedCells)
            {
                // Remove from the tree.
                _entries[cell.Item1].Remove(item);

                // Clean up: remove the tree if it's empty.
                if (_entries[cell.Item1].Count == 0)
                {
                    _entries.Remove(cell.Item1);
                }
            }

            // Get all the cells the entry now is in.
            var addedCells = new HashSet<Tuple<ulong, TPoint>>(newCells);
            addedCells.ExceptWith(oldCells);
            foreach (var cell in addedCells)
            {
                // Create the quad tree for that cell if it doesn't yet exist.
                if (!_entries.ContainsKey(cell.Item1))
                {
                    // No need to extend again, we already did.
                    _entries.Add(cell.Item1, new Collections.QuadTree<T>(_maxEntriesPerNode, _minNodeBounds, 0f, 0f));
                }

                // Convert the item bounds to the tree's local coordinate space.
                var relativeBounds = newBounds;
                relativeBounds.Offset(cell.Item2);

                // And add the item to the tree.
                _entries[cell.Item1].Add((Math.RectangleF)relativeBounds, item);
            }

            // Get all cells the entry still is in.
            oldCells.ExceptWith(addedCells);
            oldCells.ExceptWith(removedCells);
            foreach (var cell in oldCells)
            {
                // Convert the item bounds to the tree's local coordinate space.
                var relativeBounds = newBounds;
                relativeBounds.Offset(cell.Item2);

                // And update the item to the tree.
                _entries[cell.Item1].Update((Math.RectangleF)relativeBounds, Vector2.Zero, item);
            }
            
            /*/

            // Remove from old cells.
            foreach (var cell in ComputeCells(oldBounds))
            {
                Collections.DynamicQuadTree<T> tree;
                _cells.TryGetValue(cell.Item1, out tree);
                if (tree != null)
                {
                    tree.Remove(item);
                }
            }

            // Add to new cells.
            foreach (var cell in ComputeCells(newBounds))
            {
                // Create the quad tree for that cell if it doesn't yet exist.
                if (!_cells.ContainsKey(cell.Item1))
                {
                    // No need to extend again, we already did.
                    _cells.Add(
                        cell.Item1,
                        new Collections.DynamicQuadTree<T>(
                            _maxEntriesPerNode,
                            _minNodeBounds,
                            0f,
                            0f,
                            _packetizer,
                            _depacketizer));
                }

                // Convert the item bounds to the tree's local coordinate space.
                var relativeBounds = newBounds;
                relativeBounds.Offset(cell.Item2);

                // And add the item to the tree.
// ReSharper disable RedundantCast Necessary for FarCollections.
                _cells[cell.Item1].Add((Math.RectangleF) relativeBounds, item);
// ReSharper restore RedundantCast
            }

            //*/

            // Store the new item bounds.
            _entryBounds[item] = newBounds;

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
                return false;
            }

            // Remove the entry from each tree it is part of, according to
            // its current bounds.
            foreach (var cell in ComputeCells(_entryBounds[item]))
            {
                // Remove from the tree.
                _cells[cell.Item1].Remove(item);

                // Clean up: remove the tree if it's empty.
                if (_cells[cell.Item1].Count == 0)
                {
                    _cells.Remove(cell.Item1);
                }
            }

            // Forget bounds for this item, thus removing it from the index.
            _entryBounds.Remove(item);

            return true;
        }

        /// <summary>Test whether this index contains the specified item.</summary>
        /// <param name="item">The item to check.</param>
        /// <returns>
        ///     <c>true</c> if the index contains the item; <c>false</c> otherwise.
        /// </returns>
        public bool Contains(T item)
        {
            return _entryBounds.ContainsKey(item);
        }

        /// <summary>Removes all items from the index.</summary>
        public void Clear()
        {
            _cells.Clear();
            _entryBounds.Clear();
        }

        /// <summary>Get the bounds at which the specified item is currently stored.</summary>
        public TRectangle this[T item]
        {
            get { return _entryBounds[item]; }
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
            // Compute the area bounds for that query to get the involved trees.
            var queryBounds = IntersectionExtensions.BoundsFor(center, radius);
            foreach (var cell in ComputeCells(queryBounds))
            {
                // Only if the cell exists.
                if (_cells.ContainsKey(cell.Item1))
                {
                    // Convert the query to the tree's local coordinate space.
// ReSharper disable RedundantCast Necessary for FarCollections.
                    var relativePoint = (Vector2) (center + cell.Item2);
// ReSharper restore RedundantCast

                    // And do the query.
                    _cells[cell.Item1].Find(relativePoint, radius, results);
                }
            }
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
            // Getting the full list and then iterating it seems to actually be faster
            // than injecting a delegate...
            /*

            // HashSet we might use for filtering duplicate results. We initialize it lazily.
            HashSet<T> filter = null;

            // Compute the area bounds for that query to get the involved trees.
            var queryBounds = IntersectionExtensions.BoundsFor(ref center, radius);
            foreach (var cell in ComputeCells(queryBounds))
            {
                // Only if the cell exists.
                if (_entries.ContainsKey(cell.Item1))
                {
                    // Convert the query to the tree's local coordinate space.
                    var relativePoint = (Vector2)(center + cell.Item2);

                    // And do the query.
                    if (!_entries[cell.Item1].Find(relativePoint, radius,
                        value => !Filter(value, ref filter) || callback(value)))
                    {
                        return false;
                    }
                }
            }

            /*/

            ISet<T> results = new HashSet<T>();
            Find(center, radius, results);
            foreach (var result in results)
            {
                if (!callback(result))
                {
                    return false;
                }
            }

            //*/

            return true;
        }

        /// <summary>
        ///     Perform an area query on this index. This will return all entries in the tree that are contained in or
        ///     intersecting with the specified query rectangle.
        /// </summary>
        /// <param name="rectangle">The query rectangle.</param>
        /// <param name="results">The results.</param>
        public void Find(TRectangle rectangle, ISet<T> results)
        {
            foreach (var cell in ComputeCells(rectangle))
            {
                if (_cells.ContainsKey(cell.Item1))
                {
                    // Convert the query to the tree's local coordinate space.
                    var relativeFarBounds = rectangle;
                    relativeFarBounds.Offset(cell.Item2);

                    // And do the query.
// ReSharper disable RedundantCast Necessary for FarCollections.
                    var relativeBounds = (Math.RectangleF) relativeFarBounds;
// ReSharper restore RedundantCast
                    _cells[cell.Item1].Find(relativeBounds, results);
                }
            }
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
            // Getting the full list and then iterating it seems to actually be faster
            // than injecting a delegate...
            /*

            // HashSet we might use for filtering duplicate results. We initialize it lazily.
            HashSet<T> filter = null;

            foreach (var cell in ComputeCells(rectangle))
            {
                if (_entries.ContainsKey(cell.Item1))
                {
                    // Convert the query bounds to the tree's local coordinate space.
                    var relativeFarBounds = rectangle;
                    relativeFarBounds.Offset(cell.Item2);

                    // And do the query.
                    var relativeBounds = (Math.RectangleF)relativeFarBounds;
                    if (!_entries[cell.Item1].Find(relativeBounds,
                        value => !Filter(value, ref filter) || callback(value)))
                    {
                        return false;
                    }
                }
            }

            /*/

            ISet<T> results = new HashSet<T>();
            Find(rectangle, results);
            foreach (var result in results)
            {
                if (!callback(result))
                {
                    return false;
                }
            }

            //*/

            return true;
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
            foreach (var cell in ComputeCells(IntersectionExtensions.BoundsFor(start, end, t)))
            {
                if (_cells.ContainsKey(cell.Item1))
                {
                    // Convert the query to the tree's local coordinate space.
// ReSharper disable RedundantCast Necessary for FarCollections.
                    var relativeStart = (Vector2) (start + cell.Item2);
                    var relativeEnd = (Vector2) (end + cell.Item2);
// ReSharper restore RedundantCast

                    // And do the query.
                    _cells[cell.Item1].Find(relativeStart, relativeEnd, t, results);
                }
            }
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
            // HashSet we might use for filtering duplicate results. We initialize it lazily.
            HashSet<T> filter = null;

            foreach (var cell in ComputeCells(IntersectionExtensions.BoundsFor(start, end, t)))
            {
                if (_cells.ContainsKey(cell.Item1))
                {
                    // Convert the query to the tree's local coordinate space.
// ReSharper disable RedundantCast Necessary for FarCollections.
                    var relativeStart = (Vector2) (start + cell.Item2);
                    var relativeEnd = (Vector2) (end + cell.Item2);
// ReSharper restore RedundantCast

                    // And do the query.
                    if (!_cells[cell.Item1].Find(
                        relativeStart,
                        relativeEnd,
                        t,
                        (value, fraction) =>
                        {
                            if (!Filter(value, ref filter))
                            {
                                return -1f;
                            }
                            return t = callback(value, fraction);
                        }))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Utility methods

        /// <summary>
        ///     Checks if the specified value need filtering, returns true if the value should be processed, otherwise (if it
        ///     is filtered) it should be skipped.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="filter">The filter set.</param>
        /// <returns></returns>
        private bool Filter(T value, ref HashSet<T> filter)
        {
            // We want to check if we really need to check this value -- this is only
            // necessary if it's on the border between two or more cells. This way we
            // can keep our hashset as small as possible, making it much faster.
#if FALSE && FARMATH // Only works when automatically normalizing.
            var left = _entryBounds[value].X.Segment;
            var right = _entryBounds[value].Right.Segment;
            var top = _entryBounds[value].Y.Segment;
            var bottom = _entryBounds[value].Bottom.Segment;
#elif FARMATH // TODO make sure this works
            var left = _entryBounds[value].X.Segment + (int)(_entryBounds[value].X.Offset / FarValue.SegmentSize);
            var right = _entryBounds[value].Right.Segment + (int)(_entryBounds[value].Right.Offset / FarValue.SegmentSize);
            var top = _entryBounds[value].Y.Segment + (int)(_entryBounds[value].Y.Offset / FarValue.SegmentSize);
            var bottom = _entryBounds[value].Bottom.Segment + (int)(_entryBounds[value].Bottom.Offset / FarValue.SegmentSize);
#else
            var left = (int) (_entryBounds[value].X / CellSize);
            var right = (int) (_entryBounds[value].Right / CellSize);
            var top = (int) (_entryBounds[value].Y / CellSize);
            var bottom = (int) (_entryBounds[value].Bottom / CellSize);
#endif
            // All corners in one cell?
            if (left == right && top == bottom)
            {
                // Yes, we can safely process the cell and do not need to store it.
                return true;
            }

            // We need to filter, create our hashset if necessary, then store the value.
            if (filter == null)
            {
                filter = new HashSet<T>();
            }
            return filter.Add(value);
        }

        /// <summary>Computes the cells the specified rectangle falls into.</summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <returns>The cells the rectangle intersects with.</returns>
        private static IEnumerable<Tuple<ulong, TPoint>> ComputeCells(TRectangle rectangle)
        {
#if FALSE && FARMATH // Only works when automatically normalizing.
            var left = rectangle.X.Segment;
            var right = rectangle.Right.Segment;
            var top = rectangle.Y.Segment;
            var bottom = rectangle.Bottom.Segment;
#elif FARMATH // TODO make sure this works
            var left = rectangle.X.Segment + (int)(rectangle.X.Offset / FarValue.SegmentSizeHalf);
            var right = rectangle.Right.Segment + (int)(rectangle.Right.Offset / FarValue.SegmentSizeHalf);
            var top = rectangle.Y.Segment + (int)(rectangle.Y.Offset / FarValue.SegmentSizeHalf);
            var bottom = rectangle.Bottom.Segment + (int)(rectangle.Bottom.Offset / FarValue.SegmentSizeHalf);
#else
            var left = (int) (rectangle.X / CellSize);
            var right = (int) (rectangle.Right / CellSize);
            var top = (int) (rectangle.Y / CellSize);
            var bottom = (int) (rectangle.Bottom / CellSize);
#endif

            TPoint center;
            for (var x = left; x <= right; x++)
            {
                center.X = -x * CellSize;
                for (var y = top; y <= bottom; y++)
                {
                    center.Y = -y * CellSize;
                    yield return Tuple.Create(BitwiseMagic.Pack(x, y), center);
                }
            }
        }

        #endregion

        #region Implementation of IEnumerable

        /// <summary>Returns an enumerator that iterates through the collection.</summary>
        /// <returns>
        ///     A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<Tuple<TRectangle, T>> GetEnumerator()
        {
            foreach (var entry in _entryBounds)
            {
                yield return Tuple.Create(entry.Value, entry.Key);
            }
        }

        /// <summary>Returns an enumerator that iterates through a collection.</summary>
        /// <returns>
        ///     An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        ///     A utility enumerator allowing the iteration over all trees in the index. This also yields the position for
        ///     each tree.
        /// </summary>
        /// <returns>An enumerator over all trees in the index.</returns>
        /// <remarks>This is mainly intended for debugging purposes, to allow rendering the tree bounds.</remarks>
        public IEnumerable<Tuple<TPoint, Collections.DynamicQuadTree<T>>> GetTreeEnumerable()
        {
            foreach (var entry in _cells)
            {
                int segmentX, segmentY;
                BitwiseMagic.Unpack(entry.Key, out segmentX, out segmentY);

                TPoint center;
                center.X = segmentX * CellSize;
                center.Y = segmentY * CellSize;
                yield return Tuple.Create(center, entry.Value);
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

            packet.Write(_cells.Count);
            foreach (var entry in _cells)
            {
                packet.Write(entry.Key);
                packet.Write(entry.Value);
            }

            packet.Write(_entryBounds.Count);
            foreach (var entry in _entryBounds)
            {
                _packetizer(packet, entry.Key);
                packet.Write(entry.Value);
            }

            return packet;
        }

        /// <summary>
        ///     Bring the object to the state in the given packet. This is called after automatic depacketization has been
        ///     performed.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        [OnPostDepacketize]
        public void PostDepacketize(IReadablePacket packet)
        {
            if (_depacketizer == null)
            {
                throw new InvalidOperationException("No deserializer specified.");
            }

            _cells.Clear();
            var cellCount = packet.ReadInt32();
            for (var i = 0; i < cellCount; ++i)
            {
                var cellId = packet.ReadUInt64();
                var tree = new Collections.DynamicQuadTree<T>(
                    _maxEntriesPerNode,
                    _minNodeBounds,
                    _boundExtension,
                    _movingBoundMultiplier,
                    _packetizer,
                    _depacketizer);
                packet.ReadPacketizableInto(tree);
                _cells.Add(cellId, tree);
            }

            _entryBounds.Clear();
            var entryCount = packet.ReadInt32();
            for (var i = 0; i < entryCount; ++i)
            {
                var key = _depacketizer(packet);
                TRectangle value;
                packet.Read(out value);
                _entryBounds[key] = value;
            }
        }

        [OnStringify]
        public StreamWriter Dump(StreamWriter w, int indent)
        {
            w.AppendIndent(indent).Write("CellCount = ");
            w.Write(_cells.Count);
            w.AppendIndent(indent).Write("Cells = {");
            foreach (var entry in _cells)
            {
                w.AppendIndent(indent + 1).Write(entry.Key);
                w.Write(" = ");
                w.Dump(entry.Value, indent + 1);
            }
            w.AppendIndent(indent).Write("}");

            w.AppendIndent(indent).Write("EntryCount = ");
            w.Write(_entryBounds.Count);
            w.AppendIndent(indent).Write("Entries = {");
            foreach (var entry in _entryBounds)
            {
                w.AppendIndent(indent + 1).Write(entry.Key);
                w.Write(" = ");
                w.Dump(entry.Value, indent + 1);
            }
            w.AppendIndent(indent).Write("}");

            return w;
        }

        #endregion

        #region Copying

        /// <summary>Creates a new copy of the object, that shares no mutable references with this instance.</summary>
        /// <returns>The copy.</returns>
        public SpatialHashedQuadTree<T> NewInstance()
        {
            return new SpatialHashedQuadTree<T>(
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
        public void CopyInto(SpatialHashedQuadTree<T> into)
        {
            Copyable.CopyInto(this, into);

            into._cells.Clear();
            foreach (var entry in _cells)
            {
                var treeCopy = entry.Value.NewInstance();
                entry.Value.CopyInto(treeCopy);
                into._cells.Add(entry.Key, treeCopy);
            }

            into._entryBounds.Clear();
            foreach (var bound in _entryBounds)
            {
                into._entryBounds.Add(bound.Key, bound.Value);
            }
        }

        #endregion
    }
}