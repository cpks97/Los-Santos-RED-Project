using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using LSR.Vehicles;
using Rage;
using Rage.Native;
using RAGENativeUI;
using RAGENativeUI.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

public class ATMMachine : GameLocation // i know m stands for machine, makes it neater tho
{
    private UIMenuItem completeTask;
    private Vector3 PropEntryPosition;
    private float PropEntryHeading;
    private bool IsCancelled;
    private string PlayingDict;
    private string PlayingAnim;
    private bool hasAttachedProp;
    private UIMenu AccountSubMenu;
    private Rage.Object SellingProp;
    private Bank AssociatedBank;
    private bool KeepInteractionGoing;
    private BankInteraction BankInteraction;

    // How many Marked Cash Stack items the player receives on a successful ATM robbery
    public int ATMCashStackMin { get; set; } = 2;
    public int ATMCashStackMax { get; set; } = 5;

    [XmlIgnore]
    public Rage.Object ATMObject { get; private set; } = null;

    public ATMMachine() : base()
    {
    }

    [XmlIgnore]
    public DateTime? DisposeTime { get; private set; }

    public override bool ShowsOnDirectory { get; set; } = false;
    public override bool ShowsOnTaxi { get; set; } = false;
    public override string TypeName { get; set; } = "ATM";
    public override int MapIcon { get; set; } = 500;
    public override float MapIconScale { get; set; } = 0.25f;
    public override string ButtonPromptText { get; set; }

    public override bool CanCurrentlyInteract(ILocationInteractable player)
    {
        ButtonPromptText = $"Interact with {Name} ATM";
        return !player.ActivityManager.IsPerformingActivity && EntrancePosition != Vector3.Zero
            || (ATMObject.Exists() && player.CurrentLookedAtObject.Exists() && ATMObject.Handle == player.CurrentLookedAtObject.Handle);
    }

    public ATMMachine(Vector3 _EntrancePosition, float _EntranceHeading, string _Name, string _Description, string menuID, Rage.Object machineProp, Bank bank)
        : base(_EntrancePosition, _EntranceHeading, _Name, _Description)
    {
        MenuID = menuID;
        ATMObject = machineProp;
        AssociatedBank = bank;
    }

    public override void OnInteract()
    {
        if (IsLocationClosed())
        {
            return;
        }
        if (CanInteract)
        {
            Player.ActivityManager.IsInteractingWithLocation = true;
            CanInteract = false;
            Player.IsTransacting = true;
            GameFiber.StartNew(delegate
            {
                try
                {
                    Vector3 FinalPlayerPos = new Vector3();
                    float FinalPlayerHeading = 0f;
                    if (ATMObject != null && ATMObject.Exists())
                    {
                        MachineOffsetResult machineInteraction = new MachineOffsetResult(Player, ATMObject);
                        machineInteraction.StandingOffsetPosition = 0.5f;
                        machineInteraction.GetPropEntry();
                        FinalPlayerPos = machineInteraction.PropEntryPosition;
                        FinalPlayerHeading = machineInteraction.PropEntryHeading;
                    }
                    else
                    {
                        FinalPlayerPos = EntrancePosition;
                        FinalPlayerHeading = EntranceHeading;
                    }

                    MoveInteraction moveInteraction = new MoveInteraction(Player, FinalPlayerPos, FinalPlayerHeading);
                    if (moveInteraction.MoveToMachine(1.0f))
                    {
                        CreateInteractionMenu();
                        AddATMMenuItems();
                    }
                    FullDispose();
                    Player.ActivityManager.IsInteractingWithLocation = false;
                    Player.IsTransacting = false;
                    CanInteract = true;
                }
                catch (Exception ex)
                {
                    EntryPoint.WriteToConsole("Location Interaction" + ex.Message + " " + ex.StackTrace, 0);
                    EntryPoint.ModController.CrashUnload();
                }
            }, "ATM Interact");
        }
    }

    public override void OnItemPurchased(ModItem modItem, MenuItem menuItem, int totalItems)
    {
        MenuPool.CloseAllMenus();
        StartMachineBuyAnimation();
        base.OnItemPurchased(modItem, menuItem, totalItems);
        Transaction.PurchaseMenu?.Show();
    }

    private void FullDispose()
    {
        NativeFunction.Natives.STOP_GAMEPLAY_HINT(false);
        Game.LocalPlayer.HasControl = true;
        KeepInteractionGoing = false;
        Player.ButtonPrompts.RemovePrompts("ATM");
        EndUseMachine();
    }

    private void StartMachineBuyAnimation()
    {
    }

    private bool StartUseMachine()
    {
        PlayingDict = "anim@mp_atm@enter";
        PlayingAnim = "enter";
        bool IsCompleted = false;
        AnimationWatcher aw = new AnimationWatcher();
        AnimationDictionary.RequestAnimationDictionay(PlayingDict);
        EntryPoint.WriteToConsole("ATM START ENTRY");
        NativeFunction.CallByName<uint>("TASK_PLAY_ANIM", Player.Character, PlayingDict, PlayingAnim, 4.0f, -4.0f, -1, (int)AnimationFlags.StayInEndFrame, 0, false, false, false);
        while (Player.ActivityManager.CanPerformActivitiesExtended && !IsCancelled)
        {
            Player.WeaponEquipment.SetUnarmed();
            float AnimationTime = NativeFunction.CallByName<float>("GET_ENTITY_ANIM_CURRENT_TIME", Player.Character, PlayingDict, PlayingAnim);
            if (AnimationTime >= 1.0f)
            {
                EntryPoint.WriteToConsole("ATM START ENTRY COMPLETE");
                IsCompleted = true;
                break;
            }
            else if (Player.IsMoveControlPressed)
            {
                break;
            }
            GameFiber.Yield();
        }
        if (!IsCompleted)
        {
            return false;
        }
        EntryPoint.WriteToConsole("ATM START BASE");
        PlayingDict = "anim@mp_atm@base";
        PlayingAnim = "base";
        AnimationDictionary.RequestAnimationDictionay(PlayingDict);
        NativeFunction.CallByName<uint>("TASK_PLAY_ANIM", Player.Character, PlayingDict, PlayingAnim, 4.0f, -4.0f, -1, (int)AnimationFlags.Loop, 0, false, false, false);
        return IsCompleted;
    }

    private void EndUseMachine()
    {
        PlayingDict = "anim@mp_atm@exit";
        PlayingAnim = "exit";
        AnimationDictionary.RequestAnimationDictionay(PlayingDict);
        AnimationWatcher aw = new AnimationWatcher();
        NativeFunction.CallByName<uint>("TASK_PLAY_ANIM", Player.Character, PlayingDict, PlayingAnim, 4.0f, -4.0f, -1, 0, 0, false, false, false);
        while (Player.ActivityManager.CanPerformActivitiesExtended && !IsCancelled)
        {
            Player.WeaponEquipment.SetUnarmed();
            float AnimationTime = NativeFunction.CallByName<float>("GET_ENTITY_ANIM_CURRENT_TIME", Player.Character, PlayingDict, PlayingAnim);
            if (!aw.IsAnimationRunning(AnimationTime))
            {
                break;
            }
            GameFiber.Yield();
        }
        NativeFunction.Natives.CLEAR_PED_TASKS(Player.Character);
    }

    public override void StoreData(IShopMenus shopMenus, IAgencies agencies, IGangs gangs, IZones zones, IJurisdictions jurisdictions, IGangTerritories gangTerritories, INameProvideable names, ICrimes crimes, IPedGroups PedGroups, IEntityProvideable world, IStreets streets, ILocationTypes locationTypes, ISettingsProvideable settings, IPlateTypes plateTypes, IOrganizations associations, IContacts contacts, IInteriors interiors, ILocationInteractable player, IModItems modItems, IWeapons weapons, ITimeControllable time, IPlacesOfInterest placesOfInterest, IIssuableWeapons issuableWeapons, IHeads heads, IDispatchablePeople dispatchablePeople, ModDataFileManager modDataFileManager)
    {
        Bank closestBank = placesOfInterest.PossibleLocations.Banks.Where(x => x.IsEnabled).OrderBy(x => x.EntrancePosition.DistanceTo2D(EntrancePosition)).FirstOrDefault();
        AssociatedBank = closestBank;

        if (AssociatedBank == null)
        {
            EntryPoint.WriteToConsole($"ATM MACHINE HAS NO BANK ON SETUP");
        }
        else
        {
            EntryPoint.WriteToConsole($"ATM MACHINE HAS BANK ON SETUP {AssociatedBank.Name}");
        }

        base.StoreData(shopMenus, agencies, gangs, zones, jurisdictions, gangTerritories, names, crimes, PedGroups, world, streets, locationTypes, settings, plateTypes, associations, contacts, interiors, player, modItems, weapons, time, placesOfInterest, issuableWeapons, heads, dispatchablePeople, modDataFileManager);
    }

    public override void AddLocation(PossibleLocations possibleLocations)
    {
        possibleLocations.ATMMachines.Add(this);
        base.AddLocation(possibleLocations);
    }

    private void AddATMMenuItems()
    {
        UIMenuItem bankInteractionMenuItem = new UIMenuItem("Access Bank Account");
        bankInteractionMenuItem.Activated += BankInteractionMenuItem_Activated;
        UIMenuItem robATMMenuItem = new UIMenuItem("Break into ATM", "Break into ATM using a drill");
        robATMMenuItem.Activated += RobATMMenuItem_Activated;
        InteractionMenu.AddItem(bankInteractionMenuItem);
        InteractionMenu.AddItem(robATMMenuItem);
        InteractionMenu.Visible = true;
        while (IsAnyMenuVisible || KeepInteractionGoing)
        {
            MenuPool.ProcessMenus();
            GameFiber.Yield();
        }
        BankInteraction?.Dispose();
        DisposeInteractionMenu();
    }

    private void RobATMMenuItem_Activated(UIMenu sender, UIMenuItem selectedItem)
    {
        DrillItem DrillItem;
        if (Player.ActivityManager.HasDrillInHand && Player.ActivityManager.CurrentDrill != null)
        {
            DrillItem = Player.ActivityManager.CurrentDrill;
        }
        else
        {
            InventoryItem drillInventory = Player.Inventory.ItemsList.Where(x => x.ModItem != null && x.ModItem.ItemType == ItemType.Equipment && x.ModItem.ItemSubType == ItemSubType.Tool && x.ModItem.GetType() == typeof(DrillItem)).FirstOrDefault();
            if (drillInventory == null)
            {
                Game.DisplaySubtitle("Need a Drill to interact");
                return;
            }
            DrillItem = (DrillItem)drillInventory.ModItem;
            if (DrillItem == null)
            {
                Game.DisplaySubtitle("Need a Drill to interact");
                return;
            }
        }

        // Track whether the drill completed successfully via the callback
        bool drillSucceeded = false;

        // Start crime reporting so witnesses can call police while drilling is happening
        Player.Violations.SetContinuouslyViolating(StaticStrings.BankRobberyCrimeID);

        // PerformDrillingAnimation is fully synchronous — blocks until drilling finishes or player cancels
        DrillItem.PerformDrillingAnimation(Player, () => { drillSucceeded = true; OnDrillComplete(); }, true, Player.ActivityManager.ActiveDoorInterior);

        // Drilling is now finished — stop crime reporting regardless of outcome
        Player.Violations.StopContinuouslyViolating(StaticStrings.BankRobberyCrimeID);

        if (drillSucceeded)
        {
            // Small buffer to let DrillItem's own CLEAR_PED_TASKS finish before we start the grab anim
            GameFiber.Sleep(200);
            PerformGrabAnimation();
            GiveMarkedCashStacks();

            // Directly start a police investigation at the ATM position with a player description.
            // This bypasses the AnyPoliceCanSeePlayer requirement and makes responding officers
            // actively suspicious and pursue the player rather than just mill around the scene.
            // havePlayerDescription: true is what sets Investigation.IsSuspicious = true on arrival.
            Player.Investigation.Start(
                Player.Character.Position,  // where to investigate
                true,                       // havePlayerDescription — officers will actively pursue
                true,                       // requiresPolice
                false,                      // requiresEMS
                false                       // requiresFirefighters
            );
        }
    }

    private void BankInteractionMenuItem_Activated(UIMenu sender, UIMenuItem selectedItem)
    {
        InteractionMenu.Visible = false;
        InteractionMenu.Clear();
        if (StartUseMachine())
        {
            BankInteraction = new BankInteraction(Player, AssociatedBank);
            BankInteraction.Start(MenuPool, InteractionMenu, true);
        }
    }

    private void OnDrillComplete()
    {
        // Mark ATM as robbed for 1 in-game day (existing cooldown logic — unchanged)
        DisposeTime = Time.CurrentDateTime.AddDays(1);
        IsTemporarilyClosed = true;
        MenuPool.CloseAllMenus();
        // Grab animation, item reward and investigation are all handled in
        // RobATMMenuItem_Activated after PerformDrillingAnimation returns,
        // so nothing here overlaps with the drill animation
    }

    private void PerformGrabAnimation()
    {
        string grabDict = "oddjobs@shop_robbery@rob_till";
        string grabIntro = "enter";
        string grabLoop = "loop";

        AnimationDictionary.RequestAnimationDictionay(grabDict);

        // Play intro then loop once — same sequence pattern as TheftInteract.PerformAnimation
        unsafe
        {
            int seq = 0;
            NativeFunction.CallByName<bool>("OPEN_SEQUENCE_TASK", &seq);
            NativeFunction.CallByName<bool>("TASK_PLAY_ANIM", 0, grabDict, grabIntro, 4.0f, -4.0f, -1, 0, 0, false, false, false);
            NativeFunction.CallByName<bool>("TASK_PLAY_ANIM", 0, grabDict, grabLoop, 4.0f, -4.0f, -1, 0, 0, false, false, false);
            NativeFunction.CallByName<bool>("SET_SEQUENCE_TO_REPEAT", seq, false);
            NativeFunction.CallByName<bool>("CLOSE_SEQUENCE_TASK", seq);
            NativeFunction.CallByName<bool>("TASK_PERFORM_SEQUENCE", Player.Character, seq);
            NativeFunction.CallByName<bool>("CLEAR_SEQUENCE_TASK", &seq);
        }

        // Wait for the loop to finish, with a safety timeout so it can never get stuck
        uint grabStartTime = Game.GameTime;
        uint grabTimeout = 4000;
        while (Player.ActivityManager.CanPerformActivitiesExtended && Game.GameTime - grabStartTime < grabTimeout)
        {
            Player.WeaponEquipment.SetUnarmed();
            float animTime = NativeFunction.CallByName<float>("GET_ENTITY_ANIM_CURRENT_TIME", Player.Character, grabDict, grabLoop);
            if (animTime >= 0.95f)
            {
                break;
            }
            GameFiber.Yield();
        }

        NativeFunction.Natives.CLEAR_PED_TASKS(Player.Character);
    }

    private void GiveMarkedCashStacks()
    {
        ModItem markedCashItem = ModItems?.Get("Marked Cash Stack");
        if (markedCashItem == null)
        {
            EntryPoint.WriteToConsole("ATM ROB: Could not find 'Marked Cash Stack' item — check item name in XML");
            return;
        }

        int stacksToGive = RandomItems.GetRandomNumberInt(ATMCashStackMin, ATMCashStackMax);
        Player.Inventory.Add(markedCashItem, stacksToGive);
        NativeHelper.PlaySuccessSound();
        // Matches the exact format used in ItemTheftInteract.GiveReward()
        Game.DisplaySubtitle($"Got Marked Cash Stack ({stacksToGive})");
        EntryPoint.WriteToConsole($"ATM ROB: Gave {stacksToGive} Marked Cash Stack(s)");
    }
}
