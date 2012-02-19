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
    public class CollisionSystem : AbstractComponentSystem<Collidable>
    {
        #region Constants

        /// <summary>
        /// Start using indexes after the collision index.
        /// </summary>
        public static readonly byte FirstIndexGroup = IndexSystem.GetGroups(32);

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
        private List<int> _reusableNeighborList = new List<int>();
        
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

        protected override void UpdateComponent(GameTime gameTime, long frame, Collidable component)
        {
            var index = Manager.GetSystem<IndexSystem>();

            // Get a list of components actually nearby.
            // Use the inverse of the collision group, i.e. get
            // entries from all those entries where we're not in
            // that group.
            foreach (var neighbor in index.RangeQuery(
                component.Entity,
                _maxCollidableRadius,
                (ulong)(~component.CollisionGroups) << FirstIndexGroup,
                _reusableNeighborList))
            {
                TestCollision(component, Manager.GetComponent<Collidable>(neighbor));
            }

            // Clear the list for the next iteration (and after the
            // iteration so we don't keep references to stuff).
            _reusableNeighborList.Clear();

            // Update the components previous position for the next sweep test.
            component.PreviousPosition = Manager.GetComponent<Transform>(component.Entity).Translation;
        }

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
                copy._reusableNeighborList = new List<int>(_reusableNeighborList.Capacity);
            }

            return copy;
        }

        #endregion
    }
}
