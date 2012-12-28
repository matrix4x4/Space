﻿using Engine.Physics.Components;
using Engine.Physics.Systems;
using Engine.Random;
using Microsoft.Xna.Framework;

namespace EnginePhysicsTests.Tests
{
    sealed class AddPair : AbstractTest
    {
        protected override void Create()
        {
            var physics = Manager.GetSystem(PhysicsSystem.TypeId) as PhysicsSystem;
            System.Diagnostics.Debug.Assert(physics != null);
            physics.Gravity = Vector2.Zero;

            const float minX = -6.0f;
            const float maxX = 0.0f;
            const float minY = 4.0f;
            const float maxY = 6.0f;

            var random = new MersenneTwister(0);
            for (var i = 0; i < 400; ++i)
            {
                Manager.AddCircle(radius: 0.1f,
                                  type: Body.BodyType.Dynamic,
                                  worldPosition: new Vector2((float)random.NextDouble(minX, maxX),
                                                             (float)random.NextDouble(minY, maxY)),
                                  density: 0.01f);
            }

            {
                var box = Manager.AddRectangle(width: 3, height: 3,
                                               type: Body.BodyType.Dynamic,
                                               worldPosition: new Vector2(-40, 5),
                                               isBullet: true,
                                               density: 1);
                var body = Manager.GetComponent(box, Body.TypeId) as Body;
                System.Diagnostics.Debug.Assert(body != null);
                body.LinearVelocity = new Vector2(150, 0);
            }
        }
    }
}
