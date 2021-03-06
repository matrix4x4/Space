﻿using System.Collections.Generic;
using Engine.Collections;
using Engine.ComponentSystem.Physics.Collision;
using Engine.ComponentSystem.Physics.Contacts;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

#if FARMATH
using WorldPoint = Engine.FarMath.FarPosition;
#else
using WorldPoint = Microsoft.Xna.Framework.Vector2;
#endif

namespace Engine.ComponentSystem.Physics.Messages
{
    /// <summary>Called for each active contact before the solver runs.</summary>
    public struct PreSolve
    {
        /// <summary>The contact for which this message was sent.</summary>
        public Contact Contact;

        /// <summary>The old manifold state.</summary>
        internal Manifold OldManifold;

        /// <summary>Computes the world manifold data for this contact. This is relatively expensive, so use with care.</summary>
        /// <param name="normal">The world contact normal.</param>
        /// <param name="points">The contact points.</param>
        [PublicAPI]
        public void ComputeOldWorldManifold(out Vector2 normal, out IList<WorldPoint> points)
        {
            if (OldManifold.PointCount < 1)
            {
                normal = Vector2.Zero;
                points = new FixedArray2<WorldPoint>();
                return;
            }

            var bodyA = Contact.FixtureA.Body;
            var bodyB = Contact.FixtureB.Body;
            var transformA = bodyA.Transform;
            var transformB = bodyB.Transform;
            var radiusA = Contact.FixtureA.Radius;
            var radiusB = Contact.FixtureB.Radius;

            FixedArray2<WorldPoint> worldPoints;
            OldManifold.ComputeWorldManifold(
                transformA,
                radiusA,
                transformB,
                radiusB,
                out normal,
                out worldPoints);
            worldPoints.Count = OldManifold.PointCount;
            points = worldPoints;
        }
    }
}