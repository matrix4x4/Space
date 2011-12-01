﻿using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Engine.Input
{
    /// <summary>
    /// This class may be used to get an event driven access to user key presses.
    /// 
    /// Upon creation, this class registers itself as a service with the game it
    /// is created for, so it can be accessed by any other component.
    /// </summary>
    public sealed class KeyboardInputManager : GameComponent, IKeyboardInputManager
    {
        #region Events

        /// <summary>
        /// Fired when a key is newly pressed, or is repeated as set via RepeatDelay/RepeatRate.
        /// </summary>
        public event EventHandler<EventArgs> Pressed;

        /// <summary>
        /// Fired when a key is released.
        /// </summary>
        public event EventHandler<EventArgs> Released;

        #endregion

        #region Properties

        /// <summary>
        /// The delay before a key start to trigger repeatedly.
        /// </summary>
        public int RepeatDelay { get; set; }

        /// <summary>
        /// The delay between triggering when triggering repeatedly (key held down).
        /// </summary>
        public int RepeatRate { get; set; }

        #endregion

        #region Fields

        private Keys lastPressedKey;
        private DateTime lastRepeat;
        private KeyboardState previousState;

        #endregion

        /// <summary>
        /// Creates a new keyboard manager for the given game and adds it as a service.
        /// </summary>
        /// <param name="game">the game to create the manager for.</param>
        public KeyboardInputManager(Game game)
            : base(game)
        {
            RepeatDelay = 300;
            RepeatRate = 40;

            game.Services.AddService(typeof(IKeyboardInputManager), this);
        }

        #region Logic

        /// <summary>
        /// Implements key press / repeat / release logic.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            // Get a shortcut to the current keyboard state.
            var currentState = Keyboard.GetState();

            if (previousState != null)
            {
                // Active keyboard modifier combo.
                KeyModifier modifier = KeyModifier.None;
                if (currentState.GetPressedKeys().Contains(Keys.LeftAlt) ||
                    currentState.GetPressedKeys().Contains(Keys.RightAlt))
                {
                    modifier |= KeyModifier.Alt;
                }
                if (currentState.GetPressedKeys().Contains(Keys.LeftControl) ||
                    currentState.GetPressedKeys().Contains(Keys.RightControl))
                {
                    modifier |= KeyModifier.Control;
                }
                if (currentState.GetPressedKeys().Contains(Keys.LeftShift) ||
                    currentState.GetPressedKeys().Contains(Keys.RightShift))
                {
                    modifier |= KeyModifier.Shift;
                }

                // The current time, used to check for repeats.
                var currentTime = DateTime.Now;

                // Test each key that's down.
                foreach (var key in currentState.GetPressedKeys())
                {
                    if (previousState.GetPressedKeys().Contains(key))
                    {
                        // Key was held.
                        if (RepeatDelay > 0 && RepeatRate > 0 && lastRepeat != null)
                        {
                            if (key == lastPressedKey && new TimeSpan(currentTime.Ticks - lastRepeat.Ticks).TotalMilliseconds > RepeatRate)
                            {
                                lastRepeat = currentTime;
                                OnPressed(new KeyboardInputEventArgs(key, modifier));
                            }
                        }
                    }
                    else
                    {
                        // Key was pressed.
                        if (RepeatDelay > 0 && RepeatRate > 0)
                        {
                            lastPressedKey = key;
                            lastRepeat = currentTime.AddMilliseconds(RepeatDelay);
                        }
                        OnPressed(new KeyboardInputEventArgs(key, modifier));
                    }
                }

                // Test all keys that were released.
                foreach (var key in previousState.GetPressedKeys().Except(currentState.GetPressedKeys()))
                {
                    // Key was released.
                    if (key == lastPressedKey)
                    {
                        lastPressedKey = Keys.None;
                    }
                    OnReleased(new KeyboardInputEventArgs(key, modifier));
                }
            }

            previousState = currentState;

            base.Update(gameTime);
        }

        private void OnPressed(KeyboardInputEventArgs e)
        {
            if (Pressed != null)
            {
                Pressed(this, e);
            }
        }

        private void OnReleased(KeyboardInputEventArgs e)
        {
            if (Released != null)
            {
                Released(this, e);
            }
        }

        #endregion
    }
}
