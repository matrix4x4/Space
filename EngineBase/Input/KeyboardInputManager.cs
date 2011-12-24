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

        /// <summary>
        /// The last key we pressed, for repeats.
        /// </summary>
        private Keys _lastPressedKey;

        /// <summary>
        /// Last time we sent a repeated key press.
        /// </summary>
        private DateTime _lastRepeat;

        /// <summary>
        /// State from the last update, to check for changes.
        /// </summary>
        private KeyboardState _previousState;

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

        /// <summary>
        /// Get a representation for a specific key-combination, which can be used
        /// to register for events on specific combinations of keys only.
        /// </summary>
        /// <param name="key">the key that has to be pressed.</param>
        /// <param name="modifier">the modifier that has to be active.</param>
        /// <returns>an object that represents this keyboard combination.</returns>
        public KeyCombo Combo(Keys key, KeyModifier modifier)
        {
            return new KeyCombo(this, new[] { key }, modifier);
        }

        /// <summary>
        /// Get a representation for a specific key-combination, which can be used
        /// to register for events on specific combinations of keys only.
        /// </summary>
        /// <param name="keys">the list of keys that have to be pressed.</param>
        /// <returns>an object that represents this keyboard combination.</returns>
        public KeyCombo Combo(Keys[] keys)
        {
            return Combo(keys, KeyModifier.None);
        }

        /// <summary>
        /// Get a representation for a specific key-combination, which can be used
        /// to register for events on specific combinations of keys only.
        /// </summary>
        /// <param name="keys">the list of keys that have to be pressed.</param>
        /// <param name="modifier">the modifier that has to be active.</param>
        /// <returns>an object that represents this keyboard combination.</returns>
        public KeyCombo Combo(Keys[] keys, KeyModifier modifier)
        {
            return new KeyCombo(this, keys, modifier);
        }

        #region Logic

        /// <summary>
        /// Implements key press / repeat / release logic.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Do not handle input, if the game is not the active window.
            if (!Game.IsActive)
            {
                return;
            }

            // Get a shortcut to the current keyboard state.
            var currentState = Keyboard.GetState();

            if (_previousState != null)
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
                    if (_previousState.GetPressedKeys().Contains(key))
                    {
                        // Key was held.
                        if (RepeatDelay > 0 && RepeatRate > 0 && _lastRepeat != null)
                        {
                            if (key == _lastPressedKey && new TimeSpan(currentTime.Ticks - _lastRepeat.Ticks).TotalMilliseconds > RepeatRate)
                            {
                                _lastRepeat = currentTime;
                                OnPressed(new KeyboardInputEventArgs(currentState, key, modifier, true));
                            }
                        }
                    }
                    else
                    {
                        // Key was pressed.
                        if (RepeatDelay > 0 && RepeatRate > 0)
                        {
                            _lastPressedKey = key;
                            _lastRepeat = currentTime.AddMilliseconds(RepeatDelay);
                        }
                        OnPressed(new KeyboardInputEventArgs(currentState, key, modifier, false));
                    }
                }

                // Test all keys that were released.
                foreach (var key in _previousState.GetPressedKeys().Except(currentState.GetPressedKeys()))
                {
                    // Key was released.
                    if (key == _lastPressedKey)
                    {
                        _lastPressedKey = Keys.None;
                    }
                    OnReleased(new KeyboardInputEventArgs(currentState, key, modifier, false));
                }
            }

            _previousState = currentState;
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