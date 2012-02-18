﻿using Engine.ComponentSystem;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.RPG.Components;
using Engine.ComponentSystem.RPG.Constraints;
using Engine.Util;
using Microsoft.Xna.Framework.Content;
using Space.Data;

namespace Space.ComponentSystem.Factories
{
    /// <summary>
    /// Base class for item constraints.
    /// </summary>
    public abstract class ItemFactory : IFactory
    {
        #region Fields
        
        /// <summary>
        /// Unique name for this item type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Asset name of the texture to use for this item type to render it in
        /// menus and the inventory.
        /// </summary>
        [ContentSerializer(Optional = true)]
        public string Icon = "Textures/Icons/Buffs/default";

        /// <summary>
        /// The ingame texture to be displayed for items floating around in
        /// space.
        /// </summary>
        [ContentSerializer(Optional = true)]
        public string Model = "Textures/Stolen/Projectiles/missile_sabot_empty";

        /// <summary>
        /// The quality of the item, to give a rough idea of the value.
        /// </summary>
        [ContentSerializer(Optional = true)]
        public ItemQuality Quality = ItemQuality.Common;

        /// <summary>
        /// A list of attribute modifiers that are guaranteed to be applied to
        /// the generated item, just with random values.
        /// </summary>
        [ContentSerializer(Optional = true)]
        public AttributeModifierConstraint<AttributeType>[] GuaranteedAttributes = new AttributeModifierConstraint<AttributeType>[0];

        /// <summary>
        /// A list of attribute modifiers from which a certain number is
        /// randomly sampled, and from the chosen attribute modifiers will then
        /// be sampled the actual values to be applied to the generated item.
        /// </summary>
        [ContentSerializer(Optional = true)]
        public AttributeModifierConstraint<AttributeType>[] AdditionalAttributes = new AttributeModifierConstraint<AttributeType>[0];

        /// <summary>
        /// The number of additional attribute modifiers to apply to a
        /// generated item.
        /// </summary>
        [ContentSerializer(Optional = true)]
        public Interval<int> AdditionalAttributeCount = Interval<int>.Zero;

        #endregion

        #region Sampling

        /// <summary>
        /// Samples a new item.
        /// </summary>
        /// <param name="random">The randomizer to use.</param>
        /// <returns>The sampled item.</returns>
        public virtual Entity Sample(IUniformRandom random)
        {
            var entity = new Entity();

            var transform = new Transform();
            var renderer = new TransformedRenderer(Model);
            renderer.Enabled = false;

            entity.AddComponent(transform);
            entity.AddComponent(renderer);
            entity.AddComponent(new Index(Item.IndexGroup));

            return entity;
        }

        /// <summary>
        /// Samples the attributes to apply to the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="random">The randomizer to use.</param>
        /// <return>The entity with the attributes applied.</return>
        protected Entity SampleAttributes(Entity item, IUniformRandom random)
        {
            foreach (var attribute in GuaranteedAttributes)
            {
                item.AddComponent(new Attribute<AttributeType>(attribute.SampleAttributeModifier(random)));
            }
            var numAdditionalAttributes = (AdditionalAttributeCount.Low == AdditionalAttributeCount.High) ? AdditionalAttributeCount.Low
                : random.NextInt32(AdditionalAttributeCount.Low, AdditionalAttributeCount.High);
            for (int i = 0; i < numAdditionalAttributes; i++)
            {
                item.AddComponent(new Attribute<AttributeType>(AdditionalAttributes[random.NextInt32(AdditionalAttributes.Length)].SampleAttributeModifier(random)));
            }
            return item;
        }

        #endregion
    }
}