﻿using System.Collections.Generic;
using System.Linq;
using Engine.ComponentSystem.Common.Messages;
using Engine.ComponentSystem.RPG.Components;
using Engine.ComponentSystem.Spatial.Components;
using Engine.ComponentSystem.Systems;
using Engine.Graphics.PolygonTools;
using Engine.Math;
using Engine.Serialization;
using Engine.XnaExtensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Space.ComponentSystem.Components;
using Space.Data;
using Space.Util;

namespace Space.ComponentSystem.Systems
{
    /// <summary>
    ///     This system serves a double purpose: first, it is responsible for rendering textures for ships, based on their
    ///     current equipment. This is used to avoid having to run through the complete equipment tree in each render-pass.
    ///     Second, it is used to generate polygon representations of the ships, using their rendered texture. These shapes are
    ///     used in the physics system.
    /// </summary>
    public sealed class ShipShapeSystem : AbstractSystem, IUpdatingSystem, IMessagingSystem
    {
        #region Type ID

        /// <summary>The unique type ID for this system, by which it is referred to in the manager.</summary>
        public static readonly int TypeId = CreateTypeId();

        #endregion

        #region Constants

        /// <summary>How many ticks cache entries are kept alive after their last use.</summary>
        private const int CacheEntryTimeToLive = (int) (10 * Settings.TicksPerSecond);

        #endregion

        #region Fields

        /// <summary>
        ///     We cache rendered ship models, indexed by the equipment hash, to avoid having to re-render them each frame,
        ///     which is slow due to the frequent texture switching for each item.
        /// </summary>
        /// <remarks>
        ///     Note that this is a shared collection, because we want to create as few textures as possible. This is also not
        ///     so bad performance wise, as we only access it in draw operations and when creating new physics fixtures, which must
        ///     also happen in a synchronous context.
        /// </remarks>
        private static readonly Dictionary<uint, CacheEntry> ModelCache = new Dictionary<uint, CacheEntry>();

        /// <summary>The struct used to store single model cache entries.</summary>
        private sealed class CacheEntry
        {
            /// <summary>The actual model texture used for rendering.</summary>
            public Texture2D Texture;

            /// <summary>Offset to render the cached texture at.</summary>
            public Vector2 Offset;

            /// <summary>
            ///     The polygon hulls that represents the texture's outline (for collision testing). This is the list of hulls for
            ///     each individual texture making up the final texture, for better granularity.
            /// </summary>
            public List<List<Vector2>> PolygonHulls;

            /// <summary>The number of frames the model wasn't used (to clean up unused models).</summary>
            public int Age;
        }
        
        /// <summary>The sprite batch used to render textures. Also static, to provide it to trailing sims.</summary>
        private static SpriteBatch _spriteBatch;

        #endregion

        #region Accessors

        /// <summary>Gets a texture for an entity, based on its equipment tree.</summary>
        /// <remarks>
        ///     This method is <em>not</em> thread safe.
        /// </remarks>
        /// <param name="entity">The entity to get the model for.</param>
        /// <param name="texture">The model texture.</param>
        /// <param name="offset">The local offset at which to render the texture.</param>
        public void GetTexture(int entity, out Texture2D texture, out Vector2 offset)
        {
            var entry = GetCacheEntry(entity);
            texture = entry.Texture;
            offset = entry.Offset;
        }

        /// <summary>
        ///     Gets the polygon hulls for an entity, based on its equipment tree. This will return a list of multiple
        ///     polygons, one for each rendered item, for better granularity.
        /// </summary>
        /// <remarks>
        ///     This method is <em>not</em> thread safe.
        /// </remarks>
        /// <param name="entity">The entity to get the hull for.</param>
        /// <returns>The polygon hulls for the model.</returns>
        public List<List<Vector2>> GetShapes(int entity)
        {
            return GetCacheEntry(entity).PolygonHulls;
        }

        private CacheEntry GetCacheEntry(int entity)
        {
            var equipment = (SpaceItemSlot) Manager.GetComponent(entity, ItemSlot.TypeId);
            var hash = HashEquipment(equipment);
            
            // Create cache texture if necessary.
            if (!ModelCache.ContainsKey(hash))
            {
                // Simulations may run multi-threaded, so we need to lock out static table here.
                lock (ModelCache)
                {
                    // Maybe we got our entry while waiting for our lock?
                    if (!ModelCache.ContainsKey(hash))
                    {
                        // No cache entry yet, create it. Determine the needed size of the texture.
                        var size = ComputeModelSize(equipment, Vector2.Zero);
                        // Then create it and push it as our current render target.
                        var target = new RenderTarget2D(
                            _spriteBatch.GraphicsDevice,
                            (int) System.Math.Ceiling(size.Width),
                            (int) System.Math.Ceiling(size.Height));

                        var previousRenderTargets = _spriteBatch.GraphicsDevice.GetRenderTargets();
                        _spriteBatch.GraphicsDevice.SetRenderTarget(target);
                        _spriteBatch.GraphicsDevice.Clear(Color.Transparent);
                        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

                        // Render the actual equipment into our texture.
                        RenderEquipment(equipment, new Vector2(-size.X, -size.Y));

                        // Restore the old render state.
                        _spriteBatch.End();
                        _spriteBatch.GraphicsDevice.SetRenderTargets(previousRenderTargets);

                        // Build polygon hull.
                        var data = new uint[target.Width * target.Height];
                        target.GetData(data);
                        var hull = TextureConverter.DetectVertices(data, target.Width, target.Height, 2f)[0];
                        for (var i = 0; i < hull.Count; ++i)
                        {
                            // Center at origin.
                            hull[i] -= new Vector2(target.Width / 2f, target.Height / 2f);
                        }

                        // Create a new cache entry for this equipment combination.
                        ModelCache[hash] = new CacheEntry
                        {
                            Texture = target,
                            Offset = new Vector2(-size.X, -size.Y),
                            PolygonHulls = EarClipDecomposer.ConvexPartition(hull)
                        };
                    }
                }
            }

            // Update last access time.
            ModelCache[hash].Age = 0;

            // Then return the entry!
            return ModelCache[hash];
        }

        #endregion

        #region Logic

        public void Update(long frame)
        {
            // Increase age of entries.
            // TODO probably possible to merge this into the 'Where' below.
            foreach (var entry in ModelCache.Values)
            {
                ++entry.Age;
            }

            // Prune old, unused cache entries.
            var deprecated = ModelCache
                .Where(c => c.Value.Age > CacheEntryTimeToLive)
                .Select(c => c.Key).ToList();
            foreach (var entry in deprecated)
            {
                ModelCache[entry].Texture.Dispose();
                ModelCache.Remove(entry);
            }
        }

        /// <summary>Handle a message of the specified type.</summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="message">The message.</param>
        public void Receive<T>(T message) where T : struct
        {
            {
                var cm = message as GraphicsDeviceCreated?;
                if (cm != null)
                {
                    _spriteBatch = new SpriteBatch(cm.Value.Graphics.GraphicsDevice);
                }
            }
            {
                var cm = message as GraphicsDeviceDisposing?;
                if (cm != null)
                {
                    UnloadContent();
                }
            }
            {
                var cm = message as GraphicsDeviceReset?;
                if (cm != null)
                {
                    UnloadContent();
                    _spriteBatch = new SpriteBatch(cm.Value.Graphics.GraphicsDevice);
                }
            }
        }

        /// <summary>Called when the graphics device is being disposed, and any assets manually allocated should be disposed.</summary>
        private void UnloadContent()
        {
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }

            foreach (var entry in ModelCache)
            {
                entry.Value.Texture.Dispose();
            }
            ModelCache.Clear();
        }

        /// <summary>
        ///     Computes a hash based on the currently equipped items, using the values relevant for rendering that item. An
        ///     identical hash will therefore mean that an equipment branch will look exactly the same when rendered.
        /// </summary>
        /// <param name="slot">The slot to start from.</param>
        /// <returns>Hash for the equipment branch.</returns>
        private uint HashEquipment(ItemSlot slot)
        {
            var hasher = new Hasher();

            foreach (var itemId in slot.AllItems)
            {
                // Get item info.
                var item = (SpaceItem) Manager.GetComponent(itemId, Item.TypeId);
                hasher.Write(item.DrawBelowParent);
                hasher.Write(item.ModelOffset);
                hasher.Write(item.RequiredSlotSize.Scale());
                foreach (SimpleTextureDrawable drawable in Manager.GetComponents(item.Entity, SimpleTextureDrawable.TypeId))
                {
                    hasher.Write(drawable.TextureName);
                    hasher.Write(drawable.Tint);
                }
            }

            return hasher.Value;
        }

        /// <summary>
        ///     Computes the size of the model, i.e. the area covered by the fully rendered equipment starting with the
        ///     specified slot.
        /// </summary>
        private RectangleF ComputeModelSize(
            SpaceItemSlot slot, Vector2 offset, ItemSlotSize parentSize = ItemSlotSize.Small, bool? mirrored = null)
        {
            // Nothing to do if there's no item in the slot.
            if (slot.Item <= 0)
            {
                return RectangleF.Empty;
            }

            // Get item info.
            var item = (SpaceItem) Manager.GetComponent(slot.Item, Item.TypeId);

            // Get required bounds to render this item.
            var bounds = Manager.GetComponents(item.Entity, SimpleTextureDrawable.TypeId)
                                .Cast<SimpleTextureDrawable>()
                                .Aggregate(
                                    RectangleF.Empty,
                                    (current, drawable) =>
                                    {
                                        var b = (RectangleF) drawable.Texture.Bounds;
                                        b.Offset(-b.Width / 2f, -b.Height / 2f);
                                        return RectangleF.Union(current, b);
                                    });

            // See if we should mirror rendering (e.g. left wing).
            var slotOffset = slot.Offset;
            var itemOffset = item.ModelOffset;
            if (mirrored.HasValue)
            {
                if (mirrored.Value)
                {
                    slotOffset.Y = -slotOffset.Y;
                }
            }
// ReSharper disable CompareOfFloatsByEqualityOperator Intentional.
            else if (slotOffset.Y != 0.0f)
// ReSharper restore CompareOfFloatsByEqualityOperator
            {
                mirrored = slot.Offset.Y < 0;
            }
            if (mirrored.HasValue && mirrored.Value)
            {
                itemOffset.Y = -itemOffset.Y;
            }

            // Move the offset according to rotation and accumulate it.
            var localOffset = offset + parentSize.Scale() * slotOffset;
            var renderOffset = localOffset + item.RequiredSlotSize.Scale() * itemOffset;

            // Adjust to actual area the texture is rendered to.
            bounds.Offset(renderOffset);
            bounds.Inflate(
                -bounds.Width / 2f * (1 - item.RequiredSlotSize.Scale()),
                -bounds.Height / 2f * (1 - item.RequiredSlotSize.Scale()));

            // Check sub-items.
            return Manager.GetComponents(item.Entity, ItemSlot.TypeId)
                .Cast<SpaceItemSlot>()
                .Aggregate(bounds, (current, childSlot) => RectangleF.Union(current, ComputeModelSize(childSlot, localOffset, item.RequiredSlotSize, mirrored)));
        }

        /// <summary>Renders the equipment starting from the specified slot.</summary>
        private void RenderEquipment(
            SpaceItemSlot slot,
            Vector2 offset,
            int depth = 1,
            float order = 0.5f,
            ItemSlotSize parentSize = ItemSlotSize.Small,
            bool? mirrored = null)
        {
            // Nothing to do if there's no item in the slot.
            if (slot.Item <= 0)
            {
                return;
            }

            // Get item info.
            var item = (SpaceItem) Manager.GetComponent(slot.Item, Item.TypeId);

            // Adjust depth we want to render at.
            order -= (item.DrawBelowParent ? -0.25f : 0.25f) / depth;

            // See if we should mirror rendering (e.g. left wing).
            var slotOffset = slot.Offset;
            var itemOffset = item.ModelOffset;
            if (mirrored.HasValue)
            {
                if (mirrored.Value)
                {
                    slotOffset.Y = -slotOffset.Y;
                }
            }
// ReSharper disable CompareOfFloatsByEqualityOperator Intentional.
            else if (slotOffset.Y != 0.0f)
// ReSharper restore CompareOfFloatsByEqualityOperator
            {
                mirrored = slot.Offset.Y < 0;
            }
            if (mirrored.HasValue && mirrored.Value)
            {
                itemOffset.Y = -itemOffset.Y;
            }

            // Move the offset according to rotation and accumulate it.
            var localOffset = offset + parentSize.Scale() * slotOffset;
            var renderOffset = localOffset + item.RequiredSlotSize.Scale() * itemOffset;

            // Render this particular item.
            foreach (SimpleTextureDrawable drawable in Manager.GetComponents(item.Entity, SimpleTextureDrawable.TypeId))
            {
                drawable.Draw(
                    _spriteBatch,
                    renderOffset,
                    0f,
                    item.RequiredSlotSize.Scale() / drawable.Scale,
                    mirrored.HasValue && mirrored.Value ? SpriteEffects.FlipVertically : SpriteEffects.None,
                    order);
            }

            // Render sub-items.
            foreach (SpaceItemSlot childSlot in Manager.GetComponents(item.Entity, ItemSlot.TypeId))
            {
                RenderEquipment(childSlot, localOffset, depth + 1, order, item.RequiredSlotSize, mirrored);
            }
        }

        #endregion
    }
}