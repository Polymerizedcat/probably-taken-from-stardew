using System.Collections.Generic;
using StardewValley;

namespace DeepSpacePortal
{
    /// <summary>Per-save portal state.</summary>
    public class PortalSave
    {
        public bool PortalGiven { get; set; }
        public List<Item> Items { get; set; } = new List<Item>();
    }

    /// <summary>One stack inside a shipment to Probably Stolen.</summary>
    public class PortalShipment
    {
        public string Uuid { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string NameEn { get; set; }
        public string Sprite { get; set; }
        public string Desc { get; set; }
        public string Flavor { get; set; }
        public int CategoryId { get; set; }
        public string QualifiedId { get; set; }
        public string ObjectType { get; set; }
        public string Source { get; set; }
        public long Price { get; set; }
        public int Edibility { get; set; }
        public int Qty { get; set; }
    }
}
