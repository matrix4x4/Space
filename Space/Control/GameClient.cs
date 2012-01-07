﻿using System;
using Engine.ComponentSystem.Components;
using Engine.ComponentSystem.Entities;
using Engine.ComponentSystem.Systems;
using Engine.Controller;
using Engine.Input;
using Engine.Session;
using Engine.Simulation.Commands;
using Engine.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Space.Simulation.Commands;

namespace Space.Control
{
    /// <summary>
    /// The game server, handling everything client logic related.
    /// </summary>
    public class GameClient : GameComponent
    {
        #region Properties
        
        /// <summary>
        /// The controller used by this game client.
        /// </summary>
        public IClientController<IFrameCommand> Controller { get; set; }

        #endregion

        #region Fields

        private float _previousTargetRotation;

        private float _currentTargetRotation;

        private float _shipTargetRotation;

        private bool _mouseStoppedMoving = true;

        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new local client, which will be coupled to the given server.
        /// </summary>
        /// <param name="game">The game to create the client for.</param>
        /// <param name="server">The server to join.</param>
        public GameClient(Game game, GameServer server)
            : base(game)
        {
            Controller = ControllerFactory.CreateLocalClient(Game, server.Controller);
        }

        /// <summary>
        /// Creates a new remote client, which can connect to remote games.
        /// </summary>
        /// <param name="game">The game to create the client for.</param>
        public GameClient(Game game)
            : base(game)
        {
            Controller = ControllerFactory.CreateRemoteClient(Game);
        }

        /// <summary>
        /// Initializes the client, whether it's remote or local.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            // Register for key presses and releases (movement).
            var keyboard = (IKeyboardInputManager)Game.Services.GetService(typeof(IKeyboardInputManager));
            if (keyboard != null)
            {
                keyboard.Pressed += HandleKeyPressed;
                keyboard.Released += HandleKeyReleased;
            }

            // Register for mouse movement (orientation) and buttons (shooting).
            var mouse = (IMouseInputManager)Game.Services.GetService(typeof(IMouseInputManager));
            if (mouse != null)
            {
                mouse.Moved += HandleMouseMoved;
                mouse.Pressed += HandleMousePressed;
                mouse.Released += HandleMouseReleased;
            }
        }

        /// <summary>
        /// Kills off the emitter and controller.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            Controller.Dispose();

            // Register for key presses and releases (movement).
            var keyboard = (IKeyboardInputManager)Game.Services.GetService(typeof(IKeyboardInputManager));
            if (keyboard != null)
            {
                keyboard.Pressed -= HandleKeyPressed;
                keyboard.Released -= HandleKeyReleased;
            }

            // Register for mouse movement (orientation) and buttons (shooting).
            var mouse = (IMouseInputManager)Game.Services.GetService(typeof(IMouseInputManager));
            if (mouse != null)
            {
                mouse.Moved -= HandleMouseMoved;
                mouse.Pressed -= HandleMousePressed;
                mouse.Released -= HandleMouseReleased;
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Utility methods

        /// <summary>
        /// Get the local player's avatar in the game, if possible.
        /// </summary>
        /// <returns>The local player's avatar.</returns>
        public Entity GetAvatar()
        {
            var avatarSystem = GetSystem<AvatarSystem>();
            if (avatarSystem != null)
            {
                return avatarSystem.GetAvatar(Controller.Session.LocalPlayer.Number);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Get the most current representation of a component system of the
        /// specified type.
        /// </summary>
        /// <typeparam name="T">The type of the component system to get.</typeparam>
        /// <returns>The component system of that type, or <c>null</c></returns>
        public T GetSystem<T>() where T : IComponentSystem
        {
            if (Controller.Session.ConnectionState == ClientState.Connected)
            {
                return Controller.Simulation.EntityManager.SystemManager.GetSystem<T>();
            }
            else
            {
                return default(T);
            }
        }

        #endregion

        #region Logic

        /// <summary>
        /// Updates the controller.
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Controller.Session.ConnectionState == ClientState.Connected)
            {
                // This test is necessary to figure out when player has stopped
                // moving his mouse, so we can send a finalizing rotation command.
                // Otherwise the ship might stop midway in our turn, not reaching
                // the actual target we currently want. This is because we only
                // send rotation commands when we really have to (specifically:
                // we don't send commands that would only update the target angle,
                // but not the direction), which saves us quite a few commands,
                // and thus net traffic.
                if (!_mouseStoppedMoving)
                {
                    // We stopped moving when last and current position are equal.
                    if (_previousTargetRotation == _currentTargetRotation)
                    {
                        Controller.PushLocalCommand(new PlayerInputCommand(PlayerInputCommand.PlayerInput.Rotate, _currentTargetRotation));
                        _shipTargetRotation = _currentTargetRotation;
                        _mouseStoppedMoving = true;
                    }
                    _previousTargetRotation = _currentTargetRotation;
                }
            }

            Controller.Update(gameTime);
        }

        #endregion

        #region Player input

        /// <summary>
        /// Player pressed a key.
        /// </summary>
        private void HandleKeyPressed(object sender, EventArgs e)
        {
            if (Controller.Session.ConnectionState != ClientState.Connected)
            {
                return;
            }

            var args = (KeyboardInputEventArgs)e;

            if (args.IsRepeat)
            {
                return;
            }

            PlayerInputCommand command = null;
            switch (args.Key)
            {
                case Keys.Down:
                case Keys.S:
                    // Accelerate downwards.
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.AccelerateDown);
                    break;
                case Keys.Left:
                case Keys.A:
                    // Accelerate left.
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.AccelerateLeft);
                    break;
                case Keys.Right:
                case Keys.D:
                    // Accelerate right.
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.AccelerateRight);
                    break;
                case Keys.Up:
                case Keys.W:
                    // Accelerate upwards.
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.AccelerateUp);
                    break;

                default:
                    return;
            }

            Controller.PushLocalCommand(command);
        }

        /// <summary>
        /// Player released a key.
        /// </summary>
        private void HandleKeyReleased(object sender, EventArgs e)
        {
            if (Controller.Session.ConnectionState != ClientState.Connected)
            {
                return;
            }

            var args = (KeyboardInputEventArgs)e;

            PlayerInputCommand command = null;
            switch (args.Key)
            {
                case Keys.Down:
                case Keys.S:
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.StopDown);
                    break;
                case Keys.Left:
                case Keys.A:
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.StopLeft);
                    break;
                case Keys.Right:
                case Keys.D:
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.StopRight);
                    break;
                case Keys.Up:
                case Keys.W:
                    command = new PlayerInputCommand(PlayerInputCommand.PlayerInput.StopUp);
                    break;

                default:
                    return;
            }

            Controller.PushLocalCommand(command);
        }

        private void HandleMouseMoved(object sender, EventArgs e)
        {
            if (Controller.Session.ConnectionState != ClientState.Connected)
            {
                return;
            }

            var args = (MouseInputEventArgs)e;

            // Get angle to middle of screen (position of our ship), which
            // will be our new target rotation.
            int rx = args.X - Game.GraphicsDevice.Viewport.Width / 2;
            int ry = args.Y - Game.GraphicsDevice.Viewport.Height / 2;
            var mouseAngle = (float)System.Math.Atan2(ry, rx);
            UpdateTargetRotation(mouseAngle);
        }

        // TODO private void HandleGamePadStickMoved(object sender, EventArgs e) { update rotation }

        /// <summary>
        /// This is the part of the base functionality for updating the direction
        /// we're facing.
        /// </summary>
        /// <param name="targetRotation">the new direction to face.</param>
        private void UpdateTargetRotation(float targetRotation)
        {
            Entity avatar = GetLocalAvatar();
            if (avatar != null)
            {
                var transform = avatar.GetComponent<Transform>();
                var spin = avatar.GetComponent<Spin>();

                // Get ships current orientation.
                double shipAngle = (double)transform.Rotation;

                // Remember where we'd like to rotate to (for finalizing).
                _currentTargetRotation = targetRotation;

                // Get the smaller angle between our current and our target angles.
                double deltaAngle = Angle.MinAngle(shipAngle, targetRotation);

                // Remaining rotation the ship has to perform.
                double remainingAngle = Angle.MinAngle(shipAngle, _shipTargetRotation);

                // Now, if the difference to our current rotation is large enough
                // and we're either rotating in the other direction or not at all,
                // we send a rotation command.
                // If we're rotating in that direction already, we DON'T! This is
                // the exact reason for why we need to finalize our rotations by
                // checking when the mouse stops moving. But we can save ourselves
                // a lot of superfluous input commands this way, reducing network
                // load somewhat (still pretty bad if user moves his mouse slowly,
                // but meh).
                if ((deltaAngle > 10e-3 && spin.Value <= 0) ||
                    (deltaAngle < -10e-3 && spin.Value >= 0) ||
                    (Math.Abs(remainingAngle) < spin.Value))
                {
                    Controller.PushLocalCommand(new PlayerInputCommand(PlayerInputCommand.PlayerInput.Rotate, _currentTargetRotation));
                    _shipTargetRotation = _currentTargetRotation;
                }

                // Set our flag to remember we might have to finalize the movement.
                _mouseStoppedMoving = false;
            }
        }

        private void HandleMousePressed(object sender, EventArgs e)
        {
            if (Controller.Session.ConnectionState != ClientState.Connected)
            {
                return;
            }

            var args = (MouseInputEventArgs)e;

            if (args.Button == MouseInputEventArgs.MouseButton.Left)
            {
                Controller.PushLocalCommand(new PlayerInputCommand(PlayerInputCommand.PlayerInput.Shoot));
            }
        }

        private void HandleMouseReleased(object sender, EventArgs e)
        {
            if (Controller.Session.ConnectionState != ClientState.Connected)
            {
                return;
            }

            var args = (MouseInputEventArgs)e;

            if (args.Button == MouseInputEventArgs.MouseButton.Left)
            {
                Controller.PushLocalCommand(new PlayerInputCommand(PlayerInputCommand.PlayerInput.CeaseFire));
            }
        }

        private Entity GetLocalAvatar()
        {
            return Controller.Simulation.EntityManager.SystemManager.GetSystem<AvatarSystem>().GetAvatar(Controller.Session.LocalPlayer.Number);
        }

        #endregion
    }
}
