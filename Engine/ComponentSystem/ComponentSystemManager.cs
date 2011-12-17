﻿using System.Collections.Generic;
using Engine.Simulation;

namespace Engine.ComponentSystem
{
    public class CompositeComponentSystem
        : List<IComponentSystem>, IComponentSystem
    {
        public void Update(IEntity entity)
        {
            foreach (var item in this)
            {
                item.Update(entity);
            }
        }

        public object Clone()
        {
            var copy = new CompositeComponentSystem();
            foreach (var item in this)
            {
                copy.Add((IComponentSystem)item.Clone());
            }
            return copy;
        }
    }
}