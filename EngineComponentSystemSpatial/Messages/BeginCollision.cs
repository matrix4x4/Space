﻿using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Spatial.Messages
{
    /// <summary>Used to indicate a collision occurred.</summary>
    public struct BeginCollision
    {
        /// <summary>A unique ID for a contact, which allows associating begin and end events.</summary>
        public int ContactId;

        /// <summary>The first entity that was involved in the collision.</summary>
        public int EntityA;

        /// <summary>The second entity that was involved in the collision.</summary>
        public int EntityB;

        /// <summary>The normal giving the direction pointing from EntityA to EntityB at the time the collision occurred.</summary>
        public Vector2 Normal;
    }
}