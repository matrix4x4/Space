﻿using Engine.FarMath;
using Microsoft.Xna.Framework;

namespace Engine.ComponentSystem.Common.Components.Intersection
{
    /// <summary>
    /// Performs a sweep test for two spheres.
    /// </summary>
    public static class SphereSweep
    {
        /// <summary>
        /// Test for collision between two moving spheres.
        /// </summary>
        /// <param name="ra">radius of sphere A</param>
        /// <param name="A0">previous position of sphere A</param>
        /// <param name="A1">current position of sphere A</param>
        /// <param name="rb">radius of sphere B</param>
        /// <param name="B0">previous position of sphere B</param>
        /// <param name="B1">current position of sphere B</param>
        /// <returns>true if the spheres (did) collide.</returns>
        /// <see cref="http://www.gamasutra.com/view/feature/3383/simple_intersection_tests_for_games.php?page=2"/> 
        public static bool Test(float ra, ref FarPosition A0, ref FarPosition A1,
            float rb, ref FarPosition B0, ref FarPosition B1)
        {
            var va = (Vector2)(A1 - A0);
            var vb = (Vector2)(B1 - B0);
            var ab = (Vector2)(B0 - A0);

            //relative velocity (in normalized time)
            var vab = vb - va;
            var rab = ra + rb;

            //u*u coefficient
            var a = Vector2.Dot(vab, vab);

            //u coefficient
            var b = 2 * Vector2.Dot(vab, ab);

            //constant term 
            var c = Vector2.Dot(ab, ab) - rab * rab;

            //check if they're currently overlapping
            if (Vector2.Dot(ab, ab) <= rab * rab)
            {
                return true;
            }
            else if (va.X == 0 && va.Y == 0 && vb.X == 0 && vb.Y == 0)
            {
                // neither is moving, cannot collide.
                return false;
            }

            //check if they hit each other
            // during the frame
            var q = b * b - 4 * a * c;
            if (q >= 0)
            {
                q = (float)System.Math.Sqrt(q);
                var d = 1 / (a + a);
                if (d > 0)
                {
                    var r1 = (-b + q) * d;
                    var r2 = (-b - q) * d;
                    if (r2 > r1)
                    {
                        r1 = r2;
                    }
                    if (r1 >= 0 && r1 <= 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}