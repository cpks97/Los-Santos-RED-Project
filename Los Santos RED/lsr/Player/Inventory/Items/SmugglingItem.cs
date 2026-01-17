using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[Serializable]
public class SmugglingItem : ModItem
{
    public SmugglingItem()
    {
    }
    public SmugglingItem(string name, ItemType itemType) : base(name, itemType)
    {

    }
    public SmugglingItem(string name, string description, ItemType itemType) : base(name, description, itemType)
    {

    }
    public override void AddToList(PossibleItems possibleItems)
    {
        possibleItems?.SmugglingItems.RemoveAll(x => x.Name == Name);
        possibleItems?.SmugglingItems.Add(this);
    }
}
