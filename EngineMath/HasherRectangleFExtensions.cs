﻿using Engine.Serialization;

namespace Engine.Math
{
    public static class HasherRectangleFExtensions
    {
        /// <summary>
        /// Put the specified value to the data of which the hash
        /// gets computed.
        /// </summary>
        /// <param name="hasher">The hasher to use.</param>
        /// <param name="value">The data to add.</param>
        /// <returns>A reference to the hasher, for chaining.</returns>
        public static Hasher Put(this Hasher hasher, RectangleF value)
        {
            return hasher.Put(value.X).Put(value.Y).Put(value.Width).Put(value.Height);
        }
    }
}