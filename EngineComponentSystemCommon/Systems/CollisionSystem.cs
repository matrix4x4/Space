﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Engine.ComponentSystem.Common.Components;
using Engine.ComponentSystem.Common.Messages;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Systems;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Common.Systems
{
    /// <summary>
    /// This system takes care of components that support collision (anything
    /// that extends <c>AbstractCollidable</c>). It fetches the components
    /// neighbors and checks their collision groups, keeping the number of
    /// actual collision checks that have to be performed low.
    /// </summary>
    public sealed class CollisionSystem : AbstractParallelComponentSystem<Collidable>, IMessagingSystem
    {
        #region Constants

        /// <summary>
        /// Start using indexes after the collision index.
        /// </summary>
        public static readonly ulong IndexGroupMask = 1ul << IndexSystem.GetGroup();

        #endregion

        #region Logic

        /// <summary>
        /// Updates the component by checking for possible collisions.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="component">The component.</param>
        protected override void UpdateComponent(long frame, Collidable component)
        {
            // Reset flag.
            SetCollisionState(component, Color.Green);

            // Get index and allocate neighbor result list.
            var index = (IndexSystem)Manager.GetSystem(IndexSystem.TypeId);
            ISet<int> neighbors = new HashSet<int>();

            // Get the component's bounds and look for nearby elements.
            var bounds = component.ComputeBounds();
            var translation = ((Transform)Manager.GetComponent(component.Entity, Transform.TypeId)).Translation;
            bounds.X = (int)translation.X - bounds.Width / 2;
            bounds.Y = (int)translation.Y - bounds.Height / 2;
            // TODO this is a hacky workaround to get entities that are travelling at high speeds to properly check for collisions with stuff they passed through
            bounds.Inflate(50, 50);
            index.Find(ref bounds, ref neighbors, IndexGroupMask);

            // If there are no neighbors, skip the rest.
            if (neighbors.Count <= 0)
            {
                return;
            }

            // Prepare the collision message.
            Collision message;
            message.FirstEntity = component.Entity;

            // Check each neighbor.
            foreach (var neighbor in neighbors)
            {
                var otherComponent = (Collidable)Manager.GetComponent(neighbor, Collidable.TypeId);

                // Skip self.
                if (otherComponent.Id == component.Id)
                {
                    continue;
                }

                // Skip disabled components.
                if (!otherComponent.Enabled)
                {
                    continue;
                }

                // Flag it as having neighbors.
                SetCollisionState(component, Color.Blue);

                // Only test if its from a different collision group.
                if ((component.CollisionGroups & otherComponent.CollisionGroups) != 0)
                {
                    continue;
                }

                // Flag it as having collidable neighbors.
                SetCollisionState(component, Color.Yellow);

                // Test for collision.
                if (!component.Intersects(otherComponent))
                {
                    continue;
                }

                // Flag it as intersecting.
                SetCollisionState(component, Color.DarkRed);

                // If there is one, let both parties know.
                message.SecondEntity = otherComponent.Entity;
                Manager.SendMessage(ref message);
            }
        }

        /// <summary>
        /// Only make the effort to set this when in debug mode.
        /// </summary>
        [Conditional("DEBUG")]
        private static void SetCollisionState(Collidable component, Color color)
        {
            component.CollisionState = color;
        }

        /// <summary>
        /// Update the previous position to the current one when adding a component.
        /// </summary>
        /// <param name="component">The added component.</param>
        public override void OnComponentAdded(Component component)
        {
            base.OnComponentAdded(component);

            if (component is Collidable)
            {
                var transform = ((Transform)Manager.GetComponent(component.Entity, Transform.TypeId));
                if (transform != null)
                {
                    ((Collidable)component).PreviousPosition = transform.Translation;
                }
            }
        }

        /// <summary>
        /// Update the previous position when a collidable component changes its position.
        /// </summary>
        /// <param name="message">The sent message.</param>
        public void Receive<T>(ref T message) where T : struct
        {
            if (message is TranslationChanged)
            {
                var typedMessage = (TranslationChanged)(ValueType)message;

                var collidable = ((Collidable)Manager.GetComponent(typedMessage.Entity, Collidable.TypeId));
                if (collidable!= null)
                {
                    collidable.PreviousPosition = typedMessage.PreviousPosition;
                }
            }
        }

        #endregion
    }
}
