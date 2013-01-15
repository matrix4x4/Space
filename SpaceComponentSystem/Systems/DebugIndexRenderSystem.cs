﻿using Engine.ComponentSystem.Common.Messages;
using Engine.ComponentSystem.Spatial.Systems;
using Engine.ComponentSystem.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Space.ComponentSystem.Systems
{
    /// <summary>This system can be used to render an index from the index system.</summary>
    public sealed class DebugIndexRenderSystem : AbstractSystem, IDrawingSystem, IMessagingSystem
    {
        #region Properties

        /// <summary>Determines whether this system is enabled, i.e. whether it should draw.</summary>
        /// <value>
        ///     <c>true</c> if this instance is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets the index group mask to render.</summary>
        public ulong IndexGroupMask { get; set; }

        #endregion

        #region Fields

        /// <summary>The shape used to render index cells/entries.</summary>
        private Engine.Graphics.Rectangle _indexRectangle;

        #endregion

        #region Logic

        /// <summary>Handle a message of the specified type.</summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="message">The message.</param>
        public void Receive<T>(T message) where T : struct
        {
            {
                var cm = message as GraphicsDeviceCreated?;
                if (cm != null)
                {
                    if (_indexRectangle == null)
                    {
                        _indexRectangle = new Engine.Graphics.Rectangle(cm.Value.Content, cm.Value.Graphics)
                        {
                            Color = Color.LightGreen * 0.75f,
                            Thickness = 2f,
                            BlendState = BlendState.Additive
                        };
                        _indexRectangle.LoadContent();
                    }
                }
            }
        }

        /// <summary>Draws the system.</summary>
        /// <param name="frame">The frame that should be rendered.</param>
        /// <param name="elapsedMilliseconds">The elapsed milliseconds.</param>
        public void Draw(long frame, float elapsedMilliseconds)
        {
            if (IndexGroupMask > 0)
            {
                var index = (IndexSystem) Manager.GetSystem(IndexSystem.TypeId);
                var camera = (CameraSystem) Manager.GetSystem(CameraSystem.TypeId);
                _indexRectangle.SetTransform(camera.Transform);
                index.DrawIndex(IndexGroupMask, _indexRectangle, camera.Translation);
            }
        }

        #endregion
    }
}