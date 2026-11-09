// StardewPortal - bridge between Stardew Valley and this game.
//
// The Stardew Valley companion mod (installed into ../Stardew Valley/Mods on
// enable) writes shipments into Data/inbox.json plus 16x16 sprites into
// Textures/Items. This mod turns every distinct item into a real GameItem and
// shows the incoming shipments on a "Stardew Valley Portal" paper-book page.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;

public class StardewPortalMod : IMod
{
    public const string MOD_ID = "StardewPortal";
    public const string BOOK_ID = "portal";
    public const string BOOK_ITEM_ID = "sp_portal_book";
    public const string FULL_BOOK_ID = MOD_ID + ":" + BOOK_ID;

    public static StardewPortalMod Instance;

    private ModManifest manifest;
    private string modFolder;
    private ModItemDirectory itemDirectory;

    private readonly List<PortalEntry> shipments = new List<PortalEntry>();
    private readonly Dictionary<string, PortalEntry> definitions =
        new Dictionary<string, PortalEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> claimedUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> registeredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private float priceMultiplier = 2f;
    private string svFolder = "";
    private bool bridgeInstalled;

    private PaperUI lastBuiltBook;
    private bool suppressBuild;

    public string ModFolder => modFolder;
    private string DataFolder => IOPath.Combine(modFolder, "Data");
    private string ItemsTextureFolder => IOPath.Combine(modFolder, "Textures", "Items");
    private string InboxPath => IOPath.Combine(DataFolder, "inbox.json");
    private string StatePath => IOPath.Combine(DataFolder, "state.json");

    // ------------------------------------------------------------------ life cycle

    public void Init(ModManifest manifest)
    {
        Instance = this;
        this.manifest = manifest;
        modFolder = ResolveModFolder(manifest.ID);
        Debug.Log("[StardewPortal] Init. mod folder = " + (modFolder ?? "<unresolved>"));
    }

    /// <summary>
    /// The loader calls Init/OnEnable before the mod is registered in ModLoader.Mods,
    /// so ModHelper.GetModFolder returns null at that point. Fall back to walking
    /// &lt;game&gt;/Mods and matching the manifest ID.
    /// </summary>
    private static string ResolveModFolder(string modId)
    {
        try
        {
            string folder = ModHelper.GetModFolder(modId);
            if (!string.IsNullOrEmpty(folder) && IODirectory.Exists(folder)) return folder;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] ModHelper.GetModFolder failed: " + ex.Message);
        }

        try
        {
            string gameRoot = IODirectory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(gameRoot)) return null;

            string modsRoot = IOPath.Combine(gameRoot, "Mods");
            if (!IODirectory.Exists(modsRoot)) return null;

            string marker = "<ID>" + modId + "</ID>";
            foreach (string dir in IODirectory.GetDirectories(modsRoot))
            {
                string manifestPath = IOPath.Combine(dir, "manifest.xml");
                if (!IOFile.Exists(manifestPath)) continue;
                try
                {
                    string text = IOFile.ReadAllText(manifestPath, Encoding.UTF8);
                    if (text.IndexOf(marker, StringComparison.Ordinal) >= 0) return dir;
                }
                catch
                {
                    // unreadable manifest: keep looking
                }
            }

            string guess = IOPath.Combine(modsRoot, "StardewPortal");
            if (IODirectory.Exists(guess)) return guess;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not resolve the mod folder: " + ex.Message);
        }

        return null;
    }

    public void OnEnable()
    {
        if (string.IsNullOrEmpty(modFolder))
        {
            Debug.LogError("[StardewPortal] Could not find my own mod folder; the mod cannot start.");
            return;
        }

        IODirectory.CreateDirectory(DataFolder);
        IODirectory.CreateDirectory(ItemsTextureFolder);

        LoadState();
        LoadInbox();

        ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
        ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
        ModHook.OnGameLoadedInit += OnGameLoadedInit;
        ModHook.OnGameLoadedLate += OnGameLoadedLate;
        ModHook.OnShutterOpenedEarly += OnShutterOpenedEarly;
        ModHook.OnPaperOpened += OnPaperOpened;

        RegisterBook();
        InstallBridge();

        try
        {
            ModHelper.AddDebugFunction(MOD_ID, "StardewPortal", "Reload portal inbox", () =>
            {
                ReloadInbox();
                Debug.Log("[StardewPortal] Inbox reloaded: " + shipments.Count + " shipment(s).");
            });

            ModHelper.AddDebugFunction(MOD_ID, "StardewPortal", "Remove extra portal books", () =>
            {
                List<GameItem> books = FindPortalBooks();
                for (int i = 1; i < books.Count; i++)
                {
                    books[i].Destroy();
                }
                Debug.Log("[StardewPortal] Removed " + Math.Max(0, books.Count - 1) + " extra portal book(s).");
            });

            ModHelper.AddDebugFunction(MOD_ID, "StardewPortal", "Give portal book", () =>
            {
                GameItem book = ModHelper.SpawnItem(BOOK_ITEM_ID);
                if (book != null) ModHelper.MoveItemToInventory(book);
                Debug.Log("[StardewPortal] Portal book handed out again.");
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not add the debug function: " + ex.Message);
        }

        Debug.Log("[StardewPortal] Enabled. " + shipments.Count + " shipment(s), " +
                  definitions.Count + " item type(s), bridge=" + bridgeInstalled);
    }

    public void OnDisable()
    {
        ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
        ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
        ModHook.OnGameLoadedInit -= OnGameLoadedInit;
        ModHook.OnGameLoadedLate -= OnGameLoadedLate;
        ModHook.OnShutterOpenedEarly -= OnShutterOpenedEarly;
        ModHook.OnPaperOpened -= OnPaperOpened;
    }

    private void OnModAssetsLoaded(LoadedMod mod)
    {
        if (mod?.Manifest == null || mod.Manifest.ID != MOD_ID) return;
        foreach (PortalEntry entry in definitions.Values) EnsureSprite(entry.sprite);
    }

    // ------------------------------------------------------------------ item directory

    private void OnModItemDirectoryInit(ModItemDirectory directory)
    {
        itemDirectory = directory;
        foreach (PortalEntry entry in definitions.Values) TryRegisterItem(entry);
        TryRegisterBookItem();
        Debug.Log("[StardewPortal] Item directory ready, " + registeredIds.Count + " item(s) registered.");
    }

    private void TryRegisterBookItem()
    {
        if (itemDirectory == null) return;
        if (registeredIds.Contains(BOOK_ITEM_ID)) return;
        if (itemDirectory.Has(BOOK_ITEM_ID)) { registeredIds.Add(BOOK_ITEM_ID); return; }
        if (itemDirectory.Add(BOOK_ITEM_ID, CreateBookItem)) registeredIds.Add(BOOK_ITEM_ID);
    }

    private static GameItem CreateBookItem()
    {
        try
        {
            PaperBookRegistry.Entry entry = PaperBookRegistry.Find(MOD_ID, BOOK_ID);
            if (entry != null)
            {
                GameItem book = PaperBookRegistry.CreateItem(entry);
                if (book != null) return book;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not build the portal book item: " + ex.Message);
        }

        GameItem fallback = ItemDirectory.CreateEmptyItem(BOOK_ITEM_ID);
        fallback.SetName("星露谷传送门");
        fallback.shortDescription = "一个通往星露谷的传送门。";
        EnsureSpriteStatic("portal_book");
        if (Instance != null && Instance.SpriteAvailable("portal_book"))
            fallback.SetSpriteAndShapeFromMod(MOD_ID, "portal_book");
        else
            fallback.SetSpriteAndShape("Items/items_misc", "manual1");
        fallback.SetValue(0L);
        fallback.EnableTag("DOCUMENT");
        fallback.SetGameItemType("DOCUMENT");
        return fallback;
    }

    /// <summary>
    /// Safety net: if the item directory hook fired before this mod was enabled, look the
    /// component up in the scene instead of silently never registering anything.
    /// </summary>
    private void EnsureItemDirectory()
    {
        if (itemDirectory != null) return;
        try
        {
            ModItemDirectory found = UnityEngine.Object.FindObjectOfType<ModItemDirectory>();
            if (found != null)
            {
                Debug.Log("[StardewPortal] Recovered the item directory via FindObjectOfType.");
                OnModItemDirectoryInit(found);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not look up the item directory: " + ex.Message);
        }
    }

    private bool TryRegisterItem(PortalEntry entry)
    {
        if (itemDirectory == null) EnsureItemDirectory();
        if (itemDirectory == null || entry == null || string.IsNullOrEmpty(entry.key)) return false;
        if (registeredIds.Contains(entry.key)) return true;
        if (itemDirectory.Has(entry.key)) { registeredIds.Add(entry.key); return true; }

        PortalEntry captured = entry;
        if (itemDirectory.Add(entry.key, () => CreatePortalItem(captured)))
        {
            registeredIds.Add(entry.key);
            return true;
        }
        return false;
    }

    private static GameItem CreatePortalItem(PortalEntry entry)
    {
        string types = Instance != null ? Instance.ResolveItemTypes(entry) : "";
        string pattern = Instance != null ? Instance.ResolveShapePattern(entry, types) : "1";
        int shapeWidth, shapeHeight;
        GridShape shape = BuildShape(pattern, out shapeWidth, out shapeHeight);
        EnsureSpriteStatic(entry.sprite, shapeWidth, shapeHeight);

        GameItem item = ItemDirectory.CreateEmptyItem(entry.key);
        string display = !string.IsNullOrEmpty(entry.name) ? entry.name : entry.nameEn;
        if (string.IsNullOrEmpty(display)) display = entry.key;
        item.SetName(display);
        item.shortDescription = string.IsNullOrEmpty(entry.desc) ? "从星露谷运来的货物。" : entry.desc;
        item.flavorText = string.IsNullOrEmpty(entry.flavor) ? "产地：星露谷。" : entry.flavor;

        string lore = Instance != null ? Instance.ResolveFlavor(entry) : null;
        if (!string.IsNullOrEmpty(lore)) item.flavorText = lore + "\n产地：星露谷。";

        if (Instance != null && !string.IsNullOrEmpty(entry.sprite) && Instance.SpriteAvailable(entry.sprite))
            item.SetSpriteAndShapeFromMod(MOD_ID, entry.sprite);
        else
            item.SetSpriteAndShape("Items/items_food", "processed_meat");

        // Override whatever the alpha-derived shape was with the explicit footprint.
        item.SetShape(shape);

        long basePrice = entry.price;
        if (Instance != null)
        {
            long overridden = Instance.ResolveBasePrice(entry);
            if (overridden >= 0) basePrice = overridden;
        }

        long value = (long)Math.Round(basePrice * (Instance != null ? Instance.GetValueMultiplier(entry) : 2f), MidpointRounding.AwayFromZero);
        if (Instance != null)
        {
            long fixedValue = Instance.ResolveFixedValue(entry);
            if (fixedValue >= 0) value = fixedValue;
            else value = (long)Math.Round(value * Instance.ResolveValueScale(entry), MidpointRounding.AwayFromZero);
        }
        if (Instance != null) value = (long)Math.Round(value * Instance.valueGlobalScale, MidpointRounding.AwayFromZero);
        if (value < 1 && basePrice > 0) value = 1;   // cheap goods still sell for something
        if (value < 0) value = 0;
        item.SetValue(value);

        List<string> applied = new List<string>();
        foreach (string kind in SplitTypes(types))
        {
            item.SetGameItemType(kind);
            applied.Add(kind);
        }
        Instance?.ApplyFoodRules(item, entry, applied);
        Instance?.ApplyChemRules(item, entry);
        Instance?.ApplyExtraTypes(item, entry, types);
        Instance?.ApplyRemoveTypes(item, entry, types);
        return item;
    }

    // ------------------------------------------------------------------ item types

    private readonly Dictionary<string, string> typeByCategory =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> typeById =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> typeByObjectType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> rejectCategories = new HashSet<int>();
    private readonly Dictionary<string, int> contrabandLevelById =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> calorieOverrides =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> gridById =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> gridByCategory =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private string defaultShape = "1";
    private string foodShape = "1,1;1,1";
    private string fishShape = "1,0;0,1";
    private string weaponShape = "1,0;0,1";
    private float valueGlobalScale = 1f;
    private readonly Dictionary<string, List<string>> extraTypesByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> extraTypesByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> removeTypesByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> removeTypesByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> removeTypesById = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> extraTypesById = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> basePriceById = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> valueScaleById = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> valueById = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> valueByCategory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> flavorById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> flavorByCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> shapeById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> shapeByCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> weaponGridTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ChemSpec> chemById =
        new Dictionary<string, ChemSpec>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> valueMultiplierById =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> foodTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> drinkTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> gridFoodTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string defaultItemType = "";
    private float caloriesPerEdibility = 12f;
    private int minCalories = 30;
    private int drinkThirst = 250;
    private bool typeTableLoaded;

    private string TypeTablePath => IOPath.Combine(DataFolder, "types.json");

    /// <summary>
    /// Item rules live in Data/types.json (editable, no rebuild needed):
    ///   byId          qualified Stardew id  -> "TYPE" or "TYPE|TYPE2"   (wins)
    ///   byCategory    Stardew category int  -> "TYPE|TYPE2"
    ///   byObjectType  Stardew object Type   -> "TYPE"
    ///   default       fallback types ("" = none)
    ///   rejectCategories  categories that are never accepted
    ///   foodTypes / drinkTypes / caloriesPerEdibility / minCalories / drinkThirst
    /// </summary>
    private void EnsureTypeTable()
    {
        if (typeTableLoaded) return;
        typeTableLoaded = true;

        try
        {
            if (!IOFile.Exists(TypeTablePath))
            {
                IODirectory.CreateDirectory(DataFolder);
                IOFile.WriteAllText(TypeTablePath, DefaultTypeTableJson, new UTF8Encoding(false));
                Debug.Log("[StardewPortal] Wrote the default item type table: " + TypeTablePath);
            }

            JObject root = JObject.Parse(IOFile.ReadAllText(TypeTablePath, Encoding.UTF8));
            defaultItemType = ((string)root["default"] ?? "").Trim();

            LoadMap(root["byCategory"] as JObject, typeByCategory);
            LoadMap(root["byId"] as JObject, typeById);
            LoadMap(root["byObjectType"] as JObject, typeByObjectType);

            contrabandLevelById.Clear();
            if (root["contrabandLevelById"] is JObject contraband)
                foreach (JProperty property in contraband.Properties())
                    contrabandLevelById[property.Name.Trim()] = property.Value.Value<int>();

            valueMultiplierById.Clear();
            if (root["valueMultiplierById"] is JObject multipliers)
                foreach (JProperty property in multipliers.Properties())
                    valueMultiplierById[property.Name.Trim()] = property.Value.Value<float>();

            LoadCalorieOverrides();

            extraTypesByType.Clear();
            extraTypesByCategory.Clear();
            extraTypesById.Clear();
            if (root["extraTypes"] is JObject extraTypes)
            {
                LoadTypeLists(extraTypes["byType"], extraTypesByType);
                LoadTypeLists(extraTypes["byCategory"], extraTypesByCategory);
                LoadTypeLists(extraTypes["byId"], extraTypesById);
            }

            removeTypesByType.Clear();
            removeTypesByCategory.Clear();
            removeTypesById.Clear();
            if (root["removeTypes"] is JObject removeTypes)
            {
                LoadTypeLists(removeTypes["byType"], removeTypesByType);
                LoadTypeLists(removeTypes["byCategory"], removeTypesByCategory);
                LoadTypeLists(removeTypes["byId"], removeTypesById);
            }

            valueGlobalScale = 1f;
            if (root["value"] is JObject valueRoot)
                valueGlobalScale = valueRoot.Value<float?>("global") ?? 1f;

            basePriceById.Clear();
            if (root["basePrice"] is JObject basePrice)
            {
                if (basePrice["byId"] is JObject baseIds)
                    foreach (JProperty property in baseIds.Properties())
                        basePriceById[property.Name.Trim()] = property.Value.Value<long>();
            }

            valueScaleById.Clear();
            if (root["valueScale"] is JObject valueScale)
            {
                if (valueScale["byId"] is JObject scaleIds)
                    foreach (JProperty property in scaleIds.Properties())
                        valueScaleById[property.Name.Trim()] = property.Value.Value<float>();
            }

            valueById.Clear();
            valueByCategory.Clear();
            if (root["value"] is JObject valueConfig)
            {
                if (valueConfig["byId"] is JObject valueIds)
                    foreach (JProperty property in valueIds.Properties())
                        valueById[property.Name.Trim()] = property.Value.Value<long>();
                if (valueConfig["byCategory"] is JObject valueCategories)
                    foreach (JProperty property in valueCategories.Properties())
                        valueByCategory[property.Name.Trim()] = property.Value.Value<long>();
            }

            flavorById.Clear();
            flavorByCategory.Clear();
            if (root["flavor"] is JObject flavor)
            {
                if (flavor["byId"] is JObject byId)
                    foreach (JProperty property in byId.Properties())
                        flavorById[property.Name.Trim()] = property.Value.ToString();
                if (flavor["byCategory"] is JObject byCategory)
                    foreach (JProperty property in byCategory.Properties())
                        flavorByCategory[property.Name.Trim()] = property.Value.ToString();
            }

            shapeById.Clear();
            shapeByCategory.Clear();
            if (root["gridSize"] is JObject grid)
            {
                defaultShape = (string)grid["default"] ?? defaultShape;
                foodShape = (string)grid["food"] ?? foodShape;
                fishShape = (string)grid["fish"] ?? fishShape;
                weaponShape = (string)grid["weapon"] ?? weaponShape;
                if (grid["weaponTypes"] is JArray weaponTypes) LoadSet(weaponTypes, weaponGridTypes);
                if (grid["byCategory"] is JObject byCategory)
                    foreach (JProperty property in byCategory.Properties())
                        shapeByCategory[property.Name.Trim()] = property.Value.ToString();
                if (grid["byId"] is JObject byId)
                    foreach (JProperty property in byId.Properties())
                        shapeById[property.Name.Trim()] = property.Value.ToString();
            }

            chemById.Clear();
            if (root["chemComposition"] is JObject chem)
            {
                foreach (JProperty property in chem.Properties())
                {
                    JObject spec = property.Value as JObject;
                    if (spec == null) continue;
                    ChemSpec entry = new ChemSpec
                    {
                        VolumeMl = spec.Value<double?>("volumeMl") ?? 0,
                        Components = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                    };
                    if (spec["components"] is JObject components)
                        foreach (JProperty component in components.Properties())
                            entry.Components[component.Name.Trim()] = component.Value.Value<double>();
                    chemById[property.Name.Trim()] = entry;
                }
            }

            rejectCategories.Clear();
            if (root["rejectCategories"] is JArray reject)
            {
                foreach (JToken token in reject)
                {
                    if (token.Type == JTokenType.Integer) rejectCategories.Add(token.Value<int>());
                    else if (int.TryParse(token.ToString(), out int value)) rejectCategories.Add(value);
                }
            }

            LoadSet(root["foodTypes"] as JArray, foodTypes);
            LoadSet(root["drinkTypes"] as JArray, drinkTypes);

            gridFoodTypes.Clear();
            if (root["gridSize"] is JObject gridTypes && gridTypes["foodTypes"] is JArray gridFood)
                LoadSet(gridFood, gridFoodTypes);
            if (weaponGridTypes.Count == 0)
            {
                weaponGridTypes.Add("WEAPON");
                weaponGridTypes.Add("MELEE_WEAPON");
                weaponGridTypes.Add("FIREARM");
            }
            if (gridFoodTypes.Count == 0)
            {
                // solid food only; drinks are 1x1
                gridFoodTypes.Add("FOOD");
                gridFoodTypes.Add("PROCESSED_FOOD");
                gridFoodTypes.Add("TREAT");
            }

            caloriesPerEdibility = root.Value<float?>("caloriesPerEdibility") ?? caloriesPerEdibility;
            minCalories = root.Value<int?>("minCalories") ?? minCalories;
            drinkThirst = root.Value<int?>("drinkThirst") ?? drinkThirst;

            Debug.Log($"[StardewPortal] Type table v2: {typeByCategory.Count} category, {typeById.Count} id, " +
                      $"{typeByObjectType.Count} object-type, {rejectCategories.Count} rejected category(ies)");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not read types.json, using built-in defaults: " + ex.Message);
            typeTableLoaded = false;
        }
    }

    /// <summary>
    /// Optional per-item calorie overrides from Data/food_calories.csv
    /// (columns: qualifiedId,...,calories). 0 = do not attach a calorie value at all.
    /// </summary>
    private void LoadCalorieOverrides()
    {
        calorieOverrides.Clear();
        try
        {
            string path = IOPath.Combine(DataFolder, "food_calories.csv");
            if (!IOFile.Exists(path)) return;

            string[] lines = IOFile.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return;

            string[] header = lines[0].TrimStart('\ufeff').Split(',');
            int idIndex = 0;
            int calorieIndex = -1;
            for (int i = 0; i < header.Length; i++)
            {
                string column = header[i].Trim().Trim('"').ToLowerInvariant();
                if (column == "qualifiedid") idIndex = i;
                else if (column == "calories") calorieIndex = i;
            }
            if (calorieIndex < 0)
            {
                Debug.LogWarning("[StardewPortal] food_calories.csv has no 'calories' column; ignoring it.");
                return;
            }

            for (int line = 1; line < lines.Length; line++)
            {
                if (string.IsNullOrWhiteSpace(lines[line])) continue;
                string[] cells = lines[line].Split(',');
                if (cells.Length <= Math.Max(idIndex, calorieIndex)) continue;

                string id = cells[idIndex].Trim().Trim('"');
                if (id.Length == 0) continue;

                if (!int.TryParse(cells[calorieIndex].Trim().Trim('"'), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int calories)) continue;
                calorieOverrides[id] = calories;
            }

            Debug.Log("[StardewPortal] Calorie overrides: " + calorieOverrides.Count + " item(s) from food_calories.csv");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not read food_calories.csv: " + ex.Message);
        }
    }

    private static void LoadMap(JObject source, Dictionary<string, string> target)
    {
        if (source == null) return;
        foreach (JProperty property in source.Properties())
        {
            string value = (property.Value?.Type == JTokenType.Null) ? "" : property.Value.ToString().Trim();
            target[property.Name.Trim()] = value;
        }
    }

    private static void LoadSet(JArray source, HashSet<string> target)
    {
        if (source == null) return;
        foreach (JToken token in source)
        {
            string value = token.ToString().Trim();
            if (!string.IsNullOrEmpty(value)) target.Add(value);
        }
    }

    private string ResolveItemTypes(PortalEntry entry)
    {
        EnsureTypeTable();

        string types;
        string id = LookupId(entry);
        if (typeById.TryGetValue(id, out types)) return types;
        if (typeById.TryGetValue(entry.key, out types)) return types;
        if (entry.categoryId != int.MinValue && typeByCategory.TryGetValue(entry.categoryId.ToString(CultureInfo.InvariantCulture), out types)) return types;
        if (!string.IsNullOrEmpty(entry.objectType) && typeByObjectType.TryGetValue(entry.objectType, out types)) return types;
        return defaultItemType;
    }

    /// <summary>
    /// <summary>Stardew "original value" override (-1 = use the shipped price).</summary>
    private long ResolveBasePrice(PortalEntry entry)
    {
        EnsureTypeTable();
        string id = LookupId(entry);
        if (basePriceById.TryGetValue(id, out long price) || basePriceById.TryGetValue(entry.key, out price)) return price;
        return -1L;
    }

    /// <summary>Multiplier applied to the computed value (1 = unchanged).</summary>
    private float ResolveValueScale(PortalEntry entry)
    {
        EnsureTypeTable();
        string id = LookupId(entry);
        if (valueScaleById.TryGetValue(id, out float scale) || valueScaleById.TryGetValue(entry.key, out scale)) return scale;
        return 1f;
    }

    /// <summary>Fixed credit value (used to flatten e.g. every artisan good to one price). -1 = not set.</summary>
    private long ResolveFixedValue(PortalEntry entry)
    {
        EnsureTypeTable();

        string id = LookupId(entry);
        if (valueById.TryGetValue(id, out long fixedValue) || valueById.TryGetValue(entry.key, out fixedValue)) return fixedValue;
        if (entry.categoryId != int.MinValue &&
            valueByCategory.TryGetValue(entry.categoryId.ToString(CultureInfo.InvariantCulture), out fixedValue))
            return fixedValue;
        return -1L;
    }

    private static void LoadTypeLists(JToken token, Dictionary<string, List<string>> target)
    {
        if (token is not JObject obj) return;
        foreach (JProperty property in obj.Properties())
        {
            List<string> values = new List<string>();
            if (property.Value is JArray array)
                foreach (JToken value in array) values.Add(value.ToString());
            else
                values.Add(property.Value.ToString());

            if (values.Count > 0) target[property.Name.Trim()] = values;
        }
    }

    /// <summary>
    /// Extra type labels appended after the food/chem rules, so they only add tags
    /// (e.g. processed food also counts as food, milk also counts as a drink) and
    /// never change calories, footprint or price.
    /// </summary>
    private void ApplyExtraTypes(GameItem item, PortalEntry entry, string types)
    {
        EnsureTypeTable();

        List<string> extra = new List<string>();
        string id = LookupId(entry);
        if (extraTypesById.TryGetValue(id, out List<string> byId)) extra.AddRange(byId);
        else if (extraTypesById.TryGetValue(entry.key, out byId)) extra.AddRange(byId);

        if (entry.categoryId != int.MinValue &&
            extraTypesByCategory.TryGetValue(entry.categoryId.ToString(CultureInfo.InvariantCulture), out List<string> byCategory))
            extra.AddRange(byCategory);

        foreach (string kind in SplitTypes(types))
            if (extraTypesByType.TryGetValue(kind, out List<string> byType)) extra.AddRange(byType);

        foreach (string kind in extra)
        {
            if (string.IsNullOrEmpty(kind) || item.IsGameItemType(kind)) continue;
            item.SetGameItemType(kind);
        }
    }

    /// <summary>
    /// Removes type labels the game adds on its own (FoodItemHelper tags every edible item as
    /// FOOD, which is wrong for coffee, tea and alcohol).
    /// </summary>
    private void ApplyRemoveTypes(GameItem item, PortalEntry entry, string types)
    {
        EnsureTypeTable();

        List<string> remove = new List<string>();
        string id = LookupId(entry);
        if (removeTypesById.TryGetValue(id, out List<string> byId)) remove.AddRange(byId);
        else if (removeTypesById.TryGetValue(entry.key, out byId)) remove.AddRange(byId);

        if (entry.categoryId != int.MinValue &&
            removeTypesByCategory.TryGetValue(entry.categoryId.ToString(CultureInfo.InvariantCulture), out List<string> byCategory))
            remove.AddRange(byCategory);

        foreach (string kind in SplitTypes(types))
            if (removeTypesByType.TryGetValue(kind, out List<string> byType)) remove.AddRange(byType);

        foreach (string kind in remove)
        {
            if (string.IsNullOrEmpty(kind) || !item.IsGameItemType(kind)) continue;
            item.RemoveGameItemType(kind);
        }
    }

    /// <summary>Lore text for items whose origin was flattened (wine, jelly, pickles, cheese...).</summary>
    private string ResolveFlavor(PortalEntry entry)
    {
        EnsureTypeTable();

        string id = LookupId(entry);
        if (flavorById.TryGetValue(id, out string text) || flavorById.TryGetValue(entry.key, out text))
            return string.IsNullOrEmpty(text) ? "" : text;
        if (entry.categoryId != int.MinValue &&
            flavorByCategory.TryGetValue(entry.categoryId.ToString(CultureInfo.InvariantCulture), out text))
            return text;
        return null;
    }

    /// Inventory footprint, expressed as a pattern ("1" = single cell,
    /// "1,1;1,1" = full 2x2, "1,0;0,1" = two diagonal cells).
    /// It is set explicitly because letting the game derive the shape from the sprite's
    /// alpha produces ragged footprints (the Stardew icons have transparent corners).
    /// </summary>
    private string ResolveShapePattern(PortalEntry entry, string types)
    {
        EnsureTypeTable();

        string id = LookupId(entry);
        if (shapeById.TryGetValue(id, out string pattern) || shapeById.TryGetValue(entry.key, out pattern))
            return pattern;

        foreach (string kind in SplitTypes(types))
            if (drinkTypes.Contains(kind)) return defaultShape;

        if (entry.categoryId == -4) return fishShape;                 // fish: two diagonal cells

        if (entry.categoryId != int.MinValue &&
            shapeByCategory.TryGetValue(entry.categoryId.ToString(CultureInfo.InvariantCulture), out pattern))
            return pattern;

        foreach (string kind in SplitTypes(types))
            if (weaponGridTypes.Contains(kind)) return weaponShape;

        foreach (string kind in SplitTypes(types))
            if (gridFoodTypes.Contains(kind)) return foodShape;

        return defaultShape;
    }

    private static GridShape BuildShape(string pattern, out int shapeWidth, out int shapeHeight)
    {
        string text = string.IsNullOrEmpty(pattern) ? "1" : pattern.Trim();
        string[] rows = text.Split(';');
        int height = rows.Length;
        int width = 0;
        foreach (string row in rows) width = Math.Max(width, row.Split(',').Length);
        if (width <= 0) { width = 1; height = 1; }

        byte[] data = new byte[width * height];
        bool any = false;
        for (int y = 0; y < height; y++)
        {
            string[] columns = rows[y].Split(',');
            for (int x = 0; x < width; x++)
            {
                string cell = x < columns.Length ? columns[x].Trim() : "0";
                if (cell == "1") { data[y * width + x] = 1; any = true; }
            }
        }

        if (!any)
        {
            data = new byte[] { 1 };
            width = 1;
            height = 1;
        }

        shapeWidth = width;
        shapeHeight = height;
        return new GridShapeBuilder().SetData(data, width);
    }

    private sealed class ChemSpec
    {
        public double VolumeMl;
        public Dictionary<string, double> Components;
    }

    private ChemSpec GetChemSpec(PortalEntry entry)
    {
        EnsureTypeTable();
        string id = LookupId(entry);
        if (chemById.TryGetValue(id, out ChemSpec spec)) return spec;
        if (chemById.TryGetValue(entry.key, out spec)) return spec;
        return null;
    }

    /// <summary>
    /// Makes the item a real chemical product so the mass spectrometer / water analyser can read it.
    /// isSoluble=true also lets the player dissolve it into a liquid container (the game's own
    /// "get it into another container" path for CHEM items).
    /// </summary>
    private void ApplyChemRules(GameItem item, PortalEntry entry)
    {
        ChemSpec spec = GetChemSpec(entry);
        if (spec == null || spec.Components == null || spec.Components.Count == 0) return;

        ChemicalHelper.InitChemItem(item, false, true);
        foreach (KeyValuePair<string, double> component in spec.Components)
        {
            if (component.Value <= 0) continue;
            ChemicalHelper.SetChemContentML(item, component.Key, component.Value);
        }
    }

    private float GetValueMultiplier(PortalEntry entry)
    {
        EnsureTypeTable();
        string id = LookupId(entry);
        if (valueMultiplierById.TryGetValue(id, out float value)) return value;
        if (valueMultiplierById.TryGetValue(entry.key, out value)) return value;
        return priceMultiplier;
    }

    private int GetContrabandLevel(PortalEntry entry)
    {
        EnsureTypeTable();
        string id = LookupId(entry);
        if (contrabandLevelById.TryGetValue(id, out int level)) return level;
        if (contrabandLevelById.TryGetValue(entry.key, out level)) return level;
        return 0;
    }

    private bool IsRejected(PortalEntry entry)
    {
        EnsureTypeTable();
        return entry.categoryId != int.MinValue && rejectCategories.Contains(entry.categoryId);
    }

    private static IEnumerable<string> SplitTypes(string types)
    {
        if (string.IsNullOrEmpty(types)) yield break;
        foreach (string part in types.Split('|'))
        {
            string value = part.Trim();
            if (value.Length > 0) yield return value;
        }
    }

    /// <summary>Food items need CALORIE_VALUE_TAG (and drinks the edible/thirst tags) to behave like real food.</summary>
    private void ApplyFoodRules(GameItem item, PortalEntry entry, List<string> appliedTypes)
    {
        bool isFood = appliedTypes.Any(foodTypes.Contains);
        bool isDrink = appliedTypes.Any(drinkTypes.Contains);

        if (isFood)
        {
            bool hasOverride = calorieOverrides.TryGetValue(LookupId(entry), out int overrideCalories);
            if (hasOverride && overrideCalories <= 0)
            {
                // explicitly disabled for this item
            }
            else
            {
                int calories = hasOverride
                    ? overrideCalories
                    : (entry.edibility > 0
                        ? Math.Max(minCalories, (int)Math.Round(entry.edibility * caloriesPerEdibility))
                        : minCalories);
                FoodItemHelper.InitFoodItem(item, calories, false, entry.categoryId == -14);
            }
        }

        if (isDrink)
        {
            item.EnableTag("editable_item");
            item.SetType("editable_item");
            item.ModifyTag("add_thirst", state => state.Enable().SetInt(drinkThirst));
        }

        int contrabandLevel = GetContrabandLevel(entry);
        if (contrabandLevel > 0)
        {
            ContrabandHelper.InitContrabandItem(item, contrabandLevel);
        }
    }

    private const string DefaultTypeTableJson = @"{
  ""version"": 3,
  ""$comment"": ""星露谷物品 -> 深空当铺标签。值为 'TYPE' 或 'TYPE|TYPE2'，留空表示不加任何类型。改完保存后重启游戏生效。"",

  ""byCategory"": {
    ""-4"":  ""FOOD"",
    ""-5"":  ""FOOD"",
    ""-6"":  ""FOOD"",
    ""-7"":  ""PROCESSED_FOOD"",
    ""-14"": ""FOOD"",
    ""-25"": ""FOOD"",
    ""-75"": ""FOOD"",
    ""-79"": ""FOOD"",
    ""-80"": ""FOOD"",
    ""-81"": ""FOOD"",

    ""-26"": ""PROCESSED_FOOD"",
    ""-27"": ""BASIC_MATERIAL"",
    ""-2"":  ""LUXURY_ITEM"",
    ""-12"": ""MATERIAL"",
    ""-15"": ""MATERIAL"",
    ""-16"": ""MATERIAL"",
    ""-17"": ""MATERIAL"",
    ""-18"": ""MATERIAL"",
    ""-8"":  ""MATERIAL"",
    ""-28"": ""MATERIAL"",

    ""-19"": ""FARMING_SUPPLIES"",
    ""-74"": ""SEED"",
    ""-20"": ""JUNK"",
    ""-21"": ""JUNK"",
    ""-22"": ""TOOL"",
    ""-98"": ""WEAPON|MELEE_WEAPON""
  },

  ""byId"": {
    ""(O)348"": ""ALCOHOL"",
    ""(O)346"": ""ALCOHOL"",
    ""(O)303"": ""ALCOHOL"",
    ""(O)459"": ""ALCOHOL"",
    ""(O)350"": ""BREVAGE"",
    ""(O)614"": ""BREVAGE"",
    ""(O)395"": ""BREVAGE|NARCOTIC"",
    ""(O)253"": ""BREVAGE|NARCOTIC"",
    ""(O)338"": ""BASIC_MATERIAL"",
    ""(O)167"": ""BREVAGE"",
    ""(O)286"": ""CONTRABAND"",
    ""(O)287"": ""CONTRABAND"",
    ""(O)288"": ""CONTRABAND""
  },

  ""byObjectType"": {
    ""Fish"": ""FOOD"",
    ""Cooking"": ""PROCESSED_FOOD"",
    ""Minerals"": ""MATERIAL""
  },

  ""extraTypes"": {
    ""$comment"": ""额外追加的类型标签（只影响标签，不影响热量/占格/价格）。"",
    ""byType"": {
      ""PROCESSED_FOOD"": [""FOOD""],
      ""TREAT"": [""FOOD""]
    },
    ""byCategory"": {
      ""-26"": [""FOOD""],
      ""-7"": [""FOOD""],
      ""-6"": [""BREVAGE""]
    },
    ""byId"": {
      ""(O)184"": [""BREVAGE""],
      ""(O)186"": [""BREVAGE""],
      ""(O)436"": [""BREVAGE""],
      ""(O)438"": [""BREVAGE""]
    }
  },

  ""removeTypes"": {
    ""$comment"": ""摘掉游戏自动加的类型标签（可食物品的 InitFoodItem 会自动加 FOOD）。"",
    ""byType"": {},
    ""byCategory"": {},
    ""byId"": {
      ""(O)395"": [""FOOD""],
      ""(O)253"": [""FOOD""],
      ""(O)614"": [""FOOD""],
      ""(O)348"": [""FOOD""],
      ""(O)346"": [""FOOD""],
      ""(O)303"": [""FOOD""],
      ""(O)459"": [""FOOD""]
    }
  },

  ""basePrice"": {
    ""$comment"": ""覆盖星露谷的原始标价（-1/不填 = 用物品自带价格）。炸弹三件套在游戏里 salePrice 都是 100，这里按原价区分。"",
    ""byId"": {
      ""(O)286"": 50,
      ""(O)287"": 75,
      ""(O)288"": 100
    }
  },

  ""valueScale"": {
    ""$comment"": ""在算出来的价格上再乘一个系数（1 = 不变）。"",
    ""byId"": {
      ""(O)395"": 0.5,
      ""(O)253"": 0.5
    }
  },

  ""value"": {
    ""$comment"": ""固定售价（信用点），-1 表示不用固定值。加工品身份被抹平，所以统一成一个价。"",
    ""byId"": {},
    ""global"": 1.0,
    ""byCategory"": {
      ""-26"": 300
    }
  },

  ""flavor"": {
    ""$comment"": ""加工品在那边只被当成普通食物，身份被抹平了；这里给它们一段说明。用 \n 换行。"",
    ""byId"": {
      ""(O)614"": """",
      ""(O)346"": """",
      ""(O)303"": """"
    },
    ""byCategory"": {
      ""-26"": ""无论它原本由什么水果、蔬菜制成，在这个世界里都已经失去意义。\n产地、年份、品牌都不再被追问——它只是被还原成了食物本来的样子。""
    }
  },

  ""gridSize"": {
    ""$comment"": ""形状写法：'1' = 1格；'1,1;1,1' = 2x2；'1,0;0,1' = 两个对角格。分号分行，逗号分列。"",
    ""default"": ""1"",
    ""food"": ""1,1;1,1"",
    ""fish"": ""1,1;1,1"",
    ""weapon"": ""0,1;1,0"",
    ""foodTypes"": [""FOOD"", ""PROCESSED_FOOD"", ""TREAT""],
    ""weaponTypes"": [""WEAPON"", ""MELEE_WEAPON"", ""FIREARM""],
    ""byCategory"": {
      ""-12"": ""1,1;1,1"",
      ""-15"": ""1,1;1,1""
    },
    ""byId"": {
      ""(O)287"": ""1,1;1,1"",
      ""(O)288"": ""1,1;1,1"",
      ""(O)74"": ""1,1;1,1"",
      ""(O)107"": ""1,1;1,1;1,1"",
      ""(O)348"": ""0,1;1,0"",
      ""(O)346"": ""1,1;1,1"",
      ""(O)303"": ""1,1;1,1"",
      ""(O)350"": ""1,1;1,1"",
      ""(O)459"": ""1,1;1,0""
    }
  },

  ""chemComposition"": {
    ""(O)395"": { ""volumeMl"": 200, ""components"": { ""water"": 198, ""caffeine"": 2 } },
    ""(O)253"": { ""volumeMl"": 200, ""components"": { ""water"": 194, ""caffeine"": 6 } },
    ""(O)167"": { ""volumeMl"": 400, ""components"": { ""water"": 388, ""caffeine"": 4, ""chemical_contaminant"": 6, ""dream_cap_extract"": 2 } }
  },

  ""contrabandLevelById"": {
    ""(O)286"": 1,
    ""(O)287"": 2,
    ""(O)288"": 3
  },

  ""valueMultiplierById"": {
    ""(O)286"": 4,
    ""(O)287"": 4,
    ""(O)288"": 4
  },

  ""rejectCategories"": [-99, -101, -96, -95, -97, -100, -24, -9, -102, -74, -19, -22],
  ""foodTypes"": [""FOOD"", ""PROCESSED_FOOD"", ""BREVAGE"", ""ALCOHOL"", ""TREAT"", ""NARCOTIC""],
  ""drinkTypes"": [""BREVAGE"", ""ALCOHOL""],
  ""caloriesPerEdibility"": 12,
  ""minCalories"": 30,
  ""drinkThirst"": 250,

  ""default"": """"
}";

    // ------------------------------------------------------------------ sprites

    private bool SpriteAvailable(string key)
    {
        LoadedMod loaded = FindLoadedMod();
        return loaded != null && loaded.Sprites.ContainsKey(key);
    }

    private static LoadedMod FindLoadedMod()
    {
        return ModLoader.Mods.FirstOrDefault(m => m.Manifest != null && m.Manifest.ID == MOD_ID);
    }

    private static void EnsureSpriteStatic(string key, int cellsWide = 1, int cellsHigh = 1)
    {
        Instance?.EnsureSprite(key, cellsWide, cellsHigh);
    }

    private void EnsureSprite(string key, int cellsWide = 1, int cellsHigh = 1)
    {
        if (string.IsNullOrEmpty(key)) return;
        LoadedMod loaded = FindLoadedMod();
        if (loaded == null) return;

        int desiredW = Math.Max(16, cellsWide * 16);
        int desiredH = Math.Max(16, cellsHigh * 16);
        if (loaded.Sprites.TryGetValue(key, out Sprite existing) &&
            Mathf.RoundToInt(existing.rect.width) == desiredW &&
            Mathf.RoundToInt(existing.rect.height) == desiredH)
            return;

        string path = IOPath.Combine(ItemsTextureFolder, key + ".png");
        if (!IOFile.Exists(path))
        {
            string alt = IOPath.Combine(ItemsTextureFolder, key + ".jpg");
            if (!IOFile.Exists(alt))
            {
                Debug.LogWarning("[StardewPortal] Missing sprite file for '" + key + "': " + path);
                return;
            }
            path = alt;
        }

        try
        {
            byte[] bytes = IOFile.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);

            int factorX = Math.Max(1, Mathf.RoundToInt((float)desiredW / Mathf.Max(1, texture.width)));
            int factorY = Math.Max(1, Mathf.RoundToInt((float)desiredH / Mathf.Max(1, texture.height)));
            if (factorX > 1 || factorY > 1)
            {
                int srcW = texture.width;
                int srcH = texture.height;
                Texture2D scaled = new Texture2D(srcW * factorX, srcH * factorY, TextureFormat.RGBA32, false);
                Color[] src = texture.GetPixels();
                Color[] dst = new Color[src.Length * factorX * factorY];
                for (int y = 0; y < srcH; y++)
                {
                    for (int x = 0; x < srcW; x++)
                    {
                        Color c = src[y * srcW + x];
                        for (int dy = 0; dy < factorY; dy++)
                        {
                            int row = (y * factorY + dy) * scaled.width;
                            for (int dx = 0; dx < factorX; dx++) dst[row + x * factorX + dx] = c;
                        }
                    }
                }
                scaled.SetPixels(dst);
                scaled.filterMode = FilterMode.Point;
                scaled.wrapMode = TextureWrapMode.Clamp;
                scaled.Apply();
                texture = scaled;
            }
            else
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Apply();
            }

            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);

            loaded.Sprites[key] = sprite;
            loaded.PaperSprites[key] = sprite;
            Debug.Log("[StardewPortal] Loaded sprite '" + key + "' (" + texture.width + "x" + texture.height + ")");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Failed to load sprite '" + key + "': " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ paper book

    private const float DesignW = 620f;
    private const float DesignH = 360f;

    private void RegisterBook()
    {
        try
        {
            PaperBookData data = PaperStarters.NewBook("blank", BOOK_ID, "星露谷传送门");
            data.title = "星露谷传送门";
            data.background = MOD_ID + ":panel_portal";
            data.width = DesignW * 1.5f;
            data.height = DesignH * 1.5f;
            data.item = new PaperItemData
            {
                id = BOOK_ITEM_ID,
                name = "星露谷传送门",
                description = "一叠通往星露谷的旧货单。星露谷那边把货放进传送门，这边才收得到。",
                sprite = "portal_book",
                value = 0L,
                important = true
            };
            if (data.pages.Count == 0) data.pages.Add(new PaperPageData { name = "概览" });

            ModHelper.RegisterPaperBook(MOD_ID, data);
            Debug.Log("[StardewPortal] Portal book registered.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[StardewPortal] Could not register the portal book: " + ex);
        }
    }

    private void OnPaperOpened(string fullId, PaperUI ui)
    {
        if (!string.Equals(fullId, FULL_BOOK_ID, StringComparison.OrdinalIgnoreCase)) return;
        if (suppressBuild) return;
        if (ReferenceEquals(ui, lastBuiltBook)) return;
        lastBuiltBook = ui;
        BuildPages(ui);
    }

    /// <summary>
    /// Replaces the default chrome: the built-in paper sprites only resolve from the
    /// game's asset catalog, which is unreliable for mod books (they render as flat
    /// blue squares). Every button gets an explicit sprite shipped with this mod,
    /// and positions are pulled inside the rounded ends of the panel.
    /// </summary>
    private void StyleChrome(PaperUI ui)
    {
        foreach (PaperElementData element in ui.Data.chrome)
        {
            if (element == null) continue;
            switch (element.action)
            {
                case PaperAction.CLOSE:
                    element.sprite = MOD_ID + ":ui_close";
                    Place(element, 556f, 20f, 30f, 30f);
                    break;
                case PaperAction.ZOOM:
                    element.sprite = MOD_ID + ":ui_zoom";
                    Place(element, 512f, 20f, 30f, 30f);
                    break;
                case PaperAction.FLIP_LEFT:
                    element.sprite = MOD_ID + ":ui_flip_left";
                    Place(element, 236f, 312f, 30f, 30f);
                    break;
                case PaperAction.FLIP_RIGHT:
                    element.sprite = MOD_ID + ":ui_flip_right";
                    Place(element, 354f, 312f, 30f, 30f);
                    break;
            }
        }
    }

    private static void Place(PaperElementData element, float x, float y, float w, float h)
    {
        element.x = x;
        element.y = y;
        element.w = w;
        element.h = h;
    }

    private void BuildPages(PaperUI ui)
    {
        suppressBuild = true;
        try
        {
            ReloadInbox();
            ui.Data.pages.Clear();
            ui.SetChrome(true, true, true, false);
            StyleChrome(ui);

            BuildOverviewPage(ui);

            List<PortalEntry> pending = shipments.Where(e => !IsClaimed(e)).ToList();
            const int perPage = 5;
            for (int i = 0; i < pending.Count; i += perPage)
            {
                ui.AddPage("货物 " + (i / perPage + 1));
                float y = 92f;
                for (int j = i; j < Math.Min(i + perPage, pending.Count); j++)
                {
                    BuildItemRow(ui, pending[j], y);
                    y += 44f;
                }
            }

            ui.Rebuild();
        }
        catch (Exception ex)
        {
            Debug.LogError("[StardewPortal] Failed to build the portal page: " + ex);
        }
        finally
        {
            suppressBuild = false;
        }
    }

    private void BuildOverviewPage(PaperUI ui)
    {
        ui.AddPage("概览");
        ui.AddText(140f, 38f, 340f, 40f, "星露谷传送门", PaperTextStyle.TITLE, "sp_title", "center", 28f);
        ui.AddText(140f, 80f, 340f, 16f, "STARDEW VALLEY PORTAL", PaperTextStyle.LABEL, "sp_subtitle", "center", 10f);
        ui.AddLine(152f, 106f, 468f, 106f, 2f, null, "none", "sp_rule");

        string status = bridgeInstalled
            ? "目前可以接收到神秘牢乡的东西"
            : "还没有接通星露谷，暂时收不到东西。";
        ui.AddText(150f, 126f, 320f, 26f, status, PaperTextStyle.BODY, "sp_status", "center", 13f);

        int pendingCount = shipments.Count(e => !IsClaimed(e));
        ui.AddText(150f, 172f, 320f, 24f, "待收货物：" + pendingCount + " 件",
            PaperTextStyle.SECTION, "sp_count", "center", 15f);

        ui.AddButton(150f, 214f, 150f, 34f, "领取全部", () => ClaimAll(ui), "sp_btn_claim_all", "claim_all");
        ui.AddButton(320f, 214f, 150f, 34f, "刷新清单", () => OnRefresh(ui), "sp_btn_refresh", "refresh");
    }

    private void OnRefresh(PaperUI ui)
    {
        Debug.Log("[StardewPortal] Refreshing the ledger.");
        lastBuiltBook = null;
        if (ui != null && ui.IsAlive) BuildPages(ui);
    }

    private void BuildItemRow(PaperUI ui, PortalEntry entry, float y)
    {
        if (!string.IsNullOrEmpty(entry.sprite) && SpriteAvailable(entry.sprite))
        {
            ui.AddImage(140f, y, 36f, 36f, MOD_ID + ":" + entry.sprite, "contain", "sp_img_" + entry.uuid);
        }

        string title = !string.IsNullOrEmpty(entry.name) ? entry.name : entry.nameEn;
        if (string.IsNullOrEmpty(title)) title = entry.key;
        ui.AddText(184f, y + 1f, 200f, 18f, title, PaperTextStyle.SECTION, "sp_name_" + entry.uuid, "left", 12f);

        string sub = "x" + Math.Max(1, entry.qty) + "   " + entry.price + " g   收 " + ClaimValue(entry) + " cr";
        ui.AddText(184f, y + 20f, 200f, 16f, sub, PaperTextStyle.CAPTION, "sp_sub_" + entry.uuid, "left", 9f);

        if (IsClaimed(entry))
        {
            ui.AddText(392f, y + 8f, 80f, 20f, "已领取", PaperTextStyle.CAPTION, "sp_done_" + entry.uuid, "center", 10f);
        }
        else
        {
            PortalEntry captured = entry;
            ui.AddButton(392f, y + 4f, 80f, 28f, "领取", () => ClaimOne(captured), "sp_take_" + entry.uuid, "take_" + entry.uuid);
        }
    }

    // ------------------------------------------------------------------ claims

    private long ClaimValue(PortalEntry entry)
    {
        long value = (long)Math.Round(entry.price * priceMultiplier, MidpointRounding.AwayFromZero);
        return value < 1 ? 1 : value;
    }

    private bool IsClaimed(PortalEntry entry)
    {
        return entry == null || string.IsNullOrEmpty(entry.uuid) || claimedUuids.Contains(entry.uuid);
    }

    private void ClaimOne(PortalEntry entry)
    {
        if (entry == null || IsClaimed(entry)) return;
        int spawned = SpawnShipment(entry);
        if (spawned <= 0) return;

        claimedUuids.Add(entry.uuid);
        SaveState();
        Debug.Log("[StardewPortal] Claimed " + spawned + "x " + entry.key);

        if (lastBuiltBook != null && lastBuiltBook.IsAlive) BuildPages(lastBuiltBook);
    }

    private void ClaimAll(PaperUI ui)
    {
        int total = 0;
        foreach (PortalEntry entry in shipments)
        {
            if (IsClaimed(entry)) continue;
            int spawned = SpawnShipment(entry);
            if (spawned <= 0) continue;
            claimedUuids.Add(entry.uuid);
            total += spawned;
        }
        SaveState();
        Debug.Log("[StardewPortal] Claimed " + total + " item(s) in total.");
        if (ui != null) BuildPages(ui);
    }

    private int SpawnShipment(PortalEntry entry)
    {
        try
        {
            if (!registeredIds.Contains(entry.key)) TryRegisterItem(entry);
            GameItem item = ModHelper.SpawnItem(entry.key);
            if (item == null)
            {
                Debug.LogWarning("[StardewPortal] Could not spawn '" + entry.key + "' - is it registered?");
                return 0;
            }

            int count = Math.Max(1, entry.qty);
            if (count > 1)
            {
                try { item.SetUnitCount(count); }
                catch (Exception ex) { Debug.LogWarning("[StardewPortal] Could not stack " + count + "x: " + ex.Message); }
            }

            ModHelper.MoveItemToInventory(item);
            return count;
        }
        catch (Exception ex)
        {
            Debug.LogError("[StardewPortal] Spawn failed for '" + entry.key + "': " + ex);
            return 0;
        }
    }

    // ------------------------------------------------------------------ data io

    private void ReloadInbox()
    {
        shipments.Clear();
        LoadInbox();
        foreach (PortalEntry entry in definitions.Values) TryRegisterItem(entry);
    }

    /// <summary>Newer shipments carry qualifiedId/category/edibility; prefer those definitions.</summary>
    private static bool IsRicher(PortalEntry candidate, PortalEntry existing)
    {
        bool candidateRich = !string.IsNullOrEmpty(candidate.qualifiedId) || candidate.categoryId != int.MinValue;
        bool existingRich = !string.IsNullOrEmpty(existing.qualifiedId) || existing.categoryId != int.MinValue;
        return candidateRich && !existingRich;
    }

    /// <summary>Reconstructs "(O)288" from the sanitised key "sd_o_288" for legacy entries.</summary>
    private static string DeriveQualifiedId(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith("sd_", StringComparison.OrdinalIgnoreCase)) return null;
        string rest = key.Substring(3);
        int cut = rest.IndexOf('_');
        if (cut <= 0 || cut == rest.Length - 1) return null;
        return "(" + rest.Substring(0, cut).ToUpperInvariant() + ")" + rest.Substring(cut + 1);
    }

    private static string LookupId(PortalEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.qualifiedId)) return entry.qualifiedId;
        string derived = DeriveQualifiedId(entry.key);
        return derived ?? entry.key;
    }

    private void LoadInbox()
    {
        try
        {
            if (!IOFile.Exists(InboxPath))
            {
                Debug.Log("[StardewPortal] No inbox yet at " + InboxPath);
                return;
            }

            string json = IOFile.ReadAllText(InboxPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return;

            List<PortalEntry> parsed = PortalInbox.Parse(json, out float multiplier, out string error);
            if (multiplier > 0f) priceMultiplier = multiplier;
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[StardewPortal] Inbox: " + error);

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PortalEntry entry in parsed)
            {
                if (!seen.Add(entry.uuid)) continue;
                entry.claimed = claimedUuids.Contains(entry.uuid);
                shipments.Add(entry);
                if (!definitions.TryGetValue(entry.key, out PortalEntry existing) || IsRicher(entry, existing))
                    definitions[entry.key] = entry;
            }

            Debug.Log("[StardewPortal] Loaded " + shipments.Count + " shipment(s), " +
                      definitions.Count + " distinct item type(s).");
        }
        catch (Exception ex)
        {
            Debug.LogError("[StardewPortal] Failed to read inbox: " + ex);
        }
    }

    private void LoadState()
    {
        try
        {
            if (IOFile.Exists(StatePath))
            {
                JObject root = JObject.Parse(IOFile.ReadAllText(StatePath, Encoding.UTF8));
                if (root["priceMultiplier"] != null)
                {
                    float m = root.Value<float?>("priceMultiplier") ?? priceMultiplier;
                    if (m > 0f) priceMultiplier = m;
                }
                JToken claimed = root["claimed"];
                if (claimed != null && claimed.Type == JTokenType.Array)
                {
                    foreach (JToken token in claimed)
                    {
                        string id = (string)token;
                        if (!string.IsNullOrEmpty(id)) claimedUuids.Add(id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not read state: " + ex.Message);
        }

        try
        {
            string configPath = IOPath.Combine(modFolder, "config.json");
            if (IOFile.Exists(configPath))
            {
                JObject cfg = JObject.Parse(IOFile.ReadAllText(configPath, Encoding.UTF8));
                if (cfg["priceMultiplier"] != null)
                {
                    float m = cfg.Value<float?>("priceMultiplier") ?? priceMultiplier;
                    if (m > 0f) priceMultiplier = m;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not read config.json: " + ex.Message);
        }
    }

    private void SaveState()
    {
        try
        {
            JObject root = new JObject
            {
                ["version"] = 1,
                ["priceMultiplier"] = priceMultiplier,
                ["claimed"] = new JArray(claimedUuids.Cast<object>().ToArray())
            };
            IODirectory.CreateDirectory(DataFolder);
            IOFile.WriteAllText(StatePath, root.ToString(Formatting.Indented), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not save state: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ game hooks

    /// <summary>
    /// Runs before the save is read (PlayerStore.LoadGame fires it before ES3.LoadInto),
    /// so this is only for early setup - never for "does the save already have X".
    /// </summary>
    private void OnGameLoadedInit()
    {
        EnsureItemDirectory();
        TryRegisterBookItem();
        foreach (PortalEntry entry in definitions.Values) TryRegisterItem(entry);
    }

    /// <summary>
    /// Runs after PlayerStore finished restoring the save, so the inventory scan is reliable.
    /// </summary>
    private void OnGameLoadedLate()
    {
        EnsurePortalBook("after the save loaded");
    }

    /// <summary>Opening the shop also covers a brand new game (StartNewGame never fires the loaded hooks).</summary>
    private void OnShutterOpenedEarly()
    {
        EnsurePortalBook("first shutter open");
    }

    private void EnsurePortalBook(string when)
    {
        EnsureItemDirectory();
        TryRegisterBookItem();

        int existing = CountPortalBooks();
        if (existing > 0) return;

        GameItem item = ModHelper.SpawnItem(BOOK_ITEM_ID);
        if (item == null)
        {
            Debug.LogWarning("[StardewPortal] Portal book item is not available yet; will retry next load.");
            return;
        }

        item.unitValue = 0L;
        ModHelper.MoveItemToInventory(item);
        Debug.Log("[StardewPortal] Gave the portal book to the player (" + when + ", value 0).");
    }

    private List<GameItem> FindPortalBooks()
    {
        List<GameItem> found = new List<GameItem>();
        try
        {
            List<GameItem> all = EmporiumEntry.Instance?.GetAllItems();
            if (all == null) return found;
            foreach (GameItem item in all)
            {
                if (item == null) continue;
                if (string.Equals(item.identifier, BOOK_ITEM_ID, StringComparison.OrdinalIgnoreCase)) found.Add(item);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not scan the store for portal books: " + ex.Message);
        }
        return found;
    }

    private int CountPortalBooks() => FindPortalBooks().Count;

    // ------------------------------------------------------------------ bridge installer

    private void InstallBridge()
    {
        try
        {
            string gameRoot = ResolveGameRoot(modFolder);
            if (string.IsNullOrEmpty(gameRoot))
            {
                Debug.LogWarning("[StardewPortal] Could not resolve the game root from '" + modFolder + "'.");
                return;
            }

            string common = IODirectory.GetParent(gameRoot)?.FullName;
            string candidate = common != null ? IOPath.Combine(common, "Stardew Valley") : null;

            if (candidate == null || !LooksLikeStardew(candidate))
            {
                Debug.Log("[StardewPortal] No Stardew Valley install next to this game; the portal bridge was not installed.");
                return;
            }

            svFolder = candidate;
            string target = IOPath.Combine(svFolder, "Mods", "DeepSpacePortal");
            string payloadPath = IOPath.Combine(modFolder, "Bridge", "payload.json");

            if (!IOFile.Exists(payloadPath))
            {
                Debug.LogWarning("[StardewPortal] Bridge payload missing: " + payloadPath);
                return;
            }

            // The game's mod scanner blocks .dll/.exe/.bat inside a mod folder, so the
            // Stardew-side files travel as one base64+gzip JSON blob and get unpacked here.
            JObject payload = JObject.Parse(IOFile.ReadAllText(payloadPath, Encoding.UTF8));
            if (payload["priceMultiplier"] != null)
            {
                float m = payload.Value<float?>("priceMultiplier") ?? priceMultiplier;
                if (m > 0f) priceMultiplier = m;
            }

            JArray files = payload["files"] as JArray;
            if (files == null) { Debug.LogWarning("[StardewPortal] Bridge payload has no files."); return; }

            bool hasSmapi = IOFile.Exists(IOPath.Combine(svFolder, "StardewModdingAPI.exe"))
                            || IOFile.Exists(IOPath.Combine(svFolder, "StardewModdingAPI.dll"));

            int writtenMod = 0;
            int writtenSmapi = 0;
            foreach (JToken file in files)
            {
                string rootKind = (string)file["root"] ?? "mod";
                if (rootKind == "root")
                {
                    if (hasSmapi) continue;              // player already has SMAPI
                    writtenSmapi++;
                }
                else
                {
                    writtenMod++;
                }

                string relative = (string)file["path"];
                if (string.IsNullOrEmpty(relative)) continue;

                string baseDir = rootKind == "root" ? svFolder : target;
                string dest = IOPath.GetFullPath(IOPath.Combine(baseDir, relative.Replace('/', IOPath.DirectorySeparatorChar)));
                string baseFull = IOPath.GetFullPath(baseDir).TrimEnd('\\', '/');
                if (!dest.StartsWith(baseFull + IOPath.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("[StardewPortal] Skipping payload path outside the target folder: " + relative);
                    continue;
                }

                byte[] data = Convert.FromBase64String((string)file["b64"]);
                bool gzip = file.Value<bool?>("gzip") ?? false;
                if (gzip) data = Gunzip(data);

                string dir = IOPath.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir)) IODirectory.CreateDirectory(dir);
                if (IOFile.Exists(dest))
                {
                    try { IOFile.SetAttributes(dest, FileAttributes.Normal); } catch { }
                }
                IOFile.WriteAllBytes(dest, data);
            }

            if (!hasSmapi && writtenSmapi > 0)
                Debug.Log("[StardewPortal] Installed SMAPI (" + writtenSmapi + " file(s)) into " + svFolder);
            else if (!hasSmapi)
                Debug.LogWarning("[StardewPortal] Stardew Valley has no SMAPI. Install it from https://smapi.io once.");

            // SMAPI is a self-contained apphost: without <app>.deps.json the .NET host
            // falls back to "dotnet muxer" mode and exits instantly (the classic flash-crash).
            EnsureSmapiDeps(svFolder);

            WriteBridgeConfig(target);
            bridgeInstalled = true;
            Debug.Log("[StardewPortal] Installed the Stardew Valley portal mod (" + writtenMod + " file(s)) into " + target);
        }
        catch (Exception ex)
        {
            Debug.LogError("[StardewPortal] Bridge install failed: " + ex);
        }
    }

    /// <summary>
    /// Workshop mods live in steamapps/workshop/content/&lt;appid&gt;/&lt;id&gt;, so walking up from the
    /// mod folder does not reach the game root. Application.dataPath always does.
    /// </summary>
    private static string ResolveGameRoot(string modFolder)
    {
        try
        {
            string dataPath = Application.dataPath;
            string candidate = IODirectory.GetParent(dataPath)?.FullName;
            if (!string.IsNullOrEmpty(candidate) && IODirectory.Exists(IOPath.Combine(candidate, "Probably Stolen_Data")))
                return candidate;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Application.dataPath unavailable: " + ex.Message);
        }

        string modsFolder = IODirectory.GetParent(modFolder)?.FullName;
        return modsFolder != null ? IODirectory.GetParent(modsFolder)?.FullName : null;
    }

    private static bool LooksLikeStardew(string folder)
    {
        return IODirectory.Exists(folder)
               && (IOFile.Exists(IOPath.Combine(folder, "Stardew Valley.dll"))
                   || IOFile.Exists(IOPath.Combine(folder, "Stardew Valley.exe")));
    }

    private void WriteBridgeConfig(string target)
    {
        JObject config = new JObject
        {
            ["version"] = 1,
            ["portalModId"] = MOD_ID,
            ["inboxPath"] = InboxPath,
            ["texturesPath"] = ItemsTextureFolder,
            ["priceMultiplier"] = priceMultiplier,
            ["updatedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
        IODirectory.CreateDirectory(target);
        IOFile.WriteAllText(IOPath.Combine(target, "bridge.json"),
            config.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private void EnsureSmapiDeps(string svFolder)
    {
        try
        {
            string target = IOPath.Combine(svFolder, "StardewModdingAPI.deps.json");
            string source = IOPath.Combine(svFolder, "Stardew Valley.deps.json");
            if (IOFile.Exists(target)) return;
            if (!IOFile.Exists(source))
            {
                Debug.LogWarning("[StardewPortal] Neither StardewModdingAPI.deps.json nor Stardew Valley.deps.json exists; SMAPI may flash-crash.");
                return;
            }
            IOFile.Copy(source, target, false);
            Debug.Log("[StardewPortal] Created StardewModdingAPI.deps.json from the game's deps file (required by SMAPI).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StardewPortal] Could not create StardewModdingAPI.deps.json: " + ex.Message);
        }
    }

    private static byte[] Gunzip(byte[] data)
    {
        using (MemoryStream input = new MemoryStream(data))
        using (System.IO.Compression.GZipStream gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
        using (MemoryStream output = new MemoryStream())
        {
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        IODirectory.CreateDirectory(target);
        foreach (string file in IODirectory.GetFiles(source))
        {
            string dest = IOPath.Combine(target, IOPath.GetFileName(file));
            if (IOFile.Exists(dest))
            {
                IOFile.SetAttributes(dest, FileAttributes.Normal);
                IOFile.Delete(dest);
            }
            IOFile.Copy(file, dest, true);
        }
        foreach (string dir in IODirectory.GetDirectories(source))
        {
            CopyDirectory(dir, IOPath.Combine(target, IOPath.GetFileName(dir)));
        }
    }
}
