﻿using System.Collections.Generic;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Components.Messages;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Systems
{
    /// <summary>
    /// This system takes care of components that support collision (anything
    /// that extends <c>AbstractCollidable</c>). It fetches the components
    /// neighbors and checks their collision groups, keeping the number of
    /// actual collision checks that have to be performed low.
    /// </summary>
    public sealed class CollisionSystem : AbstractComponentSystem<Collidable>
    {
        #region Constants

        /// <summary>
        /// Start using indexes after the collision index.
        /// </summary>
        public static readonly byte FirstIndexGroup = IndexSystem.GetGroups(32);

        /// <summary>
        /// The mask covering all indexes that are used by the collision system.
        /// </summary>
        public static readonly ulong CollisionIndexGroupMask = 0xFFFFFFFFul << FirstIndexGroup;

        #endregion

        #region Fields

        /// <summary>
        /// The maximum radius any object ever used in this system can have.
        /// </summary>
        private readonly int _maxCollidableRadius;

        #endregion

        #region Single-Allocation

        /// <summary>
        /// Reused for iterating components.
        /// </summary>
        private HashSet<int> _reusableNeighborList = new HashSet<int>();

        #endregion

        #region Constructor

        public CollisionSystem(int maxCollidableRadius)
        {
            // Use a range a little larger than the max collidable size, to
            // account for fast moving objects (sweep test).
            _maxCollidableRadius = maxCollidableRadius * 3;
        }

        #endregion

        #region Logic

        /// <summary>
        /// Does a normal update but clears the list of performed checks
        /// afterwards..
        /// </summary>
        /// <param name="gameTime">The game time.</param>
        /// <param name="frame">The frame.</param>
        public override void Update(GameTime gameTime, long frame)
        {
            var index = Manager.GetSystem<IndexSystem>();
            ICollection<int> neighbors = _reusableNeighborList;

            _updatingComponents.AddRange(_components);
            for (var i = 0; i < _updatingComponents.Count; ++i)
            {
                var component1 = _updatingComponents[i];
                if (!component1.Enabled)
                {
                    continue;
                }
                var bounds = component1.ComputeBounds();
                var translation = Manager.GetComponent<Transform>(component1.Entity).Translation;
                bounds.X = (int)translation.X - bounds.Width / 2;
                bounds.Y = (int)translation.Y - bounds.Height / 2;
                index.Find(ref bounds, ref neighbors, CollisionIndexGroupMask & ((ulong)(~component1.CollisionGroups) << FirstIndexGroup));
                for (var j = i + 1; j < _updatingComponents.Count; ++j)
                {
                    var component2 = _updatingComponents[j];
                    if (!component2.Enabled)
                    {
                        continue;
                    }
                    if (neighbors.Contains(component2.Entity))
                    {
                        TestCollision(component1, component2);
                    }
                }
                _reusableNeighborList.Clear();
            }
            _updatingComponents.Clear();
        }

        //protected override void UpdateComponent(GameTime gameTime, long frame, Collidable component)
        //{
        //    var index = Manager.GetSystem<IndexSystem>();

        //    // Get a list of components actually nearby.
        //    // Use the inverse of the collision group, i.e. get
        //    // entries from all those entries where we're not in
        //    // that group.
        //    ICollection<int> neighbors = _reusableNeighborList;
        //    var bounds = component.ComputeBounds();
        //    var translation = Manager.GetComponent<Transform>(component.Entity).Translation;
        //    bounds.X = (int)translation.X - bounds.Width / 2;
        //    bounds.Y = (int)translation.Y - bounds.Height / 2;
        //    index.Find(ref bounds, ref neighbors, (ulong)(~component.CollisionGroups) << FirstIndexGroup);
        //    foreach (var neighbor in neighbors)
        //    {
        //        Debug.Assert(neighbor != component.Entity);

        //        // Test if we really need to do this check, or if it has been
        //        // handled before. We do this by keeping track of the paired
        //        // entity ids (packed together in one ulong).
        //        var permutation = CoordinateIds.Combine(neighbor, component.Entity);

        //        // We store tuples of (a, b), where a is an entity we already
        //        // ran through. So what we need to look for are tuples that
        //        // are actually (b, a), because b will be then be an entity
        //        // that has *potentially* already been checked.
        //        if (_performedChecks.Contains(permutation))
        //        {
        //            // Already did that check, skip it.
        //            continue;
        //        }

        //        // Not checked yet, push this permutation.
        //        _performedChecks.Add(CoordinateIds.Combine(component.Entity, neighbor));

        //        // Then do the actual work.
        //        TestCollision(component, Manager.GetComponent<Collidable>(neighbor));

        //        // Stop if the component was invalidated.
        //        if (!component.Enabled)
        //        {
        //            // Clear the list for the next iteration (and after the
        //            // iteration so we don't keep references to stuff).
        //            _reusableNeighborList.Clear();
        //            return;
        //        }
        //    }

        //    // Clear the list for the next iteration (and after the
        //    // iteration so we don't keep references to stuff).
        //    _reusableNeighborList.Clear();

        //    // Update the components previous position for the next sweep test.
        //    component.PreviousPosition = Manager.GetComponent<Transform>(component.Entity).Translation;
        //}

        /// <summary>
        /// Performs a collision check between the two given collidable
        /// components.
        /// </summary>
        /// <param name="currentCollidable">The first object.</param>
        /// <param name="otherCollidable">The second object.</param>
        private void TestCollision(Collidable currentCollidable, Collidable otherCollidable)
        {
            if (
                // Skip disabled components.
                otherCollidable.Enabled &&
                // Only test if its from a different collision group.
                (currentCollidable.CollisionGroups & otherCollidable.CollisionGroups) == 0 &&
                // Test for collision, if there is one, let both parties know.
                currentCollidable.Intersects(otherCollidable))
            {
                Collision message;
                message.FirstEntity = currentCollidable.Entity;
                message.SecondEntity = otherCollidable.Entity;
                Manager.SendMessage(ref message);
            }
        }

        #endregion

        #region Copying

        public override AbstractSystem DeepCopy(AbstractSystem into)
        {
            var copy = (CollisionSystem)base.DeepCopy(into);

            if (copy != into)
            {
                copy._reusableNeighborList = new HashSet<int>();
            }

            return copy;
        }

        #endregion
    }
}
