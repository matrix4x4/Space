﻿using Engine.Commands;
using Engine.Serialization;

namespace Engine.Controller
{
    /// <summary>
    /// Signature for methods that can handle a certain type of emitted command.
    /// </summary>
    /// <param name="command">the command that was emitted.</param>
    public delegate void CommandEmittedEventHandler<TCommand, TPlayerData>(TCommand command)
        where TCommand : ICommand<TPlayerData>
        where TPlayerData : IPacketizable<TPlayerData>;

    /// <summary>
    /// Interface for "command emitters", i.e. objects that generate commands
    /// in some fashion (e.g. user input commands via key presses).
    /// </summary>
    public interface ICommandEmitter<TCommand, TPlayerData>
        where TCommand : ICommand<TPlayerData>
        where TPlayerData : IPacketizable<TPlayerData>
    {
        /// <summary>
        /// Event dispatched whenever a new command was generated. This command
        /// will be injected into the simulation at it's current frame.
        /// 
        /// The dispatched events must be of type <c>CommandEmittedEventArgs</c>,
        /// with the proper generics as to match the controller it'll be registered
        /// with.
        /// </summary>
        event CommandEmittedEventHandler<TCommand, TPlayerData> CommandEmitted;
    }
}
