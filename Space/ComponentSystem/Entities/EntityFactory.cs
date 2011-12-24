﻿using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Entities;
using Engine.Math;
using Space.ComponentSystem.Components;
using Space.Data;

namespace Space.ComponentSystem.Entities
{
    class EntityFactory
    {
        public static IEntity CreateShip(ShipData shipData, int playerNumber)
        {
            var ship = new Entity();
            ship.AddComponent(new Transform());
            ship.AddComponent(new Acceleration());
            ship.AddComponent(new Friction());
            ship.AddComponent(new Spin());
            ship.AddComponent(new Velocity());
            ship.AddComponent(new CollidableSphere());
            ship.AddComponent(new EntityModules<EntityAttributeType>());
            ship.AddComponent(new WeaponControl());
            ship.AddComponent(new WeaponSound());
            ship.AddComponent(new ShipControl());
            ship.AddComponent(new Avatar());
            ship.AddComponent(new TransformedRenderer());

            var friction = ship.GetComponent<Friction>();
            friction.Value = (Fixed)0.01;
            friction.MinVelocity = (Fixed)0.02;

            var collidable = ship.GetComponent<CollidableSphere>();
            collidable.Radius = shipData.Radius;

            var modules = ship.GetComponent<EntityModules<EntityAttributeType>>();
            modules.AddModules(shipData.Hulls);
            modules.AddModules(shipData.Reactors);
            modules.AddModules(shipData.Thrusters);
            modules.AddModules(shipData.Shields);
            modules.AddModules(shipData.Weapons);

            var avatar = ship.GetComponent<Avatar>();
            avatar.PlayerNumber = playerNumber;

            var renderer = ship.GetComponent<TransformedRenderer>();
            renderer.TextureName = shipData.Texture;

            return ship;
        }

        public static IEntity CreateProjectile(IEntity emitter, ProjectileData projectile)
        {
            var entity = new Entity();

            // Give the projectile its position.
            var transform = new Transform();
            var emitterTransform = emitter.GetComponent<Transform>();
            if (emitterTransform != null)
            {
                transform.Translation = emitterTransform.Translation;
                transform.Rotation = emitterTransform.Rotation;
            }
            entity.AddComponent(transform);

            // Make it visible.
            if (!string.IsNullOrWhiteSpace(projectile.Texture))
            {
                var renderer = new TransformedRenderer();
                renderer.TextureName = projectile.Texture;
                entity.AddComponent(renderer);
            }

            // Give it its initial velocity.
            var velocity = new Velocity();
            if (projectile.InitialVelocity != Fixed.Zero)
            {
                FPoint rotation = FPoint.Create((Fixed)1, (Fixed)0);
                if (emitterTransform != null)
                {
                    rotation = FPoint.Rotate(rotation, transform.Rotation);
                }
                velocity.Value = rotation * projectile.InitialVelocity;
            }
            var emitterVelocity = entity.GetComponent<Velocity>();
            if (emitterVelocity != null)
            {
                velocity.Value += emitterVelocity.Value;
            }
            entity.AddComponent(velocity);

            // Make it collidable.
            var collision = new CollidableSphere();
            collision.Radius = projectile.CollisionRadius;
            entity.AddComponent(collision);

            // Give it some friction.
            if (projectile.Friction > 0)
            {
                var friction = new Friction();
                friction.Value = projectile.Friction;
                entity.AddComponent(friction);
            }

            // Make it expire after some time.
            if (projectile.TimeToLive > 0)
            {
                var expiration = new Expiration();
                expiration.TimeToLive = projectile.TimeToLive;
                entity.AddComponent(expiration);
            }

            return entity;
        }

        public static IEntity CreateStar(string texture, FPoint position)
        {
            var entity = new Entity();

            var transform = new Transform();
            transform.Translation = position;
            entity.AddComponent(transform);

            var renderer = new TransformedRenderer();
            renderer.TextureName = texture;
            entity.AddComponent(renderer);

            return entity;
        }
    }
}
