﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Engine.Util;
using Microsoft.Xna.Framework.Input;

namespace Space
{
    /// <summary>
    /// All the game settings that can be changed and saved. Also provides
    /// utility methods for saving to and loading from XML files.
    /// </summary>
    public class Settings
    {
        #region Types

        /// <summary>
        /// Possible commands in the game's menu.
        /// </summary>
        public enum MenuCommand
        {
            /// <summary>
            /// Move one entry up.
            /// </summary>
            Up,

            /// <summary>
            /// Move one entry down.
            /// </summary>
            Down,

            /// <summary>
            /// Toggle to the next option.
            /// </summary>
            Next,

            /// <summary>
            /// Toggle to the previous option.
            /// </summary>
            Previous,

            /// <summary>
            /// Select an entry or confirm option.
            /// </summary>
            Select,

            /// <summary>
            /// Go back in the menu, or abort editing (text fields).
            /// </summary>
            Back,

            /// <summary>
            /// Pause the game to open the ingame menu.
            /// </summary>
            Pause,

            /// <summary>
            /// Open up the console.
            /// </summary>
            Console
        }

        /// <summary>
        /// Possible command type in the game (ship control essentially).
        /// </summary>
        public enum GameCommand
        {
            /// <summary>
            /// Accelerate up.
            /// </summary>
            Up,

            /// <summary>
            /// Accelerate down.
            /// </summary>
            Down,

            /// <summary>
            /// Accelerate left.
            /// </summary>
            Left,

            /// <summary>
            /// Accelerate right.
            /// </summary>
            Right,

            /// <summary>
            /// Use object in game (e.g. space station for trading).
            /// </summary>
            Use,

            /// <summary>
            /// Stabilize the ship's position.
            /// </summary>
            Stabilize
        }

        #endregion

        #region Constants

        public static readonly Dictionary<Keys, MenuCommand> DefaultMenuBindings = new Dictionary<Keys, MenuCommand>()
        {
            { Keys.Up, MenuCommand.Up },
            { Keys.W, MenuCommand.Up },
            { Keys.S, MenuCommand.Down },
            { Keys.Down, MenuCommand.Down},
            { Keys.A, MenuCommand.Previous },
            { Keys.Left, MenuCommand.Previous },
            { Keys.D, MenuCommand.Next },
            { Keys.Right, MenuCommand.Next },
            { Keys.E, MenuCommand.Select },
            { Keys.Enter, MenuCommand.Select },
            { Keys.Back, MenuCommand.Back },
            { Keys.Escape, MenuCommand.Back },
            { Keys.Pause, MenuCommand.Pause },
            { Keys.F10, MenuCommand.Pause },
            { Keys.OemTilde, MenuCommand.Console }
        };

        public static readonly Dictionary<Keys, GameCommand> DefaultGameBindings = new Dictionary<Keys, GameCommand>()
        {
            { Keys.W, GameCommand.Up },
            { Keys.Up, GameCommand.Up },
            { Keys.S, GameCommand.Down },
            { Keys.Down, GameCommand.Down },
            { Keys.A, GameCommand.Left },
            { Keys.Left, GameCommand.Left },
            { Keys.D, GameCommand.Right },
            { Keys.Right, GameCommand.Right },
            { Keys.E, GameCommand.Use },
            { Keys.Enter, GameCommand.Use },
            { Keys.LeftShift, GameCommand.Stabilize },
            { Keys.RightShift, GameCommand.Stabilize }
        };

        #endregion

        #region Display

        /// <summary>
        /// The horizontal display resolution to use.
        /// </summary>
        public int ScreenWidth = 1280;

        /// <summary>
        /// The vertical display resolution to use.
        /// </summary>
        public int ScreenHeight = 720;

        /// <summary>
        /// Run full screen mode or not.
        /// </summary>
        public bool Fullscreen = false;

        /// <summary>
        /// Whether to enable post processing effects.
        /// </summary>
        public bool PostProcessing = true;

        #endregion

        #region Interface

        /// <summary>
        /// The locale to use for localized content.
        /// </summary>
        public string Language = "en";

        #endregion

        #region Miscellaneous

        /// <summary>
        /// The Name of the Player
        /// </summary>
        public string PlayerName = "Player";

        /// <summary>
        /// Address of the last server we tried to connect to.
        /// </summary>
        public string LastServerAddress = "127.0.0.1";

        /// <summary>
        /// Autoexec file, contains console commands to automatically execute
        /// after joining a game.
        /// </summary>
        public string AutoexecFilename = "autoexec.cfg";

        /// <summary>
        /// Compute the distance displayed in radar icons as the distance of
        /// the object to the screen edge instead of to the player ship.
        /// </summary>
        public bool RadarDistanceFromBorder = true;

        #endregion

        #region Input

        /// <summary>
        /// Whether to toggle stabilizer functionality or keep it active only
        /// while the key is pressed.
        /// </summary>
        public bool ToggleStabilize = false;

        /// <summary>
        /// Key bindings for menu control as set by the player.
        /// </summary>
        public SerializableDictionary<Keys, MenuCommand> MenuBindings = new SerializableDictionary<Keys, MenuCommand>(DefaultMenuBindings);

        /// <summary>
        /// Key bindings for in game ship control as set by the player.
        /// </summary>
        /// <remarks>
        /// Make sure to call <c>UpdateInversGameBindings</c> after modifying
        /// the <c>GameBindings</c>.
        /// </remarks>
        public SerializableDictionary<Keys, GameCommand> GameBindings = new SerializableDictionary<Keys, GameCommand>(DefaultGameBindings);

        /// <summary>
        /// Inverse game key bindings, mapping commands to keys. This is used
        /// when looking up whether an action should be taken based on the
        /// current keyboard state (as opposed to reacting to a key press
        /// event).
        /// </summary>
        /// <remarks>
        /// Make sure to call <c>UpdateInversGameBindings</c> after modifying
        /// the <c>GameBindings</c>.
        /// </remarks>
        [XmlIgnore]
        public Dictionary<GameCommand, Keys[]> InverseGameBindings;

        /// <summary>
        /// Updates the inverse game key bindings.
        /// </summary>
        private void UpdateInverseGameBindings()
        {
            InverseGameBindings = BuildInverseGameBindings();
        }

        private Dictionary<GameCommand, Keys[]> BuildInverseGameBindings()
        {
            var buffer = new Dictionary<GameCommand, List<Keys>>();

            foreach (var item in GameBindings)
            {
                if (!buffer.ContainsKey(item.Value))
                {
                    buffer.Add(item.Value, new List<Keys>());
                }
                buffer[item.Value].Add(item.Key);
            }

            var result = new Dictionary<GameCommand, Keys[]>();
            foreach (var item in buffer)
            {
                result.Add(item.Key, item.Value.ToArray());
            }
            
            return result;
        }

        #endregion

        #region Save / Load / Singleton

        /// <summary>
        /// Save all current values to a file with the given name.
        /// </summary>
        /// <param name="filename">the path to the file to save the settings to.</param>
        public static void Save(string filename)
        {
            try
            {
                using (Stream stream = File.Create(filename))
                {
                    // Produce minimal XML, so strip away the <?xml ... ?> header.
                    using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true }))
                    {
                        // And strip away namespaces, we don't want those.
                        new XmlSerializer(typeof(Settings)).Serialize(writer, _instance, new XmlSerializerNamespaces(new XmlSerializerNamespaces(new[] { new XmlQualifiedName("", "") })));
                    }
                }
            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// Load settings from a file. This will invalidate any old references
        /// to <c>Settings.Instance</c>. It will also overwrite <em>all</em> settings,
        /// not just the ones loaded from the file. For all fields not set, the default
        /// values will be restored.
        /// </summary>
        /// <param name="filename">the path to the file to load the settings from.</param>
        public static void Load(string filename)
        {
            if (File.Exists(filename))
            {
                try
                {
                    using (Stream stream = File.OpenRead(filename))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                        _instance = (Settings)serializer.Deserialize(stream);
                    }
                }
                catch (IOException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        /// <summary>
        /// The singleton instance of the settings class.
        /// </summary>
        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Settings();
                }
                return _instance;
            }
        }

        /// <summary>
        /// The actual current instance.
        /// </summary>
        private static Settings _instance;

        /// <summary>
        /// Singleton enforcement.
        /// </summary>
        private Settings()
        {
            UpdateInverseGameBindings();
        }

        #endregion
    }
}
