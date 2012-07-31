﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;

// Adjust these as necessary, they just have to share a compatible
// interface with the XNA types.
using TPoint = Microsoft.Xna.Framework.Vector2;
using TRectangle = Engine.Math.RectangleF;

namespace Engine.Collections
{
    public sealed class RTree<T> : IIndex<T>
    {
        public void Test(List<T> list)
        {
            var rect = new TRectangle(0, 0, 2, 2);
            Console.WriteLine(rect.X);
            Console.WriteLine(rect.Y);
            Console.WriteLine(rect.Center);
            Console.WriteLine(rect.Location);
            Add(new TPoint(0, 0), list[0]);
            Add(new TPoint(0, 2), list[1]);
            Add(new TPoint(2, 0), list[2]);
            Add(new TPoint(2, 2), list[3]);
            Add(new TPoint(1, 2), list[4]);
            Add(new TPoint(1, 3), list[4]);
            Add(new TPoint(1, 4), list[4]);
            Add(new TPoint(1, 5), list[4]);
            Add(new TPoint(1, 6), list[4]);
            Add(new TPoint(2, 3), list[4]);
            Add(new TPoint(3, 4), list[4]);
            Add(new TPoint(4, 5), list[4]);
            Add(new TPoint(5, 6), list[4]);

            //foreach(var asd in RangeQuery(new TRectangle(0,0,1,1)))
            //{
            //    Console.WriteLine(asd);
            //}
            Console.WriteLine(root);
        }
        #region Fields
        /// <summary>
        /// 
        /// </summary>
        private int maxEntrys = 128;

        private int minEntrys = 20;

        /// <summary>
        /// 
        /// </summary>
        Node root = new LeafNode();


        private Dictionary<T, LeafNode> _pointDict = new Dictionary<T, LeafNode>();
        #endregion

        #region Implementation of IEnumerable

        public IEnumerator<Tuple<TRectangle, T>> GetEnumerator()
        {
            foreach (var entry in _pointDict)
            {
                yield return Tuple.Create(TRectangle.Empty, entry.Key);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Implementation of IIndex<T>

        public int Count
        {
            get { return _pointDict.Count; }
        }

        /// <summary>
        /// Add a new entry to the tree, with the specified bounds, with the
        /// specified associated value.
        /// </summary>
        /// <param name="bounds">The bounds of the entry.</param>
        /// <param name="item">The value associated with the point.</param>
        /// <exception cref="ArgumentException">This value is already stored
        /// in the tree.</exception>
        public void Add(TRectangle bounds, T item)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update an entry by changing its bounds. If the item is not
        /// stored in the index, this will return <code>false</code>.
        /// </summary>
        /// <param name="newBounds">The new bounds of the item.</param>
        /// <param name="delta"> </param>
        /// <param name="item">The item for which to update the bounds.</param>
        /// <returns><c>true</c> if the update was successful; <c>false</c> otherwise.</returns>
        public bool Update(TRectangle newBounds, TPoint delta, T item)
        {
            throw new NotImplementedException();
        }

        public void Add(TPoint point, T item)
        {
            //the entry to be stored
            var entry = new Entry() { Point = point, Value = item };
            if (_pointDict.ContainsKey(item))
            {
                throw new ArgumentException("Entry is already in the tree at the specified point.", "item");
            }
            Insert(entry);
        }

        private void Insert(Entry entry)
        {
            //coose the leaf in which the value shall be stored
            var node = ChooseLeaf(ref entry.Point);
            //if the node has enough space to hold the entry
            LeafNode createdNode = null;
            if (TestCapacity(node))
            {
                node.Entrys.Add(entry);
                if (_pointDict.ContainsKey(entry.Value))
                {
                    _pointDict.Remove(entry.Value);
                }
                _pointDict.Add(entry.Value, node);
            }
            else
            {
                node.Entrys.Add(entry);
                if (_pointDict.ContainsKey(entry.Value))
                {
                    _pointDict.Remove(entry.Value);
                }
                _pointDict.Add(entry.Value, node);
                createdNode = (LeafNode)SplitNode(node);
            }
            AdjustTree(node, createdNode);
        }
        /// <summary>
        /// Inserts the given node in the tree at the given father node level
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parentLevel"></param>
        private void Insert(Node childnode, int parentLevel)
        {
            //coose the leaf in which the value shall be stored
            var node = ChooseNode(childnode,parentLevel);
            //if the node has enough space to hold the entry
            InnerNode createdNode = null;
            if (node.Nodes.Count<maxEntrys)
            {
                node.Nodes.Add(childnode);
            }
            else
            {
                node.Nodes.Add(childnode);
                
                createdNode = (InnerNode)SplitNode(node);
            }
            AdjustTree(node, createdNode);
        }

        public bool Update(TPoint newPoint, T item)
        {
            LeafNode node = null;
            //check if node exists
            var found = _pointDict.TryGetValue(item,out node);
            if (!found)
                throw new ArgumentException("Value not in Tree");
            
            //still in old parent
            if (node.Boundingbox.Contains((int)newPoint.X, (int)newPoint.Y))
            {
                var entry = node.getEntry(item);
                entry.Point = newPoint;
                AdjustTree(node);
                return true;
            }
            // TODO implement!
            //throw new NotImplementedException();
            return false;
        }

        public bool Remove(T item)
        {
            if (!_pointDict.ContainsKey(item))
            {
                return false;
            }
            var node = _pointDict[item];
            _pointDict.Remove(item);
            foreach (var entry in node.Entrys)
            {
                //find entry in node
                if(entry.Value.Equals(item))
                {
                    node.Entrys.Remove(entry);
                    CondenseTree(node);
                    return true;
                }
            }
            return false;
        }

        public bool Contains(T item)
        {
            return _pointDict.ContainsKey(item);
        }

        public void Clear()
        {
            _pointDict = new Dictionary<T, LeafNode>();
            root = new LeafNode();
        }

        /// <summary>
        /// Get the bounds at which the specified item is currently stored.
        /// </summary>
        public TRectangle this[T item] { get { return _pointDict[item].Boundingbox; } }

        public void Find(TPoint point, float range, ref ICollection<T> list)
        {
            Accumulate(root, ref point, range, list);
        }

        public void Find(ref TRectangle rectangle, ref ICollection<T> list)
        {
            Accumulate(root, ref rectangle, list);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="rectangle"></param>
        /// <param name="list"></param>
        private void Accumulate(Node currentNode,ref TRectangle rectangle,ICollection<T> list)
        {
            if (currentNode is LeafNode)
            {
                var leafnode = (LeafNode)currentNode;
                foreach (var entry in leafnode.Entrys)
                {
                    if (RectangleContainsPoint(ref rectangle, ref entry.Point))
                    {
                        list.Add(entry.Value);
                    }
                }
            }
            else//innernode
            {
                foreach (var child in ((InnerNode)currentNode).Nodes)
                {
                    //if searched rectangle is inside mbb search in there
                    if (child.Boundingbox.Intersects(rectangle))
                    {
                        Accumulate(child, ref rectangle, list);
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="rectangle"></param>
        /// <param name="list"></param>
        private void Accumulate(Node currentNode, ref TPoint point, float range, ICollection<T> list)
        {
            if (currentNode is LeafNode)
            {
                var leafnode = (LeafNode)currentNode;
                foreach (var entry in leafnode.Entrys)
                {
                    if (ComputeIntersection(ref point,range, ref entry.Point))
                    {
                        list.Add(entry.Value);
                    }
                }
            }
            else//innernode
            {
                foreach (var child in ((InnerNode)currentNode).Nodes)
                {
                    //if searched rectangle is inside mbb search in there
                    if (ComputeIntersection(ref point,range,ref child.Boundingbox))
                    {
                        Accumulate(child, ref point,range,list);
                    }
                }
            }
        }

        
        /// <summary>
        /// Choose the Leaf in which the point fits best
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private LeafNode ChooseLeaf(ref TPoint point)
        {
            Node node = root;
            while (!(node is LeafNode))
            {
                node = MMBMinNode((InnerNode)node, ref point);
            }
            return (LeafNode)node;
        }

        private InnerNode ChooseNode(Node node, int parentLevel)
        {
            
            var testnode = (InnerNode)root;
            while (testnode.Level!=parentLevel)
            {
                testnode = MMBMinNode(testnode, node.Boundingbox);
            }
            return testnode;
        }

        private InnerNode MMBMinNode(InnerNode testnode, TRectangle boundingbox)
        {
            float minexpansion = float.PositiveInfinity;
            Node returnNode= null;
            foreach (var childnode in testnode.Nodes)
            {
                var mbb = childnode.Boundingbox;
                var area = TRectangle.Union(boundingbox, mbb);
                var size = area.Height * area.Width;
                if (size < minexpansion)
                {
                    minexpansion = size;
                    returnNode = childnode;
                }
            }
            return (InnerNode)returnNode;
        }
        /// <summary>
        /// Returns the Child node of the given node with the minimal expansion to contain the given point
        /// </summary>
        /// <param name="node"></param>
        /// <param name="point"> </param>
        /// <returns></returns>
        private Node MMBMinNode(InnerNode node, ref TPoint point)
        {
            var minExpanision = float.PositiveInfinity;
            var minArea = float.PositiveInfinity;
            Node returnNode = null;
            foreach (var childNode in node.Nodes)
            {
                var box = childNode.Boundingbox;
                //check if point is already in bounding box
                if (box.Contains((int)point.X, (int)point.Y))
                {
                    var area = box.Width * box.Height;
                    //check if area is smaller
                    if (minExpanision > 0 || minExpanision == 0 && area < minArea)
                    {
                        minArea = area;
                        minExpanision = 0;
                        returnNode = childNode;

                    }


                }
                //no node would contain the point
                else if (minExpanision > 0)
                {
                    var x = point.X;
                    var y = point.Y;
                    var distX = 0f;
                    var distY = 0f;
                    var boxWidht = box.Width;
                    var boxheigth = box.Height;
                    if (box.Left > x)
                    {
                        distX = box.Left - x;
                    }
                    else if (box.Right < x)
                    {
                        distX = x - box.Right;
                    }
                    if (box.Top > y)
                    {
                        distY = box.Top - y;
                    }
                    else if (box.Bottom < y)
                    {
                        distY = y - box.Bottom;
                    }
                    distX = System.Math.Abs(distX);
                    distY = System.Math.Abs(distY);
                    var increase = distX * boxheigth + distX * distY + distY * boxWidht;
                    if (increase == minExpanision)
                    {
                        var area = boxWidht * boxheigth + increase;
                        if (area < minArea)
                        {
                            minArea = area;
                            returnNode = childNode;
                        }
                    }
                    else if (increase < minExpanision)
                    {
                        var area = boxWidht * boxheigth + increase;
                        minArea = area;
                        returnNode = childNode;

                    }
                }

            }
            return returnNode;
        }

        /// <summary>
        /// Calculates the Area of an Rectangle
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        private float RectangleArea(TRectangle rect)
        {
            return rect.Width * rect.Height;
        }

        /// <summary>
        /// Returns the Sort order of two tuples by calculating the lower value of the Interesection Rectangle Area
        /// </summary>
        /// <param name="tuple1"></param>
        /// <param name="tuple2"></param>
        /// <returns></returns>
        private int SortTupleListByOverlapValue(Tuple<TRectangle, TRectangle,int> tuple1, Tuple<TRectangle, TRectangle,int> tuple2)
        {
            return (int)System.Math.Round(RectangleArea(TRectangle.Intersect(tuple1.Item1, tuple1.Item2)) -
                   RectangleArea(TRectangle.Intersect(tuple2.Item1, tuple2.Item2)), MidpointRounding.AwayFromZero);
        }
        /// <summary>
        /// Splits the given Node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private Node SplitNode(Node node)
        {

            var list = (node is LeafNode) ? ChosseSplitAxis((LeafNode)node) : ChosseSplitAxis((InnerNode)node);
            if (list.Count == 0)
                return null;
            list.Sort(SortTupleListByOverlapValue);
            var smalest = list[0].Item3;
            if (node is LeafNode)
            {
                var leafnode = (LeafNode) node;
                var newNode = new LeafNode();
                //note to split is root so create new root node
                if (node == root)
                {
                    var rootNode = new InnerNode(1);
                    root = rootNode;
                    node.ParentNode = rootNode;
                    rootNode.Nodes.Add(node);
                }

                newNode.ParentNode = node.ParentNode;
                node.ParentNode.Nodes.Add(newNode);
                var max = leafnode.Entrys.Count;
                for (int i = max-1; i >= smalest; i--)
                {
                    var entry = leafnode.Entrys[i];
                    leafnode.Entrys.Remove(entry);
                    _pointDict.Remove(entry.Value);
                    newNode.Entrys.Add(entry);
                    _pointDict.Add(entry.Value, newNode);
                }
                return newNode;
            }
            //else innernode
            var innernode = (InnerNode) node;
            var newInnerNode = new InnerNode(innernode.Level);
            //note to split is root so create new root node
            if (node == root)
            {
                var rootNode = new InnerNode(innernode.Level+1);
                root = rootNode;
                node.ParentNode = rootNode;
                rootNode.Nodes.Add(node);
            }
            newInnerNode.ParentNode = node.ParentNode;
            node.ParentNode.Nodes.Add(newInnerNode);
            var maxCount = innernode.Nodes.Count;
            for (int i = maxCount - 1; i >= smalest; i--)
            {
                var entry = innernode.Nodes[i];
                innernode.Nodes.Remove(entry);
                newInnerNode.Nodes.Add(entry);
            }
            return newInnerNode;
        }

        /// <summary>
        /// Compares to entrys against each other compared is only the x value of the point
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static int CompareEntrysByXAxis(Entry x, Entry y)
        {
            var pointx = x.Point.X;
            var pointy = y.Point.X;
            return (int)(pointx - pointy);
        }

        /// <summary>
        /// Compares to entrys against each other compared is only the y value of the point
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static int CompareEntrysByYAxis(Entry x, Entry y)
        {
            var pointx = x.Point.Y;
            var pointy = y.Point.Y;
            return (int)(pointx - pointy);
        }

        /// <summary>
        /// Compares to Nodes against each other. Compared is only the x Axis
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static int CompareNodesByXAxis(Node x, Node y)
        {
            var xLeft = x.Boundingbox.Left;
            var xRight = x.Boundingbox.Right;
            var yLeft = y.Boundingbox.Left;
            var yRight = y.Boundingbox.Right;

            if (xLeft == yLeft)
            {
                return (int)System.Math.Round(xRight - yRight, MidpointRounding.AwayFromZero);
            }
            return (int)System.Math.Round(xLeft - yLeft, MidpointRounding.AwayFromZero);
        }


        /// <summary>
        /// Compares to Nodes against each other. Compared is only the ý Axis
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private static int CompareNodesByYAxis(Node x, Node y)
        {
            var xTop = x.Boundingbox.Top;
            var xBottom = x.Boundingbox.Bottom;
            var yTop = y.Boundingbox.Top;
            var yBottom = y.Boundingbox.Bottom;

            if (xTop == yTop)
            {
                return (int)System.Math.Round(xBottom - yBottom, MidpointRounding.AwayFromZero);
            }
            return (int)System.Math.Round(xTop - yTop, MidpointRounding.AwayFromZero);
        }
        /// <summary>
        /// Chooses the Split Axis of the Given Node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private List<Tuple<TRectangle, TRectangle, int>> ChosseSplitAxis(LeafNode node)
        {


            node.Entrys.Sort(CompareEntrysByXAxis);
            var sumX = 0f;
            var listx = new List<Tuple<TRectangle, TRectangle, int>>();
            for (var i = 1; i < maxEntrys - 1; i++)
            {
                var list = new List<Entry>();
                for (var j = 0; j <= i; j++)
                {
                    list.Add(node.Entrys[j]);
                }
                var box = CreateEntryBox(list);

                sumX += box.Height * 2 + box.Width * 2;
                list = new List<Entry>();
                for (var j = i+1; j < node.Entrys.Count; j++)
                {
                    list.Add(node.Entrys[j]);
                }
                var box2 = CreateEntryBox(list);
                listx.Add(new Tuple<TRectangle, TRectangle, int>(box, box2, i));
                sumX += box2.Height * 2 + box2.Width * 2;
            }
            node.Entrys.Sort(CompareEntrysByYAxis);
            var sumY = 0f;
            var listy = new List<Tuple<TRectangle, TRectangle, int>>();
            for (var i = 1; i < maxEntrys - 1; i++)
            {
                var list = new List<Entry>();
                for (var j = 0; j < i; j++)
                {
                    list.Add(node.Entrys[j]);
                }
                var box = CreateEntryBox(list);
                sumY += box.Height * 2 + box.Width * 2;
                list = new List<Entry>();
                for (var j = i + 1; j < node.Entrys.Count; j++)
                {
                    list.Add(node.Entrys[j]);
                }
                var box2 = CreateEntryBox(list);
                listy.Add(new Tuple<TRectangle, TRectangle, int>(box, box2, i));
                sumY += box2.Height * 2 + box2.Width * 2;
            }
            if(sumX < sumY)
            {
                //we have to sort this again
                node.Entrys.Sort(CompareEntrysByXAxis);
                return listx;
            }
            
            return listy;
        }


        /// <summary>
        /// Chooses the Split Axis and returns the Tuple of Rectangles on this Axis
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private List<Tuple<TRectangle, TRectangle,int>> ChosseSplitAxis(InnerNode node)
        {

            node.Nodes.Sort(CompareNodesByXAxis);
            var sumX = 0f;
            var listx = new List<Tuple<TRectangle, TRectangle,int>>();
            for (var i = 1; i < maxEntrys - 1; i++)
            {
                var list = new List<Node>();
                for (var j = 0; j < i; j++)
                {
                    list.Add(node.Nodes[j]);
                }
                var box = CreateNodeBox(list);

                sumX += box.Height * 2 + box.Width * 2;
                list = new List<Node>();
                for (var j = i; j < node.Nodes.Count; j++)
                {
                    list.Add(node.Nodes[j]);
                }
                var box2 = CreateNodeBox(list);
                listx.Add(new Tuple<TRectangle, TRectangle,int>(box, box2,i));
                sumX += box2.Height * 2 + box2.Width * 2;
            }
            node.Nodes.Sort(CompareNodesByYAxis);
            var sumY = 0f;
            var listy = new List<Tuple<TRectangle, TRectangle,int>>();
            for (var i = 1; i < maxEntrys - 1; i++)
            {
                var list = new List<Node>();
                for (var j = 0; j < i; j++)
                {
                    list.Add(node.Nodes[j]);
                }
                var box = CreateNodeBox(list);
                sumY += box.Height * 2 + box.Width * 2;
                list = new List<Node>();
                for (var j = i; j < node.Nodes.Count; j++)
                {
                    list.Add(node.Nodes[j]);
                }
                var box2 = CreateNodeBox(list);
                listy.Add(new Tuple<TRectangle, TRectangle, int>(box, box2,i));
                sumY += box2.Height * 2 + box2.Width * 2;
            }
            if (sumX < sumY)
            {
                //we have to sort this again
                node.Nodes.Sort(CompareNodesByXAxis);
                return listx;
            }

            return listy;
        }

        /// <summary>
        /// Adjusts the MBB and makes nodesplits if neccesary
        /// </summary>
        /// <param name="node"></param>
        /// <param name="createdNode"> </param>
        private void AdjustTree(Node node, Node createdNode = null)
        {
            Node testnode = node;
            while (testnode != root)
            {
                var changed = false;
                var parent = (InnerNode)testnode.ParentNode;
                changed = CalculateMBB(testnode);
                if (createdNode != null)
                {
                    changed |= CalculateMBB(createdNode);
                    if (parent.Nodes.Count > maxEntrys)
                    {
                        SplitNode(parent);
                        changed = true;
                    }
                }
                if (!changed)
                {
                    return;
                }

                testnode = parent;

            }
        }
        
        /// <summary>
        /// Returns the MBB for the given Leaf Node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private TRectangle CreateLeafNodeBox(LeafNode node)
        {
            return CreateEntryBox(node.Entrys);
        }

        /// <summary>
        /// Creates a MMB for the given List of entrys
        /// </summary>
        /// <param name="entrys"></param>
        /// <returns></returns>
        private TRectangle CreateEntryBox(List<Entry> entrys)
        {

            var xmin = int.MaxValue;
            var xmax = int.MinValue;
            var ymin = int.MaxValue;
            var ymax = int.MinValue;
            foreach (var entry in entrys)
            {
                var point = entry.Point;
                xmin = System.Math.Min(xmin, (int)point.X);
                xmax = System.Math.Max(xmax, (int)point.X);
                ymin = System.Math.Min(ymin, (int)point.Y);
                ymax = System.Math.Max(ymax, (int)point.Y);
            }
            var rectangle = new TRectangle(xmin,ymin,xmax-xmin,ymax-ymin);
            return rectangle;
        }

        /// <summary>
        /// Returns a MMB for the given Inner Node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private TRectangle CreateInnerNodeBox(InnerNode node)
        {
            return CreateNodeBox(node.Nodes);
        }

        /// <summary>
        /// Creates a MMB for the given List of Nodes
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private TRectangle CreateNodeBox(List<Node> nodes)
        {
            if (nodes.Count == 0)
                return TRectangle.Empty;
            var rect = nodes[0].Boundingbox;
            foreach (var child in nodes)
            {
                rect = TRectangle.Union(rect, child.Boundingbox);
            }
            return rect;
        }

        /// <summary>
        /// Calculates the new MBB for the given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool CalculateMBB(Node node)
        {
            var changed = false;
            if (node is LeafNode)
            {
                var rectangle = CreateLeafNodeBox((LeafNode)node);
                changed = rectangle != node.Boundingbox;
                node.Boundingbox = rectangle;

            }
            else if (node is InnerNode)
            {
                var rect = CreateInnerNodeBox((InnerNode)node);
                changed = rect != node.Boundingbox;
                node.Boundingbox = rect;
            }
            return changed;
        }
        private void CondenseTree(Node node)
        {
            var testnode = node;
            var deletedNodes = new List<InnerNode>();
            var deletedLeafs = new List<LeafNode>();
            while (testnode != root)
            {
                var parent = testnode.ParentNode;
                if (testnode is InnerNode && ((InnerNode)testnode).Nodes.Count < minEntrys)
                  {
                    parent.Nodes.Remove(testnode);
                    deletedNodes.Add((InnerNode)testnode);
                }
                else if (testnode is LeafNode && ((LeafNode)testnode).Entrys.Count < minEntrys)
                {
                    parent.Nodes.Remove(testnode);
                    deletedLeafs.Add((LeafNode)testnode);
                }
                else 
                {
                    CalculateMBB(testnode);
                }
                testnode = parent;
            }

            foreach (var nodes in deletedNodes)
            {
                foreach ( var entry in nodes.Nodes)
                {
                    Insert(entry,nodes.Level);
                }
            }
            foreach (var entrys in deletedLeafs)
            {
                
                foreach (var entry in entrys.Entrys)
                {
                    Insert(entry);
                }
                
            }
            if (root is InnerNode && ((InnerNode)root).Nodes.Count == 1)
            {
                root = ((InnerNode)root).Nodes[0];
            }
            
        }
        /// <summary>
        /// Tests if the tree can contain the given point.
        /// </summary>
        /// <param name="node">The Node to test for</param>
        private bool TestCapacity(LeafNode node)
        {
            return node.Entrys.Count < maxEntrys;
        }

        /// <summary>
        /// Circle / Box intersection test.
        /// </summary>
        /// <param name="center">The center of the circle.</param>
        /// <param name="radiusSquared">The squared radius of the circle.</param>
        /// <param name="x">The x position of the box.</param>
        /// <param name="y">The y position of the box.</param>
        /// <param name="size">The size of the box.</param>
        /// <returns>How the two intersect.</returns>
        private static bool ComputeIntersection(ref TPoint center, float radius,ref TRectangle rect)
        {
            // Check for axis aligned separation.
            if (rect.Right < center.X - radius ||
                rect.Bottom < center.Y - radius ||
                rect.Left > center.X + radius ||
                rect.Top > center.Y + radius)
            {
                return false;
            }

            // Check for unaligned separation.
            var closest = center;
            if (center.X < rect.Left)
            {
                closest.X = rect.Left;
            }
            else if (center.X > rect.Right)
            {
                closest.X = rect.Right;
            }
            if (center.Y < rect.Top)
            {
                closest.Y = rect.Top;
            }
            else if (center.Y > rect.Bottom)
            {
                closest.Y = rect.Bottom;
            }
            float distanceX = closest.X - center.X;
            float distanceY = closest.Y - center.Y;
            if ((distanceX * distanceX + distanceY * distanceY) > radius * radius)
            {
                return false;
            }
            return true;
        }
        private bool ComputeIntersection(ref TPoint center, float radius, ref TPoint vector2)
        {
            // Check for axis aligned separation.
            if (vector2.X < center.X - radius ||
                vector2.Y < center.Y - radius ||
                vector2.X > center.X + radius ||
                vector2.Y > center.Y + radius)
            {
                return false;
            }

            // Check for unaligned separation.
            var closest = center;
            if (center.X < vector2.X)
            {
                closest.X = vector2.X;
            }
            else if (center.X > vector2.X)
            {
                closest.X = vector2.X;
            }
            if (center.Y < vector2.Y)
            {
                closest.Y = vector2.Y;
            }
            else if (center.Y > vector2.Y)
            {
                closest.Y = vector2.Y;
            }
            float distanceX = closest.X - center.X;
            float distanceY = closest.Y - center.Y;
            if ((distanceX * distanceX + distanceY * distanceY) > radius * radius)
            {
                return false;
            }
            return true;
        }
        private bool RectangleContainsPoint(ref TRectangle rect, ref TPoint point)
        {
            return (rect.Top <= point.Y && rect.Bottom >= point.Y
                && rect.Left <= point.X && rect.Right >= point.X);
        }
        /// <summary>
        /// 
        /// </summary>
        private abstract class Node
        {
            #region Fields
            /// <summary>
            /// The Minimum Bounding Box 
            /// </summary>
            public TRectangle Boundingbox;

            /// <summary>
            /// The Parent Node
            /// </summary>
            public InnerNode ParentNode;

            #endregion
        }

        private class InnerNode : Node
        {
            #region Fields

            public int Level;
            public List<Node> Nodes = new List<Node>();

            public InnerNode(int level)
            {
                Level = level;
            }
            public override string ToString()
            {
                var returnstring = "Leaf Node childs: " + Nodes.Count + "\n";
                foreach (var entry in Nodes)
                {
                    returnstring += entry.ToString();
                }
                return returnstring;
            }
            #endregion
        }
        class LeafNode : Node
        {
            #region Fields

            public List<Entry> Entrys = new List<Entry>();
            public Entry getEntry(T value)
            {
                foreach (var entrytest in Entrys)
                {
                    //find entry in node
                    if (entrytest.Value.Equals(value))
                    {
                        return entrytest;
                    }
                    
                }
                return null;

            }
            public override string ToString()
            {
                var returnstring = "Leaf Node childs: " + Entrys.Count + "\n";
                foreach (var entry in Entrys)
                {
                    returnstring += entry.ToString();
                }
                return returnstring;
            }
            #endregion
        }

        /// <summary>
        /// A single entry in the tree, uniquely identified by its position
        /// and value.
        /// </summary>
        [DebuggerDisplay("Point = {Point}, Value = {Value}")]
        public class Entry
        {
            #region Fields

            /// <summary>
            /// The point at which the entry is stored.
            /// </summary>
            public TPoint Point;

            /// <summary>
            /// The value stored in this entry.
            /// </summary>
            public T Value;

            public override string ToString()
            {
                return "Entry: " + Point + "| " + Value + "\n";
            }
            #endregion

            #region Overrides

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }

                var e = obj as Entry;
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
    }
}
