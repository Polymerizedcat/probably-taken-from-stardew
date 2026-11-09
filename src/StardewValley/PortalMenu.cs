using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace DeepSpacePortal
{
    /// <summary>
    /// The portal container. Closing the lid ships everything inside.
    ///
    /// Built with the same constructor arguments as Chest.showChest so the grid is laid out,
    /// highlighted and clickable exactly like a vanilla chest:
    ///  - showReceivingMenu=true   -> the container grid is drawn at all
    ///  - highlightAllItems        -> slots hover/accept the cursor
    ///  - behaviorOnItemSelect     -> the deposit path (without it items never leave the cursor)
    /// </summary>
    public class PortalMenu : ItemGrabMenu
    {
        public const string Hint =
            "深空当铺传送门 —— 放进要卖的东西，关掉箱盖（或按 F）即发货到店里。";

        private readonly IList<Item> container;

        public PortalMenu(ModEntry mod, IList<Item> items, SObject context)
            : base(items,
                   reverseGrab: false,
                   showReceivingMenu: true,
                   highlightFunction: InventoryMenu.highlightAllItems,
                   behaviorOnItemSelectFunction: (item, who) => DepositHeldItem(items, item, who),
                   message: Hint,
                   behaviorOnItemGrab: null,
                   snapToBottom: false,
                   canBeExitedWithKey: true,
                   playRightClickSound: true,
                   allowRightClick: true,
                   showOrganizeButton: true,
                   source: 0,
                   sourceItem: context,
                   whichSpecialButton: -1,
                   context: context)
        {
            this.Mod = mod;
            this.container = items;
        }

        public ModEntry Mod { get; }

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            if (key == Microsoft.Xna.Framework.Input.Keys.F)
            {
                // Closing the menu triggers the actual shipment (MenuChanged handler).
                this.exitThisMenu();
                return;
            }
            base.receiveKeyPress(key);
        }

        /// <summary>
        /// Deposit the item the player is holding into the container.
        /// Mirrors <c>Chest.grabItemFromInventory</c> (minus the chest-only ShowMenu()).
        /// </summary>
        private static void DepositHeldItem(IList<Item> slots, Item item, Farmer who)
        {
            if (item == null || who == null) return;
            if (item.Stack <= 0) item.Stack = 1;

            Item leftover = AddToContainer(slots, item);
            if (leftover == null)
            {
                who.removeItemFromInventory(item);
            }
            else if (!ReferenceEquals(leftover, item))
            {
                who.addItemToInventory(leftover);
            }

            if (Game1.activeClickableMenu is ItemGrabMenu menu)
            {
                menu.heldItem = leftover;
            }
        }

        /// <summary>Stack onto a matching slot first, then take the first free slot.</summary>
        private static Item AddToContainer(IList<Item> slots, Item item)
        {
            item.resetState();

            for (int i = 0; i < slots.Count; i++)
            {
                Item slot = slots[i];
                if (slot == null || !slot.canStackWith(item)) continue;

                int overflow = item.Stack - slot.addToStack(item);
                if (item.ConsumeStack(overflow) == null) return null;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null) continue;
                slots[i] = item;
                return null;
            }

            return item;
        }
    }
}
