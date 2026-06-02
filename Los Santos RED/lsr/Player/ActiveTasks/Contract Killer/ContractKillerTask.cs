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
        private FixerContact Contact;

        
        private readonly List<string> FemaleVictimPossibleModels = new List<string>()
        {
            "a_f_y_fitness_02", "cs_guadalope", "a_f_y_eastsa_03", "s_f_m_sweatshop_01",
            "cs_mrs_thornhill", "a_f_y_beach_01", "a_f_y_golfer_01", "a_f_o_indian_01",
            "a_f_m_eastsa_01", "a_f_y_fitness_01", "a_f_y_hipster_02", "a_f_m_soucent_02",
            "a_f_y_tennis_01", "a_f_y_hippie_01", "a_f_m_fatwhite_01", "u_f_y_mistress",
            "a_f_y_hiker_01"
        };

        private readonly List<string> MaleVictimPossibleModels = new List<string>()
        {
            "a_m_m_afriamer_01", "a_m_m_beach_01", "a_m_m_bevhills_01", "a_m_m_bevhills_02",
            "a_m_m_business_01", "a_m_m_fatlatin_01", "a_m_m_genfat_01", "a_m_m_malibu_01",
            "a_m_m_ktown_01", "a_m_m_mexcntry_01", "a_m_m_soucent_01", "a_m_m_soucent_02",
            "a_m_m_tourist_01", "a_m_y_bevhills_01", "a_m_y_bevhills_02", "a_m_y_beachvesp_01",
            "a_m_y_business_02", "a_m_y_business_01", "a_m_y_business_03", "a_m_y_clubcust_01",
            "a_m_y_genstreet_01", "a_m_y_genstreet_02", "a_m_y_hipster_01", "a_m_y_hipster_03",
            "a_m_y_ktown_02", "a_m_y_polynesian_01", "a_m_y_soucent_02", "a_m_y_mexthug_01",
            "u_m_m_partytarget", "u_m_y_fibmugger_01", "a_m_m_salton_03", "a_m_m_malibu_01",
            "csb_chin_goon"
        };

        private readonly List<string> VictimVehicleModels = new List<string>()
        {
            "sultan", "buffalo", "schwarzer", "oracle", "tailgater",
            "fugitive", "jackal", "primo", "ingot", "stanier", "Bison2", "utillitruck", "cavalcade2", "speedo", "bullet", "baller2", "serrano", "stratum", "landstalker", "superd"
        };

       
        private List<VictimData> Victims = new List<VictimData>();

       
        private bool HasSpawnPosition => Victims.Any();

        
        private class VictimData
        {
            public string Name;
            public string Model;
            public bool IsMale;
            public bool IsAtHome;
            public GameLocation Location;
            public Vector3 SpawnPosition;
            public float SpawnHeading;
            public int CellX;
            public int CellY;

            public PedExt Ped;
            public PedVariation Variation;
            public Vehicle Vehicle;
            public object HeadshotHandle;

            public bool IsSpawned;
            public bool IsEliminated;

            public bool WillAddComplications;
            public bool WillFlee;
            public bool WillFight;
            public bool IsCustomer;
            public ShopMenu ShopMenu;
            public WeaponInformation Weapon;


            public bool IsPlayerNearSpawn =>
                CellX != -1 && CellY != -1 &&
                NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, CellX, CellY, 12);

            public bool IsPlayerFarFrom(ITaskAssignable player) =>
                Ped != null && Ped.Pedestrian.Exists() &&
                !NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, CellX, CellY, 10) &&
                Ped.Pedestrian.DistanceTo2D(player.Character) >= 850f;
        }

        public ContractKillerTask(ITaskAssignable player, ITimeReportable time, IGangs gangs,
            PlayerTasks playerTasks, IPlacesOfInterest placesOfInterest, List<DeadDrop> activeDrops,
            ISettingsProvideable settings, IEntityProvideable world, ICrimes crimes,
            INameProvideable names, IWeapons weapons, IShopMenus shopMenus, FixerContact fixerContact)
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

        public void Setup() { }

        public void Dispose()
        {
            foreach (var v in Victims)
            {
                if (v.Ped != null && v.Ped.Pedestrian.Exists())
                {
                    v.Ped.DeleteBlip();
                    v.Ped.Pedestrian.IsPersistent = false;
                    v.Ped.Pedestrian.Delete();
                }
                if (v.Vehicle != null && v.Vehicle.Exists())
                {
                    v.Vehicle.IsPersistent = false;
                    v.Vehicle.Delete();
                }
                if (v.Location != null)
                {
                    v.Location.IsPlayerInterestedInLocation = false;
                }
            }
        }

        public void Start(FixerContact contact)
        {
            Contact = contact;
            if (Contact == null) return;

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
            Victims.Clear();

            
            int targetCount = RandomItems.RandomPercent(30)
                ? RandomItems.GetRandomNumberInt(2, 3)
                : 1;

            for (int i = 0; i < targetCount; i++)
            {
                var v = new VictimData();
                v.IsMale = RandomItems.RandomPercent(60);
                v.Name = Names.GetRandomName(v.IsMale);
                v.IsAtHome = RandomItems.RandomPercent(30);

                if (v.IsAtHome)
                {
                    v.Location = PlacesOfInterest.PossibleLocations.Residences
                        .Where(x => !x.IsOwnedOrRented && x.IsCorrectMap(World.IsMPMapLoaded) &&
                                    x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState))
                        .PickRandom();
                }
                else
                {
                    v.Location = PlacesOfInterest.PossibleLocations.VictimTaskLocations()
                        .Where(x => x.IsCorrectMap(World.IsMPMapLoaded) &&
                                    x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState))
                        .PickRandom();
                }

                if (v.Location == null) continue;

                v.Model = (v.IsMale ? MaleVictimPossibleModels : FemaleVictimPossibleModels)
                    .Where(x => Player.ModelName.ToLower() != x.ToLower())
                    .PickRandom();

                v.SpawnPosition = v.Location.EntrancePosition;
                v.SpawnHeading = v.Location.EntranceHeading;
                v.CellX = (int)(v.SpawnPosition.X / EntryPoint.CellSize);
                v.CellY = (int)(v.SpawnPosition.Y / EntryPoint.CellSize);

                Victims.Add(v);
            }
        }

        
        private void Loop()
        {
            while (true)
            {
                if (CurrentTask == null || !CurrentTask.IsActive) break;

                foreach (var v in Victims)
                {
                    if (v.IsEliminated) continue;

                    if (v.IsPlayerNearSpawn)
                    {
                        if (!v.IsSpawned)
                        {
                            SpawnVictim(v);
                        }
                    }
                    else
                    {
                        if (v.IsSpawned && !v.IsPlayerNearSpawn && v.IsPlayerFarFrom(Player))
                        {
                            EntryPoint.WriteToConsole($"DESPAWN victim {v.Name}");
                            DespawnVictim(v);
                            if (v.Ped != null && v.Ped.HasSeenPlayerCommitCrime)
                            {
                                Game.DisplayHelp($"{Contact.Name}: {v.Name} fled!");
                                // Count a fled target as eliminated (task will fail at end)
                                v.IsEliminated = true;
                            }
                        }
                    }

                    
                    if (v.IsSpawned && v.Ped != null && v.Ped.Pedestrian.Exists() && v.Ped.Pedestrian.IsDead)
                    {
                        v.Ped.Pedestrian.IsPersistent = false;
                        v.Ped.DeleteBlip();
                        v.IsEliminated = true;
                        v.IsSpawned = false;
                        EntryPoint.WriteToConsole($"CONTRACT KILLER: {v.Name} eliminated");
                    }
                    // Ped disappeared unexpectedly
                    else if (v.IsSpawned && v.Ped != null && !v.Ped.Pedestrian.Exists() &&
                             v.IsPlayerFarFrom(Player) && !v.IsPlayerNearSpawn)
                    {
                        EntryPoint.WriteToConsole($"DESPAWN 2 victim {v.Name}");
                        DespawnVictim(v);
                    }
                }

               
                if (Victims.All(v => v.IsEliminated))
                {
                    // Only pay out if none fled (fled targets were not killed — check Ped is dead)
                    bool allKilled = Victims.All(v => v.Ped == null ||
                        !v.Ped.Pedestrian.Exists() || v.Ped.Pedestrian.IsDead);
                    if (allKilled)
                    {
                        CurrentTask.OnReadyForPayment(true);
                    }
                    break;
                }

                GameFiber.Sleep(1000);
            }
        }

        
        private bool SpawnVictim(VictimData v)
        {
            if (v.SpawnPosition == Vector3.Zero) return false;

            World.Pedestrians.CleanupAmbient();
            Ped ped = new Ped(v.Model, v.SpawnPosition, v.SpawnHeading);
            GameFiber.Yield();
            NativeFunction.Natives.SET_MODEL_AS_NO_LONGER_NEEDED(Game.GetHashKey(v.Model));

            if (!ped.Exists()) return false;

            string groupName = v.IsMale ? "Man" : "Woman";
            v.Ped = new PedExt(ped, Settings, Crimes, Weapons, v.Name, groupName, World);

            if (Settings.SettingsManager.TaskSettings.ShowEntityBlips)
            {
                v.Ped.AddBlip();
            }

            World.Pedestrians.AddEntity(v.Ped);
            v.Ped.WasEverSetPersistent = true;
            v.Ped.CanBeAmbientTasked = true;
            v.Ped.CanBeTasked = true;
            v.Ped.WasModSpawned = true;
            v.Ped.IsManuallyDeleted = true;
            v.IsSpawned = true;

            if (v.Variation == null)
            {
                v.Ped.Pedestrian.RandomizeVariation();
                v.Variation = NativeHelper.GetPedVariation(v.Ped.Pedestrian);
            }
            else
            {
                v.Variation.ApplyToPed(v.Ped.Pedestrian, true);
            }

            v.HeadshotHandle = NativeFunction.Natives.REGISTER_PEDHEADSHOT<int>(ped);

            if (v.IsCustomer)
            {
                v.Ped.SetupTransactionItems(v.ShopMenu, false);
            }

            if (v.WillAddComplications)
            {
                ped.RelationshipGroup = RelationshipGroup.HatesPlayer;
                if (v.WillFlee)
                {
                    v.Ped.WillCallPolice = true;
                    v.Ped.WillCallPoliceIntense = true;
                    v.Ped.WillFight = false;
                    v.Ped.WillFightPolice = false;
                    v.Ped.WillAlwaysFightPolice = false;
                    NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFlee, true);
                    NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 2, true);
                }
                else if (v.WillFight)
                {
                    v.Ped.WillFight = true;
                    v.Ped.WillCallPolice = false;
                    v.Ped.WillCallPoliceIntense = false;
                    v.Ped.WillFightPolice = true;
                    v.Ped.WillAlwaysFightPolice = true;
                    NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFight, true);
                    NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_CanFightArmedPedsWhenNotArmed, true);
                    NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 0, false);
                    if (v.Weapon != null)
                    {
                        NativeFunction.Natives.GIVE_WEAPON_TO_PED(ped, (uint)v.Weapon.Hash, v.Weapon.AmmoAmount, false, false);
                    }
                }
            }

            SendVictimSpawnedMessage(v);

            
            if (RandomItems.RandomPercent(40))
            {
                Vector3 vehSpawnPos = v.SpawnPosition.Around(40f);
                Vehicle veh = new Vehicle(VictimVehicleModels.PickRandom(), vehSpawnPos, v.SpawnHeading);
                GameFiber.Yield();
                if (veh.Exists())
                {
                    v.Vehicle = veh;
                    NativeFunction.Natives.SET_PED_INTO_VEHICLE(ped, veh, -1);
                    NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(ped, veh, 15f, 786603);
                }
            }

            return true;
        }

        private void DespawnVictim(VictimData v)
        {
            if (v.Ped != null && v.Ped.Pedestrian.Exists())
            {
                v.Ped.DeleteBlip();
                v.Ped.Pedestrian.Delete();
            }
            if (v.Vehicle != null && v.Vehicle.Exists())
            {
                v.Vehicle.IsPersistent = false;
                v.Vehicle.Delete();
            }
            v.Vehicle = null;
            v.IsSpawned = false;
            EntryPoint.WriteToConsole($"Victim {v.Name} despawned");
        }

        
        private void AddTask()
        {
            PlayerTasks.AddTask(Contact, 0, 2000, 0, -500, 7, "Contract Killing");
            CurrentTask = PlayerTasks.GetTask(Contact.Name);

            foreach (var v in Victims)
            {
                v.IsSpawned = false;
                v.IsEliminated = false;

                v.WillAddComplications = RandomItems.RandomPercent(Settings.SettingsManager.TaskSettings.ContractKillerComplicationsPercentage);
                v.WillFlee = false;
                v.WillFight = false;
                if (v.WillAddComplications)
                {
                    if (RandomItems.RandomPercent(50)) v.WillFlee = true;
                    else v.WillFight = true;
                }

                v.Weapon = null;
                if (RandomItems.RandomPercent(40))
                {
                    v.Weapon = Weapons.GetRandomRegularWeapon(WeaponCategory.Melee);
                }
                else if (RandomItems.RandomPercent(50))
                {
                    v.Weapon = Weapons.GetRandomRegularWeapon(WeaponCategory.Pistol);
                }
                else if (RandomItems.RandomPercent(50))
                {
                    v.Weapon = Weapons.GetRandomRegularWeapon(WeaponCategory.AR);
                }
                else
                {
                    v.Weapon = Weapons.GetRandomRegularWeapon(WeaponCategory.Shotgun);
                }

                v.IsCustomer = RandomItems.RandomPercent(30f);
                v.ShopMenu = v.IsCustomer ? ShopMenus.GetRandomDrugCustomerMenu() : null;

                if (v.Location != null)
                {
                    v.Location.IsPlayerInterestedInLocation = true;
                }
            }
        }

        private void FinishTask()
        {
            foreach (var v in Victims)
            {
                if (v.Location != null) v.Location.IsPlayerInterestedInLocation = false;
            }

            if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
            {
                StartDeadDropPayment();
            }
            else if (CurrentTask != null && CurrentTask.IsActive)
            {
                SetFailed();
            }
            else
            {
                Dispose();
            }
        }

        private void SetFailed()
        {
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
                    if (CurrentTask == null || !CurrentTask.IsActive) break;
                    if (myDrop.InteractionComplete)
                    {
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

        private void GetPayment()
        {
            MoneyToRecieve = RandomItems.GetRandomNumberInt(
                Settings.SettingsManager.TaskSettings.ContractKillerPaymentMin,
                Settings.SettingsManager.TaskSettings.ContractKillerPaymentMax).Round(500);
            if (MoneyToRecieve <= 0) MoneyToRecieve = 500;
        }

        
        private void SendInitialInstructionsMessage()
        {
            if (Victims.Count == 1)
            {
                
                var v = Victims[0];
                List<string> Replies;
                if (v.IsAtHome)
                {
                    Replies = new List<string>()
                    {
                        $"Got a hit for you here, target is at their home. Their address is ~p~{v.Location.FullStreetAddress}~s~. Name ~y~{v.Name}~s~. ${MoneyToRecieve}",
                        $"Get to the house at ~p~{v.Location.FullStreetAddress}~s~ and get rid of ~y~{v.Name}~s~. ${MoneyToRecieve} on completion",
                        $"We got a contract that's just came in. Target lives at ~p~{v.Location.FullStreetAddress}~s~. The name is ~y~{v.Name}~s~. Payment of ${MoneyToRecieve}",
                        $"~y~{v.Name}~s~ is living at ~p~{v.Location.FullStreetAddress}~s~. They should be home. You know what to do. ${MoneyToRecieve}",
                        $"Need a guy whacked, their name is ~y~{v.Name}~s~ and they live at ~p~{v.Location.FullStreetAddress}~s~. ${MoneyToRecieve}",
                    };
                }
                else
                {
                    Replies = new List<string>()
                    {
                        $"Got a hit for you, they're hanging around ~p~{v.Location.Name}~s~. Address is ~p~{v.Location.FullStreetAddress}~s~. Name ~y~{v.Name}~s~. ${MoneyToRecieve}",
                        $"Get to ~p~{v.Location.Name}~s~ on ~p~{v.Location.FullStreetAddress}~s~ and get rid of ~y~{v.Name}~s~. ${MoneyToRecieve} on completion",
                        $"Need you to take someone out! They're at ~p~{v.Location.Name}~s~ ~p~{v.Location.FullStreetAddress}~s~. The name is ~y~{v.Name}~s~. Payment of ${MoneyToRecieve}",
                        $"~y~{v.Name}~s~ is at ~p~{v.Location.Name}~s~, address is ~p~{v.Location.FullStreetAddress}~s~. You know what to do. ${MoneyToRecieve}",
                        $"Got someone called ~y~{v.Name}~s~ who needs to not wake up again, they're currently at ~p~{v.Location.Name}~s~ on ~p~{v.Location.FullStreetAddress}~s~. ${MoneyToRecieve}",
                    };
                }
                Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
            }
            else
            {
                
                string targetList = string.Join(", ", Victims.Select(v => $"~y~{v.Name}~s~ (~p~{v.Location.FullStreetAddress}~s~)"));
                List<string> Replies = new List<string>()
                {
                    $"Big contract just came in. Got {Victims.Count} targets for you: {targetList}. Take them all out. ${MoneyToRecieve}",
                    $"Multiple hits today. I need {Victims.Count} people dealt with: {targetList}. Don't leave any of them breathing. ${MoneyToRecieve}",
                    $"You're going to be busy. {Victims.Count} targets: {targetList}. All of them, no exceptions. ${MoneyToRecieve}",
                };
                Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
            }
        }

        private void SendVictimSpawnedMessage(VictimData v)
        {
            string LookingForItem = "";
            if (v.IsCustomer)
            {
                MenuItem myMenuItem = v.Ped.ShopMenu?.Items
                    .Where(x => x.NumberOfItemsToPurchaseFromPlayer > 0 && x.IsIllicilt)
                    .PickRandom();
                if (myMenuItem != null) LookingForItem = myMenuItem.ModItemName;
            }

            string complicationSuffix = (v.WillFight || v.WillFlee)
                ? " ~s~The target knows you're coming, be ready."
                : "";

            string itemSuffix = "";
            if (v.IsCustomer && LookingForItem != "")
            {
                List<string> ItemReplies = new List<string>()
                {
                    $" They are probably looking for ~p~{LookingForItem}~s~.",
                    $" They like ~p~{LookingForItem}~s~.",
                    $" Will be looking to buy ~p~{LookingForItem}~s~.",
                    $" They are interested in ~p~{LookingForItem}~s~.",
                    $" The target likes ~p~{LookingForItem}~s~.",
                };
                itemSuffix = ItemReplies.PickRandom();
            }

            if (NativeFunction.Natives.IsPedheadshotReady<bool>(v.HeadshotHandle))
            {
                List<string> Replies = new List<string>()
                {
                    $"Picture of ~y~{v.Name}~s~ attached. I heard they were still around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"Sent you a picture of ~y~{v.Name}~s~. They should still be around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"~y~{v.Name}~s~. They are still around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"The name is ~y~{v.Name}~s~, pic attached. They are loitering around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"Remember, ~y~{v.Name}~s~ is the name. I also sent a picture. I got word they are still around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                };
                string reply = Replies.PickRandom() + itemSuffix + complicationSuffix;
                string str = NativeFunction.Natives.GET_PEDHEADSHOT_TXD_STRING<string>(v.HeadshotHandle);
                EntryPoint.WriteToConsole($"CONTRACT KILLER SENT PICTURE MESSAGE {str}");
                Player.CellPhone.AddCustomScheduledText(Contact, reply, Time.CurrentDateTime, str, true);
            }
            else
            {
                List<string> Replies = new List<string>()
                {
                    $"~y~{v.Name}~s~. I heard they were still around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"~y~{v.Name}~s~. They should still be around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"~y~{v.Name}~s~. They are still around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"The name is ~y~{v.Name}~s~. They are loitering around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                    $"Remember, ~y~{v.Name}~s~ is the name. I got word they are still around ~p~{v.Location.Name} {v.Location.FullStreetAddress}~s~.",
                };
                string reply = Replies.PickRandom() + itemSuffix;
                if (v.WillFight || v.WillFlee) reply += " ~s~The target might have gotten wind, be careful.";
                EntryPoint.WriteToConsole("CONTRACT KILLER SENT REGULAR MESSAGE");
                Player.CellPhone.AddCustomScheduledText(Contact, reply, Time.CurrentDateTime, null, true);
            }
        }

        private void SendTaskAbortMessage()
        {
            List<string> Replies = new List<string>()
            {
                "Nothing yet, I'll let you know",
                "I've got nothing for you yet",
                "Give me a few days",
                "Not a lot to be done right now",
                "We will let you know when you can do something for us",
                "Check back later.",
            };
            Player.CellPhone.AddPhoneResponse(Contact.Name, Replies.PickRandom());
        }

        private void SendQuickPaymentMessage()
        {
            List<string> Replies = new List<string>()
            {
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
            List<string> Replies = new List<string>()
            {
                $"Pickup your payment of ${MoneyToRecieve} from {myDrop.FullStreetAddress}, its {myDrop.Description}.",
                $"Go get your payment of ${MoneyToRecieve} from {myDrop.Description}, address is {myDrop.FullStreetAddress}.",
            };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }

        private void SendFailMessage()
        {
            List<string> Replies = new List<string>()
            {
                $"You spooked them, they are gone. Thanks for nothing.",
                $"How could you let them get away?",
                $"You failed the job, dickhead!",
                $"You fucking idiot, they got away!",
                $"I should have hired someone who was more fucking competent!",
            };
            Player.CellPhone.AddScheduledText(Contact, Replies.PickRandom(), 1, false);
        }
    }
}
