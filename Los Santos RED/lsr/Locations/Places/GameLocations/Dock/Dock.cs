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

public class Dock : GameLocation, ILocationSetupable
{
    private protected List<Carrier> Carriers = new List<Carrier>();
    private protected IPlacesOfInterest PlacesOfInterest;
    private bool IsBoatingToLocation;

    public Dock() : base()
    {

    }
    public override string TypeName { get; set; } = "Dock";
    public override int MapIcon { get; set; } = 780;
    public override string ButtonPromptText { get; set; }
    public string DockID { get; set; }
    public Vector3 ArrivalPosition { get; set; }
    public float ArrivalHeading { get; set; }
    public Vector3 BoatArrivalPosition { get; set; }
    public float BoatArrivalHeading { get; set; }
    public List<string> RequestIPLs { get; set; }
    public List<string> RemoveIPLs { get; set; }
    public List<DockFerry> CommercialFerries { get; set; } = new List<DockFerry>();
    public HashSet<RoadToggler> RoadToggels { get; set; } = new HashSet<RoadToggler>();
    public HashSet<string> ZonesToEnable { get; set; } = new HashSet<string>();
    public int FuelPrice { get; set; } = 6;
    public bool RequiresMPMap { get; set; } = false;
    public Dock(string dockID, Vector3 _EntrancePosition, float _EntranceHeading, string _Name, string _Description) : base(_EntrancePosition, _EntranceHeading, _Name, _Description)
    {
        DockID = dockID;
    }
    public override bool CanCurrentlyInteract(ILocationInteractable player)
    {
        ButtonPromptText = $"Enter {Name}";
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
                IsBoatingToLocation = false;
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
        }, "DockInteract");
    }
    protected override void DisposeCamera(bool isInside)
    {
        if (IsBoatingToLocation)
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
        UIMenu commercialSubMenu = MenuPool.AddSubMenu(InteractionMenu, "Commercial Ferries");
        commercialSubMenu.SubtitleText = "Pick a Destination";
        InteractionMenu.MenuItems[InteractionMenu.MenuItems.Count() - 1].Description = "Need to get away? Chose one of our fine commercial ferries to get you where you need to go";

        foreach (string groupedDockID in CommercialFerries.GroupBy(x => x.ToDockID).Select(x => x.Key).Distinct().OrderBy(x => x))
        {
            string DestinationName = groupedDockID;
            string DestinationDescription = groupedDockID;

            Dock dockGroup = PlacesOfInterest.PossibleLocations.Docks.FirstOrDefault(x => x.DockID == groupedDockID);
            if (dockGroup != null)
            {
                DestinationName = dockGroup.Name;
                DestinationDescription = dockGroup.Description;
            }

            UIMenu destinationSubMenu = MenuPool.AddSubMenu(commercialSubMenu, DestinationName);
            destinationSubMenu.SubtitleText = "Destination: " + DestinationName;
            commercialSubMenu.MenuItems[commercialSubMenu.MenuItems.Count() - 1].Description = DestinationDescription;


            int? minCost = CommercialFerries.Where(x => x.ToDockID == groupedDockID)?.Min(x => x.Cost);
            if (minCost.HasValue)
            {
                commercialSubMenu.MenuItems[commercialSubMenu.MenuItems.Count() - 1].RightLabel = $"From ${minCost}";
            }


            foreach (DockFerry ferry in CommercialFerries.Where(x => x.ToDockID == groupedDockID))
            {
                bool canBoat = false;
                Dock destinationDock = PlacesOfInterest.PossibleLocations.Docks.FirstOrDefault(x => x.DockID == ferry.ToDockID);
                if (destinationDock != null && destinationDock.IsEnabled && (!destinationDock.RequiresMPMap || World.IsMPMapLoaded))
                {
                    canBoat = true;
                }


                Carrier carrier = Carriers.Where(x => x.CarrierID == ferry.CarrierID).FirstOrDefault();
                string title = ferry.CarrierID;
                string description = ferry.Description;
                if (carrier != null)
                {
                    title = carrier.Name;
                    description = "~p~'" + carrier.Description + "'~s~" + "~n~~n~" + ferry.Description;
                }
                description += $"~n~~n~Cost: ~r~{ferry.Cost:C0}~s~";
                description += $"~n~Travel Time: ~y~{ferry.FerryTime}~s~ hour(s)";
                UIMenuItem destinationMenu = new UIMenuItem(title, description) { RightLabel = $"{ferry.Cost:C0} - {ferry.FerryTime} hour(s)", Enabled = canBoat };
                destinationMenu.Activated += (sender, selectedItem) =>
                {
                    if (Player.BankAccounts.GetMoney(true) >= ferry.Cost)
                    {
                        Player.BankAccounts.GiveMoney(-1 * ferry.Cost, true);
                        IsBoatingToLocation = true;
                        Game.FadeScreenOut(1000, true);
                        sender.Visible = false;
                        BoatToDock(destinationDock, ferry.FerryTime);
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
    private void BoatToDock(Dock destinationDock, int ferryTime)
    {
        GameFiber.StartNew(delegate
        {
            try
            {
                Game.FadeScreenOut(1500, true);
                OnDepart(Player);
                destinationDock.OnSetDestination(Player, ModItems, World, Settings, Weapons, Time, PlacesOfInterest);
                GameFiber.Sleep(1000);
                destinationDock.OnArrive(Player, true);
                Time.SetDateTime(Time.CurrentDateTime.AddHours(ferryTime));
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
    private void BoatInToDock(Dock destinationDock, VehicleExt boat, int ferryTime, float fuelSpent)
    {
        GameFiber.StartNew(delegate
        {
            try
            {
                Game.FadeScreenOut(1500, true);
                OnDepart(Player);
                destinationDock.OnSetDestination(Player, ModItems, World, Settings, Weapons, Time, PlacesOfInterest);
                GameFiber.Sleep(1000);
                destinationDock.OnArrive(Player, false);
                Time.SetDateTime(Time.CurrentDateTime.AddHours(ferryTime));
                if (boat != null && boat.Vehicle.Exists())
                {
                    boat.Vehicle.Position = destinationDock.BoatArrivalPosition;
                    boat.Vehicle.Heading = destinationDock.BoatArrivalHeading;
                    Player.Character.WarpIntoVehicle(boat.Vehicle, -1);
                    boat.Vehicle.ApplyForce(new Vector3(0.0f, 500.0f, 0.0f), Vector3.Zero, true, true);
                    boat.Vehicle.IsEngineOn = true;
                    NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(boat.Vehicle, 90f);
                    NativeFunction.Natives.SET_VEHICLE_ENGINE_ON(boat.Vehicle, true, true, false);
                    NativeFunction.Natives.SET_HELI_BLADES_FULL_SPEED(boat.Vehicle);
                    NativeFunction.Natives.CONTROL_LANDING_GEAR(boat.Vehicle, 3);
                    if (boat.Vehicle.FuelLevel - fuelSpent < 0.0f)
                    {
                        boat.Vehicle.FuelLevel = 0.0f;
                    }
                    else
                    {
                        boat.Vehicle.FuelLevel -= fuelSpent;
                    }
                }
                else
                {
                    Player.Character.Position = destinationDock.ArrivalPosition;
                    Player.Character.Heading = destinationDock.ArrivalHeading;
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
            new Carrier(StaticStrings.LosSantosFerryCarrierID,"Los Santos Ferry", "From Los Santos to Blaine County"),
            new Carrier(StaticStrings.CayoPericoFerryCarrierID,"Cayo Perico Ferry", "It's not just humans we transport"),
        };
    }
    public override void AddDistanceOffset(Vector3 offsetToAdd)
    {
        ArrivalPosition += offsetToAdd;
        BoatArrivalPosition += offsetToAdd;
        base.AddDistanceOffset(offsetToAdd);
    }


    public override void AddLocation(PossibleLocations possibleLocations)
    {
        possibleLocations.Docks.Add(this);
        base.AddLocation(possibleLocations);
    }
}

