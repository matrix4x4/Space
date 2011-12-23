﻿using Engine.Serialization;

namespace Engine.ComponentSystem.Components
{
    public static class PacketizerRegistration
    {
        public static void Initialize<TAttribute>()
            where TAttribute : struct
        {
            Packetizer.Register<EntityModules<TAttribute>>();

            Packetizer.Register<Acceleration>();
            Packetizer.Register<Avatar>();
            Packetizer.Register<Expiration>();
            Packetizer.Register<CollidableBox>();
            Packetizer.Register<CollidableSphere>();
            Packetizer.Register<Friction>();
            Packetizer.Register<Sound>();
            Packetizer.Register<Spin>();
            Packetizer.Register<Transform>();
            Packetizer.Register<TransformedRenderer>();
            Packetizer.Register<Velocity>();
        }
    }
}
