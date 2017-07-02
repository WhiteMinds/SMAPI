﻿using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TrainerMod.Framework.ItemData;

namespace TrainerMod.Framework.Commands.Player
{
    /// <summary>A command which list items available to spawn.</summary>
    internal class ListItemsCommand : TrainerCommand
    {
        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        public ListItemsCommand()
            : base("list_items", "Lists and searches items in the game data.\n\nUsage: list_items [search]\n- search (optional): an arbitrary search string to filter by.") { }

        /// <summary>Handle the command.</summary>
        /// <param name="monitor">Writes messages to the console and log file.</param>
        /// <param name="command">The command name.</param>
        /// <param name="args">The command arguments.</param>
        public override void Handle(IMonitor monitor, string command, ArgumentParser args)
        {
            var matches = this.GetItems(args.ToArray()).ToArray();

            // show matches
            string summary = "Searching...\n";
            if (matches.Any())
                monitor.Log(summary + this.GetTableString(matches, new[] { "type", "id", "name" }, val => new[] { val.Type.ToString(), val.ID.ToString(), val.Name }), LogLevel.Info);
            else
                monitor.Log(summary + "No items found", LogLevel.Info);
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get all items which can be searched and added to the player's inventory through the console.</summary>
        /// <param name="searchWords">The search string to find.</param>
        private IEnumerable<ISearchItem> GetItems(string[] searchWords)
        {
            // normalise search term
            searchWords = searchWords?.Where(word => !string.IsNullOrWhiteSpace(word)).ToArray();
            if (searchWords?.Any() == false)
                searchWords = null;

            // find matches
            return (
                from item in this.GetItems()
                let term = $"{item.ID}|{item.Type}|{item.Name}"
                where searchWords == null || searchWords.All(word => term.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) != -1)
                select item
            );
        }

        /// <summary>Get all items which can be searched and added to the player's inventory through the console.</summary>
        private IEnumerable<ISearchItem> GetItems()
        {
            // objects
            foreach (int id in Game1.objectInformation.Keys)
            {
                ISearchItem obj = id >= Ring.ringLowerIndexRange && id <= Ring.ringUpperIndexRange
                    ? new SearchableRing(id)
                    : (ISearchItem)new SearchableObject(id);
                if (obj.IsValid)
                    yield return obj;
            }

            // weapons
            foreach (int id in Game1.content.Load<Dictionary<int, string>>("Data\\weapons").Keys)
            {
                ISearchItem weapon = new SearchableWeapon(id);
                if (weapon.IsValid)
                    yield return weapon;
            }
        }
    }
}
