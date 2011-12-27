﻿namespace Engine.Util
{
    public static class Angle
    {
        public static double MinAngle(double angle1, double angle2)
        {
            var delta = angle2 - angle1;
            if (delta > System.Math.PI)
            {
                delta -= System.Math.PI * 2;
            }
            else if (delta < -System.Math.PI)
            {
                delta += System.Math.PI * 2;
            }
            return delta;
        }
    }
}
