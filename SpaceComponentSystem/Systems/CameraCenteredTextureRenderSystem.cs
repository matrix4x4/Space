﻿using Engine.ComponentSystem.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Space.ComponentSystem.Systems
{
    /// <summary>
    /// Defines a render system which always translates the view to be
    /// centered to the camera.
    /// </summary>
    public sealed class CameraCenteredTextureRenderSystem : CullingTextureRenderSystem
    {
        #region Constructor
        
        public CameraCenteredTextureRenderSystem(ContentManager content, SpriteBatch spriteBatch)
            : base(content, spriteBatch)
        {
        }

        #endregion

        #region Logic

        /// <summary>
        /// Returns the <em>transformation</em> for offsetting and scaling rendered content.
        /// </summary>
        /// <returns>The transformation.</returns>
        protected override Matrix GetTransform()
        {
            return ((CameraSystem)Manager.GetSystem(CameraSystem.TypeId)).GetTransformation();
        }

        /// <summary>
        /// Returns the current bounds of the viewport, i.e. the rectangle of
        /// the world to actually render.
        /// </summary>
        protected override Rectangle ComputeViewport()
        {
            return ((CameraSystem)Manager.GetSystem(CameraSystem.TypeId)).ComputeVisibleBounds(SpriteBatch.GraphicsDevice.Viewport);
        }

        #endregion
    }
}
