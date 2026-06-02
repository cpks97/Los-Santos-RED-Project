using LosSantosRED.lsr.Interface;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

public class VictimLocation : GameLocation
{
    public VictimLocation(Vector3 _EntrancePosition, float _EntranceHeading, string _Name, string _Description) : base(_EntrancePosition, _EntranceHeading, _Name, _Description)
    {
        OpenTime = 0;
        CloseTime = 24;
    }
    public VictimLocation() : base()
    {

    }
    public override string TypeName { get; set; } = "Target Location";
    public override bool CanCurrentlyInteract(ILocationInteractable player)
    {
        ButtonPromptText = $"Interact With {Name}";
        return false;
    }
    public override void OnInteract()//ILocationInteractable player, IModItems modItems, IEntityProvideable world, ISettingsProvideable settings, IWeapons weapons, ITimeControllable time, IPlacesOfInterest placesOfInterest)
    {
        if (IsLocationClosed())
        {
            return;
        }
        if (CanInteract)
        {
            Game.DisplayHelp("~r~No Interaction~s~");
        }
    }
    public override void AddLocation(PossibleLocations possibleLocations)
    {
        possibleLocations.VictimLocations.Add(this);
        base.AddLocation(possibleLocations);
    }
}

