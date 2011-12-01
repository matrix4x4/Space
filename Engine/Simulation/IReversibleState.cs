﻿using System;
using Engine.Serialization;

namespace Engine.Simulation
{
    /// <summary>
    /// A special kind of state which allows rolling back the simulation to a
    /// previous state. It will automatically try to roll back if a command is
    /// pushed that was issued earlier than the current frame (if the command
    /// was authoritative, i.e. not tentative).
    /// </summary>
    interface IReversibleState<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext> : IState<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TState : IReversibleSubstate<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TSteppable : ISteppable<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TCommandType : struct
        where TPlayerData : IPacketizable<TPacketizerContext>
    {
        /// <summary>
        /// Dispatched when the state needs to roll back further than it can
        /// to accommodate a server command. The handler must trigger the
        /// process of getting a valid snapshot, and feed it back to the
        /// state (e.g. using Depacketize()).
        /// </summary>
        event EventHandler<EventArgs> ThresholdExceeded;

        /// <summary>
        /// Run the simulation to the given frame, which may be in the past.
        /// </summary>
        /// <param name="frame">the frame to run to.</param>
        void RunToFrame(long frame);

        /// <summary>
        /// Add an object in a specific time frame. This will roll back, if
        /// necessary, to insert the object, meaning it can trigger desyncs.
        /// </summary>
        /// <param name="steppable">the object to insert.</param>
        /// <param name="frame">the frame to insert it at.</param>
        void Add(TSteppable steppable, long frame);

        /// <summary>
        /// Remove an object in a specific time frame. This will roll back, if
        /// necessary, to remove the object, meaning it can trigger desyncs.
        /// </summary>
        /// <param name="steppableUid">the id of the object to remove.</param>
        /// <param name="frame">the frame to remove it at.</param>
        void Remove(long steppableUid, long frame);

        /// <summary>
        /// Tells if the state is currently waiting to be synchronized.
        /// </summary>
        bool WaitingForSynchronization { get; }
    }
}
