﻿using System;
using System.Collections.Generic;
using Engine.Commands;
using Engine.Serialization;

namespace Engine.Simulation
{
    /// <summary>
    /// Base class for state implementation.
    /// 
    /// <para>
    /// State implementations sub-classing this base class must take care of
    /// (at least) two things:
    /// - Handling of commands (via the HandleCommand function).
    /// - Cloning of the state (may use CloneTo to take care of the basics).
    /// </para>
    /// </summary>
    public abstract class AbstractState<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext> : IState<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TState : AbstractState<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TSteppable : ISteppable<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TCommandType : struct
        where TPlayerData : IPacketizable<TPacketizerContext>
    {
        #region Properties

        /// <summary>
        /// The current frame of the simulation the state represents.
        /// </summary>
        public long CurrentFrame { get; protected set; }

        /// <summary>
        /// Enumerator over all children.
        /// </summary>
        public IEnumerable<TSteppable> Children { get { return steppables; } }

        /// <summary>
        /// The steppable factory to be used in this state.
        /// </summary>
        public ISteppableFactory<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext> SteppableFactory { get; protected set; }

        /// <summary>
        /// Packetizer used for serialization purposes.
        /// </summary>
        public IPacketizer<TPacketizerContext> Packetizer { get; protected set; }

        /// <summary>
        /// Getter to return <c>this</c> pointer of actual implementation type... damn generics.
        /// </summary>
        protected abstract TState ThisState { get; }

        #endregion

        #region Fields

        /// <summary>
        /// List of queued commands to execute in the future.
        /// </summary>
        protected Dictionary<long, List<ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext>>> commands = new Dictionary<long, List<ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext>>>();

        /// <summary>
        /// List of child steppables this state drives.
        /// </summary>
        protected List<TSteppable> steppables = new List<TSteppable>();

        #endregion

        #region Constructor

        protected AbstractState(IPacketizer<TPacketizerContext> packetizer)
        {
            this.SteppableFactory = new SteppableFactory<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>();
            this.Packetizer = packetizer;
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Add an steppable object to the list of participants of this state.
        /// </summary>
        /// <param name="steppable">the object to add.</param>
        /// <param name="keepUid">keep the objects UID, just increment the factories counter.</param>
        public void Add(TSteppable steppable, bool keepUid = false)
        {
            if (keepUid)
            {
                SteppableFactory.Increment();
            }
            else
            {
                SteppableFactory.GetUniqueId(steppable);
            }

            steppables.Add(steppable);
            steppable.State = ThisState;
        }

        /// <summary>
        /// Remove an steppable object to the list of participants of this state.
        /// </summary>
        /// <param name="steppable">the object to remove.</param>
        public void Remove(TSteppable steppable)
        {
            steppables.Remove(steppable);
            steppable.State = null;
        }

        /// <summary>
        /// Remove a steppable object by its id.
        /// </summary>
        /// <param name="steppableUid">the remove object.</param>
        public TSteppable Remove(long steppableUid)
        {
            if (steppableUid >= 0)
            {
                for (int i = 0; i < steppables.Count; i++)
                {
                    if (steppables[i].UID == steppableUid)
                    {
                        TSteppable steppable = steppables[i];
                        steppables.RemoveAt(i);
                        return steppable;
                    }
                }
            }
            return default(TSteppable);
        }

        /// <summary>
        /// Get a steppable's current representation in this state by its id.
        /// </summary>
        /// <param name="steppableUid">the id of the steppable to look up.</param>
        /// <returns>the current representation in this state.</returns>
        public TSteppable Get(long steppableUid)
        {
            for (int i = 0; i < steppables.Count; i++)
            {
                if (steppables[i].UID == steppableUid)
                {
                    return steppables[i];
                }
            }
            return default(TSteppable);
        }

        /// <summary>
        /// Apply a given command to the simulation state.
        /// </summary>
        /// <param name="command">the command to apply.</param>
        public virtual void PushCommand(ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext> command)
        {
            if (command.Frame <= CurrentFrame)
            {
                throw new ArgumentException("Command is from a frame in the past.");
            }
            if (!commands.ContainsKey(command.Frame))
            {
                commands.Add(command.Frame, new List<ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext>>());
                commands[command.Frame].Add(command);
            }
            else
            {
                // At least that frame is known, so there's a chance we have that
                // command in a tentative version. Let's check.
                var list = commands[command.Frame];
                int known = list.FindIndex(x => x.Equals(command));
                if (known >= 0)
                {
                    // Already there! Use the authoritative one (or if neither is do nothing).
                    var existing = list[known];
                    if (existing.IsTentative && !command.IsTentative)
                    {
                        list.RemoveAt(known);
                        list.Add(command);
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Advance the simulation by one frame.
        /// </summary>
        public virtual void Update()
        {
            // Increment frame number.
            ++CurrentFrame;

            // Execute any commands for the current frame.
            if (commands.ContainsKey(CurrentFrame))
            {
                foreach (var command in commands[CurrentFrame])
                {
                    HandleCommand(command);
                }
                commands.Remove(CurrentFrame);
            }

            // Update all objects in this state.
            foreach (var steppable in steppables)
            {
                steppable.Update();
            }
        }

        public abstract object Clone();

        public virtual void Packetize(Packet packet)
        {
            packet.Write(CurrentFrame);

            SteppableFactory.Packetize(packet);

            int totalCommands = 0;
            foreach (var list in commands.Values)
            {
                totalCommands += list.Count;
            }
            packet.Write(totalCommands);
            foreach (var kv in commands)
            {
                foreach (var command in kv.Value)
                {
                    Packetizer.Packetize(command, packet);
                }
            }

            packet.Write(steppables.Count);
            foreach (var steppable in steppables)
            {
                Packetizer.Packetize(steppable, packet);
            }
        }

        public virtual void Depacketize(Packet packet, TPacketizerContext context)
        {
            // Get the current frame of the simulation.
            CurrentFrame = packet.ReadInt64();

            // Read factory state.
            SteppableFactory.Depacketize(packet, context);

            // Find commands that our out of date now, but keep newer ones.
            List<long> deprecated = new List<long>();
            foreach (var key in commands.Keys)
            {
                if (key <= CurrentFrame)
                {
                    deprecated.Add(key);
                }
            }
            foreach (var frame in deprecated)
            {
                commands.Remove(frame);
            }

            // Continue with reading the list of commands.
            int numCommands = packet.ReadInt32();
            for (int j = 0; j < numCommands; ++j)
            {
                PushCommand(Packetizer.Depacketize<ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext>>(packet));
            }

            // And finally the objects. Remove the one we know before that.
            steppables.Clear();
            int numSteppables = packet.ReadInt32();
            for (int i = 0; i < numSteppables; ++i)
            {
                Add(Packetizer.Depacketize<TSteppable>(packet), true);
            }
        }

        /// <summary>
        /// Call this from the implemented Clone() method to clone basic properties.
        /// </summary>
        /// <param name="clone"></param>
        protected virtual object CloneTo(AbstractState<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext> clone)
        {
            clone.CurrentFrame = CurrentFrame;

            clone.SteppableFactory = (ISteppableFactory<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>)SteppableFactory.Clone();

            clone.Packetizer = Packetizer;

            clone.commands.Clear();
            foreach (var keyValue in commands)
            {
                clone.commands.Add(keyValue.Key, new List<ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext>>(keyValue.Value));
            }

            clone.steppables.Clear();
            foreach (var steppable in steppables)
            {
                clone.steppables.Add((TSteppable)steppable.Clone());
            }

            return clone;
        }

        /// <summary>
        /// Implement this to handle commands. This will be called for each command
        /// at the moment it should be applied.
        /// </summary>
        /// <param name="command">the command to handle.</param>
        protected abstract void HandleCommand(ISimulationCommand<TCommandType, TPlayerData, TPacketizerContext> command);
    }
}
