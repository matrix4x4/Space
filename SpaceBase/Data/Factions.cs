﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Space.Data
{
    /// <summary>A list of all factions in the game.</summary>
    [Flags]
    public enum Factions
    {
        /// <summary>No valid faction.</summary>
        None = 0,

        /// <summary>Fixed world entities such as suns, black holes, etc.</summary>
        Nature = 1 << 0,

        /// <summary>Projectiles have their own group to allow "allying" them, which avoid inter-projectile collisions.</summary>
        Projectiles = 1 << 1,

        /// <summary>Same concept as with projectiles, we want to put shields into one collision group (together with ships).</summary>
        Shields = 1 << 2,

        /// <summary>A neutral faction that will always appear neutral to all other factions.</summary>
        Neutral = 1 << 3,

        /// <summary>Fraction one.</summary>
        NPCFactionA = 1 << 4,

        /// <summary>Fraction two.</summary>
        NPCFactionB = 1 << 5,

        /// <summary>Fraction two.</summary>
        NPCFactionC = 1 << 6,

        /// <summary>Player one.</summary>
        Player1 = 1 << 7,

        /// <summary>Player two.</summary>
        Player2 = 1 << 8,

        /// <summary>Player three.</summary>
        Player3 = 1 << 9,

        /// <summary>Player four.</summary>
        Player4 = 1 << 10,

        /// <summary>Player five.</summary>
        Player5 = 1 << 11,

        /// <summary>Player six.</summary>
        Player6 = 1 << 12,

        /// <summary>Player seven.</summary>
        Player7 = 1 << 13,

        /// <summary>Player eight.</summary>
        Player8 = 1 << 14,

        /// <summary>Player nine.</summary>
        Player9 = 1 << 15,

        /// <summary>Player ten.</summary>
        Player10 = 1 << 16,

        /// <summary>Player eleven.</summary>
        Player11 = 1 << 17,

        /// <summary>Player twelve.</summary>
        Player12 = 1 << 18,

        /// <summary>Always represents the last entry, for masking when inverting.</summary>
        /// <remarks>Make sure to update this when adding or removing entries.</remarks>
        End = Player12,

        /// <summary>Compound group for all players.</summary>
        Players = Player1 | Player2 | Player3 | Player4 | Player5 | Player6 | Player7 | Player8 | Player9 | Player10 | Player11 | Player12
    }

    #region Conversion utils

    public static class FactionsExtension
    {
        #region Lookup tables

        private static readonly Dictionary<Factions, int> FactionToPlayerNumber = new Dictionary<Factions, int>
        {
            {Factions.Player1, 0},
            {Factions.Player2, 1},
            {Factions.Player3, 2},
            {Factions.Player4, 3},
            {Factions.Player5, 4},
            {Factions.Player6, 5},
            {Factions.Player7, 6},
            {Factions.Player8, 7},
            {Factions.Player9, 8},
            {Factions.Player10, 9},
            {Factions.Player11, 10},
            {Factions.Player12, 11}
        };

        private static readonly Dictionary<Factions, Color> FactionToColor = new Dictionary<Factions, Color>
        {
            {Factions.Player1, Color.Red},
            {Factions.Player2, Color.Blue},
            {Factions.Player3, Color.Yellow},
            {Factions.Player4, Color.Green},
            {Factions.Player5, Color.Orange},
            {Factions.Player6, Color.Purple},
            {Factions.Player7, Color.Turquoise},
            {Factions.Player8, Color.Pink},
            {Factions.Player9, Color.LightCyan},
            {Factions.Player10, Color.Olive},
            {Factions.Player11, Color.Navy},
            {Factions.Player12, Color.DarkRed},
            {Factions.NPCFactionA, Color.Crimson},
            {Factions.NPCFactionB, Color.DarkBlue},
            {Factions.NPCFactionC, Color.DarkGreen},
            {Factions.Neutral, Color.White}
        };

        private static readonly Factions[] PlayerNumberToFaction = new[]
        {
            Factions.Player1,
            Factions.Player2,
            Factions.Player3,
            Factions.Player4,
            Factions.Player5,
            Factions.Player6,
            Factions.Player7,
            Factions.Player8,
            Factions.Player9,
            Factions.Player10,
            Factions.Player11,
            Factions.Player12
        };

        #endregion

        #region Methods

        /// <summary>
        ///     Gets the inverse of a list of factions, i.e. all factions
        ///     <em>not</em> present in the list.
        /// </summary>
        /// <param name="factions">The factions for which to get the inverse.</param>
        /// <returns>The list of factions not in the given group.</returns>
        public static Factions Inverse(this Factions factions)
        {
            return (Factions) (~(uint) factions & (uint) Factions.End);
        }

        /// <summary>Convert the specified faction to a player number.</summary>
        /// <param name="faction">The faction to convert.</param>
        /// <returns>The player number the faction represents.</returns>
        public static int ToPlayerNumber(this Factions faction)
        {
            if (!FactionToPlayerNumber.ContainsKey(faction))
            {
                throw new ArgumentException("faction");
            }
            return FactionToPlayerNumber[faction];
        }

        /// <summary>Convert the specified faction to a color.</summary>
        /// <param name="faction">The faction to convert.</param>
        /// <returns>The color for the faction.</returns>
        public static Color ToColor(this Factions faction)
        {
            if (!FactionToColor.ContainsKey(faction))
            {
                throw new ArgumentException("faction");
            }
            return FactionToColor[faction];
        }

        /// <summary>Converts one or multiple factions to the collision group they belong to (won't be checked against each other).</summary>
        /// <param name="factions">The factions to convert.</param>
        /// <returns>The collision group.</returns>
        public static uint ToCollisionGroup(this Factions factions)
        {
            return (uint) factions;
        }

        /// <summary>Checks if the given faction represents a player and nothing else.</summary>
        /// <param name="factions">The faction to check.</param>
        /// <returns></returns>
        public static bool IsPlayerNumber(this Factions factions)
        {
            return (NumberOfSetBits((int) factions) == 1) &&
                   factions >= Factions.Player1 &&
                   factions <= Factions.Player12;
        }

        /// <summary>Checks if this faction is allied to the specified faction.</summary>
        /// <param name="factions">The first faction to check.</param>
        /// <param name="other">The other faction to check.</param>
        /// <returns>Whether the two factions are allied.</returns>
        public static bool IsAlliedTo(this Factions factions, Factions other)
        {
            return (factions & other) != 0;
        }

        /// <summary>Convert the specified player number to the player's faction.</summary>
        /// <param name="playerNumber">The player number to convert.</param>
        /// <returns>The faction representing that player.</returns>
        public static Factions ToFaction(this int playerNumber)
        {
            if (playerNumber < 0 || playerNumber >= PlayerNumberToFaction.Length)
            {
                throw new ArgumentException("playerNumber");
            }
            return PlayerNumberToFaction[playerNumber];
        }

        #endregion

        #region Utils

        /// <summary>Magic.</summary>
        /// <see cref="http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel"/>
        /// <param name="i">The int of which to count the bits.</param>
        /// <returns>The number of bits in the int.</returns>
        private static int NumberOfSetBits(int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }

        #endregion
    }

    #endregion
}