﻿using System;
using System.Collections.Generic;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Parameterizations;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Space.ComponentSystem.Components;
using Space.ComponentSystem.Entities;
using Space.ComponentSystem.Systems.Messages;
using Space.Data;

namespace Space.ComponentSystem.Systems
{
    //public class AiInfo :  IPacketizable, IHashable
    //{
    //    #region Fields

    //    public Vector2 SpawnPoint;
    //    public int RespawnTime;
    //    public AiComponent.AiCommand AiCommand;
    //    public Factions Faction;
    //    #endregion
    //    #region Constructor
    //    public AiInfo(Vector2 spawnPoint,int respawnTime,Factions faction,AiComponent.AiCommand command)
    //    {
    //        SpawnPoint = spawnPoint;
    //        RespawnTime = respawnTime;
    //        AiCommand = command;
    //        Faction = faction;
    //    }
    //    public AiInfo()
    //    {
            
    //    }
    //    #endregion

    //    #region Hash/Copy

        

    //    public IComponentSystem DeepCopy(IComponentSystem into)
    //    {

    //        return into;
    //    }

    //    public Packet Packetize(Packet packet)
    //    {
    //        packet.Write(SpawnPoint)
    //            .Write(RespawnTime)
    //            ;
    //        return packet;
    //    }

    //    public void Depacketize(Packet packet)
    //    {
    //        SpawnPoint = packet.ReadVector2();
    //        RespawnTime = packet.ReadInt32();
    //    }

    //    public void Hash(Hasher hasher)
    //    {


    //        hasher.Put(BitConverter.GetBytes(SpawnPoint.X));
    //        hasher.Put(BitConverter.GetBytes(SpawnPoint.Y));
    //        hasher.Put(BitConverter.GetBytes(RespawnTime));
    //    }
    //    #endregion
    //}
    class ShipsSpawnSystem : AbstractComponentSystem<NullParameterization, NullParameterization>
    {
        #region Properties
        
        /// <summary>
        /// The component system manager this system is part of.
        /// </summary>
        public override IComponentSystemManager Manager
        {
            get
            {
                return base.Manager;
            }
            set
            {
                if (Manager != null)
                {
                    Manager.EntityManager.Removed -= HandleEntityRemoved;
                }

                base.Manager = value;

                if (Manager != null)
                {
                    Manager.EntityManager.Removed += HandleEntityRemoved;
                }
            }
        }

        #endregion

        #region Fields

        private List<int> _entities = new List<int>();

        private ContentManager _content;

        #endregion

        #region Constructor

        public ShipsSpawnSystem(ContentManager content)
        {
            _content = content;
            ShouldSynchronize = true;
        }

        #endregion

        #region Logic

        /// <summary>
        /// Update all components in this system.
        /// </summary>
        /// <param name="frame">The frame in which the update is applied.</param>
        public override void Update(long frame)
        {
            var cellSystem = Manager.GetSystem<CellSystem>();
            foreach (var entityId in _entities)
            {
                var entity = Manager.EntityManager.GetEntity(entityId);
                var transform = entity.GetComponent<Transform>();
                if (!cellSystem.IsCellActive(CellSystem.GetCellIdFromCoordinates(ref transform.Translation)))
                {
                    _entities.Remove(entityId);
                    Manager.EntityManager.RemoveEntity(entityId);
                }
            }
        }

        public override void HandleMessage(ValueType message)
        {
            if (message is CellStateChanged)
            {
                var info = (CellStateChanged)message;
                if (info.State && info.X == 0 && info.Y == 0)
                {
                    const int cellSize = CellSystem.CellSize;
                    for (var i = 0; i < 10; i++)
                    {
                        var spawnPoint = new Vector2(60000+i* 500, 62500-i*600);
                        var order =
                            new AiComponent.AiCommand(
                                new Vector2(cellSize*info.X + (cellSize >> 1), cellSize*info.Y + (cellSize >> 1)),
                                cellSize, AiComponent.Order.Guard);
                        var faction = Factions.Player5;
                        _entities.Add(Manager.EntityManager.AddEntity(
                            EntityFactory.CreateAIShip(_content.Load<ShipData[]>("Data/ships")[0],faction,spawnPoint ,order
                            )));
                    }
                }
            }
        }

        private void HandleEntityRemoved(object sender, EntityEventArgs e)
        {
            _entities.Remove(e.EntityUid);
        }

        #endregion

        #region Cloning

        public override Packet Packetize(Packet packet)
        {
            packet.Write(_entities.Count);
            foreach (var item in _entities)
            {
                packet.Write(item);
                

            }

            return packet;
        }

        public override void Depacketize(Packet packet)
        {
            _entities.Clear();
            int numCells = packet.ReadInt32();
            for (int i = 0; i < numCells; i++)
            {
                var key = packet.ReadInt32();
                
                _entities.Add(key);
            }
        }

        public override void Hash(Hasher hasher)
        {
            foreach (var entities in _entities)
            {
                hasher.Put(BitConverter.GetBytes(entities));
                
                
                
            }
        }

        public override IComponentSystem DeepCopy(IComponentSystem into)
        {
            var copy = (ShipsSpawnSystem)base.DeepCopy(into);

            if (copy == into)
            {
                
                copy._entities.Clear();
            }
            else
            {
                copy._entities = new List<int>();
            }

            foreach (var item in _entities)
            {
                copy._entities.Add(item);
            }

            return copy;
        }

        #endregion
    }
}
