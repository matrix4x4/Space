﻿using System.IO;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.RPG.Components;
using Engine.ComponentSystem.Spatial.Components;
using Engine.FarMath;
using Engine.Serialization;
using Microsoft.Xna.Framework;
using Space.ComponentSystem.Systems;
using Space.ComponentSystem.Util;

namespace Space.ComponentSystem.Components
{
    /// <summary>
    ///     This component has no actual functionality, but serves merely as a facade to centralize common tasks for
    ///     retrieving information on ships.
    /// </summary>
    public sealed class ShipInfo : Component, IInformation
    {
        #region Type ID

        /// <summary>The unique type ID for this object, by which it is referred to in the manager.</summary>
        public static readonly int TypeId = CreateTypeId();

        /// <summary>The type id unique to the entity/component system in the current program.</summary>
        public override int GetTypeId()
        {
            return TypeId;
        }

        #endregion

        #region Constants
        
        /// <summary>Store for performance.</summary>
        private static readonly int TransformTypeId = Engine.ComponentSystem.Manager.GetComponentTypeId<ITransform>();
        
        /// <summary>Store for performance.</summary>
        private static readonly int VelocityTypeId = Engine.ComponentSystem.Manager.GetComponentTypeId<IVelocity>();

        #endregion

        #region Initialization

        /// <summary>Reset the component to its initial state, so that it may be reused without side effects.</summary>
        public override void Reset()
        {
            base.Reset();

            MaxAcceleration = 0;
            MaxSpeed = 0;
            Mass = 0;
            RadarRange = 0;
            WeaponRange = 0;
        }

        #endregion

        #region Health / Energy

        /// <summary>Tests whether the ship is currently alive.</summary>
        /// <remarks>
        ///     For player ships this checks if they are currently respawning. All AI controlled ships have only a single
        ///     life, so if they exist they are considered to be alive.
        /// </remarks>
        public bool IsAlive
        {
            get
            {
                var respawn = (Respawn) Manager.GetComponent(Entity, Respawn.TypeId);
                return respawn == null || !respawn.IsRespawning;
            }
        }

        /// <summary>Gets the ship's current absolute health.</summary>
        public float Health
        {
            get
            {
                var health = (Health) Manager.GetComponent(Entity, Components.Health.TypeId);
                return health != null ? health.Value : 0;
            }
        }

        /// <summary>Gets the ship's maximum absolute health.</summary>
        public float MaxHealth
        {
            get
            {
                var health = (Health) Manager.GetComponent(Entity, Components.Health.TypeId);
                return health != null ? health.MaxValue : 0;
            }
        }

        /// <summary>Gets the ship's current relative health.</summary>
        public float RelativeHealth
        {
            get
            {
                var health = (Health) Manager.GetComponent(Entity, Components.Health.TypeId);
                return health != null ? health.Value / health.MaxValue : 0;
            }
        }

        /// <summary>Gets the ship's current absolute energy.</summary>
        public float Energy
        {
            get
            {
                var energy = (Energy) Manager.GetComponent(Entity, Components.Energy.TypeId);
                return energy != null ? energy.Value : 0;
            }
        }

        /// <summary>Gets the ship's maximum absolute energy.</summary>
        public float MaxEnergy
        {
            get
            {
                var energy = (Energy) Manager.GetComponent(Entity, Components.Energy.TypeId);
                return energy != null ? energy.MaxValue : 0;
            }
        }

        /// <summary>Gets the ship's current relative energy.</summary>
        public float RelativeEnergy
        {
            get
            {
                var energy = (Energy) Manager.GetComponent(Entity, Components.Energy.TypeId);
                return energy != null ? energy.Value / energy.MaxValue : 0;
            }
        }

        #endregion

        #region Physics

        /// <summary>Get the ship's current position.</summary>
        public FarPosition Position
        {
            get
            {
                var transform = (ITransform) Manager.GetComponent(Entity, TransformTypeId);
                return transform != null ? transform.Position : FarPosition.Zero;
            }
        }

        /// <summary>Get the ship's current rotation, in radians.</summary>
        public float Rotation
        {
            get
            {
                var transform = (ITransform) Manager.GetComponent(Entity, TransformTypeId);
                return transform != null ? transform.Angle : 0;
            }
        }

        /// <summary>Get whether the ship is currently accelerating.</summary>
        public bool IsAccelerating
        {
            get
            {
                var control = (ShipControl) Manager.GetComponent(Entity, ShipControl.TypeId);
                return control != null && control.DirectedAcceleration != Vector2.Zero;
            }
        }

        /// <summary>Tells whether the ship is currently stabilizing its position.</summary>
        public bool IsStabilizing
        {
            get
            {
                var control = (ShipControl) Manager.GetComponent(Entity, ShipControl.TypeId);
                return control != null && control.Stabilizing;
            }
        }

        /// <summary>
        ///     Get the ship's current speed.
        ///     <para>
        ///         Note: this value max exceed the <c>MaxSpeed</c> if external forces such as gravitation are involved.
        ///     </para>
        /// </summary>
        /// <remarks>Performance note: store this value if you use it more than once.</remarks>
        public float Speed
        {
            get
            {
                var velocity = (IVelocity) Manager.GetComponent(Entity, VelocityTypeId);
                return velocity != null ? velocity.LinearVelocity.Length() : 0;
            }
        }

        /// <summary>Get the maximum speed of the ship.</summary>
        public float MaxSpeed { get; internal set; }

        /// <summary>Get the maximum acceleration this ship is capable of.</summary>
        public float MaxAcceleration { get; internal set; }

        /// <summary>Get the ship's current rotation speed, in radians per tick.</summary>
        public float RotationSpeed
        {
            get
            {
                var velocity = (IVelocity) Manager.GetComponent(Entity, VelocityTypeId);
                return velocity != null ? velocity.AngularVelocity : 0f;
            }
        }

        #endregion

        #region Modules / Attributes

        /// <summary>Gets the overall mass of this ship.</summary>
        public float Mass { get; internal set; }

        /// <summary>Get the ship's overall radar range.</summary>
        public float RadarRange { get; internal set; }

        /// <summary>The distance our highest range weapon can shoot.</summary>
        public float WeaponRange { get; internal set; }

        #endregion

        #region Equipment / Inventory

        /// <summary>The current number of items in the ship's inventory.</summary>
        public int InventoryCapacity
        {
            get
            {
                var inventory = (Inventory) Manager.GetComponent(Entity, Inventory.TypeId);
                return inventory != null ? inventory.Capacity : 0;
            }
        }

        /// <summary>The item at the specified index in the ship's inventory.</summary>
        /// <param name="index">The index of the item.</param>
        /// <returns>The item at that index.</returns>
        public int InventoryItemAt(int index)
        {
            var inventory = (Inventory) Manager.GetComponent(Entity, Inventory.TypeId);
            return inventory != null ? inventory[index] : 0;
        }

        /// <summary>Get the root item slot.</summary>
        /// <returns>The root item slot.</returns>
        public SpaceItemSlot Equipment
        {
            get { return (SpaceItemSlot) Manager.GetComponent(Entity, ItemSlot.TypeId); }
        }

        /// <summary>Get the equipped item in the slot with the specified id.</summary>
        /// <param name="slotId">The slot id from which to get the item, or zero for the root slot.</param>
        /// <returns>The item at that slot index.</returns>
        public int GetItem(int slotId = 0)
        {
            ItemSlot slot = null;
            if (slotId == 0)
            {
                slot = Equipment;
            }
            else if (Manager.HasComponent(slotId))
            {
                slot = Manager.GetComponentById(slotId) as ItemSlot;
            }
            return slot != null ? slot.Item : 0;
        }

        #endregion

        string[] IInformation.getDisplayText()
        {
            using (var s = new MemoryStream())
            {
                var w = new StreamWriter(s);
                w.Dump(this);
                w.Flush();
                s.Position = 0;
                var r = new StreamReader(s);
                return new[] {r.ReadToEnd()};
            }
        }

        Color IInformation.getDisplayColor()
        {
            return Color.LightSalmon;
        }

        bool IInformation.shallDraw()
        {
            if (Entity == ((LocalPlayerSystem) Manager.GetSystem(LocalPlayerSystem.TypeId)).LocalPlayerAvatar)
            {
                return true;
            }

            return false;
        }
    }
}