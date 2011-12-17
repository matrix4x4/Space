﻿using Engine.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Space.Model
{
    interface IGameObject : IEntity<PlayerInfo>
    {
        void Draw(GameTime gameTime, Vector2 translation, SpriteBatch spriteBatch);
    }
}
