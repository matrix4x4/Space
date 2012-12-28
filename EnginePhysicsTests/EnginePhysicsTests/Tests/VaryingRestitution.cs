﻿using Engine.Physics.Components;
using Microsoft.Xna.Framework;

namespace EnginePhysicsTests.Tests
{
    sealed class VaryingRestitution : AbstractTest
    {
        protected override void Create()
        {
            Manager.AddEdge(new Vector2(-40.0f, 0.0f), new Vector2(40.0f, 0.0f));

            var restitutions = new[] {0.0f, 0.1f, 0.3f, 0.5f, 0.75f, 0.9f, 1.0f};
            for (var i = 0; i < restitutions.Length; ++i)
            {
                Manager.AddCircle(worldPosition: new Vector2(-10.0f + 3.0f * i, 20.0f),
                                  type: Body.BodyType.Dynamic,
                                  radius: 1,
                                  density: 1,
                                  restitution: restitutions[i]);
            }
        }
    }
}
