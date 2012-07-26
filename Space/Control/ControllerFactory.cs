﻿using Engine.ComponentSystem;
using Engine.ComponentSystem.Common.Systems;
using Engine.ComponentSystem.RPG.Systems;
using Engine.ComponentSystem.Systems;
using Engine.Controller;
using Engine.Session;
using Engine.Simulation.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Space.ComponentSystem.Systems;
using Space.Data;
using Space.Session;
using Space.Simulation.Commands;
using Space.Util;

namespace Space.Control
{
    /// <summary>
    /// Utility class for creating new game server and client instances.
    /// </summary>
    internal static class ControllerFactory
    {
        /// <summary>
        /// Creates a new game server.
        /// </summary>
        /// <param name="game">The game to create the game for.</param>
        /// <returns>A new server.</returns>
        public static ISimulationController<IServerSession> CreateServer(Game game)
        {
            // Create actual controller.
            var controller = new SimpleServerController<Profile>(7777, 12, SpaceCommandHandler.HandleCommand);

            // Add all systems we need in our game as a server.
            AddSpaceServerSystems(controller.Simulation.Manager, game);

            // Done.
            return controller;
        }

        /// <summary>
        /// Creates a new client that can be used to join remote games.
        /// </summary>
        /// <param name="game">The game to create the client for.</param>
        /// <returns>A new client.</returns>
        public static IClientController<FrameCommand> CreateRemoteClient(Game game)
        {
            // Create actual controller.
            var controller = new SimpleClientController<Profile>(SpaceCommandHandler.HandleCommand);

            // Needed by some systems. Add all systems we need in our game as a client.
            AddSpaceServerSystems(controller.Simulation.Manager, game);
            AddSpaceClientSystems(controller.Simulation.Manager, game, controller.Session);

            // Done.
            return controller;
        }

        /// <summary>
        /// Creates a new client that will automatically connect to the given
        /// local server, and reuse the server's game state.
        /// </summary>
        /// <param name="game">The game to create the client for.</param>
        /// <param name="server">The server to couple the client with.</param>
        /// <returns>A new client.</returns>
        public static IClientController<FrameCommand> CreateLocalClient(Game game, ISimulationController<IServerSession> server)
        {
            // Create actual controller.
            var controller = new ThinClientController<Profile>(server, Settings.Instance.PlayerName, (Profile)Settings.Instance.CurrentProfile);

            // Check if the server has all the services we need (enough to
            // check for one, because we only add all at once -- here).
            if (server.Simulation.Manager.GetSystem<CameraSystem>() == null)
            {
                // Needed by some systems. Add all systems we need in
                // *addition* to the ones the server already has.
                AddSpaceClientSystems(server.Simulation.Manager, game, controller.Session);
            }

            // Done.
            return controller;
        }

        /// <summary>
        /// Adds systems used by the server and the client.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="game">The game.</param>
        private static void AddSpaceServerSystems(IManager manager, Game game)
        {
            manager.AddSystems(
                new AbstractSystem[]
                {
                    // These systems have to be updated in a specific order to
                    // function as intended.

                    // Get rid of expired components as soon as we can.
                    new ExpirationSystem(),
                    
                    // Damager should react first when a collision happened.
                    new CollisionDamageSystem(),

                    // Purely reactive systems (only do stuff when receiving
                    // messages), but make sure we run them relatively early,
                    // because other systems may need their updates.
                    new AvatarSystem(),
                    new IndexSystem(30, 64),
                    new CharacterSystem<AttributeType>(),
                    new InventorySystem(), 
                    new StatusEffectSystem(),
                    new PlayerMassSystem(),
                    new ShipInfoSystem(),
                    new DetectableSystem(game.Content),
                    new DropSystem(game.Content),
                    new SpaceUsablesSystem(),
                    
                    // Update our universe before our spawn system, to give
                    // it a chance to generate cell information.
                    new UniverseSystem(),
                    new ShipSpawnSystem(),

                    // Friction has to be updated before acceleration is, to allow
                    // maximum speed to be reached.
                    new FrictionSystem(),

                    // Apply gravitation before ship control, to allow it to
                    // compensate for the gravitation.
                    new GravitationSystem(),

                    // Ship control must come first, but after stuff like gravitation,
                    // to be able to compute the stabilizer acceleration.
                    new ShipControlSystem(),

                    // Acceleration must come after ship control, due to it setting
                    // its value.
                    new AccelerationSystem(),

                    // Velocity must come after acceleration, so that all other forces
                    // already have been applied (gravitation).
                    new VelocitySystem(),
                    new SpinSystem(),
                    new EllipsePathSystem(),

                    // Check for collisions after positions have been updated.
                    new CollisionSystem(10),
                    
                    // Check which cells are active after updating positions.
                    new CellSystem(),
                    
                    // Update this system after updating the cell system, to
                    // make sure we give cells a chance to 'activate' before
                    // checking if there are entities inside them.
                    new DeathSystem(),

                    // Run weapon control after velocity, to spawn projectiles at the
                    // correct position.
                    new WeaponControlSystem(),

                    // Energy should be update after it was used, to give it a chance
                    // to regenerate (e.g. if we're using less than we produce this
                    // avoids always displaying slightly less than max). Same for health.
                    new RegeneratingValueSystem(),
                    
                    // AI should react after everything else had its turn.
                    new AISystem()
                });
        }

        /// <summary>
        /// Adds systems only used by the client.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="game">The game.</param>
        /// <param name="session">The session.</param>
        private static void AddSpaceClientSystems(IManager manager, Game game, IClientSession session)
        {
            var soundBank = (SoundBank)game.Services.GetService(typeof(SoundBank));
            var spriteBatch = (SpriteBatch)game.Services.GetService(typeof(SpriteBatch));
            var graphicsDevice = ((Program)game).GraphicsDeviceManager;

            manager.AddSystems(
                new AbstractSystem[]
                {
                    // Update camera first.
                    new CameraSystem(game, session),

                    // Handle sound.
                    new CameraCenteredSoundSystem(soundBank, session),
                    
                    // Mind the order: orbits below planets below suns below normal
                    // objects below particle effects below radar.
                    new OrbitRenderSystem(game.Content, spriteBatch, session),
                    new PlanetRenderSystem(game.Content, game.GraphicsDevice),
                    new SunRenderSystem(game.Content, game.GraphicsDevice, spriteBatch),
                    new CameraCenteredTextureRenderSystem(game.Content, spriteBatch),
                    new CameraCenteredParticleEffectSystem(game.Content, graphicsDevice),
                    new RadarRenderSystem(game.Content, spriteBatch, session)
                });
        }
    }
}
