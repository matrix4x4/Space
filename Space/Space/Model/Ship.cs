﻿using Engine.Physics;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Space.Commands;
using SpaceData;

namespace Space.Model
{
    class Ship : Sphere<GameState, IGameObject, GameCommandType, PlayerInfo, PacketizerContext>, IGameObject
    {
        /// <summary>
        /// Time in ticks it takes before a ship may respawn.
        /// </summary>
        public const long RespawnTime = 1000;

        /// <summary>
        /// The last frame this ship was destroyed in.
        /// </summary>
        protected long lastDestroyed;

        public bool IsAlive { get { return true /* State.CurrentFrame - lastDestroyed > RespawnTime */; } }

        public int PlayerNumber { get; private set; }

        private ShipData data;

        private Texture2D texture;

        private Directions accelerationDirection = Directions.None;
        private Directions rotateDirection = Directions.None;

        public Ship()
        {
        }

        public Ship(string name, int player, PacketizerContext context)
        {
            ShipData data = context.shipData[name];
            this.radius = data.Radius;
            this.data = data;
            this.texture = context.shipTextures[name];
            this.PlayerNumber = player;
        }

        public void Accelerate(Directions direction)
        {
            accelerationDirection |= direction;
            acceleration = DirectionConversion.DirectionToFPoint(accelerationDirection) * data.Acceleration;
        }

        public void StopAccelerate(Directions direction)
        {
            accelerationDirection &= ~direction;
            acceleration = DirectionConversion.DirectionToFPoint(accelerationDirection) * data.Acceleration;
        }

        public void Rotate(Directions direction)
        {
            rotateDirection |= direction;
            speedRotation = DirectionConversion.DirectionToFixed(rotateDirection) * data.RotationSpeed;
        }

        public void StopRotate(Directions direction)
        {
            rotateDirection &= ~direction;
            speedRotation = DirectionConversion.DirectionToFixed(rotateDirection) * data.RotationSpeed;
        }

        public override void Update()
        {
            if (IsAlive)
            {
                base.Update();
            }
        }

        public override void PostUpdate()
        {
            if (IsAlive)
            {
                base.PostUpdate();

                foreach (var collideable in State.Collideables)
                {
                    if (collideable.Intersects(this.radius, ref this.previousPosition, ref this.position))
                    {
                        collideable.NotifyOfCollision();
                        this.NotifyOfCollision();
                    }
                }
            }
        }

        public override void NotifyOfCollision()
        {
            lastDestroyed = State.CurrentFrame;
        }

        public override object Clone()
        {
            return this.MemberwiseClone();
        }

        public void Draw(GameTime gameTime, Vector2 translation, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(texture,
                new Rectangle(position.X.IntValue + (int)translation.X, position.Y.IntValue + (int)translation.Y,
                              texture.Width, texture.Height),
                null,
                Color.White,
                (float)rotation.DoubleValue,
                new Vector2(texture.Width / 2, texture.Height / 2),
                SpriteEffects.None,
                0);
        }

        public override void Packetize(Packet packet)
        {
            packet.Write(data.Name);
            packet.Write(PlayerNumber);

            base.Packetize(packet);
        }

        public override void Depacketize(Packet packet, PacketizerContext context)
        {
            string name = packet.ReadString();
            data = context.shipData[name];
            texture = context.game.Content.Load<Texture2D>(data.Texture);

            PlayerNumber = packet.ReadInt32();

            base.Depacketize(packet, context);
        }
    }
}
