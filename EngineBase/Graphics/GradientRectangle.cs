﻿using System;
using Microsoft.Xna.Framework;

namespace Engine.Graphics
{
    /// <summary>
    /// Utility class for rendering filled gradient rectangles, interpolating
    /// between multiple colors.
    /// </summary>
    public sealed class GradientRectangle : AbstractShape
    {
        #region Fields

        /// <summary>
        /// The colors to use when blending.
        /// </summary>
        private Vector4[] _colors = new Vector4[3];

        /// <summary>
        /// The points on the interval from and to which to interpolate.
        /// </summary>
        private float[] _points = new float[3];

        /// <summary>
        /// The actual number of points currently in use.
        /// </summary>
        private int _numPoints;

        #endregion

        #region Constructor

        public GradientRectangle(Game game)
            : base(game, "Shaders/GradientRectangle")
        {
            // Set defaults.
            SetGradients(new[] { Color.Black, Color.White }, new[] { 0f, 1f });
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Sets the gradient points with their colors to use for interpolating
        /// the color over the size of this rectangle.
        /// 
        /// <para>
        /// A minimum of two points and two colors is required, and the number
        /// of colors has to be same as the number of points. The colors will
        /// then be interpolated between the relative positions of the points.
        /// </para>
        /// </summary>
        /// <param name="colors">The colors between which to interpolate.</param>
        /// <param name="points">The points between which to interpolate.</param>
        public void SetGradients(Color[] colors, float[] points)
        {
            if (colors == null || points == null)
            {
                throw new ArgumentNullException(colors == null ? "colors" : "points");
            }
            if (colors.Length != points.Length)
            {
                throw new ArgumentException("Number of colors not equal to the number of points.", "colors");
            }
            if (colors.Length > _colors.Length)
            {
                throw new ArgumentException("Maximum number of points exceeded.", "colors");
            }
            float lowest = points[0];
            for (int i = 1; i < points.Length; i++)
            {
                if (points[i] >= lowest)
                {
                    lowest = points[i];
                }
                else
                {
                    throw new ArgumentException("Points are not ascending in order.", "points");
                }
            }

            _numPoints = colors.Length;

            for (int i = 0; i < colors.Length; i++)
            {
                _colors[i] = colors[i].ToVector4();
            }

            points.CopyTo(_points, 0);
        }

        /// <summary>
        /// Sets the gradient points with the colors to use for interpolating
        /// the color over the size of the rectangle.
        /// 
        /// <para>
        /// A minimum of two colors is required. The points will be
        /// automatically generated by linear interpolation.
        /// </para>
        /// </summary>
        /// <param name="points">The points between which to interpolate.</param>
        public void SetGradients(Color[] colors)
        {
            if (colors == null)
            {
                throw new ArgumentNullException("colors");
            }
            if (colors.Length > _colors.Length)
            {
                throw new ArgumentException("Maximum number of points exceeded.", "colors");
            }
            
            _numPoints = colors.Length;

            for (int i = 0; i < colors.Length; i++)
            {
                _colors[i] = colors[i].ToVector4();
                _points[i] = MathHelper.Lerp(0f, 1f, (float)i / (float)(_numPoints - 1));
            }
        }

        #endregion

        #region Draw

        /// <summary>
        /// Adjusts effect parameters prior to the draw call.
        /// </summary>
        protected override void AdjustParameters()
        {
            base.AdjustParameters();

            _effect.Parameters["Colors"].SetValue(_colors);
            _effect.Parameters["Points"].SetValue(_points);
            _effect.Parameters["NumValues"].SetValue(_numPoints);

            _effect.Parameters["Gradient"].SetValue(2f / _width);
        }

        #endregion
    }
}
