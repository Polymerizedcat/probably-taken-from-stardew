// Inbox parsing, kept free of Unity dependencies so it can be unit-tested
// outside the game (see .dsh_tools/tests).
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

public class PortalEntry
{
    public string key = "";
    public string name = "";
    public string nameEn = "";
    public string sprite = "";
    public string desc = "";
    public string flavor = "";
    public string category = "";
    public string objectType = "";
    public string qualifiedId = "";
    public string source = "";
    public string uuid = "";
    public long price;
    public int qty = 1;
    public int edibility = -300;
    public int categoryId = int.MinValue;
    public bool claimed;
}

public static class PortalInbox
{
    /// <summary>
    /// Parses Data/inbox.json. Tolerates older entries (category as a word instead of a
    /// number, missing fields) and skips a broken entry instead of failing the whole file.
    /// </summary>
    public static List<PortalEntry> Parse(string json, out float priceMultiplier, out string error)
    {
        priceMultiplier = 2f;
        error = null;
        List<PortalEntry> shipments = new List<PortalEntry>();

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (Exception ex)
        {
            error = "inbox is not valid JSON: " + ex.Message;
            return shipments;
        }

        try
        {
            float? multiplier = root.Value<float?>("priceMultiplier");
            if (multiplier.HasValue && multiplier.Value > 0f) priceMultiplier = multiplier.Value;
        }
        catch
        {
            // ignore a malformed multiplier
        }

        JArray items = root["items"] as JArray;
        if (items == null) return shipments;

        foreach (JToken token in items)
        {
            try
            {
                PortalEntry entry = ParseEntry(token);
                if (entry != null) shipments.Add(entry);
            }
            catch (Exception ex)
            {
                error = (error == null ? "" : error + "; ") + "skipped one entry: " + ex.Message;
            }
        }

        return shipments;
    }

    public static PortalEntry ParseEntry(JToken token)
    {
        if (token == null || token.Type != JTokenType.Object) return null;

        PortalEntry entry = new PortalEntry
        {
            key = ReadString(token["key"]),
            name = ReadString(token["name"]),
            nameEn = ReadString(token["nameEn"]),
            sprite = ReadString(token["sprite"]),
            desc = ReadString(token["desc"]),
            flavor = ReadString(token["flavor"]),
            category = ReadString(token["category"]),
            objectType = ReadString(token["objectType"]),
            qualifiedId = ReadString(token["qualifiedId"]),
            source = ReadString(token["source"]),
            uuid = ReadString(token["uuid"]),
            price = ReadLong(token["price"], 0L),
            qty = ReadInt(token["qty"], 1),
            edibility = ReadInt(token["edibility"], -300),
            categoryId = ReadInt(token["category"], int.MinValue)
        };

        if (string.IsNullOrEmpty(entry.key)) return null;
        if (string.IsNullOrEmpty(entry.sprite)) entry.sprite = entry.key;
        if (string.IsNullOrEmpty(entry.uuid)) entry.uuid = entry.key;
        if (entry.qty < 1) entry.qty = 1;
        return entry;
    }

    private static string ReadString(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null) return null;
        return token.ToString();
    }

    private static int ReadInt(JToken token, int fallback)
    {
        if (token == null || token.Type == JTokenType.Null) return fallback;

        if (token.Type == JTokenType.Integer) return token.Value<int>();
        if (token.Type == JTokenType.Float) return (int)token.Value<double>();

        return int.TryParse(token.ToString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    private static long ReadLong(JToken token, long fallback)
    {
        if (token == null || token.Type == JTokenType.Null) return fallback;

        if (token.Type == JTokenType.Integer) return token.Value<long>();
        if (token.Type == JTokenType.Float) return (long)token.Value<double>();

        return long.TryParse(token.ToString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : fallback;
    }
}
