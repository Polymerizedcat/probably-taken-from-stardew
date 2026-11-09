using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Extensions;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace DeepSpacePortal
{
    /// <summary>Stardew Valley side of the Probably Stolen portal bridge.</summary>
    public class ModEntry : Mod
    {
        public const string PortalId = "DeepSpacePortal_Portal";
        public const string PortalQualifiedId = "(BC)" + PortalId;
        public const string PortalTextureAsset = "Mods/NaHz.DeepSpacePortal/portal";
        public const string PortalGeodeAsset = "Mods/NaHz.DeepSpacePortal/portal_geode";
        public const string SaveKey = "portal";

        private string geodeId;

        internal string GeodeTextureFile => Path.Combine(this.Helper.DirectoryPath, "assets", "portal_geode.png");

        internal string InboxPath;
        internal string TexturesPath;
        internal float PriceMultiplier = 2f;
        internal bool BridgeFound;

        private readonly List<Item> storage = new List<Item>(new Item[27]);

        /// <summary>Categories the portal refuses, with the message shown in Stardew Valley.</summary>
        private static readonly Dictionary<int, string> RejectedCategories = new Dictionary<int, string>
        {
            { -99,  "在那边真的放得下吗" },
            { -101, "在那边真的放得下吗" },
            { -96,  "在那边真的放得下吗" },
            { -95,  "在那边真的放得下吗" },
            { -97,  "在那边真的放得下吗" },
            { -100, "在那边真的放得下吗" },
            { -24,  "在那边真的放得下吗" },
            { -9,   "在那边真的放得下吗" },
            { -102, "在那边真的放得下吗" },
            { -74,  "这种种子真的适合那边生长吗。" },
            { -19,  "星露谷肥料在那边真的有作用吗" },
            { -22,  "钓具还是留着自己用吧。" }
        };

        private PortalSave save;

        public override void Entry(IModHelper helper)
        {
            this.ReadBridgeConfig();

            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;

            helper.ConsoleCommands.Add("portal_info", "Shows the portal bridge paths.", this.CommandInfo);
            helper.ConsoleCommands.Add("portal_give", "Gives the player a Probably Stolen portal.", this.CommandGive);
            helper.ConsoleCommands.Add("portal_send", "Ships everything currently inside the portal.", this.CommandSend);
            helper.ConsoleCommands.Add("portal_test",
                "Ships one sample of every item category (for testing the pawnshop side).",
                this.CommandTestSamples);
            helper.ConsoleCommands.Add("portal_dump_food",
                "Writes stardew_food.csv and stardew_items.csv (all items + inventory footprint) to the pawnshop mod folder.",
                this.CommandDumpFood);
            helper.ConsoleCommands.Add("portal_add", "Puts an item into the portal (debug). Usage: portal_add <qualified id> [count]", this.CommandAdd);

            this.Monitor.Log($"bridge: inbox={this.InboxPath ?? "<unresolved>"} textures={this.TexturesPath ?? "<unresolved>"} ok={this.BridgeFound}", LogLevel.Info);
        }

        // ------------------------------------------------------------------ config

        private void ReadBridgeConfig()
        {
            try
            {
                string configPath = Path.Combine(this.Helper.DirectoryPath, "bridge.json");
                if (File.Exists(configPath))
                {
                    JObject root = JObject.Parse(File.ReadAllText(configPath, Encoding.UTF8));
                    this.InboxPath = (string)root["inboxPath"];
                    this.TexturesPath = (string)root["texturesPath"];
                    float? multiplier = root.Value<float?>("priceMultiplier");
                    if (multiplier.HasValue && multiplier.Value > 0f) this.PriceMultiplier = multiplier.Value;
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Could not read bridge.json: " + ex.Message, LogLevel.Warn);
            }

            if (string.IsNullOrEmpty(this.InboxPath) || string.IsNullOrEmpty(this.TexturesPath))
            {
                // Fallback: <steamapps>/common/Probably Stolen/Mods/StardewPortal
                string modsFolder = Path.GetDirectoryName(this.Helper.DirectoryPath);
                string svFolder = modsFolder != null ? Path.GetDirectoryName(modsFolder) : null;
                string common = svFolder != null ? Path.GetDirectoryName(svFolder) : null;
                if (common != null)
                {
                    string ps = Path.Combine(common, "Probably Stolen", "Mods", "StardewPortal");
                    this.InboxPath = Path.Combine(ps, "Data", "inbox.json");
                    this.TexturesPath = Path.Combine(ps, "Textures", "Items");
                }
            }

            this.BridgeFound = !string.IsNullOrEmpty(this.InboxPath);
        }

        // ------------------------------------------------------------------ content

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo("Data/BigCraftables"))
            {
                e.Edit(asset =>
                {
                    IDictionary<string, BigCraftableData> data = asset.AsDictionary<string, BigCraftableData>().Data;

                    // Reuse the vanilla "Standing Geode" art instead of shipping our own sprite.
                    BigCraftableData geode = null;
                    foreach (BigCraftableData candidate in data.Values)
                    {
                        if (candidate != null && string.Equals(candidate.Name, "Standing Geode", StringComparison.OrdinalIgnoreCase))
                        {
                            geode = candidate;
                            break;
                        }
                    }

                    data[PortalId] = new BigCraftableData
                    {
                        Name = PortalId,
                        DisplayName = "深空当铺传送门",
                        Description = "一个只会把货物送去深空当铺的单向传送门。把要卖的东西放进去，关掉箱盖即可发货。",
                        Texture = File.Exists(GeodeTextureFile) ? PortalGeodeAsset : PortalTextureAsset,
                        SpriteIndex = 0,
                        Price = 0,
                        Fragility = 0,
                        CanBePlacedOutdoors = true,
                        CanBePlacedIndoors = true,
                        IsLamp = false,
                        ContextTags = new List<string> { "placeable" }
                    };
                });
            }
            else if (e.Name.IsEquivalentTo(PortalGeodeAsset))
            {
                e.LoadFromModFile<Texture2D>("assets/portal_geode.png", AssetLoadPriority.Exclusive);
            }
            else if (e.Name.IsEquivalentTo(PortalTextureAsset))
            {
                e.LoadFromModFile<Texture2D>("assets/portal.png", AssetLoadPriority.Exclusive);
            }
        }

        // ------------------------------------------------------------------ save data

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            this.EnsurePortalTexture();

            this.save = this.Helper.Data.ReadSaveData<PortalSave>(SaveKey) ?? new PortalSave();

            this.save.Items = this.save.Items ?? new List<Item>();
            for (int i = 0; i < this.storage.Count; i++)
                this.storage[i] = i < this.save.Items.Count ? this.save.Items[i] : null;

            this.TryDumpFoodTable();

            if (!this.save.PortalGiven)
            {
                // Safety net: if the save data lost the flag but the item is still in the
                // backpack, don't hand out a second one.
                if (this.PlayerHasPortal())
                {
                    this.save.PortalGiven = true;
                    this.Helper.Data.WriteSaveData(SaveKey, this.save);
                }
                else
                {
                    bool gave = this.GivePortal();
                    bool placed = this.PlacePortalInFarmhouse();
                    if (gave || placed)
                    {
                        this.save.PortalGiven = true;
                        this.Helper.Data.WriteSaveData(SaveKey, this.save);
                    }
                }
            }
        }

        private void OnSaving(object sender, SavingEventArgs e)
        {
            if (this.save == null) this.save = new PortalSave();
            this.save.Items = this.storage.Where(item => item != null).ToList();
            this.Helper.Data.WriteSaveData(SaveKey, this.save);
        }

        /// <summary>
        /// Copies the vanilla "Standing Geode" sprite out of the furniture sheet so the portal
        /// reuses the original art instead of shipping a lookalike.
        /// </summary>
        private void EnsurePortalTexture()
        {
            try
            {
                if (File.Exists(this.GeodeTextureFile)) return;

                this.geodeId ??= this.FindFurnitureId("Standing Geode");
                if (this.geodeId == null)
                {
                    this.Monitor.Log("Could not find the Standing Geode furniture; the portal keeps its default sprite.", LogLevel.Warn);
                    return;
                }

                ParsedItemData data = ItemRegistry.GetDataOrErrorItem("(F)" + this.geodeId);
                if (data == null || data.IsErrorItem) return;

                Texture2D sheet = this.Helper.GameContent.Load<Texture2D>(data.TextureName);
                Rectangle rect = data.GetSourceRect();
                if (rect.Width <= 0 || rect.Height <= 0 ||
                    rect.X < 0 || rect.Y < 0 || rect.Right > sheet.Width || rect.Bottom > sheet.Height)
                {
                    this.Monitor.Log($"Standing Geode rect {rect} is outside {data.TextureName} ({sheet.Width}x{sheet.Height}).", LogLevel.Warn);
                    return;
                }

                Color[] pixels = new Color[rect.Width * rect.Height];
                sheet.GetData(0, rect, pixels, 0, pixels.Length);

                using (Texture2D sprite = new Texture2D(Game1.graphics.GraphicsDevice, rect.Width, rect.Height))
                {
                    sprite.SetData(pixels);
                    Directory.CreateDirectory(Path.GetDirectoryName(this.GeodeTextureFile) ?? ".");
                    using (FileStream stream = File.Create(this.GeodeTextureFile))
                    {
                        sprite.SaveAsPng(stream, rect.Width, rect.Height);
                    }
                }

                this.Helper.GameContent.InvalidateCache(PortalGeodeAsset);
                this.Helper.GameContent.InvalidateCache("Data/BigCraftables");
                this.Monitor.Log($"Portal sprite extracted from the Standing Geode ({rect.Width}x{rect.Height} at {rect.X},{rect.Y}).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Could not extract the Standing Geode sprite: " + ex.Message, LogLevel.Warn);
            }
        }

        private string FindFurnitureId(string internalName)
        {
            for (int id = 0; id < 4000; id++)
            {
                ParsedItemData data = ItemRegistry.GetDataOrErrorItem("(F)" + id);
                if (data == null || data.IsErrorItem) continue;
                if (string.Equals(data.InternalName, internalName, StringComparison.OrdinalIgnoreCase)) return id.ToString();
            }
            return null;
        }

        private bool PlayerHasPortal()
        {
            try
            {
                foreach (Item item in Game1.player.Items)
                {
                    if (item != null && item.QualifiedItemId == PortalQualifiedId) return true;
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Could not scan the backpack: " + ex.Message, LogLevel.Warn);
            }
            return false;
        }

        private bool GivePortal()
        {
            try
            {
                Item portal = ItemRegistry.Create(PortalQualifiedId, 1, 0, true);
                if (portal == null) portal = new SObject(PortalId, 1);
                Game1.player.addItemToInventoryBool(portal, true);
                return true;
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Could not give the portal item: " + ex, LogLevel.Error);
                return false;
            }
        }

        private bool PlacePortalInFarmhouse()
        {
            try
            {
                GameLocation house = Game1.getLocationFromName("FarmHouse");
                if (house == null) return false;

                Vector2[] candidates =
                {
                    new Vector2(8f, 5f), new Vector2(9f, 5f), new Vector2(7f, 5f),
                    new Vector2(8f, 4f), new Vector2(6f, 5f), new Vector2(10f, 5f)
                };

                foreach (Vector2 tile in candidates)
                {
                    if (house.Objects.ContainsKey(tile)) continue;
                    if (house.terrainFeatures.ContainsKey(tile)) continue;

                    Vector2 below = tile + new Vector2(0f, 1f);
                    bool belowBlocked = house.Objects.ContainsKey(below) ||
                                        house.terrainFeatures.ContainsKey(below);

                    SObject portal = new SObject(tile, PortalId);
                    if (belowBlocked)
                    {
                        this.Monitor.Log($"Skipped {tile}: the tile below is occupied.", LogLevel.Trace);
                        continue;
                    }

                    house.Objects.Add(tile, portal);
                    this.Monitor.Log($"Placed a portal in the farmhouse at {tile}.", LogLevel.Info);
                    return true;
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Could not place a portal in the farmhouse: " + ex, LogLevel.Warn);
            }
            return false;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.OldMenu is PortalMenu && e.NewMenu == null)
            {
                this.SendPortalItems();
            }
        }

        // ------------------------------------------------------------------ input

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsPlayerFree) return;
            if (e.Button != SButton.MouseRight) return;

            Vector2 tile = this.GetTargetTile();
            GameLocation location = Game1.currentLocation;
            if (location == null) return;

            SObject obj = this.FindPortalAt(location, tile);
            if (obj != null)
            {
                this.Helper.Input.Suppress(SButton.MouseRight);
                this.OpenPortal(obj);
            }
        }

        /// <summary>A big craftable sits on its top tile, so also look one tile up and down.</summary>
        private SObject FindPortalAt(GameLocation location, Vector2 tile)
        {
            Vector2[] candidates =
            {
                tile,
                tile + new Vector2(0f, -1f),
                tile + new Vector2(0f, 1f),
                Game1.player.Tile
            };

            foreach (Vector2 candidate in candidates)
            {
                if (!location.Objects.TryGetValue(candidate, out SObject found) || found == null) continue;
                if (found.ItemId == PortalId) return found;
            }
            return null;
        }

        private Vector2 GetTargetTile()
        {
            Farmer player = Game1.player;
            Vector2 facing = new Vector2(0f, 0f);
            switch (player.FacingDirection)
            {
                case 0: facing = new Vector2(0f, -1f); break;
                case 1: facing = new Vector2(1f, 0f); break;
                case 2: facing = new Vector2(0f, 1f); break;
                case 3: facing = new Vector2(-1f, 0f); break;
            }

            Vector2 tile = player.Tile + facing;
            if (Game1.currentLocation.Objects.ContainsKey(player.Tile)) return player.Tile;
            return tile;
        }

        private void OpenPortal(SObject portal)
        {
            try
            {
                Game1.activeClickableMenu = new PortalMenu(this, this.storage, portal);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Could not open the portal menu: " + ex, LogLevel.Error);
            }
        }

        // ------------------------------------------------------------------ shipping

        /// <summary>Ship everything inside the portal to Probably Stolen.</summary>
        public int SendPortalItems()
        {
            int sent = 0;
            try
            {
                if (!this.BridgeFound)
                {
                    Game1.showGlobalMessage("找不到深空当铺的传送门档案（bridge.json）。");
                    return 0;
                }

                List<PortalShipment> shipments = new List<PortalShipment>();
                int refused = 0;
                List<string> refusedMessages = new List<string>();
                for (int i = 0; i < this.storage.Count; i++)
                {
                    Item item = this.storage[i];
                    if (item == null) continue;

                    if (RejectedCategories.TryGetValue(item.Category, out string refuseReason))
                    {
                        refused++;
                        if (!refusedMessages.Contains(refuseReason)) refusedMessages.Add(refuseReason);
                        continue;
                    }

                    PortalShipment shipment = this.ExportItem(item);
                    if (shipment == null) continue;
                    shipments.Add(shipment);
                    this.storage[i] = null;
                    sent++;
                }

                if (refused > 0)
                {
                    foreach (string reason in refusedMessages.Take(3))
                    {
                        Game1.showGlobalMessage(reason);
                    }
                }

                if (sent == 0)
                {
                    Game1.showGlobalMessage("传送门是空的：先放点东西进去。");
                    return 0;
                }

                this.WriteInbox(shipments);
                Game1.showGlobalMessage($"已把 {sent} 组货物送去了深空当铺。");
                this.Monitor.Log($"Shipped {sent} stack(s) to the pawnshop.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Shipping failed: " + ex, LogLevel.Error);
                Game1.showGlobalMessage("发货失败，详情看 SMAPI 日志。");
            }
            return sent;
        }

        private PortalShipment ExportItem(Item item)
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId);
            if (data == null) return null;

            string key = MakeKey(item.QualifiedItemId);
            string spriteKey = "sd_" + key;

            this.ExportSprite(item, data, spriteKey);

            return new PortalShipment
            {
                Uuid = Guid.NewGuid().ToString("N"),
                Key = "sd_" + key,
                Name = item.DisplayName,
                NameEn = data.InternalName,
                Sprite = spriteKey,
                Desc = data.Description,
                Flavor = "产地：星露谷。" + Environment.NewLine + "运自星露谷的货物。",
                CategoryId = item.Category,
                QualifiedId = item.QualifiedItemId,
                ObjectType = data.ObjectType ?? "",
                Price = Math.Max(1, item.salePrice()),
                Edibility = (item as SObject)?.edibility.Value ?? -300,
                Qty = Math.Max(1, item.Stack),
                Source = "Stardew Valley"
            };
        }

        private void ExportSprite(Item item, ParsedItemData data, string spriteKey)
        {
            try
            {
                if (string.IsNullOrEmpty(this.TexturesPath)) return;
                Directory.CreateDirectory(this.TexturesPath);
                string path = Path.Combine(this.TexturesPath, spriteKey + ".png");

                Texture2D sheet = Game1.content.Load<Texture2D>(data.TextureName);
                if (sheet == null) return;

                int perRow = Math.Max(1, sheet.Width / 16);
                int index = Math.Max(0, data.SpriteIndex);
                Rectangle source = new Rectangle((index % perRow) * 16, (index / perRow) * 16, 16, 16);
                if (source.Right > sheet.Width || source.Bottom > sheet.Height) return;

                Color[] all = new Color[sheet.Width * sheet.Height];
                sheet.GetData(all);
                Color[] pixels = new Color[16 * 16];
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        pixels[y * 16 + x] = all[(source.Y + y) * sheet.Width + (source.X + x)];
                    }
                }

                using (Texture2D icon = new Texture2D(Game1.graphics.GraphicsDevice, 16, 16))
                {
                    icon.SetData(pixels);
                    using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        icon.SaveAsPng(stream, 16, 16);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not export the sprite for {item.QualifiedItemId}: {ex.Message}", LogLevel.Warn);
            }
        }

        private void WriteInbox(List<PortalShipment> shipments)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.InboxPath) ?? ".");

            JObject root = null;
            if (File.Exists(this.InboxPath))
            {
                try
                {
                    root = JObject.Parse(File.ReadAllText(this.InboxPath, Encoding.UTF8));
                }
                catch
                {
                    root = null;
                }
            }
            root = root ?? new JObject();
            root["version"] = 1;
            root["priceMultiplier"] = this.PriceMultiplier;
            root["source"] = "Stardew Valley";
            root["updatedAt"] = DateTime.UtcNow.ToString("o");

            JArray items = root["items"] as JArray;
            if (items == null)
            {
                items = new JArray();
                root["items"] = items;
            }

            foreach (PortalShipment shipment in shipments)
            {
                items.Add(new JObject
                {
                    ["uuid"] = shipment.Uuid,
                    ["key"] = shipment.Key,
                    ["name"] = shipment.Name,
                    ["nameEn"] = shipment.NameEn,
                    ["sprite"] = shipment.Sprite,
                    ["desc"] = shipment.Desc,
                    ["flavor"] = shipment.Flavor,
                    ["category"] = shipment.CategoryId,
                    ["objectType"] = shipment.ObjectType,
                    ["qualifiedId"] = shipment.QualifiedId,
                    ["price"] = shipment.Price,
                    ["qty"] = shipment.Qty,
                    ["edibility"] = shipment.Edibility,
                    ["source"] = shipment.Source
                });
            }

            while (items.Count > 300) items.RemoveAt(0);

            File.WriteAllText(this.InboxPath, root.ToString(Formatting.Indented), new UTF8Encoding(false));
        }

        private static string MakeKey(string qualifiedId)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in qualifiedId)
            {
                if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
                else builder.Append('_');
            }
            return builder.ToString().Trim('_');
        }

        // ------------------------------------------------------------------ commands

        /// <summary>Every command needs a loaded save; say so instead of failing silently.</summary>
        private bool RequireWorld(string command)
        {
            if (Context.IsWorldReady) return true;
            this.Monitor.Log($"'{command}' 需要先进入存档（读档之后）再使用。现在还在标题界面，所以没有执行。", LogLevel.Warn);
            return false;
        }

        private void CommandInfo(string command, string[] args)
        {
            this.Monitor.Log($"inbox: {this.InboxPath}", LogLevel.Info);
            this.Monitor.Log($"textures: {this.TexturesPath}", LogLevel.Info);
            this.Monitor.Log($"bridge ok: {this.BridgeFound}, price x{this.PriceMultiplier}", LogLevel.Info);
        }

        private void CommandGive(string command, string[] args)
        {
            if (!this.RequireWorld(command)) return;
            bool gave = this.GivePortal();
            this.Monitor.Log(gave ? "Portal item added to your inventory." : "Could not add the portal item.", LogLevel.Info);
        }

        /// <summary>One sample per rule: food, artisan, drinks, chem, gems, materials, junk, bombs, weapon, refusals.</summary>
        private static readonly string[] SampleIds =
        {
            "(O)24",         // 防风草     蔬菜 -> FOOD
            "(O)258",        // 蓝莓       水果 -> FOOD
            "(O)130",        // 沙丁鱼     鱼   -> FOOD
            "(O)593",        // 郁金香     花   -> FOOD
            "(O)176",        // 鸡蛋       蛋   -> FOOD
            "(O)186",        // 牛奶       奶   -> FOOD
            "(O)226",        // 香辣鳗鱼   料理 -> PROCESSED_FOOD
            "(O)424",        // 奶酪       加工品 -> PROCESSED_FOOD
            "(O)306",        // 蛋黄酱     加工品 -> PROCESSED_FOOD
            "(O)344",        // 果酱       加工品 -> PROCESSED_FOOD
            "(O)342",        // 腌菜       加工品 -> PROCESSED_FOOD
            "(O)348",        // 酒         -> ALCOHOL
            "(O)350",        // 果汁       -> BREVAGE
            "(O)395",        // 咖啡       -> BREVAGE|NARCOTIC + 化学成分
            "(O)253",        // 三倍浓缩   -> BREVAGE|NARCOTIC + 化学成分
            "(O)614",        // 绿茶       -> BREVAGE
            "(O)167",        // Joja 可乐  -> BREVAGE + 化学成分
            "(O)66",         // 石英       矿物 -> MATERIAL
            "(O)72",         // 钻石       宝石 -> LUXURY_ITEM
            "(O)388",        // 木材       材料 -> MATERIAL
            "(O)390",        // 石头       材料 -> MATERIAL
            "(O)378",        // 铜矿石     材料 -> MATERIAL
            "(O)338",        // 布料       -> BASIC_MATERIAL
            "(O)771",        // 纤维       材料 -> MATERIAL
            "(O)168",        // 垃圾       -> JUNK
            "(O)286",        // 樱桃炸弹   违禁品 1 级
            "(O)287",        // 炸弹       违禁品 2 级 + 2x2
            "(O)288",        // 超级炸弹   违禁品 3 级 + 2x2
            "(W)0",          // 生锈的剑   武器 -> WEAPON|MELEE_WEAPON + 2x2
            "(O)74",         // 五彩碎片   2x2 例外
            "(O)SeaJelly",   // 字符串 id 的边界案例
            "(O)472",        // 防风草种子 应被拒收
            "(O)368",        // 基础肥料   应被拒收
            "(O)686",        // 旋式假饵   应被拒收
            "(T)Axe",        // 斧头       应被拒收
            "(TR)MagicQuiver" // 饰品      应被拒收
        };

        private void CommandTestSamples(string command, string[] args)
        {
            if (!this.RequireWorld(command)) return;

            try
            {
                List<PortalShipment> shipments = new List<PortalShipment>();
                List<string> refused = new List<string>();
                List<string> missing = new List<string>();

                foreach (string id in SampleIds)
                {
                    Item item;
                    try
                    {
                        item = ItemRegistry.Create(id, 1, 0, true);
                    }
                    catch
                    {
                        item = null;
                    }

                    if (item == null)
                    {
                        missing.Add(id);
                        continue;
                    }

                    if (RejectedCategories.TryGetValue(item.Category, out string reason))
                    {
                        refused.Add($"{item.DisplayName}({id}) -> {reason}");
                        continue;
                    }

                    PortalShipment shipment = this.ExportItem(item);
                    if (shipment == null)
                    {
                        missing.Add(id);
                        continue;
                    }
                    shipments.Add(shipment);
                }

                if (shipments.Count == 0)
                {
                    this.Monitor.Log("No sample could be created.", LogLevel.Warn);
                    return;
                }

                this.WriteInbox(shipments);
                this.Monitor.Log($"Shipped {shipments.Count} sample(s).", LogLevel.Info);
                if (refused.Count > 0) this.Monitor.Log("Refused: " + string.Join(" | ", refused), LogLevel.Info);
                if (missing.Count > 0) this.Monitor.Log("Could not create: " + string.Join(", ", missing), LogLevel.Warn);
                Game1.showGlobalMessage($"已发送 {shipments.Count} 件测试样品，去深空当铺翻货单看看。");
            }
            catch (Exception ex)
            {
                this.Monitor.Log("portal_test failed: " + ex, LogLevel.Error);
            }
        }

        private void CommandDumpFood(string command, string[] args)
        {
            if (!this.RequireWorld(command)) return;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(this.InboxPath);
                if (string.IsNullOrEmpty(dir))
                {
                    this.Monitor.Log("No inbox path resolved.", LogLevel.Warn);
                    return;
                }
                string path = System.IO.Path.Combine(dir, "stardew_food.csv");
                int count = this.DumpFoodTable(path, force: true);
                int all = this.DumpItemTable(System.IO.Path.Combine(dir, "stardew_items.csv"), force: true);
                this.Monitor.Log($"Wrote {count} edible item(s) and {all} item(s) total to {dir}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Dump failed: " + ex, LogLevel.Error);
            }
        }

        private void TryDumpFoodTable()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(this.InboxPath);
                if (string.IsNullOrEmpty(dir)) return;
                string path = System.IO.Path.Combine(dir, "stardew_food.csv");
                string allPath = System.IO.Path.Combine(dir, "stardew_items.csv");
                if (System.IO.File.Exists(path) && System.IO.File.Exists(allPath)) return;
                int count = this.DumpFoodTable(path, force: false);
                int all = this.DumpItemTable(allPath, force: false);
                this.Monitor.Log($"Wrote {count} edible item(s) and {all} item(s) total to {dir}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("Food table dump failed: " + ex.Message, LogLevel.Warn);
            }
        }

        /// <summary>Same footprint rules as the pawnshop side (types.json -> gridSize).</summary>
        private static string ShapeOf(string qualifiedId, int category)
        {
            switch (qualifiedId)
            {
                case "(O)287":
                case "(O)288":
                case "(O)74":
                    return "2x2";
                case "(O)107":
                    return "2x3";
                case "(O)348":
                    return "对角2格";
                case "(O)459":
                    return "L形3格";
                case "(O)346":
                case "(O)303":
                case "(O)350":
                    return "2x2";
                case "(O)614":
                case "(O)395":
                case "(O)253":
                case "(O)167":
                    return "1x1";
            }

            if (category == -98) return "对角2格";
            if (category == -12 || category == -15) return "2x2";

            switch (category)
            {
                case -4:
                case -5:
                case -6:
                case -7:
                case -14:
                case -25:
                case -75:
                case -79:
                case -80:
                case -81:
                    return "2x2";
            }

            return "1x1";
        }

        private int DumpItemTable(string path, bool force)
        {
            if (!force && System.IO.File.Exists(path)) return 0;

            List<string> rows = new List<string>
            {
                "qualifiedId,name,category,edibility,price,objectType,shape"
            };

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                ObjectData data = pair.Value;
                if (data == null) continue;

                string qualified = "(O)" + pair.Key;
                string name;
                try
                {
                    name = ItemRegistry.GetDataOrErrorItem(qualified).DisplayName;
                }
                catch
                {
                    name = data.DisplayName ?? pair.Key;
                }

                name = (name ?? pair.Key).Replace(",", "，").Replace("\r", " ").Replace("\n", " ");
                rows.Add($"{qualified},{name},{data.Category},{data.Edibility},{data.Price},{data.Type},{ShapeOf(qualified, data.Category)}");
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
            System.IO.File.WriteAllLines(path, rows, new UTF8Encoding(true));
            return rows.Count - 1;
        }

        private int DumpFoodTable(string path, bool force)
        {
            if (!force && System.IO.File.Exists(path)) return 0;

            List<string> rows = new List<string>
            {
                "id,qualifiedId,name,category,edibility,calories,price,objectType"
            };

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                ObjectData data = pair.Value;
                if (data == null || data.Edibility <= 0) continue;

                string qualified = "(O)" + pair.Key;
                string name;
                try
                {
                    name = ItemRegistry.GetDataOrErrorItem(qualified).DisplayName;
                }
                catch
                {
                    name = data.DisplayName ?? pair.Key;
                }

                int calories = (int)Math.Round(data.Edibility * 12.0);
                name = (name ?? pair.Key).Replace(",", "，").Replace("\r", " ").Replace("\n", " ");

                rows.Add($"{pair.Key},{qualified},{name},{data.Category},{data.Edibility},{calories},{data.Price},{data.Type}");
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
            System.IO.File.WriteAllLines(path, rows, new UTF8Encoding(true));
            return rows.Count - 1;
        }

        private void CommandAdd(string command, string[] args)
        {
            if (!this.RequireWorld(command)) return;
            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: portal_add <qualified item id> [count]   e.g. portal_add (O)256 3", LogLevel.Info);
                return;
            }

            string id = args[0];
            int count = 1;
            if (args.Length > 1) int.TryParse(args[1], out count);
            if (count < 1) count = 1;

            try
            {
                Item item = ItemRegistry.Create(id, count, 0, true);
                if (item == null)
                {
                    this.Monitor.Log($"No item with id '{id}'. Try the qualified id, e.g. (O)256.", LogLevel.Warn);
                    return;
                }

                int slot = this.storage.FindIndex(existing => existing == null);
                if (slot < 0)
                {
                    this.Monitor.Log("The portal is full.", LogLevel.Warn);
                    return;
                }

                this.storage[slot] = item;
                this.Monitor.Log($"Added {item.DisplayName} x{item.Stack} to the portal (slot {slot}). Use 'portal_send' to ship it.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log("portal_add failed: " + ex.Message, LogLevel.Error);
            }
        }

        private void CommandSend(string command, string[] args)
        {
            if (!this.RequireWorld(command)) return;
            int sent = this.SendPortalItems();
            this.Monitor.Log($"Sent {sent} stack(s).", LogLevel.Info);
        }
    }
}
