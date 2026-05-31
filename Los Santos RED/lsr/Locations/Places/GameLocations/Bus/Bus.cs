using ExtensionsMethods;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using LSR.Vehicles;
using Mod;
using Rage;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;
using static RAGENativeUI.Elements.UIMenuStatsPanel;

public class Bus : GameLocation, ILocationSetupable
{
    private protected List<Carrier> Carriers = new List<Carrier>();
    private protected IPlacesOfInterest PlacesOfInterest;
    private bool IsBussingToLocation;

    public Bus() : base()
    {

    }
    public override string TypeName { get; set; } = "Bus Stop";
    public override int MapIcon { get; set; } = 513;
    public override string ButtonPromptText { get; set; }
    public string BusID { get; set; }
    public Vector3 ArrivalPosition { get; set; }
    public float ArrivalHeading { get; set; }
    public Vector3 BusArrivalPosition { get; set; }
    public float BusArrivalHeading { get; set; }
    public List<string> RequestIPLs { get; set; }
    public List<string> RemoveIPLs { get; set; }
    public List<BusTravel> CommercialBusses { get; set; } = new List<BusTravel>();
    public HashSet<RoadToggler> RoadToggels { get; set; } = new HashSet<RoadToggler>();
    public HashSet<string> ZonesToEnable { get; set; } = new HashSet<string>();
    public int FuelPrice { get; set; } = 6;
    public bool RequiresMPMap { get; set; } = false;
    public Bus(string busID, Vector3 _EntrancePosition, float _EntranceHeading, string _Name, string _Description) : base(_EntrancePosition, _EntranceHeading, _Name, _Description)
    {
        BusID = busID;
    }
    public override bool CanCurrentlyInteract(ILocationInteractable player)
    {
        ButtonPromptText = $"Wait at {Name}";
        return true;
    }
    public override void StoreData(IShopMenus shopMenus, IAgencies agencies, IGangs gangs, IZones zones, IJurisdictions jurisdictions, IGangTerritories gangTerritories, INameProvideable names,
        ICrimes crimes, IPedGroups PedGroups, IEntityProvideable world, IStreets streets, ILocationTypes locationTypes, ISettingsProvideable settings, IPlateTypes plateTypes,
        IOrganizations associations, IContacts contacts, IInteriors interiors, ILocationInteractable player, IModItems modItems, IWeapons weapons, ITimeControllable time,
        IPlacesOfInterest placesOfInterest, IIssuableWeapons issuableWeapons, IHeads heads, IDispatchablePeople dispatchablePeople, ModDataFileManager modDataFileManager)
    {
        PlacesOfInterest = placesOfInterest;
        base.StoreData(shopMenus, agencies, gangs, zones, jurisdictions, gangTerritories, names, crimes, PedGroups, world, streets, locationTypes, settings, plateTypes, associations,
            contacts, interiors, player, modItems, weapons, time, placesOfInterest, issuableWeapons, heads, dispatchablePeople, modDataFileManager);
    }
    public override void OnInteract()//ILocationInteractable player, IModItems modItems, IEntityProvideable world, ISettingsProvideable settings, IWeapons weapons, ITimeControllable time, IPlacesOfInterest placesOfInterest)
    {
        if (!IsOpen(Time.CurrentHour))
        {
            return;
        }
        if (!CanCurrentlyInteract(Player))
        {
            return;
        }
        if (!CanInteract)
        {
            return;
        }
        if (Interior != null && Interior.IsTeleportEntry)
        {
            DoEntranceCamera(true);
            Interior.Teleport(Player, this, StoreCamera);
        }
        else
        {
            StandardInteract(null, false);
        }
    }
    public override void StandardInteract(LocationCamera locationCamera, bool isInside)
    {
        if (!CanInteract)
        {
            return;
        }
        Player.ActivityManager.IsInteractingWithLocation = true;
        CanInteract = false;
        Player.IsTransacting = true;
        GameFiber.StartNew(delegate
        {
            try
            {
                IsBussingToLocation = false;
                SetupLocationCamera(locationCamera, isInside, true);
                CreateInteractionMenu();
                InteractionMenu.Visible = true;
                SetupMenu();
                ProcessInteractionMenu();
                DisposeInteractionMenu();
                DisposeCamera(isInside);
                Player.ActivityManager.IsInteractingWithLocation = false;
                CanInteract = true;
                Player.IsTransacting = false;
                DisposeInterior();
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                EntryPoint.ModController.CrashUnload();
            }
        }, "BusInteract");
    }
    protected override void DisposeCamera(bool isInside)
    {
        if (IsBussingToLocation)
        {
            StoreCamera.StopImmediately(true);
            return;
        }
        base.DisposeCamera(isInside);
    }
    public void OnSetDestination(ILocationInteractable player, IModItems modItems, IEntityProvideable world, ISettingsProvideable settings, IWeapons weapons, ITimeControllable time, IPlacesOfInterest placesOfInterest)
    {
        Player = player;
        ModItems = modItems;
        World = world;
        Settings = settings;
        Weapons = weapons;
        Time = time;
        PlacesOfInterest = placesOfInterest;
    }
    private void SetupMenu()
    {
        InteractionMenu.SubtitleText = "Pick an Option";
        AddCommercialMenu();
    }
    private void AddCommercialMenu()
    {
        UIMenu commercialSubMenu = MenuPool.AddSubMenu(InteractionMenu, "Local Busses");
        commercialSubMenu.SubtitleText = "Pick a Destination";
        InteractionMenu.MenuItems[InteractionMenu.MenuItems.Count() - 1].Description = "Need to ride a piss soaked bus seat?";

        foreach (string groupedBusID in CommercialBusses.GroupBy(x => x.ToBusID).Select(x => x.Key).Distinct().OrderBy(x => x))
        {
            string DestinationName = groupedBusID;
            string DestinationDescription = groupedBusID;

            Bus busGroup = PlacesOfInterest.PossibleLocations.Busses.FirstOrDefault(x => x.BusID == groupedBusID);
            if (busGroup != null)
            {
                DestinationName = busGroup.Name;
                DestinationDescription = busGroup.Description;
            }

            UIMenu destinationSubMenu = MenuPool.AddSubMenu(commercialSubMenu, DestinationName);
            destinationSubMenu.SubtitleText = "Destination: " + DestinationName;
            commercialSubMenu.MenuItems[commercialSubMenu.MenuItems.Count() - 1].Description = DestinationDescription;


            int? minCost = CommercialBusses.Where(x => x.ToBusID == groupedBusID)?.Min(x => x.Cost);
            if (minCost.HasValue)
            {
                commercialSubMenu.MenuItems[commercialSubMenu.MenuItems.Count() - 1].RightLabel = $"From ${minCost}";
            }


            foreach (BusTravel travel in CommercialBusses.Where(x => x.ToBusID == groupedBusID))
            {
                bool canBus = false;
                Bus destinationBus = PlacesOfInterest.PossibleLocations.Busses.FirstOrDefault(x => x.BusID == travel.ToBusID);
                if (destinationBus != null && destinationBus.IsEnabled && (!destinationBus.RequiresMPMap || World.IsMPMapLoaded))
                {
                    canBus = true;
                }


                Carrier carrier = Carriers.Where(x => x.CarrierID == travel.CarrierID).FirstOrDefault();
                string title = travel.CarrierID;
                string description = travel.Description;
                if (carrier != null)
                {
                    title = carrier.Name;
                    description = "~p~'" + carrier.Description + "'~s~" + "~n~~n~" + travel.Description;
                }
                description += $"~n~~n~Cost: ~r~{travel.Cost:C0}~s~";
                description += $"~n~Travel Time: ~y~{travel.TravelTime}~s~ hour(s)";
                UIMenuItem destinationMenu = new UIMenuItem(title, description) { RightLabel = $"{travel.Cost:C0} - {travel.TravelTime} hour(s)", Enabled = canBus };
                destinationMenu.Activated += (sender, selectedItem) =>
                {
                    if (Player.BankAccounts.GetMoney(true) >= travel.Cost)
                    {
                        Player.BankAccounts.GiveMoney(-1 * travel.Cost, true);
                        IsBussingToLocation = true;
                        Game.FadeScreenOut(1000, true);
                        sender.Visible = false;
                        TravelToBus(destinationBus, travel.TravelTime);
                    }
                    else
                    {
                        PlayErrorSound();
                        DisplayMessage("~r~Insufficient Funds", "We are sorry, we are unable to complete this transation, as you do not have the required funds");
                    }
                };
                destinationSubMenu.AddItem(destinationMenu);

            }
        }
    }

    public virtual void OnArrive(ILocationInteractable Player, bool setPos)
    {
        if (RequestIPLs != null)
        {
            foreach (string requestIPL in RequestIPLs)
            {
                NativeFunction.Natives.REQUEST_IPL(requestIPL);
            }
        }
        if (RoadToggels != null)
        {
            foreach (RoadToggler rt in RoadToggels)
            {
                rt.SetRoad(true);
            }
        }
        if (ZonesToEnable != null)
        {
            foreach (string ze in ZonesToEnable)
            {
                NativeFunction.Natives.SET_ZONE_ENABLED(NativeFunction.Natives.GET_ZONE_FROM_NAME_ID<int>(ze), true);
            }
        }
        if (setPos)
        {
            Player.Character.Position = ArrivalPosition;
            Player.Character.Heading = ArrivalHeading;
        }
    }
    public virtual void OnDepart(ILocationInteractable Player)
    {
        if (RemoveIPLs != null)
        {
            foreach (string removeIPL in RemoveIPLs)
            {
                NativeFunction.Natives.REMOVE_IPL(removeIPL);
            }
        }
        if (RoadToggels != null)
        {
            foreach (RoadToggler rt in RoadToggels)
            {
                rt.SetRoad(false);
            }
        }
        if (ZonesToEnable != null)
        {
            foreach (string ze in ZonesToEnable)
            {
                NativeFunction.Natives.SET_ZONE_ENABLED(NativeFunction.Natives.GET_ZONE_FROM_NAME_ID<int>(ze), false);
            }
        }
    }
    private void TravelToBus(Bus destinationBus, int travelTime)
    {
        GameFiber.StartNew(delegate
        {
            try
            {
                Game.FadeScreenOut(1500, true);
                OnDepart(Player);
                destinationBus.OnSetDestination(Player, ModItems, World, Settings, Weapons, Time, PlacesOfInterest);
                GameFiber.Sleep(1000);
                destinationBus.OnArrive(Player, true);
                Time.SetDateTime(Time.CurrentDateTime.AddHours(travelTime));
                GameFiber.Sleep(3000);//do the whole shebang ehre
                Game.FadeScreenIn(1500, true);
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                EntryPoint.ModController.CrashUnload();
            }
        }, "DestinationGoTo");
    }
    private void TravelInToBus(Bus destinationBus, VehicleExt bus, int travelTime, float fuelSpent)
    {
        GameFiber.StartNew(delegate
        {
            try
            {
                Game.FadeScreenOut(1500, true);
                OnDepart(Player);
                destinationBus.OnSetDestination(Player, ModItems, World, Settings, Weapons, Time, PlacesOfInterest);
                GameFiber.Sleep(1000);
                destinationBus.OnArrive(Player, false);
                Time.SetDateTime(Time.CurrentDateTime.AddHours(travelTime));
                if (bus != null && bus.Vehicle.Exists())
                {
                    bus.Vehicle.Position = destinationBus.BusArrivalPosition;
                    bus.Vehicle.Heading = destinationBus.BusArrivalHeading;
                    Player.Character.WarpIntoVehicle(bus.Vehicle, -1);
                    bus.Vehicle.ApplyForce(new Vector3(0.0f, 500.0f, 0.0f), Vector3.Zero, true, true);
                    bus.Vehicle.IsEngineOn = true;
                    NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(bus.Vehicle, 90f);
                    NativeFunction.Natives.SET_VEHICLE_ENGINE_ON(bus.Vehicle, true, true, false);
                    NativeFunction.Natives.SET_HELI_BLADES_FULL_SPEED(bus.Vehicle);
                    NativeFunction.Natives.CONTROL_LANDING_GEAR(bus.Vehicle, 3);
                    if (bus.Vehicle.FuelLevel - fuelSpent < 0.0f)
                    {
                        bus.Vehicle.FuelLevel = 0.0f;
                    }
                    else
                    {
                        bus.Vehicle.FuelLevel -= fuelSpent;
                    }
                }
                else
                {
                    Player.Character.Position = destinationBus.ArrivalPosition;
                    Player.Character.Heading = destinationBus.ArrivalHeading;
                }
                GameFiber.Sleep(500);//do the whole shebang ehre
                Game.FadeScreenIn(1500, true);
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                EntryPoint.ModController.CrashUnload();
            }
        }, "DestinationGoTo");
    }
    public void Setup()//ICrimes crimes, INameProvideable names, ISettingsProvideable settings)
    {
        Carriers = new List<Carrier>()
        {
            new Carrier(StaticStrings.LosSantosTransitCarrierID,"Los Santos Transit", "From Los Santos to Blaine County"),
            new Carrier(StaticStrings.DashoundCoachCarrierID,"Dashound Coaches", "Covering the distance"),
        };
    }
    public override void AddDistanceOffset(Vector3 offsetToAdd)
    {
        ArrivalPosition += offsetToAdd;
        BusArrivalPosition += offsetToAdd;
        base.AddDistanceOffset(offsetToAdd);
    }


    public override void AddLocation(PossibleLocations possibleLocations)
    {
        possibleLocations.Busses.Add(this);
        base.AddLocation(possibleLocations);
    }
}

