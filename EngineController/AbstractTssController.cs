﻿using System;
using Engine.Serialization;
using Engine.Session;
using Engine.Simulation;
using Engine.Simulation.Commands;
using Engine.Util;
using Microsoft.Xna.Framework;

namespace Engine.Controller
{
    /// <summary>
    /// Base class for clients and servers using the UDP protocol and a TSS state.
    /// </summary>
    public abstract class AbstractTssController<TSession>
        : AbstractController<TSession, FrameCommand>, ISimulationController<TSession>
        where TSession : ISession
    {
        #region Types

        /// <summary>
        /// Used in abstract TSS server and client implementations.
        /// </summary>
        internal enum TssControllerMessage
        {
            /// <summary>
            /// Normal game command, handled in base class.
            /// </summary>
            Command,

            /// <summary>
            /// Server sends current frame to clients, used to synchronize
            /// run speeds of clients to server.
            /// </summary>
            Synchronize,

            /// <summary>
            /// Client requested the game state, e.g. because it could not
            /// roll back to a required state.
            /// </summary>
            GameStateRequest,

            /// <summary>
            /// Server sends game state to client in response to <c>GameStateRequest</c>.
            /// </summary>
            GameStateResponse,

            /// <summary>
            /// Server tells players about a new object to insert into the simulation.
            /// </summary>
            AddGameObject,

            /// <summary>
            /// Server tells players to remove an object from the simulation.
            /// </summary>
            RemoveGameObject,

            /// <summary>
            /// Compare the hash of the leading game state at a given frame. If
            /// the client fails the check, it'll have to request a new snapshot.
            /// </summary>
            HashCheck
        }

        #endregion

        #region Constants

        /// <summary>
        /// The number of milliseconds a single update should take.
        /// </summary>
        protected const double _targetElapsedMilliseconds = 1000.0 / 60.0;

        /// <summary>
        /// The actual load is multiplied with this factor to provide a little
        /// buffer, allowing server/clients to react to slow downs before the
        /// game becomes unresponsive.
        /// </summary>
        protected const double _loadBufferFactor = 1.8;

        #endregion

        #region Properties

        /// <summary>
        /// The underlying simulation controlled by this controller.
        /// </summary>
        public ISimulation Simulation { get { return _simulation; } }

        /// <summary>
        /// The current 'load', i.e. how much of the available time is actually
        /// needed to perform an update.
        /// </summary>
        /// <remarks>
        /// The factor represents a buffer, to start slowing down the
        /// simulation before it's too late.
        /// </remarks>
        public override double CurrentLoad { get { return _updateLoad.Mean(); } }

        /// <summary>
        /// The current actual game speed, based on possible slow-downs due
        /// to the server or other clients.
        /// </summary>
        public double ActualSpeed { get { return _adjustedSpeed; } }

        /// <summary>
        /// Adjusted load value to allow reacting to slow downs of server or
        /// clients before the game becomes unresponsive.
        /// </summary>
        protected double SafeLoad { get { return CurrentLoad * _loadBufferFactor; } }

        #endregion

        #region Fields

        /// <summary>
        /// The underlying simulation used. Directly changing this is strongly
        /// discouraged, as it will lead to clients having to resynchronize
        /// themselves by getting a snapshot of the complete simulation.
        /// </summary>
        protected TSS _tss;

        /// <summary>
        /// The adjusted speed we're currently running at, based on how well
        /// other clients (and the server) currently fare.
        /// </summary>
        protected double _adjustedSpeed = 1.0;

        /// <summary>
        /// Wrapper to restrict interaction with TSS.
        /// </summary>
        private SimulationWrapper _simulation;

        /// <summary>
        /// The remainder of time we did not update last frame, which we'll add to the
        /// elapsed time in the next frame update.
        /// </summary>
        private double _lastUpdateRemainder;

        /// <summary>
        /// Used to sample how long it takes for us to perform our simulation
        /// updates in relation to the available time.
        /// </summary>
        private DoubleSampling _updateLoad = new DoubleSampling(30);

        #endregion

        #region Construction / Destruction

        /// <summary>
        /// Initialize session and base classes.
        /// </summary>
        /// <param name="game">the game this belongs to.</param>
        /// <param name="port">the port to listen on.</param>
        /// <param name="header">the protocol header.</param>
        public AbstractTssController(TSession session, uint[] delays)
            : base(session)
        {
            _tss = new TSS(delays);
            _simulation = new SimulationWrapper(_tss, this);
        }

        #endregion

        #region Logic

        /// <summary>
        /// Update the simulation. This determines how many steps to perform,
        /// based on the elapsed time.
        /// </summary>
        /// <param name="gameTime">the game time information for the current
        /// update.</param>
        protected void UpdateSimulation(GameTime gameTime, double targetSpeed = 1.0)
        {
            // We can run at least one frame, so do the update(s).
            long begin = DateTime.Now.Ticks;

            // Time we spent updating. We don't want to take longer than
            // one update should take (i.e. targetTime), to avoid the game
            // getting slower and slower (because updates take longer and
            // longer -> more and more to catch up).
            double timePassed = 0;

            // Compensate for dynamic time step.
            double elapsed = gameTime.ElapsedGameTime.TotalMilliseconds + _lastUpdateRemainder;
            double targetFrequency = _targetElapsedMilliseconds / targetSpeed;
            if (elapsed < targetFrequency)
            {
                // If we can't actually run to the next frame, at least update
                // back to the current frame in case rollbacks were made to
                // accommodate player commands.
                _tss.RunToFrame(_tss.CurrentFrame);

                // Track how much time we spent in this update.
                timePassed = new TimeSpan(DateTime.Now.Ticks - begin).TotalMilliseconds;

                // Carry this to the next frame, for uncapped frame rates.
                _lastUpdateRemainder = elapsed;
            }
            else
            {
                // This is how much time we have to catch up for. The number
                // simulation frames this equates to is:
                //     remainingTime / targetTime.
                double remainingTime = elapsed;

                // Do as many updates as we can.
                while (remainingTime >= targetFrequency && timePassed < targetFrequency)
                {
                    _tss.Update();

                    // One less to do.
                    remainingTime -= targetFrequency;

                    // Track how much time we spent in this update.
                    timePassed = new TimeSpan(DateTime.Now.Ticks - begin).TotalMilliseconds;
                }

                // Keep a carry for the next update. But never try to catch up
                // on frames while we took too long, as this'll lead to the
                // simulation running to fast when catching up.
                _lastUpdateRemainder = System.Math.Min(remainingTime, targetFrequency);
            }

            // Track our load, but ignore idle rounds (this is for unlocked
            // frame rates, because we'd need a much larger sampling then,
            // otherwise).
            if (timePassed > 1 && timePassed < 128)
            {
                _updateLoad.Put(timePassed / _targetElapsedMilliseconds);
            }
        }

        #endregion

        #region Modify simulation

        /// <summary>
        /// Apply a command. This will apply the command to the frame it was issued in.
        /// </summary>
        /// <param name="command">The command to push.</param>
        protected virtual void Apply(FrameCommand command)
        {
            _tss.PushCommand(command, command.Frame);
        }

        #endregion

        #region Protocol layer

        /// <summary>
        /// Got command data from another client or the server.
        /// </summary>
        /// <param name="command">the received command.</param>
        protected override void HandleRemoteCommand(FrameCommand command)
        {
            Apply(command);
        }

        /// <summary>
        /// Prepends all normal command messages with the corresponding flag.
        /// </summary>
        /// <param name="command">the command to send.</param>
        /// <param name="packet">the final packet to send.</param>
        /// <returns>the given packet, after writing.</returns>
        protected override Packet WrapDataForSend(FrameCommand command, Packet packet)
        {
            packet.Write((byte)TssControllerMessage.Command);
            return base.WrapDataForSend(command, packet);
        }

        #endregion

        #region Simulation wrapper

        private class SimulationWrapper : ISimulation
        {
            public long CurrentFrame { get { return _tss.CurrentFrame; } }

            public ComponentSystem.IEntityManager EntityManager { get { return _tss.EntityManager; } }

            private TSS _tss;

            private AbstractTssController<TSession> _controller;

            public SimulationWrapper(TSS tss, AbstractTssController<TSession> controller)
            {
                _tss = tss;
                _controller = controller;
            }

            public void Update()
            {
                _tss.Update();
            }

            public void PushCommand(Command command)
            {
                _controller.Apply((FrameCommand)command);
            }

            #region Unsupported
            
            public ISimulation DeepCopy()
            {
                throw new NotImplementedException();
            }

            public ISimulation DeepCopy(ISimulation into)
            {
                throw new NotImplementedException();
            }

            public Packet Packetize(Packet packet)
            {
                throw new NotImplementedException();
            }

            public void Depacketize(Packet packet)
            {
                throw new NotImplementedException();
            }

            public void Hash(Hasher hasher)
            {
                throw new NotImplementedException();
            }

            #endregion
        }

        #endregion
    }
}
