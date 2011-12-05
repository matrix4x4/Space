﻿using Engine.Commands;
using Engine.Network;
using Engine.Serialization;
using Engine.Session;
using Engine.Simulation;
using Microsoft.Xna.Framework;

namespace Engine.Controller
{
    /// <summary>
    /// Base class for clients and servers using the UDP protocol and a TSS state.
    /// </summary>
    public abstract class AbstractTssController<TSession, TState, TSteppable, TCommand, TCommandType, TPlayerData, TPacketizerContext>
        : AbstractController<TSession, IFrameCommand<TCommandType, TPlayerData, TPacketizerContext>, TCommandType, TPlayerData, TPacketizerContext>,
          IStateController<TState, TSteppable, TSession, TCommand, TCommandType, TPlayerData, TPacketizerContext>
        where TSession : ISession<TPlayerData, TPacketizerContext>
        where TState : IReversibleSubstate<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TSteppable : ISteppable<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>
        where TCommand : IFrameCommand<TCommandType, TPlayerData, TPacketizerContext>
        where TCommandType : struct
        where TPlayerData : IPacketizable<TPlayerData, TPacketizerContext>
        where TPacketizerContext : IPacketizerContext<TPlayerData, TPacketizerContext>
    {
        #region Properties

        /// <summary>
        /// The underlying simulation used. Directly changing this is strongly
        /// discouraged, as it will lead to clients having to resynchronize
        /// themselves by getting a snapshot of the complete simulation.
        /// </summary>
        protected TSS<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext> Simulation { get; private set; }

        #endregion

        #region Fields

        /// <summary>
        /// The remainder of time we did not update last frame, which we'll add to the
        /// elapsed time in the next frame update.
        /// </summary>
        private double lastUpdateRemainder;

        #endregion

        #region Construction / Destruction

        /// <summary>
        /// Initialize session and base classes.
        /// </summary>
        /// <param name="game">the game this belongs to.</param>
        /// <param name="port">the port to listen on.</param>
        /// <param name="header">the protocol header.</param>
        public AbstractTssController(Game game, TSession session, uint[] delays)
            : base(game, session)
        {
            Simulation = new TSS<TState, TSteppable, TCommandType, TPlayerData, TPacketizerContext>(delays);
        }

        #endregion

        #region Logic

        /// <summary>
        /// Update the simulation. This adjusts the update procedure based
        /// on the selected timestep of the game. For fixed, it just does
        /// one step. For variable, it determines how many steps to perform,
        /// based on the elapsed time.
        /// </summary>
        /// <param name="gameTime">the game time information for the current
        /// update.</param>
        /// <param name="timeCorrection">some value to add to the elapsed time as
        /// a correction factor. Used by clients to better sync to the server's
        /// game speed.</param>
        protected void UpdateSimulation(GameTime gameTime, double timeCorrection = 0)
        {
            if (Game.IsFixedTimeStep)
            {
                Simulation.Update();
            }
            else
            {
                // Compensate for dynamic time step.
                double elapsed = gameTime.ElapsedGameTime.TotalMilliseconds + lastUpdateRemainder + timeCorrection;
                if (elapsed < Game.TargetElapsedTime.TotalMilliseconds)
                {
                    // If we can't actually run to the next frame, at least update
                    // back to the current frame in case rollbacks were made to
                    // accommodate player commands.
                    Simulation.RunToFrame(Simulation.CurrentFrame);
                }
                else
                {
                    // We can run at least one frame, so do the update(s). Due to the
                    // carry there may occur more than one simulation update per xna
                    // update, but that should be below the threshold of the noticeable.
                    while (elapsed >= Game.TargetElapsedTime.TotalMilliseconds)
                    {
                        elapsed -= Game.TargetElapsedTime.TotalMilliseconds;
                        Simulation.Update();
                    }
                    lastUpdateRemainder = elapsed;
                }
            }
        }

        #endregion

        #region Modify simulation

        /// <summary>
        /// Add a steppable to the simulation. Will be inserted at the
        /// current leading frame. The steppable will be given a unique
        /// id, by which it may later be referenced for removals.
        /// </summary>
        /// <param name="steppable">the steppable to add.</param>
        /// <returns>the id the steppable was assigned.</returns>
        public long AddSteppable(TSteppable steppable)
        {
            return AddSteppable(steppable, Simulation.CurrentFrame);
        }

        /// <summary>
        /// Add a steppable to the simulation. Will be inserted at the
        /// current leading frame. The steppable will be given a unique
        /// id, by which it may later be referenced for removals.
        /// </summary>
        /// <param name="steppable">the steppable to add.</param>
        /// <param name="frame">the frame in which to add the steppable.</param>
        /// <returns>the id the steppable was assigned.</returns>
        public virtual long AddSteppable(TSteppable steppable, long frame)
        {
            // Add the steppable to the simulation.
            Simulation.AddSteppable(steppable, frame);
            return steppable.UID;
        }

        /// <summary>
        /// Get a steppable in this simulation based on its unique identifier.
        /// </summary>
        /// <param name="steppableUid">the id of the object.</param>
        /// <returns>the object, if it exists.</returns>
        public TSteppable GetSteppable(long steppableUid)
        {
            return Simulation.GetSteppable(steppableUid);
        }

        /// <summary>
        /// Removes a steppable with the given id from the simulation.
        /// The steppable will be removed at the current frame.
        /// </summary>
        /// <param name="steppableId">the id of the steppable to remove.</param>
        public void RemoveSteppable(long steppableUid)
        {
            RemoveSteppable(steppableUid, Simulation.CurrentFrame);
        }

        /// <summary>
        /// Removes a steppable with the given id from the simulation.
        /// The steppable will be removed at the given frame.
        /// </summary>
        /// <param name="steppableId">the id of the steppable to remove.</param>
        /// <param name="frame">the frame in which to remove the steppable.</param>
        public virtual void RemoveSteppable(long steppableUid, long frame)
        {
            // Remove the steppable from the simulation.
            Simulation.RemoveSteppable(steppableUid, frame);
        }

        /// <summary>
        /// Apply a command.
        /// </summary>
        /// <param name="command">the command to send.</param>
        /// <param name="priority">the priority with which to distribute the command.</param>
        protected virtual void Apply(IFrameCommand<TCommandType, TPlayerData, TPacketizerContext> command, PacketPriority priority)
        {
            Simulation.PushCommand(command, command.Frame);
        }

        #endregion

        #region Protocol layer

        /// <summary>
        /// Prepends all normal command messages with the corresponding flag.
        /// </summary>
        /// <param name="command">the command to send.</param>
        /// <param name="packet">the final packet to send.</param>
        /// <returns>the given packet, after writing.</returns>
        protected override Packet WrapDataForSend(IFrameCommand<TCommandType, TPlayerData, TPacketizerContext> command, Packet packet)
        {
            packet.Write((byte)TssUdpControllerMessage.Command);
            return base.WrapDataForSend(command, packet);
        }

        #endregion
    }
}