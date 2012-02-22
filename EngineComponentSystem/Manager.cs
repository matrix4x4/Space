﻿using System;
using System.Collections.Generic;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Messages;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework;
namespace Engine.ComponentSystem
{
    /// <summary>
    /// Manager for a complete component system. Tracks live entities and
    /// components, and allows lookup of components for entities.
    /// </summary>
    public sealed class Manager : IManager
    {
        #region Fields
        
        /// <summary>
        /// Lookup table for quick access to component by type.
        /// </summary>
        private Dictionary<Type, AbstractSystem> _systems = new Dictionary<Type, AbstractSystem>();

        /// <summary>
        /// Keeps track of entity->component relationships.
        /// </summary>
        private Dictionary<int, Entity> _entities = new Dictionary<int, Entity>();

        /// <summary>
        /// Component mapping id to instance (because there can be gaps
        /// </summary>
        private Dictionary<int, Component> _components = new Dictionary<int, Component>();

        /// <summary>
        /// Manager for entity ids.
        /// </summary>
        private IdManager _entityIds = new IdManager();

        /// <summary>
        /// Manager for entity ids.
        /// </summary>
        private IdManager _componentIds = new IdManager();

        #endregion

        #region Logic

        /// <summary>
        /// Update all registered systems.
        /// </summary>
        /// <param name="gameTime">Time elapsed since the last call to Update.</param>
        /// <param name="frame">The frame in which the update is applied.</param>
        public void Update(GameTime gameTime, long frame)
        {
            foreach (var system in _systems.Values)
            {
                system.Update(gameTime, frame);
            }

            // Make released component instances from the last update available
            // for reuse, as we can be sure they're not referenced in our
            // systems anymore.
            ReleaseDirty();
        }

        /// <summary>
        /// Renders all registered systems.
        /// </summary>
        /// <param name="gameTime">Time elapsed since the last call to Draw.</param>
        /// <param name="frame">The frame to render.</param>
        public void Draw(GameTime gameTime, long frame)
        {
            foreach (var system in _systems.Values)
            {
                system.Draw(gameTime, frame);
            }
        }
        
        #endregion

        #region Systems

        /// <summary>
        /// Add the specified system to this manager.
        /// </summary>
        /// <param name="system">The system to add.</param>
        /// <returns>
        /// This manager, for chaining.
        /// </returns>
        public IManager AddSystem(AbstractSystem system)
        {
            _systems.Add(system.GetType(), system);
            system.Manager = this;
            return this;
        }

        /// <summary>
        /// Add multiple systems to this manager.
        /// </summary>
        /// <param name="systems">The systems to add.</param>
        public void AddSystems(IEnumerable<AbstractSystem> systems)
        {
            foreach (var system in systems)
            {
                AddSystem(system);
            }
        }

        /// <summary>
        /// Removes the specified system from this manager.
        /// </summary>
        /// <param name="system">The system to remove.</param>
        /// <returns>
        /// Whether the system was successfully removed.
        /// </returns>
        public bool RemoveSystem(AbstractSystem system)
        {
            if (_systems.Remove(system.GetType()))
            {
                system.Manager = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get a system of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the system to get.</typeparam>
        /// <returns>
        /// The system with the specified type.
        /// </returns>
        public T GetSystem<T>() where T : AbstractSystem
        {
            return (T)_systems[typeof(T)];
        }

        #endregion

        #region Entities and Components

        /// <summary>
        /// Creates a new entity and returns its ID.
        /// </summary>
        /// <returns>
        /// The id of the new entity.
        /// </returns>
        public int AddEntity()
        {
            // Allocate a new entity id and a component mapping for the entity.
            var entity = _entityIds.GetId();
            _entities[entity] = AllocateEntity();
            return entity;
        }

        /// <summary>
        /// Test whether the specified entity exists.
        /// </summary>
        /// <param name="entity">The entity to check for.</param>
        /// <returns>
        /// Whether the manager contains the entity or not.
        /// </returns>
        public bool HasEntity(int entity)
        {
            return _entityIds.InUse(entity);
        }

        /// <summary>
        /// Removes an entity and all its components from the system.
        /// </summary>
        /// <param name="entity">The entity to remove.</param>
        public void RemoveEntity(int entity)
        {
            // Make sure that entity exists.
            if (!HasEntity(entity))
            {
                throw new ArgumentException("No such entity in the system.", "entity");
            }

            // Remove all of the components attached to that entity and free up
            // the entity object itself, and release the id.
            foreach (var component in _entities[entity].Components)
            {
                RemoveComponent(component);
            }
            ReleaseEntity(_entities[entity]);
            _entities.Remove(entity);
            _entityIds.ReleaseId(entity);

            // Send a message to all interested systems.
            EntityRemoved message;
            message.Entity = entity;
            SendMessage(ref message);
        }

        /// <summary>
        /// Creates a new component for the specified entity.
        /// </summary>
        /// <typeparam name="T">The type of component to create.</typeparam>
        /// <param name="entity">The entity to attach the component to.</param>
        /// <returns>
        /// The new component.
        /// </returns>
        public T AddComponent<T>(int entity) where T : Component, new()
        {
            // Make sure that entity exists.
            if (!HasEntity(entity))
            {
                throw new ArgumentException("No such entity in the system.", "entity");
            }

            // The create the component and set it up.
            var component = GetInstance<T>();
            component.Manager = this;
            component.Id = _componentIds.GetId();
            component.Entity = entity;
            component.Enabled = true;
            _components[component.Id] = component;

            // Add to entity index.
            _entities[entity].Add(component);
            
            // Send a message to all interested systems.
            ComponentAdded message;
            message.Component = component;
            SendMessage(ref message);

            // Return the created component.
            return component;
        }

        /// <summary>
        /// Test whether the component with the specified id exists.
        /// </summary>
        /// <param name="componentId">The id of the component to check for.</param>
        /// <returns>
        /// Whether the manager contains the component or not.
        /// </returns>
        public bool HasComponent(int componentId)
        {
            return _componentIds.InUse(componentId);
        }

        /// <summary>
        /// Removes the specified component from the system.
        /// </summary>
        /// <param name="component">The component to remove.</param>
        public void RemoveComponent(Component component)
        {
            // Validate the component.
            if (component == null)
            {
                throw new ArgumentNullException("component");
            }
            if (!HasComponent(component.Id))
            {
                throw new ArgumentException("No such component in the system.", "component");
            }

            // Remove it from the mapping and release the id for reuse.
            _entities[component.Entity].Remove(component);
            _components.Remove(component.Id);
            _componentIds.ReleaseId(component.Id);

            // Send a message to all interested systems.
            ComponentRemoved message;
            message.Component = component;
            SendMessage(ref message);

            // This will reset the component, so do that after sending the
            // event, to allow listeners to do something sensible with the
            // component before that.
            ReleaseComponent(component);
        }

        /// <summary>
        /// Gets the component of the specified type for an entity.
        /// </summary>
        /// <typeparam name="T">The type of component to get.</typeparam>
        /// <param name="entity">The entity to get the component of.</param>
        /// <returns>
        /// The component.
        /// </returns>
        public T GetComponent<T>(int entity) where T : Component
        {
            return (T)GetComponent(entity, typeof(T));
        }

        /// <summary>
        /// Gets a component of the specified type for an entity. If there are
        /// multiple components of the same type attached to the entity, use
        /// the <c>index</c> parameter to select which one to get.
        /// </summary>
        /// <param name="entity">The entity to get the component of.</param>
        /// <param name="type">The type of the component to get.</param>
        /// <returns>
        /// The component.
        /// </returns>
        public Component GetComponent(int entity, Type type)
        {
            // Make sure that entity exists.
            if (!HasEntity(entity))
            {
                throw new ArgumentException("No such entity in the system.", "entity");
            }

            var components = _entities[entity].GetComponents(type);
            return components.Count > 0 ? components[0] : null;
        }

        /// <summary>
        /// Get a component by its id.
        /// </summary>
        /// <param name="componentId">The if of the component to retrieve.</param>
        /// <returns>The component with the specified id.</returns>
        public Component GetComponentById(int componentId)
        {
            // Make sure that component exists.
            if (!HasComponent(componentId))
            {
                throw new ArgumentException("No such component in the system.", "componentId");
            }

            return _components[componentId];
        }

        /// <summary>
        /// Allows enumerating over all components of the specified entity.
        /// </summary>
        /// <typeparam name="T">The type of component to iterate.</typeparam>
        /// <param name="entity">The entity for which to get the components.</param>
        /// <returns>
        /// An enumerable listing all components of that entity.
        /// </returns>
        public IEnumerable<T> GetComponents<T>(int entity) where T : Component
        {
            // Make sure that entity exists.
            if (!HasEntity(entity))
            {
                throw new ArgumentException("No such entity in the system.", "entity");
            }

            return (IEnumerable<T>)_entities[entity].GetComponents(typeof(T));
        }
        
        #endregion

        #region Messaging

        /// <summary>
        /// Inform all interested systems of a message.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="message">The sent message.</param>
        public void SendMessage<T>(ref T message) where T : struct
        {
        }

        #endregion

        #region Serialization / Hashing

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>
        /// The packet after writing.
        /// </returns>
        public Packet Packetize(Packet packet)
        {
            // Write systems, with their types, as these will only be read back
            // via <c>ReadPacketizableInto()</c> to keep some variables that
            // can only passed in the constructor.
            packet.Write(_systems.Count);
            foreach (var system in _systems.Values)
            {
                packet.Write(system.GetType().AssemblyQualifiedName);
                packet.Write(system);
            }

            // Write the components, which are enough to implicitly restore the
            // entity to component mapping as well, so we don't need to write
            // the entity mapping.
            packet.Write(_components.Count);
            foreach (var component in _components.Values)
            {
                packet.Write(component.GetType().AssemblyQualifiedName);
                packet.Write(component);
            }

            // And finally, the managers for used ids.
            packet.Write(_entityIds);
            packet.Write(_componentIds);

            return packet;
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public void Depacketize(Packet packet)
        {
            int numSystems = packet.ReadInt32();
            for (int i = 0; i < numSystems; i++)
            {
                var typeName = packet.ReadString();
                var type = Type.GetType(typeName);
                if (type == null)
                {
                    throw new PacketException(string.Format("Invalid system type, not known locally: {0}.", typeName));
                }
                packet.ReadPacketizableInto(_systems[type]);
                _systems[type].Manager = this;
            }

            // Release all current objects.
            foreach (var entity in _entities.Values)
            {
                ReleaseEntity(entity);
            }
            _entities.Clear();
            foreach (var component in _components.Values)
            {
                ReleaseComponent(component);
            }
            _components.Clear();

            // Read back all components, fill in entity info as well, as that
            // is stored implicitly in the components.
            int numComponents = packet.ReadInt32();
            for (int i = 0; i < numComponents; i++)
            {
                var type = Type.GetType(packet.ReadString());
                var component = packet.ReadPacketizableInto(AllocateComponent(type));
                component.Manager = this;
                _components.Add(component.Id, component);
                // Add to entity mapping, create entries as necessary.
                if (!_entities.ContainsKey(component.Entity))
                {
                    _entities.Add(component.Entity, AllocateEntity());
                }
                _entities[component.Entity].Add(component);
            }

            // And finally, the managers for ids.
            packet.ReadPacketizableInto(_entityIds);
            packet.ReadPacketizableInto(_componentIds);
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public void Hash(Hasher hasher)
        {
            foreach (var component in _components.Values)
            {
                component.Hash(hasher);
            }
        }

        #endregion

        #region Copying

        public IManager DeepCopy()
        {
            return DeepCopy(null);
        }

        public IManager DeepCopy(IManager into)
        {
            var copy = (Manager)((into is Manager) ? into : new Manager());

            if (copy == into)
            {
                // Copy systems.
                foreach (var item in _systems)
                {
                    item.Value.DeepCopy(copy._systems[item.Key]);
                }

                // Free all instances.
                foreach (var entity in copy._entities.Values)
                {
                    ReleaseEntity(entity);
                }
                copy._entities.Clear();
                foreach (var component in copy._components.Values)
                {
                    ReleaseComponent(component);
                }
                copy._components.Clear();

                // Copy id managers.
                copy._entityIds = _entityIds.DeepCopy(copy._entityIds);
                copy._componentIds = _componentIds.DeepCopy(copy._componentIds);
            }
            else
            {
                copy._systems = new Dictionary<Type, AbstractSystem>();
                copy._entities = new Dictionary<int, Entity>();
                copy._components = new Dictionary<int, Component>();

                // Copy systems.
                foreach (var item in _systems)
                {
                    copy._systems.Add(item.Key, item.Value.DeepCopy());
                }

                // Copy id managers.
                copy._entityIds = _entityIds.DeepCopy();
                copy._componentIds = _componentIds.DeepCopy();
            }

            // Copy components and entity mapping.
            foreach (var component in _components.Values)
            {
                copy._components.Add(component.Id, component);
                if (!copy._entities.ContainsKey(component.Entity))
                {
                    copy._entities.Add(component.Entity, AllocateEntity());
                }
                copy._entities[component.Entity].Add(component);
            }

            return copy;
        }

        #endregion

        #region Pooling

        /// <summary>
        /// Objects that were released this very update run, so we don't want
        /// to reuse them until we enter the next update, to avoid reuse of
        /// components that might still be referenced in running system update
        /// loops.
        /// </summary>
        private readonly Dictionary<Type, Stack<Component>> _dirtyPool = new Dictionary<Type, Stack<Component>>();

        /// <summary>
        /// Object pool for reusable instances.
        /// </summary>
        private static readonly Dictionary<Type, Stack<Component>> ComponentPool = new Dictionary<Type, Stack<Component>>();

        /// <summary>
        /// Pool for entities (index structure only, used for faster component queries).
        /// </summary>
        private static readonly Stack<Entity> EntityPool = new Stack<Entity>();

        /// <summary>
        /// Try fetching an instance from the pool, if that fails, create a new one.
        /// </summary>
        /// <typeparam name="T">The type of component to get.</typeparam>
        /// <returns>
        /// A new component of the specified type.
        /// </returns>
        private static T GetInstance<T>() where T : Component, new()
        {
            var type = typeof(T);
            lock (ComponentPool)
            {
                if (ComponentPool.ContainsKey(type) && ComponentPool[type].Count > 0)
                {
                    return (T)ComponentPool[type].Pop();
                }
            }
            return new T();
        }

        /// <summary>
        /// Try fetching an instance from the pool, if that fails, create a new one.
        /// </summary>
        /// <param name="type">The type of component to get.</param>
        /// <returns>
        /// A new component of the specified type.
        /// </returns>
        private static Component AllocateComponent(Type type)
        {
            lock (ComponentPool)
            {
                if (ComponentPool.ContainsKey(type) && ComponentPool[type].Count > 0)
                {
                    return ComponentPool[type].Pop();
                }
            }
            return (Component)Activator.CreateInstance(type);
        }

        /// <summary>
        /// Releases a component for later reuse.
        /// </summary>
        /// <param name="component">The component to release.</param>
        private void ReleaseComponent(Component component)
        {
            component.Reset();
            var type = component.GetType();
            if (!_dirtyPool.ContainsKey(type))
            {
                _dirtyPool.Add(type, new Stack<Component>());
            }
            _dirtyPool[type].Push(component);
        }

        /// <summary>
        /// Releases all instances that were possibly still referenced when
        /// they were removed from the manager in running system update
        /// loops.
        /// </summary>
        private void ReleaseDirty()
        {
            lock (ComponentPool)
            {
                foreach (var type in _dirtyPool)
                {
                    if (!ComponentPool.ContainsKey(type.Key))
                    {
                        ComponentPool.Add(type.Key, new Stack<Component>());
                    }
                    foreach (var instance in type.Value)
                    {
                        ComponentPool[type.Key].Push(instance);
                    }
                }
            }
        }

        /// <summary>
        /// Allocates an entity for indexing.
        /// </summary>
        /// <returns>A new entity.</returns>
        private static Entity AllocateEntity()
        {
            lock (EntityPool)
            {
                if (EntityPool.Count > 0)
                {
                    return EntityPool.Pop();
                }
            }
            return new Entity();
        }

        /// <summary>
        /// Releases the entity for later reuse.
        /// </summary>
        /// <param name="entity">The entity.</param>
        private static void ReleaseEntity(Entity entity)
        {
            entity.Components.Clear();
            foreach (var cache in entity.TypeCache.Values)
            {
                cache.Clear();
            }
            lock (EntityPool)
            {
                EntityPool.Push(entity);
            }
        }

        #endregion

        #region Utility types

        /// <summary>
        /// Represents an entity, for easier internal access. We do not expose
        /// this class, to keep the whole component system's representation to
        /// the outside world 'flatter', which also means it's easier to copy
        /// and serialize, and to guarantee that everything in the system is in
        /// a valid state.
        /// </summary>
        private sealed class Entity
        {
            #region Fields
            
            /// <summary>
            /// List of all components attached to this entity.
            /// </summary>
            public readonly List<Component> Components = new List<Component>();

            /// <summary>
            /// Cache used for faster look-up of components of a specific type.
            /// </summary>
            public readonly Dictionary<Type, List<Component>> TypeCache = new Dictionary<Type, List<Component>>();

            #endregion

            #region Accessors
            
            /// <summary>
            /// Adds the specified component.
            /// </summary>
            /// <param name="component">The component.</param>
            public void Add(Component component)
            {
                Components.Add(component);

                // Add to all relevant caches.
                foreach (var entry in TypeCache)
                {
                    if (entry.Key.IsInstanceOfType(component))
                    {
                        entry.Value.Add(component);
                    }
                }
            }

            /// <summary>
            /// Removes the specified component.
            /// </summary>
            /// <param name="component">The component.</param>
            public void Remove(Component component)
            {
                Components.Remove(component);

                // Remove from all relevant caches.
                foreach (var entry in TypeCache.Values)
                {
                    entry.Remove(component);
                }
            }

            /// <summary>
            /// Gets the components of the specified type.
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns></returns>
            public List<Component> GetComponents(Type type)
            {
                BuildCache(type);
                return TypeCache[type];
            }

            #endregion

            #region Utility methods

            /// <summary>
            /// Builds the cache for the specified type, if it doesn't exist, yet.
            /// </summary>
            /// <param name="type">The type.</param>
            private void BuildCache(Type type)
            {
                if (TypeCache.ContainsKey(type))
                {
                    return;
                }

                var cache = new List<Component>();
                foreach (var component in Components)
                {
                    if (type.IsInstanceOfType(component))
                    {
                        cache.Add(component);
                    }
                }
                TypeCache.Add(type, cache);
            }

            #endregion
        }

        #endregion
    }
}
