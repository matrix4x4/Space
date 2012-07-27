﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Engine.Collections;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Messages;
using Engine.ComponentSystem.Systems;
using Engine.Serialization;
using Engine.Util;

namespace Engine.ComponentSystem
{
    /// <summary>
    /// Manager for a complete component system. Tracks live entities and
    /// components, and allows lookup of components for entities.
    /// </summary>
    [DebuggerDisplay("#Systems = {NumSystems}, #Components = {NumComponents}")]
    public sealed partial class Manager : IManager
    {
        #region Properties

        /// <summary>
        /// A list of all components currently registered with this manager,
        /// in order of their ID.
        /// </summary>
        public IEnumerable<Component> Components
        {
            get { return _components; }
        }

        /// <summary>
        /// The number of components registered with this manager.
        /// </summary>
        public int NumComponents
        {
            get { return _components.Count; }
        }

        /// <summary>
        /// A list of all systems registered with this manager.
        /// </summary>
        public IEnumerable<AbstractSystem> Systems
        {
            get { return _systems; }
        }

        /// <summary>
        /// The number of systems registered with this manager.
        /// </summary>
        public int NumSystems
        {
            get { return _systems.Count; }
        }

        #endregion

        #region Fields

        /// <summary>
        /// Manager for entity ids.
        /// </summary>
        private readonly IdManager _entityIds = new IdManager();

        /// <summary>
        /// Manager for entity ids.
        /// </summary>
        private readonly IdManager _componentIds = new IdManager();

        /// <summary>
        /// List of systems registered with this manager.
        /// </summary>
        private readonly List<AbstractSystem> _systems = new List<AbstractSystem>(); 

        /// <summary>
        /// Lookup table for quick access to systems by their type id.
        /// </summary>
        private readonly SparseArray<AbstractSystem> _systemsByTypeId = new SparseArray<AbstractSystem>();

        /// <summary>
        /// Keeps track of entity->component relationships.
        /// </summary>
        private readonly SparseArray<Entity> _entities = new SparseArray<Entity>();

        /// <summary>
        /// List of all components in this system, sorted by id.
        /// </summary>
        private readonly List<Component> _components = new List<Component>();

        /// <summary>
        /// Lookup table for quick access to components by their id.
        /// </summary>
        private readonly SparseArray<Component> _componentsById = new SparseArray<Component>();

        #endregion

        #region Logic

        /// <summary>
        /// Update all registered systems.
        /// </summary>
        /// <param name="frame">The frame in which the update is applied.</param>
        public void Update(long frame)
        {
            for (int i = 0, j = _systems.Count; i < j; ++i)
            {
                _systems[i].Update(frame);
            }

            // Make released component instances from the last update available
            // for reuse, as we can be sure they're not referenced in our
            // systems anymore.
            ReleaseDirty();
        }

        /// <summary>
        /// Renders all registered systems.
        /// </summary>
        /// <param name="frame">The frame to render.</param>
        public void Draw(long frame)
        {
            for (int i = 0, j = _systems.Count; i < j; ++i)
            {
                _systems[i].Draw(frame);
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
            // Get type ID for that system.
            var systemTypeId = GetSystemTypeId(system.GetType());

            Debug.Assert(_systemsByTypeId[systemTypeId] == null, "Must not add the same system twice.");

            _systemsByTypeId[systemTypeId] = system;
            _systems.Add(system);

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
        /// Adds a copy of the specified system.
        /// </summary>
        /// <param name="system">The system to copy.</param>
        public void CopySystem(AbstractSystem system)
        {
            var systemTypeId = GetSystemTypeId(system.GetType());
            if (_systemsByTypeId[systemTypeId] == null)
            {
                var systemCopy = system.NewInstance();
                systemCopy.Manager = this;
                _systemsByTypeId[systemTypeId] = systemCopy;
                _systems.Add(systemCopy);
            }
            system.CopyInto(_systemsByTypeId[systemTypeId]);
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
            _systemsByTypeId[GetSystemTypeId(system.GetType())] = null;
            _systems.Remove(system);

            system.Manager = null;

            return true;
        }

        /// <summary>
        /// Get a system of the specified type.
        /// </summary>
        /// <param name="type">The type of the system to get.</param>
        /// <returns>
        /// The system with the specified type.
        /// </returns>
        public AbstractSystem GetSystem(int type)
        {
            return _systemsByTypeId[type];
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
            var components = _entities[entity].Components;
            while (components.Count > 0)
            {
                RemoveComponent(components[components.Count - 1]);
            }
            var instance = _entities[entity];
            _entities[entity] = null;
            _entityIds.ReleaseId(entity);
            ReleaseEntity(instance);

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
            return (T)AddComponent(entity, typeof(T));
        }

        /// <summary>
        /// Creates a new component for the specified entity.
        /// </summary>
        /// <param name="entity">The entity to attach the component to.</param>
        /// <param name="type">The type of component to create.</param>
        /// <returns>
        /// The new component.
        /// </returns>
        public Component AddComponent(int entity, Type type)
        {
            Debug.Assert(type.IsSubclassOf(typeof(Component)));

            // Make sure that entity exists.
            if (!HasEntity(entity))
            {
                throw new ArgumentException("No such entity in the system.", "entity");
            }

            // The create the component and set it up.
            var component = AllocateComponent(type);
            component.Manager = this;
            component.Id = _componentIds.GetId();
            component.Entity = entity;
            component.Enabled = true;
            _components.Insert(~_components.BinarySearch(component, Component.Comparer), component);
            _componentsById[component.Id] = component;

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
            Debug.Assert(component != null);
            Debug.Assert(HasComponent(component.Id), "No such component in the system.");

            // Remove it from the mapping and release the id for reuse.
            _entities[component.Entity].Remove(component);
            _components.RemoveAt(_components.BinarySearch(component, Component.Comparer));
            _componentsById[component.Id] = null;
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
        /// Gets a component of the specified type for an entity. If there are
        /// multiple components of the same type attached to the entity, use
        /// the <c>index</c> parameter to select which one to get.
        /// </summary>
        /// <param name="entity">The entity to get the component of.</param>
        /// <param name="typeId">The type of the component to get.</param>
        /// <returns>
        /// The component.
        /// </returns>
        public Component GetComponent(int entity, int typeId)
        {
            Debug.Assert(HasEntity(entity), "No such entity in the system.");
            return _entities[entity].GetComponent(typeId);
        }

        /// <summary>
        /// Get a component by its id.
        /// </summary>
        /// <param name="componentId">The if of the component to retrieve.</param>
        /// <returns>The component with the specified id.</returns>
        public Component GetComponentById(int componentId)
        {
            Debug.Assert(HasComponent(componentId), "No such component in the system.");
            return _componentsById[componentId];
        }

        /// <summary>
        /// Allows enumerating over all components of the specified entity.
        /// </summary>
        /// <param name="entity">The entity for which to get the components.</param>
        /// <param name="typeId">The type of components to get.</param>
        /// <returns>
        /// An enumerable listing all components of that entity.
        /// </returns>
        public IEnumerable<Component> GetComponents(int entity, int typeId)
        {
            Debug.Assert(HasEntity(entity), "No such entity in the system.");
            return _entities[entity].GetComponents(typeId);
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
            for (int i = 0, j = _systems.Count; i < j; ++i)
            {
                _systems[i].Receive(ref message);
            }
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
            // Write the managers for used ids.
            packet.Write(_entityIds);
            packet.Write(_componentIds);

            // Write the components, which are enough to implicitly restore the
            // entity to component mapping as well, so we don't need to write
            // the entity mapping.
            packet.Write(_components.Count);
            foreach (var component in _components)
            {
                packet.Write(component.GetType());
                packet.Write(component);
            }

            // Write systems, with their types, as these will only be read back
            // via <c>ReadPacketizableInto()</c> to keep some variables that
            // can only passed in the constructor.
            packet.Write(_systems.Count);
            foreach (var system in _systems)
            {
                packet.Write(system.GetType());
                packet.Write(system);
            }

            return packet;
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public void Depacketize(Packet packet)
        {
            // Release all current objects.
            foreach (var entity in _entityIds)
            {
                ReleaseEntity(_entities[entity]);
            }
            _entities.Clear();
            foreach (var component in _components)
            {
                ReleaseComponent(component);
            }
            _components.Clear();
            _componentsById.Clear();

            // Get the managers for ids (restores "known" ids before restoring components).
            packet.ReadPacketizableInto(_entityIds);
            packet.ReadPacketizableInto(_componentIds);

            // Read back all components, fill in entity info as well, as that
            // is stored implicitly in the components.
            var numComponents = packet.ReadInt32();
            for (var i = 0; i < numComponents; ++i)
            {
                var type = packet.ReadType();
                var component = AllocateComponent(type);
                packet.ReadPacketizableInto(component);
                component.Manager = this;
                _components.Insert(~_components.BinarySearch(component, Component.Comparer), component);
                _componentsById[component.Id] = component;

                // Add to entity mapping, create entries as necessary.
                if (_entities[component.Entity] == null)
                {
                    _entities[component.Entity] = AllocateEntity();
                }
                _entities[component.Entity].Add(component);
            }
            // Fill in empty entities. This is to re-create empty entities, i.e.
            // entities with no components.
            foreach (var entityId in _entityIds)
            {
                if (_entities[entityId] == null)
                {
                    _entities[entityId] = AllocateEntity();
                }
            }

            // Read back all systems. This must be done after reading the components,
            // because the systems will fetch their components again at this point.
            var numSystems = packet.ReadInt32();
            for (var i = 0; i < numSystems; ++i)
            {
                var type = packet.ReadType();
                if (!SystemTypes.ContainsKey(type))
                {
                    throw new PacketException("Could not depacketize system of unknown type " + type.FullName);
                }
                packet.ReadPacketizableInto(_systemsByTypeId[GetSystemTypeId(type)]);
            }

            // All done, send message to allow post-processing.
            Depacketized message;
            SendMessage(ref message);
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public void Hash(Hasher hasher)
        {
            foreach (var system in _systems)
            {
                system.Hash(hasher);
            }
            foreach (var component in _components)
            {
                component.Hash(hasher);
            }
        }

        /// <summary>
        /// Write a complete entity, meaning all its components, to the
        /// specified packet. Entities saved this way can be restored using
        /// the <c>ReadEntity()</c> method.
        /// <para/>
        /// This uses the components' <c>Packetize</c> facilities.
        /// </summary>
        /// <param name="entity">The entity to write.</param>
        /// <param name="packet">The packet to write to.</param>
        /// <returns>
        /// The packet after writing the entity's components.
        /// </returns>
        public Packet PacketizeEntity(int entity, Packet packet)
        {
            return packet.WriteWithTypeInfo(_entities[entity].Components);
        }

        /// <summary>
        /// Reads an entity from the specified packet, meaning all its
        /// components. This will create a new entity, with an id that
        /// may differ from the id the entity had when it was written.
        /// <para/>
        /// In particular, all re-created components will likely have different
        /// different ids as well, so this method is not suited for storing
        /// components that reference other components, even if just by their
        /// ID.
        /// <para/>
        /// This will act as though all of the written components were added,
        /// i.e. each restored component will send a <c>ComponentAdded</c>
        /// message.
        /// <para/>
        /// This uses the components' <c>Depacketize</c> facilities.
        /// </summary>
        /// <param name="packet">The packet to read the entity from.</param>
        /// <returns>The id of the read entity.</returns>
        public int DepacketizeEntity(Packet packet)
        {
            var entity = AddEntity();
            var components = packet.ReadPacketizablesWithTypeInfo<Component>();
            foreach (var component in components)
            {
                component.Id = _componentIds.GetId();
                component.Entity = entity;
                component.Manager = this;
                _components.Insert(~_components.BinarySearch(component, Component.Comparer), component);
                _componentsById[component.Id] = component;

                // Add to entity index.
                _entities[entity].Add(component);

                // Send a message to all interested systems.
                ComponentAdded message;
                message.Component = component;
                SendMessage(ref message);
            }
            return entity;
        }

        #endregion

        #region Copying

        /// <summary>
        /// Creates a new copy of the same type as the object.
        /// </summary>
        /// <returns>The copy.</returns>
        public IManager NewInstance()
        {
            var copy = new Manager();

            foreach (var system in _systems)
            {
                copy.AddSystem(system.NewInstance());
            }

            return copy;
        }

        /// <summary>
        /// Creates a deep copy of the object, reusing the given object.
        /// </summary>
        /// <param name="into">The object to copy into.</param>
        /// <returns>The copy.</returns>
        public void CopyInto(IManager into)
        {
            Debug.Assert(into is Manager);
            Debug.Assert(into != this);

            var copy = (Manager)into;

            // Copy id managers.
            _entityIds.CopyInto(copy._entityIds);
            _componentIds.CopyInto(copy._componentIds);

            // Copy components and entities.
            copy._components.Clear();
            copy._componentsById.Clear();
            foreach (var component in _components)
            {
                // The create the component and set it up.
                var componentCopy = AllocateComponent(component.GetType()).Initialize(component);
                componentCopy.Id = component.Id;
                componentCopy.Entity = component.Entity;
                componentCopy.Manager = copy;
                copy._components.Add(componentCopy);
                copy._componentsById[componentCopy.Id] = componentCopy;
            }

            copy._entities.Clear();
            foreach (var entity in _entityIds)
            {
                // Create copy.
                var entityCopy = AllocateEntity();
                copy._entities[entity] = entityCopy;

                // Assign copied components.
                foreach (var component in _entities[entity].Components)
                {
                    entityCopy.Add(copy.GetComponentById(component.Id));
                }
            }

            // Copy systems after copying components so they can fetch their
            // components again.
            foreach (var item in _systems)
            {
                copy.CopySystem(item);
            }
        }

        #endregion
    }
}
