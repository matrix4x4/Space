﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Engine.Input
{
    /// <summary>
    /// This class may be used to get an event driven access to user mouse interaction.
    /// 
    /// Upon creation, this class registers itself as a service with the game it
    /// is created for, so it can be accessed by any other component.
    /// </summary>
    public sealed class MouseInputManager : GameComponent, IMouseInputManager
    {
        #region Events

        /// <summary>
        /// Fired when a mouse button is pressed.
        /// </summary>
        public event EventHandler<EventArgs> Pressed;

        /// <summary>
        /// Fired when a mouse button is released.
        /// </summary>
        public event EventHandler<EventArgs> Released;

        /// <summary>
        /// Fired when the scroll wheel is scrolled.
        /// </summary>
        public event EventHandler<EventArgs> Scrolled;

        /// <summary>
        /// Fired when the mouse moves.
        /// </summary>
        public event EventHandler<EventArgs> Moved;
        
        #endregion
        
        #region Fields

        /// <summary>
        /// State from the last update, to check for changes.
        /// </summary>
        private MouseState previousState;

        #endregion

        /// <summary>
        /// Creates a new mouse manager for the given game and adds it as a service.
        /// </summary>
        /// <param name="game">the game to create the manager for.</param>
        public MouseInputManager(Game game)
            : base(game)
        {
            game.Services.AddService(typeof(IMouseInputManager), this);
        }
        
        #region Logic

        /// <summary>
        /// Implements key press / repeat / release logic.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            // Get a shortcut to the current keyboard state.
            var currentState = Mouse.GetState();

            if (previousState != null)
            {
                // Check for pressed / released events.
                if (currentState.LeftButton != previousState.LeftButton)
                {
                    if (currentState.LeftButton == ButtonState.Pressed)
                    {
                        OnPressed(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Left, ButtonState.Pressed));
                    }
                    else
                    {
                        OnReleased(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Left, ButtonState.Released));
                    }
                }
                if (currentState.RightButton != previousState.RightButton)
                {
                    if (currentState.RightButton == ButtonState.Pressed)
                    {
                        OnPressed(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Right, ButtonState.Pressed));
                    }
                    else
                    {
                        OnReleased(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Right, ButtonState.Released));
                    }
                }
                if (currentState.MiddleButton != previousState.MiddleButton)
                {
                    if (currentState.MiddleButton == ButtonState.Pressed)
                    {
                        OnPressed(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Middle, ButtonState.Pressed));
                    }
                    else
                    {
                        OnReleased(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Middle, ButtonState.Released));
                    }
                }
                if (currentState.XButton1 != previousState.XButton1)
                {
                    if (currentState.XButton1 == ButtonState.Pressed)
                    {
                        OnPressed(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Extra1, ButtonState.Pressed));
                    }
                    else
                    {
                        OnReleased(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Extra1, ButtonState.Released));
                    }
                }
                if (currentState.XButton2 != previousState.XButton2)
                {
                    if (currentState.XButton2 == ButtonState.Pressed)
                    {
                        OnPressed(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Extra2, ButtonState.Pressed));
                    }
                    else
                    {
                        OnReleased(new MouseInputEventArgs(currentState, MouseInputEventArgs.MouseButton.Extra2, ButtonState.Released));
                    }
                }

                // Check for scroll wheel.
                if (currentState.ScrollWheelValue != previousState.ScrollWheelValue)
                {
                    OnScrolled(new MouseInputEventArgs(currentState, previousState.ScrollWheelValue - currentState.ScrollWheelValue));
                }

                // Check for mouse movement.
                if (currentState.X != previousState.X || currentState.Y != previousState.Y)
                {
                    OnMoved(new MouseInputEventArgs(currentState, currentState.X, currentState.Y, previousState.X - currentState.X, previousState.Y - currentState.Y));
                }
            }
            previousState = currentState;

            base.Update(gameTime);
        }

        private void OnPressed(MouseInputEventArgs e)
        {
            if (Pressed != null)
            {
                Pressed(this, e);
            }
        }

        private void OnReleased(MouseInputEventArgs e)
        {
            if (Released != null)
            {
                Released(this, e);
            }
        }

        private void OnScrolled(MouseInputEventArgs e)
        {
            if (Scrolled != null)
            {
                Scrolled(this, e);
            }
        }

        private void OnMoved(MouseInputEventArgs e)
        {
            if (Moved != null)
            {
                Moved(this, e);
            }
        }

        #endregion
    }
}