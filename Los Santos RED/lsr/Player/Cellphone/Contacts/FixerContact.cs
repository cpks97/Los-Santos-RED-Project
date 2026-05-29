using LosSantosRED.lsr.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class FixerContact : PhoneContact
{




    public FixerContact()
    {

    }

    public FixerContact(string name, string iconName) : base(name, iconName)
    {

    }

    public override void OnAnswered(IContactInteractable player, CellPhone cellPhone, IGangs gangs, IPlacesOfInterest placesOfInterest, ISettingsProvideable settings,
        IJurisdictions jurisdictions, ICrimes crimes, IEntityProvideable world, IModItems modItems, IWeapons weapons, INameProvideable names, IShopMenus shopMenus, IAgencies agencies)
    {
        MenuInteraction = new JobFixerInteraction(player, gangs, placesOfInterest, settings, this, agencies);
        MenuInteraction.Start(this);
    }
    public override ContactRelationship CreateRelationship()
    {
        return new FixerRelationship(Name, this);
    }
    public override void AddContacts(PossibleContacts possibleContacts)
    {
        possibleContacts.FixerContact = this;
    }
}

