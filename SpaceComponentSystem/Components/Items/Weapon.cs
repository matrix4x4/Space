﻿using System;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.RPG.Components;
using Engine.Serialization;
using Engine.Util;
using Space.ComponentSystem.Util;
using Space.Data;
using System.Collections.Generic;

namespace Space.ComponentSystem.Components
{
    /// <summary>
    /// Represents a single weapon item.
    /// </summary>
    public sealed class Weapon : Item<AttributeType>
    {
        #region Fields

        /// <summary>
        /// The texture used to render this weapon.
        /// </summary>
        public string Texture;

        /// <summary>
        /// The sound this weapon emits when firing.
        /// </summary>
        public string Sound;

        /// <summary>
        /// The cooldown time to wait between shots, in seconds.
        /// </summary>
        public float Cooldown;

        /// <summary>
        /// The amount of energy this weapon requires for a single shot.
        /// </summary>
        public float EnergyConsumption;

        /// <summary>
        /// The amount of energy this weapon requires for a single shot.
        /// </summary>
        public float Damage;

        /// <summary>
        /// The projectiles this weapon fires.
        /// </summary>
        public ProjectileConstraints[] Projectiles;

        #endregion

        #region Constructor

        public Weapon(string texture, string sound, float cooldown, float energyConsumption, float damage, ProjectileConstraints[] projectiles,string name)
        {
            this.Texture = texture;
            this.Sound = sound;
            this.Cooldown = cooldown;
            this.EnergyConsumption = energyConsumption;
            this.Damage = damage;
            this.Projectiles = projectiles;
            _name = name;
        }
        public Weapon(string texture, string sound, float cooldown, float energyConsumption, float damage, ProjectileConstraints[] projectiles)
        {
            this.Texture = texture;
            this.Sound = sound;
            this.Cooldown = cooldown;
            this.EnergyConsumption = energyConsumption;
            this.Damage = damage;
            this.Projectiles = projectiles;
            _name = "Weapon";
        }
        public Weapon()
        {
            
        }

        #endregion
        #region Logic
        public override List<Attribute<AttributeType>> Attributes()
        {
            if (attributes == null)
            {
                attributes = new List<Attribute<AttributeType>>();
                attributes.Add(new Attribute<AttributeType>(new AttributeModifier<AttributeType>(AttributeType.WeaponDamage, Damage)));
                foreach (var component in Entity.Components)
                {
                    if (component is Attribute<AttributeType>)
                    {
                        attributes.Add((Attribute<AttributeType>)component);
                    }
                }
                attributes.Add(new Attribute<AttributeType>(new AttributeModifier<AttributeType>(AttributeType.WeaponCooldown,Cooldown)));
                attributes.Add(new Attribute<AttributeType>(new AttributeModifier<AttributeType>(AttributeType.WeaponEnergyConsumption, EnergyConsumption)));
                
            }
            return attributes;
        }
        #endregion
        #region Serialization / Hashing / Cloning

        /// <summary>
        /// Packetizes the specified packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <returns></returns>
        public override Packet Packetize(Packet packet)
        {
            return base.Packetize(packet)
                .Write(Texture)
                .Write(Sound)
                .Write(Cooldown)
                .Write(EnergyConsumption)
                .Write(Damage)
                .Write(Projectiles);
        }

        /// <summary>
        /// Depacketizes the specified packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        public override void Depacketize(Packet packet)
        {
            base.Depacketize(packet);

            Texture = packet.ReadString();
            Sound = packet.ReadString();
            Cooldown = packet.ReadSingle();
            EnergyConsumption = packet.ReadSingle();
            Damage = packet.ReadSingle();
            Projectiles = packet.ReadPacketizables<ProjectileConstraints>();
        }

        /// <summary>
        /// Hashes the specified hasher.
        /// </summary>
        /// <param name="hasher">The hasher.</param>
        public override void Hash(Hasher hasher)
        {
            base.Hash(hasher);

            hasher.Put(BitConverter.GetBytes(Cooldown));
            hasher.Put(BitConverter.GetBytes(EnergyConsumption));
            hasher.Put(BitConverter.GetBytes(Damage));
        }

        #endregion

        #region Copying

        public override AbstractComponent DeepCopy(AbstractComponent into)
        {
            var copy = (Weapon)base.DeepCopy(into);

            if (copy == into)
            {
                // Copying into other instance.
                copy.Texture = Texture;
                copy.Sound = Sound;
                copy.Cooldown = Cooldown;
                copy.EnergyConsumption = EnergyConsumption;
                copy.Damage = Damage;
                copy.Projectiles = Projectiles;
            }

            return copy;
        }

        #endregion
    }
}
