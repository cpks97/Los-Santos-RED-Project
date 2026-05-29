using ExtensionsMethods;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using LSR.Vehicles;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace LosSantosRED.lsr.Player.ActiveTasks
{
    public class ContractKillerTask : IPlayerTask
    {
        private ITaskAssignable Player;
        private ITimeReportable Time;
        private IGangs Gangs;
        private PlayerTasks PlayerTasks;
        private IPlacesOfInterest PlacesOfInterest;
        private List<DeadDrop> ActiveDrops = new List<DeadDrop>();
        private ISettingsProvideable Settings;
        private IEntityProvideable World;
        private ICrimes Crimes;
        private IShopMenus ShopMenus;
        private PlayerTask CurrentTask;
        private int MoneyToRecieve;
        private DeadDrop myDrop;
        private IWeapons Weapons;
        private INameProvideable Names;
        private bool VictimIsMale;
        private string VictimName;
        private bool VictimIsAtHome;
        private GameLocation VictimLocation;
        private Vector3 VictimSpawnPosition;
        private float VictimSpawnHeading;
        private readonly List<string> FemaleVictimPossibleModels = new List<string>() { "a_f_y_fitness_02", "cs_guadalope", "a_f_y_eastsa_03", "s_f_m_sweatshop_01", "cs_mrs_thornhill", "a_f_y_beach_01", "a_f_y_golfer_01", "a_f_o_indian_01", "a_f_m_eastsa_01", "a_f_y_fitness_01", "a_f_y_hipster_02", "a_f_m_soucent_02", "a_f_y_tennis_01", "a_f_y_hippie_01", "a_f_m_fatwhite_01", "u_f_y_mistress", "a_f_y_hiker_01" };

        private readonly List<string> MaleVictimPossibleModels = new List<string>() { "a_m_m_afriamer_01", "a_m_m_beach_01","a_m_m_bevhills_01", "a_m_m_bevhills_02", "a_m_m_business_01", "a_m_m_fatlatin_01","a_m_m_genfat_01", "a_m_m_malibu_01", "a_m_m_ktown_01", "a_m_m_mexcntry_01",
            "a_m_m_soucent_01", "a_m_m_soucent_02", "a_m_m_tourist_01", "a_m_y_bevhills_01", "a_m_y_bevhills_02","a_m_y_beachvesp_01","a_m_y_business_02","a_m_y_business_01","a_m_y_business_03","a_m_y_clubcust_01","a_m_y_genstreet_01","a_m_y_genstreet_02","a_m_y_hipster_01","a_m_y_hipster_03","a_m_y_ktown_02","a_m_y_polynesian_01","a_m_y_soucent_02", "a_m_y_mexthug_01", "u_m_m_partytarget", "u_m_y_fibmugger_01", "a_m_m_salton_03", "a_m_m_malibu_01", "csb_chin_goon" };

        private bool HasSpawnPosition => VictimSpawnPosition != Vector3.Zero;
        private int SpawnPositionCellX;
        private int SpawnPositionCellY;
        private bool IsVictimSpawned;
        private int GameTimeToWaitBeforeComplications;
        private string VictimModel;

        private PedExt Victim;
        private PedVariation VictimVariation;
        private bool HasAddedComplications;
        private bool WillAddComplications;
        private object pedHeadshotHandle;
        private bool VictimIsCustomer;
        private bool WillFlee;
        private bool WillFight;
        private ShopMenu VictimShopMenu;
        private WeaponInformation VictimWeapon;
        private FixerContact Contact;

        private bool IsPlayerFarFromVictim => Victim != null && Victim.Pedestrian.Exists() && !NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, SpawnPositionCellX, SpawnPositionCellY, 10) && Victim.Pedestrian.DistanceTo2D(Player.Character) >= 850f;
        private bool IsPlayerNearVictimSpawn => SpawnPositionCellX != -1 && SpawnPositionCellY != -1 && NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, SpawnPositionCellX, SpawnPositionCellY, 6);
        public ContractKillerTask(ITaskAssignable player, ITimeReportable time, IGangs gangs, PlayerTasks playerTasks, IPlacesOfInterest placesOfInterest, List<DeadDrop> activeDrops, ISettingsProvideable settings, IEntityProvideable world,
            ICrimes crimes, INameProvideable names, IWeapons weapons, IShopMenus shopMenus, FixerContact fixerContact)
        {
            Player = player;
            Time = time;
            Gangs = gangs;
            PlayerTasks = playerTasks;
            PlacesOfInterest = placesOfInterest;
            ActiveDrops = activeDrops;
            Settings = settings;
            World = world;
            Crimes = crimes;
            Names = names;
            Weapons = weapons;
            ShopMenus = shopMenus;
            Contact = fixerContact;
        }
        public void Setup()
        {

        }
        public void Dispose()
        {
            if (Victim != null && Victim.Pedestrian.Exists())
            {
                Victim.DeleteBlip();
                Victim.Pedestrian.IsPersistent = false;
                Victim.Pedestrian.Delete();
            }
            if (VictimLocation != null)
            {
                VictimLocation.IsPlayerInterestedInLocation = false;
            }
        }
        public void Start(FixerContact contact)
        {
            Contact = contact;
            if (Contact == null)
            {
                return;
            }
            if (PlayerTasks.CanStartNewTask(Contact.Name))
            {
                GetPedInformation();
                if (HasSpawnPosition)
                {
                    GetPayment();
                    SendInitialInstructionsMessage();
                    AddTask();
                    GameFiber PayoffFiber = GameFiber.StartNew(delegate
                    {
                        try
                        {
                            Loop();
                            FinishTask();
                        }
                        catch (Exception ex)
                        {
                            EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                            EntryPoint.ModController.CrashUnload();
                        }
                    }, "PayoffFiber");
                }
                else
                {
                    SendTaskAbortMessage();
                }
            }
        }
        private void GetPedInformation()
        {
            VictimIsMale = RandomItems.RandomPercent(60);
            VictimName = Names.GetRandomName(VictimIsMale);
            VictimIsAtHome = RandomItems.RandomPercent(30);
            if (VictimIsAtHome)
            {
                VictimLocation = PlacesOfInterest.PossibleLocations.Residences.Where(x => !x.IsOwnedOrRented && x.IsCorrectMap(World.IsMPMapLoaded) && x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState)).PickRandom();
            }
            else
            {
                VictimLocation = PlacesOfInterest.PossibleLocations.VictimTaskLocations().Where(x => x.IsCorrectMap(World.IsMPMapLoaded) && x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState)).PickRandom();
            }

            VictimVariation = null;
            if (VictimIsMale)
            {
                VictimModel = MaleVictimPossibleModels.Where(x => Player.ModelName.ToLower() != x.ToLower()).PickRandom();
            }
            else
            {
                VictimModel = FemaleVictimPossibleModels.Where(x => Player.ModelName.ToLower() != x.ToLower()).PickRandom();
            }
            if (VictimLocation != null)
            {
                VictimSpawnPosition = VictimLocation.EntrancePosition;
                VictimSpawnHeading = VictimLocation.EntranceHeading;
                SpawnPositionCellX = (int)(VictimSpawnPosition.X / EntryPoint.CellSize);
                SpawnPositionCellY = (int)(VictimSpawnPosition.Y / EntryPoint.CellSize);
            }
            else
            {
                VictimSpawnPosition = Vector3.Zero;
                SpawnPositionCellX = -1;
                SpawnPositionCellY = -1;
            }
        }
        private void Loop()
        {
            while (true)
            {
                if (CurrentTask == null || !CurrentTask.IsActive)
                {
                    //EntryPoint.WriteToConsoleTestLong($"Task Inactive for {Contact.Name}");
                    break;
                }
                //if(!IsWitnessSpawned && IsPlayerNearWitnessSpawn && !IsPlayerFarFromWitness)
                //{
                //    SpawnWitness();
                //    EntryPoint.WriteToConsole("SHUIT THE FUCK UP");
                //}
                //else if(IsWitnessSpawned && IsPlayerFarFromWitness)
                //{
                //    DespawnWitness();
                //    if(Witness.HasSeenPlayerCommitCrime)
                //    {
                //        //EntryPoint.WriteToConsoleTestLong("Witness Elimination WITNESS FLED");
                //        Game.DisplayHelp($"{Contact.Name} The witness fled");
                //        break;
                //    }
                //}


                if (IsPlayerNearVictimSpawn)
                {
                    if (!IsVictimSpawned)
                    {
                        SpawnVictim();
                        EntryPoint.WriteToConsole("SHUT THE FUCK UP");
                    }
                }
                else
                {
                    if (IsVictimSpawned && !IsPlayerNearVictimSpawn && IsPlayerFarFromVictim)
                    {
                        EntryPoint.WriteToConsole("DESPAWN 1");
                        DespawnVictim();
                        if (Victim.HasSeenPlayerCommitCrime)
                        {
                            //EntryPoint.WriteToConsoleTestLong("Witness Elimination WITNESS FLED");
                            Game.DisplayHelp($"{Contact.Name} The target fled");
                            break;
                        }
                    }
                }

                if (IsVictimSpawned && Victim != null && Victim.Pedestrian.Exists() && Victim.Pedestrian.IsDead)
                {
                    Victim.Pedestrian.IsPersistent = false;
                    Victim.DeleteBlip();
                    //EntryPoint.WriteToConsoleTestLong("Contract Killer VICTIM WAS KILLED");
                    CurrentTask.OnReadyForPayment(true);
                    break;
                }
                else if (IsVictimSpawned && Victim != null && !Victim.Pedestrian.Exists() && IsPlayerFarFromVictim && !IsPlayerNearVictimSpawn)//somehow it got removed, set it as despawned
                {
                    EntryPoint.WriteToConsole("DESPAWN 2");
                    DespawnVictim();

                }
                GameFiber.Sleep(1000);
            }
        }
        private void FinishTask()
        {
            if (VictimLocation != null)
            {
                VictimLocation.IsPlayerInterestedInLocation = false;
            }
            if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
            {
                //GameFiber.Sleep(RandomItems.GetRandomNumberInt(5000, 10000));

                StartDeadDropPayment();//sets u teh whole dead drop thingamajic
            }
            else if (CurrentTask != null && CurrentTask.IsActive)
            {
                //GameFiber.Sleep(RandomItems.GetRandomNumberInt(5000, 10000));
                SetFailed();
            }
            else
            {
                Dispose();
            }
        }
        private void SetCompleted()
        {
            //EntryPoint.WriteToConsoleTestLong("Contract Killing COMPLETED");

            PlayerTasks.CompleteTask(Contact, true);

            SendCompletedMessage();
        }
        private void SetFailed()
        {
            //EntryPoint.WriteToConsoleTestLong("Contract Killing FAILED");
            SendFailMessage();
            PlayerTasks.FailTask(Contact);
        }
        private void StartDeadDropPayment()
        {
            myDrop = PlacesOfInterest.GetUsableDeadDrop(World.IsMPMapLoaded, Player.CurrentLocation);
            if (myDrop != null)
            {
                myDrop.SetupDrop(MoneyToRecieve, false);
                ActiveDrops.Add(myDrop);
                SendDeadDropStartMessage();
                while (true)
                {
                    if (CurrentTask == null || !CurrentTask.IsActive)
                    {
                        //EntryPoint.WriteToConsoleTestLong($"Task Inactive for {Contact.Name}");
                        break;
                    }
                    if (myDrop.InteractionComplete)
                    {
                        //EntryPoint.WriteToConsoleTestLong($"Picked up money for Contract Killing for {Contact.Name}");
                        Game.DisplayHelp($"{Contact.Name} Money Picked Up");
                        break;
                    }
                    GameFiber.Sleep(1000);
                }
                if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
                {
                    PlayerTasks.CompleteTask(Contact, true);
                }
                myDrop?.Reset();
                myDrop?.Deactivate(true);
            }
            else
            {

                PlayerTasks.CompleteTask(Contact, true);
                SendQuickPaymentMessage();
            }
        }
        private void AddTask()
        {
            //EntryPoint.WriteToConsoleTestLong($"You are hired to kill a someone with a hit on them!");
            PlayerTasks.AddTask(Contact, 0, 2000, 0, -500, 7, "Contract Killing");
            CurrentTask = PlayerTasks.GetTask(Contact.Name);
            IsVictimSpawned = false;
            GameTimeToWaitBeforeComplications = RandomItems.GetRandomNumberInt(3000, 10000);
            HasAddedComplications = false;
            WillAddComplications = RandomItems.RandomPercent(Settings.SettingsManager.TaskSettings.ContractKillerComplicationsPercentage);
            WillFlee = false;
            WillFight = false;
            if (WillAddComplications)
            {
                if (RandomItems.RandomPercent(50))
                {
                    WillFlee = true;
                }
                else
                {
                    WillFight = true;
                }
            }
            VictimWeapon = null;
            if (RandomItems.RandomPercent(40))
            {
                Weapons.GetRandomRegularWeapon(WeaponCategory.Melee);
            }
            else
            {
                if (RandomItems.RandomPercent(50))
                {
                    Weapons.GetRandomRegularWeapon(WeaponCategory.Pistol);
                }
                else
                {
                    if (RandomItems.RandomPercent(50))
                    {
                        Weapons.GetRandomRegularWeapon(WeaponCategory.AR);
                    }
                    else
                    {
                        Weapons.GetRandomRegularWeapon(WeaponCategory.Shotgun);
                    }
                }
            }
            VictimShopMenu = null;
            VictimIsCustomer = RandomItems.RandomPercent(30f);
            if (VictimIsCustomer)
            {
                VictimShopMenu = ShopMenus.GetRandomDrugCustomerMenu();
            }

            if (VictimLocation != null)
            {
                VictimLocation.IsPlayerInterestedInLocation = true;
            }
        }
        private bool SpawnVictim()
        {
            if (VictimSpawnPosition != Vector3.Zero)
            {
                World.Pedestrians.CleanupAmbient();
                Ped ped = new Ped(VictimModel, VictimSpawnPosition, VictimSpawnHeading);
                GameFiber.Yield();
                NativeFunction.Natives.SET_MODEL_AS_NO_LONGER_NEEDED(Game.GetHashKey(VictimModel));
                if (ped.Exists())
                {

                    string GroupName = "Man";
                    if (!VictimIsMale)
                    {
                        GroupName = "Woman";
                    }
                    Victim = new PedExt(ped, Settings, Crimes, Weapons, VictimName, GroupName, World);
                    if (Settings.SettingsManager.TaskSettings.ShowEntityBlips)
                    {
                        Victim.AddBlip();
                    }
                    World.Pedestrians.AddEntity(Victim);
                    Victim.WasEverSetPersistent = true;
                    Victim.CanBeAmbientTasked = true;
                    Victim.CanBeTasked = true;
                    Victim.WasModSpawned = true;
                    Victim.IsManuallyDeleted = true;
                    IsVictimSpawned = true;
                    if (VictimVariation == null)
                    {
                        Victim.Pedestrian.RandomizeVariation();
                        VictimVariation = NativeHelper.GetPedVariation(Victim.Pedestrian);
                    }
                    else
                    {
                        VictimVariation.ApplyToPed(Victim.Pedestrian, true);
                    }
                    pedHeadshotHandle = NativeFunction.Natives.REGISTER_PEDHEADSHOT<int>(ped);
                    if (VictimIsCustomer)
                    {
                        Victim.SetupTransactionItems(VictimShopMenu, false);
                    }
                    if (WillAddComplications)
                    {
                        ped.RelationshipGroup = RelationshipGroup.HatesPlayer;
                        if (WillFlee)//flee
                        {
                            Victim.WillCallPolice = true;
                            Victim.WillCallPoliceIntense = true;
                            Victim.WillFight = false;
                            Victim.WillFightPolice = false;
                            Victim.WillAlwaysFightPolice = false;
                            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFlee, true);
                            NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 2, true);
                            //EntryPoint.WriteToConsoleTestLong("WITNESS ELIMINATION, THE WITNESS WITH FLEE FROM YOU");
                        }
                        else if (WillFight)
                        {
                            Victim.WillFight = true;
                            Victim.WillCallPolice = false;
                            Victim.WillCallPoliceIntense = false;
                            Victim.WillFightPolice = true;
                            Victim.WillAlwaysFightPolice = true;
                            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFight, true);
                            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_CanFightArmedPedsWhenNotArmed, true);
                            NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 0, false);

                            if (VictimWeapon != null)
                            {
                                NativeFunction.Natives.GIVE_WEAPON_TO_PED(ped, (uint)VictimWeapon.Hash, VictimWeapon.AmmoAmount, false, false);
                            }
                            //EntryPoint.WriteToConsoleTestLong("WITNESS ELIMINATION, THE WITNESS WITH FIGHT YOU");
                        }
                        //they either know and flee, or know and fight     
                    }
                    //GameFiber.Sleep(1000);
                    SendVictimSpawnedMessage();
                    return true;
                }
            }
            return false;
        }
        private void SendVictimSpawnedMessage()
        {

            List<string> Replies;
            string LookingForItem = "";
            if (VictimIsCustomer)
            {
                MenuItem myMenuItem = Victim.ShopMenu?.Items.Where(x => x.NumberOfItemsToPurchaseFromPlayer > 0 && x.IsIllicilt).PickRandom();
                if (myMenuItem != null)
                {
                    LookingForItem = myMenuItem.ModItemName;
                }
            }
            if (NativeFunction.Natives.IsPedheadshotReady<bool>(pedHeadshotHandle))
            {
                Replies = new List<string>() {
                    $"Picture of ~y~{VictimName}~s~ attached. I heard they were still around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"Sent you a picture of ~y~{VictimName}~s~. They should still be around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"~y~{VictimName}~s~. They are still around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"The name is ~y~{VictimName}~s~, pic attached. They are loitering around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"Remember, ~y~{VictimName}~s~ is the name. I also sent a picture. I got word they are still around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                     };
                string PickedReply = Replies.PickRandom();
                if (VictimIsCustomer && LookingForItem != "")
                {
                    List<string> ItemReplies = new List<string>() {
                    $" They are probably looking for ~p~{LookingForItem}~s~.",
                    $" They like ~p~{LookingForItem}~s~.",
                    $" Will be looking to buy ~p~{LookingForItem}~s~.",
                    $" They are interested in ~p~{LookingForItem}~s~.",
                    $" The target likes ~p~{LookingForItem}~s~.",
                     };
                    PickedReply += ItemReplies.PickRandom();
                }

                if (WillFight || WillFlee)
                {
                    PickedReply += " ~s~The target knows you're coming, be ready.";
                }
                string str = NativeFunction.Natives.GET_PEDHEADSHOT_TXD_STRING<string>(pedHeadshotHandle);
                EntryPoint.WriteToConsole($"CONTRACT KILLER SENT PICTURE MESSAGE {str}");
                Player.CellPhone.AddCustomScheduledText(Contact, PickedReply, Time.CurrentDateTime, str, true);
            }
            else
            {
                Replies = new List<string>() {
                    $"~y~{VictimName}~s~. I heard they were still around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"~y~{VictimName}~s~. They should still be around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"~y~{VictimName}~s~. They are still around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"The name is ~y~{VictimName}~s~. They are loitering around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                    $"Remember, ~y~{VictimName}~s~ is the name. I got word they are still around ~p~{VictimLocation.Name} {VictimLocation.FullStreetAddress}~s~.",
                     };
                string PickedReply = Replies.PickRandom();
                if (VictimIsCustomer && LookingForItem != "")
                {
                    List<string> ItemReplies = new List<string>() {
                    $" They are probably looking for ~p~{LookingForItem}~s~.",
                    $" They like ~p~{LookingForItem}~s~.",
                    $" Will be looking to buy ~p~{LookingForItem}~s~.",
                    $" They are interested in ~p~{LookingForItem}~s~.",
                    $" The target likes ~p~{LookingForItem}~s~.",
                     };
                    PickedReply += ItemReplies.PickRandom();
                }

                if (WillFight || WillFlee)
                {
                    PickedReply += " ~s~The target might have gotten wind, be careful.";
                }
                EntryPoint.WriteToConsole("CONTRACT KILLER SENT REGULAR MESSAGE");
                Player.CellPhone.AddCustomScheduledText(Contact, PickedReply, Time.CurrentDateTime, null, true);
            }
        }
        private void DespawnVictim()
        {
            if (Victim != null && Victim.Pedestrian.Exists())
            {
                Victim.DeleteBlip();
                Victim.Pedestrian.Delete();
                //EntryPoint.WriteToConsoleTestLong("Contract Killer DESPAWNED WITNESS");
            }
            IsVictimSpawned = false;
            EntryPoint.WriteToConsole("IS VICTIM DESPAWNED");
        }
        private void GetPayment()
        {
            MoneyToRecieve = RandomItems.GetRandomNumberInt(Settings.SettingsManager.TaskSettings.ContractKillerPaymentMin, Settings.SettingsManager.TaskSettings.ContractKillerPaymentMax).Round(500);
            if (MoneyToRecieve <= 0)
            {
                MoneyToRecieve = 500;
            }
        }
        private void SendTaskAbortMessage()
        {
            List<string> Replies = new List<string>() {
                    "Nothing yet, I'll let you know",
                    "I've got nothing for you yet",
                    "Give me a few days",
                    "Not a lot to be done right now",
                    "We will let you know when you can do something for us",
                    "Check back later.",
                    };
            Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
        }
        private void SendInitialInstructionsMessage()
        {
            List<string> Replies;
            if (VictimIsAtHome)
            {
                Replies = new List<string>() {
                    $"Got a hit for you here, target is at their home. Their address is ~p~{VictimLocation.FullStreetAddress}~s~. Name ~y~{VictimName}~s~. ${MoneyToRecieve}",
                    $"Get to the house at ~p~{VictimLocation.FullStreetAddress}~s~ and get rid of ~y~{VictimName}~s~. ${MoneyToRecieve} on complation",
                    $"We need to you shut this guy up before he squeals. He lives at ~p~{VictimLocation.FullStreetAddress}~s~. The name is ~y~{VictimName}~s~. Payment of ${MoneyToRecieve}",
                    $"~y~{VictimName}~s~ is living at ~p~{VictimLocation.FullStreetAddress}~s~. They should be home. You know what to do. ${MoneyToRecieve}",
                    $"Need you to make sure ~y~{VictimName}~s~ doesn't make it to the deposition, they live at ~p~{VictimLocation.FullStreetAddress}~s~. ${MoneyToRecieve}",
                     };
            }
            else
            {
                Replies = new List<string>() {
                    $"Got a hit for you, they're hanging around ~p~{VictimLocation.Name}~s~. Address is ~p~{VictimLocation.FullStreetAddress}~s~. Name ~y~{VictimName}~s~. ${MoneyToRecieve}",
                    $"Get to ~p~{VictimLocation.Name}~s~ on ~p~{VictimLocation.FullStreetAddress}~s~ and get rid of ~y~{VictimName}~s~. ${MoneyToRecieve} on complation",
                    $"We need to you shut this guy up before he squeals. He's at ~p~{VictimLocation.Name}~s~ ~p~{VictimLocation.FullStreetAddress}~s~. The name is ~y~{VictimName}~s~. Payment of ${MoneyToRecieve}",
                    $"~y~{VictimName}~s~ is at ~p~{VictimLocation.Name}~s~, address is ~p~{VictimLocation.FullStreetAddress}~s~. You know what to do. ${MoneyToRecieve}",
                    $"Need you to make sure ~y~{VictimName}~s~ doesn't make it to the deposition, they are currently at ~p~{VictimLocation.Name}~s~ on ~p~{VictimLocation.FullStreetAddress}~s~. ${MoneyToRecieve}",
                     };
            }

            Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
        }
        private void SendQuickPaymentMessage()
        {
            List<string> Replies = new List<string>() {
                            $"Seems like that thing we discussed is done? Sending you ${MoneyToRecieve}",
                            $"Word got around that you are done with that thing for us, sending your payment of ${MoneyToRecieve}",
                            $"Sending your payment of ${MoneyToRecieve}",
                            $"Sending ${MoneyToRecieve}",
                            $"Heard you were done. We owe you ${MoneyToRecieve}",
                            };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
        private void SendDeadDropStartMessage()
        {
            List<string> Replies = new List<string>() {
                            $"Pickup your payment of ${MoneyToRecieve} from {myDrop.FullStreetAddress}, its {myDrop.Description}.",
                            $"Go get your payment of ${MoneyToRecieve} from {myDrop.Description}, address is {myDrop.FullStreetAddress}.",
                            };

            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
        private void SendCompletedMessage()
        {
            List<string> Replies = new List<string>() {
                        $"Seems like that thing we discussed is done? Sending you ${MoneyToRecieve}",
                        $"Word got around that you are done with that thing for us, sending your payment of ${MoneyToRecieve}",
                        $"Sending your payment of ${MoneyToRecieve}",
                        $"Sending ${MoneyToRecieve}",
                        $"Heard you were done. We owe you ${MoneyToRecieve}",
                        };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
        private void SendFailMessage()
        {
            List<string> Replies = new List<string>() {
                        $"You spooked them, they are gone. Thanks for nothing.",
                        $"How could you let them get away?",
                        $"You failed the job, dickhead!.",
                        $"You fucking idiot, they got away!",
                        $"I should have hired someone who was more fucking competent!",
                        };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
    }

}
