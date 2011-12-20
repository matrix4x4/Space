﻿using Engine.Commands;
using Engine.ComponentSystem.Entities;
using Engine.ComponentSystem.Systems;
using Engine.Util;
using Space.Commands;
using Space.ComponentSystem.Components;

namespace Space.Control
{
    static class GameCommandHandler
    {
        public static void HandleCommand(ICommand command, IEntityManager manager)
        {
            switch ((GameCommandType)command.Type)
            {
                case GameCommandType.PlayerInput:
                    // Player input command, apply it.
                    {
                        IEntity avatar = manager.SystemManager.GetSystem<AvatarSystem>().GetAvatar(command.PlayerNumber);
                        if (avatar != null)
                        {
                            var input = avatar.GetComponent<ShipControl>();

                            // What did he do?
                            var inputCommand = (PlayerInputCommand)command;
                            switch (inputCommand.Input)
                            {
                                // Start accelerating in the given direction.
                                case PlayerInputCommand.PlayerInput.AccelerateUp:
                                    input.Accelerate(Directions.North);
                                    break;
                                case PlayerInputCommand.PlayerInput.AccelerateRight:
                                    input.Accelerate(Directions.East);
                                    break;
                                case PlayerInputCommand.PlayerInput.AccelerateDown:
                                    input.Accelerate(Directions.South);
                                    break;
                                case PlayerInputCommand.PlayerInput.AccelerateLeft:
                                    input.Accelerate(Directions.West);
                                    break;

                                // Stop accelerating in the given direction.
                                case PlayerInputCommand.PlayerInput.StopUp:
                                    input.StopAccelerate(Directions.North);
                                    break;
                                case PlayerInputCommand.PlayerInput.StopRight:
                                    input.StopAccelerate(Directions.East);
                                    break;
                                case PlayerInputCommand.PlayerInput.StopDown:
                                    input.StopAccelerate(Directions.South);
                                    break;
                                case PlayerInputCommand.PlayerInput.StopLeft:
                                    input.StopAccelerate(Directions.West);
                                    break;

                                // Begin rotating.
                                case PlayerInputCommand.PlayerInput.Rotate:
                                    input.TargetRotation = inputCommand.TargetRotation;
                                    break;

                                // Begin/stop shooting.
                                case PlayerInputCommand.PlayerInput.Shoot:
                                    input.IsShooting = true;
                                    break;
                                case PlayerInputCommand.PlayerInput.CeaseFire:
                                    input.IsShooting = false;
                                    break;
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }
}