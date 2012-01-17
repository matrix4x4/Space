﻿using System.Collections.Generic;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Entities;
using Engine.ComponentSystem.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Space.ComponentSystem.Components;
using Space.Control;
using Space.Data;
using Space.ScreenManagement.Screens.Helper;

namespace Space.ScreenManagement.Screens.Gameplay
{
    /// <summary>
    /// Renderer class that's responsible for drawing a player's radar, i.e.
    /// the overlay that displays icons for nearby but out-of-screen objects
    /// of interest (ones with a <c>Detectable</c> component).
    /// </summary>
    sealed class Radar
    {
        #region Constants

        /// <summary>
        /// Size of the radar border in pixel.
        /// </summary>
        private const int _radarBorderSize = 50;

        #endregion

        #region Fields

        /// <summary>
        /// The local client, used to fetch player's position and radar range.
        /// </summary>
        private readonly GameClient _client;

        /// <summary>
        /// Sprite batch used for rendering.
        /// </summary>
        private SpriteBatch _spriteBatch;

        /// <summary>
        /// Background image for radar icons.
        /// </summary>
        private Texture2D _radarBackground;

        #endregion

        #region Single-Allocation

        /// <summary>
        /// Reused for iterating components.
        /// </summary>
        private readonly List<Entity> _reusableNeighborList = new List<Entity>(64);

        #endregion

        #region Constructor

        public Radar(GameClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Load graphics content for the game.
        /// </summary>
        public void LoadContent(SpriteBatch spriteBatch, ContentManager content)
        {
            _spriteBatch = spriteBatch;

            _radarBackground = content.Load<Texture2D>("Textures/radar_background");
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Render our local radar system, with whatever detectables are close
        /// enough.
        /// </summary>
        public void Draw()
        {
            // Get local player's avatar.
            var info = _client.GetPlayerShipInfo();

            // Can't do anything without an avatar.
            if (info == null)
            {
                return;
            }

            // Fetch all the components we need.
            var position = info.Position;
            var index = _client.GetSystem<IndexSystem>();

            // Bail if we're missing something.
            if (index == null)
            {
                return;
            }

            // Figure out the overall range of our radar system.
            float radarRange = info.RadarRange;

            // Our mass.
            float mass = info.Mass;

            // Get our viewport.
            var viewport = _spriteBatch.GraphicsDevice.Viewport;

            // Get the screen's center, used for diverse computations, and as
            // a center for relative computations (because the player's always
            // rendered in the center of the screen).
            Vector2 center;
            center.X = viewport.Width / 2f;
            center.Y = viewport.Height / 2f;

            // Get the texture origin (middle of the texture).
            Vector2 backgroundOrigin;
            backgroundOrigin.X = _radarBackground.Width / 2.0f;
            backgroundOrigin.Y = _radarBackground.Height / 2.0f;

            // Get bounds in which to display the icon.
            Rectangle screenBounds = viewport.Bounds;

            // Get the inner bounds in which to display the icon, i.e. minus
            // half the size of the icon, so deflate by that.
            Rectangle innerBounds = screenBounds;
            innerBounds.Inflate(-(int)backgroundOrigin.X, -(int)backgroundOrigin.Y);

            // Now this is the tricky part: we take the minimal bounding sphere
            // (or rather, circle) that fits our screen space. For each
            // detectable entity we then pick the point on this circle that's
            // in the direction of that entity.
            // Because the only four points where this'll actually be in screen
            // space will be the four corners, we'll map them down to the edges
            // again. See below for that.
            float a = center.X - backgroundOrigin.X;
            float b = center.Y - backgroundOrigin.Y;
            float radius = (float)System.Math.Sqrt(a * a + b * b);

            // Precomputed for the loop.
            float radarRangeSquared = radarRange * radarRange;

            // Begin drawing.
            _spriteBatch.Begin();

            // Loop through all our neighbors.
            foreach (var neighbor in index.
                GetNeighbors(ref position, radarRange, Detectable.IndexGroup, _reusableNeighborList))
            {
                // Get the components we need.
                var neighborTransform = neighbor.GetComponent<Transform>();
                var neighborDetectable = neighbor.GetComponent<Detectable>();
                var faction = neighbor.GetComponent<Faction>();

                // Bail if we're missing something.
                if (neighborTransform == null || neighborDetectable.Texture == null)
                {
                    continue;
                }

                // We don't show the icons for anything that's inside our
                // viewport. Get the position of the detectable inside our
                // viewport. This will also serve as our direction vector.
                var direction = neighborTransform.Translation - position;

                // Check if the object's inside. If so, skip it.
                if (screenBounds.Contains((int)(direction.X + center.X), (int)(direction.Y + center.Y)))
                {
                    continue;
                }

                // Get the color of the faction faction the detectable belongs
                // to (it any).
                var color = Color.White;
                if (faction != null)
                {
                    color = faction.Value.ToColor();
                }

                // We'll make stuff far away a little less opaque. First get
                // the linear relative distance.
                float ld = direction.LengthSquared() / radarRangeSquared;
                // Then apply a exponential fall-off, and make it cap a little
                // early to get the 100% alpha when nearby, not only when
                // exactly on top of the object ;)
                ld = System.Math.Min(1, (1.1f - ld * ld * ld) * 1.1f);

                // Make stuff far away a little less opaque.
                color *= ld;

                // Get the direction to the detectable and normalize it.
                direction.Normalize();

                // Figure out where we want to position our icon. As described
                // above, we first get the point on the surrounding circle,
                // by multiplying our normalized direction vector with that
                // circle's radius.
                Vector2 iconPosition;
                iconPosition.X = radius * direction.X;
                iconPosition.Y = radius * direction.Y;

                // But now it's almost certainly outside our screen. So let's
                // see in which sector we are (we can treat left/right and
                // up/down identically).
                if (iconPosition.X > center.X || iconPosition.X < -center.X)
                {
                    // Out of screen on the X axis. Guaranteed to be in bound
                    // for Y axis, though. Scale down.
                    var scale = center.X / System.Math.Abs(iconPosition.X);
                    iconPosition.X *= scale;
                    iconPosition.Y *= scale;
                }
                else if (iconPosition.Y > center.Y || iconPosition.Y < -center.Y)
                {
                    // Out of screen on the Y axis. Guaranteed to be in bound
                    // for X axis, though. Scale down.
                    var scale = center.Y / System.Math.Abs(iconPosition.Y);
                    iconPosition.X *= scale;
                    iconPosition.Y *= scale;
                }

                // Adjust to the center.
                iconPosition += center;

                // Finally, clamp the point to be far enough inside our
                // viewport for the complete texture of our icon to be
                // displayed, which is the original viewport minus half the
                // size of the icon.

                // Clamp our coordinates.
                iconPosition.X = MathHelper.Clamp(iconPosition.X, innerBounds.Left, innerBounds.Right);
                iconPosition.Y = MathHelper.Clamp(iconPosition.Y, innerBounds.Top, innerBounds.Bottom);

                // And, finally, draw it. First the background.
                _spriteBatch.Draw(_radarBackground, iconPosition, null, color, 0,
                    backgroundOrigin, 1, SpriteEffects.None, 0);

                // Get the texture origin (middle of the texture).
                Vector2 origin;
                origin.X = neighborDetectable.Texture.Width / 2.0f;
                origin.Y = neighborDetectable.Texture.Height / 2.0f;

                // And draw that, too.
                _spriteBatch.Draw(neighborDetectable.Texture, iconPosition, null, color, 0,
                    origin, 1, SpriteEffects.None, 0);
            }

            // Clear the list for the next run.
            _reusableNeighborList.Clear();

            // Make the background of the radar a bit darker...
            BasicForms.FillRectangle(_spriteBatch, 0, 0, _radarBorderSize, viewport.Height, Color.Black * 0.15f);
            BasicForms.FillRectangle(_spriteBatch, viewport.Width - _radarBorderSize, 0, _radarBorderSize, viewport.Height, Color.Black * 0.15f);
            BasicForms.FillRectangle(_spriteBatch, _radarBorderSize, 0, viewport.Width - 2 * _radarBorderSize, _radarBorderSize, Color.Black * 0.15f);
            BasicForms.FillRectangle(_spriteBatch, _radarBorderSize, viewport.Height - _radarBorderSize, viewport.Width - 2 * _radarBorderSize, _radarBorderSize, Color.Black * 0.15f);

            // ... and the border of the radar a bit lighter.
            BasicForms.DrawRectangle(_spriteBatch, _radarBorderSize, _radarBorderSize, viewport.Width - 2 * _radarBorderSize, viewport.Height - 2 * _radarBorderSize, Color.White * 0.3f);

            // Done drawing.
            _spriteBatch.End();
        }

        #endregion
    }
}
