using ExtensionsMethods;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using LSR.Vehicles;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LosSantosRED.lsr.Player.ActiveTasks
{
    public class SecurityTruckTask : IPlayerTask
    {
        private ITaskAssignable      Player;
        private ITimeReportable      Time;
        private IGangs               Gangs;
        private PlayerTasks          PlayerTasks;
        private IPlacesOfInterest    PlacesOfInterest;
        private List<DeadDrop>       ActiveDrops = new List<DeadDrop>();
        private ISettingsProvideable Settings;
        private IEntityProvideable   World;
        private ICrimes              Crimes;
        private IWeapons             Weapons;
        private INameProvideable     Names;
        private FixerContact         Contact;
        private PlayerTask           CurrentTask;
        private int                  MoneyToReceive;
        private DeadDrop             MyDrop;

        private const string TruckModel    = "stockade";
        private const string GuardModel    = "S_M_M_Armoured_01";
        private const float  DropOffRadius = 30f;

      
        private GameLocation SpawnLocation;
        private Vector3      SpawnPosition;
        private float        SpawnHeading;
        private int          SpawnCellX;
        private int          SpawnCellY;

       
        private GameLocation DropOffLocation;
        private Blip         DropOffBlip;

        
        private Vehicle TruckVehicle;
        private PedExt  Driver;
        private PedExt  Passenger;
        private bool    IsSpawned;

       
        private bool TruckSecured;   
        private bool TaskComplete;

        private bool HasSpawnPosition => SpawnPosition != Vector3.Zero && DropOffLocation != null;

        private bool IsPlayerNearSpawn =>
            SpawnCellX != -1 && SpawnCellY != -1 &&
            NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, SpawnCellX, SpawnCellY, 12);

        private bool IsPlayerFarFromSpawn =>
            !NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, SpawnCellX, SpawnCellY, 10) &&
            Player.Character.DistanceTo2D(SpawnPosition) >= 850f;

        public SecurityTruckTask(ITaskAssignable player, ITimeReportable time, IGangs gangs,
            PlayerTasks playerTasks, IPlacesOfInterest placesOfInterest, List<DeadDrop> activeDrops,
            ISettingsProvideable settings, IEntityProvideable world, ICrimes crimes,
            INameProvideable names, IWeapons weapons, FixerContact contact)
        {
            Player           = player;
            Time             = time;
            Gangs            = gangs;
            PlayerTasks      = playerTasks;
            PlacesOfInterest = placesOfInterest;
            ActiveDrops      = activeDrops;
            Settings         = settings;
            World            = world;
            Crimes           = crimes;
            Names            = names;
            Weapons          = weapons;
            Contact          = contact;
        }

        public void Setup() { }

        public void Dispose()
        {
            DespawnTruck();
            if (DropOffLocation != null)
                DropOffLocation.IsPlayerInterestedInLocation = false;
            if (DropOffBlip.Exists())
                DropOffBlip.Delete();
        }

        public void Start(FixerContact contact)
        {
            Contact = contact;
            if (Contact == null) return;
            if (!PlayerTasks.CanStartNewTask(Contact.Name)) return;

            GetInformation();
            if (!HasSpawnPosition) { SendAbortMessage(); return; }

            GetPayment();
            AddTask();
            SendInitialMessage();

            GameFiber.StartNew(delegate
            {
                try   { Loop(); FinishTask(); }
                catch (Exception ex)
                {
                    EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                    EntryPoint.ModController.CrashUnload();
                }
            }, "SecurityTruckFiber");
        }

        

        private void GetInformation()
        {
            
            SpawnLocation = PlacesOfInterest.PossibleLocations.Banks
                .Where(x => x.IsCorrectMap(World.IsMPMapLoaded) &&
                            x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState))
                .PickRandom();

            if (SpawnLocation == null) return;

            SpawnPosition = SpawnLocation.EntrancePosition;
            SpawnHeading  = SpawnLocation.EntranceHeading;
            SpawnCellX    = (int)(SpawnPosition.X / EntryPoint.CellSize);
            SpawnCellY    = (int)(SpawnPosition.Y / EntryPoint.CellSize);

           
            DropOffLocation = PlacesOfInterest.PossibleLocations.DrugMeetSpots
                .Where(x => x.IsCorrectMap(World.IsMPMapLoaded) &&
                            x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState) &&
                            x.EntrancePosition.DistanceTo2D(SpawnPosition) > 300f)
                .PickRandom();

            if (DropOffLocation == null)
            {
                DropOffLocation = PlacesOfInterest.PossibleLocations.DrugMeetSpots
                    .Where(x => x.IsCorrectMap(World.IsMPMapLoaded) &&
                                x.EntrancePosition.DistanceTo2D(SpawnPosition) > 300f)
                    .PickRandom();
            }

            EntryPoint.WriteToConsole($"SecurityTruck: spawn={SpawnLocation?.Name} dropoff={DropOffLocation?.Name}");
        }

        private void GetPayment()
        {
            MoneyToReceive = RandomItems.GetRandomNumberInt(
                Settings.SettingsManager.TaskSettings.SecurityTruckTheftPaymentMin,
                Settings.SettingsManager.TaskSettings.SecurityTruckTheftPaymentMax).Round(500);
            if (MoneyToReceive <= 0) MoneyToReceive = 500;
        }

        private void AddTask()
        {
            PlayerTasks.AddTask(Contact, 0, 2000, 0, -500, 7, "Security Truck Heist");
            CurrentTask = PlayerTasks.GetTask(Contact.Name);

            if (SpawnLocation != null)
                SpawnLocation.IsPlayerInterestedInLocation = true;

            IsSpawned    = false;
            TruckSecured = false;
            TaskComplete = false;
        }

        

        private void Loop()
        {
            while (true)
            {
                if (CurrentTask == null || !CurrentTask.IsActive) break;

                
                if (!Player.IsAliveAndFree)
                {
                    EntryPoint.WriteToConsole("SecurityTruck: player died — failing");
                    SetFailed();
                    return;
                }

                if (!TruckSecured)
                {
                    

                    if (IsPlayerNearSpawn && !IsSpawned)
                        SpawnTruck();
                    else if (IsSpawned && IsPlayerFarFromSpawn && !TruckVehicle.Exists())
                        DespawnTruck();

                    if (IsSpawned && TruckVehicle.Exists())
                    {
                        
                        bool driverGone    = Driver    == null || !Driver.Pedestrian.Exists()    || Driver.Pedestrian.IsDead;
                        bool passengerGone = Passenger == null || !Passenger.Pedestrian.Exists() || Passenger.Pedestrian.IsDead;

                        if (driverGone && passengerGone)
                        {
                            
                            bool playerInTruck = NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Player.Character, false) &&
                                                 Player.Character.CurrentVehicle.Exists() &&
                                                 Player.Character.CurrentVehicle.Handle == TruckVehicle.Handle;

                            if (playerInTruck)
                            {
                                TruckSecured = true;

                                
                                if (SpawnLocation != null)
                                    SpawnLocation.IsPlayerInterestedInLocation = false;

                                if (!DropOffBlip.Exists() && DropOffLocation != null)
                                {
                                    DropOffBlip = new Blip(DropOffLocation.EntrancePosition)
                                    {
                                        Sprite = BlipSprite.PointOfInterest,
                                        Color  = System.Drawing.Color.Blue,
                                        Scale  = 1.0f,
                                        Name   = DropOffLocation.Name
                                    };
                                }

                                SendTruckSecuredMessage();
                                EntryPoint.WriteToConsole("SecurityTruck: truck secured by player");
                            }
                        }
                    }
                }
                else
                {
                    

                    if (DropOffLocation == null) break;

                    
                    bool playerInTruck = NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Player.Character, false) &&
                                         Player.Character.CurrentVehicle.Exists() &&
                                         Player.Character.CurrentVehicle.Handle == TruckVehicle.Handle;

                    if (!playerInTruck && TruckVehicle.Exists())
                    {
                        
                        EntryPoint.WriteToConsole("SecurityTruck: player not in truck, monitoring...");
                    }

                   
                    if (playerInTruck &&
                        TruckVehicle.Exists() &&
                        TruckVehicle.Position.DistanceTo2D(DropOffLocation.EntrancePosition) <= DropOffRadius)
                    {
                        TaskComplete = true;
                        CurrentTask.OnReadyForPayment(true);
                        EntryPoint.WriteToConsole("SecurityTruck: delivered — task complete");
                        break;
                    }
                }

                GameFiber.Sleep(1000);
            }
        }

        

        private void SpawnTruck()
        {
            if (SpawnPosition == Vector3.Zero) return;

            
            Vector3 vehSpawnPos = Vector3.Zero;
            bool foundRoad = NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE<bool>(
                SpawnPosition.X + 30f, SpawnPosition.Y + 30f, SpawnPosition.Z,
                out vehSpawnPos, 1, 3.0f, 0);
            if (!foundRoad || vehSpawnPos == Vector3.Zero)
                vehSpawnPos = SpawnPosition.Around(40f);

            TruckVehicle = new Vehicle(TruckModel, vehSpawnPos, SpawnHeading);
            GameFiber.Yield();
            NativeFunction.Natives.SET_MODEL_AS_NO_LONGER_NEEDED(Game.GetHashKey(TruckModel));

            if (!TruckVehicle.Exists())
            {
                EntryPoint.WriteToConsole("SecurityTruck: failed to spawn vehicle");
                return;
            }

            TruckVehicle.IsPersistent = true;

            
            Driver = SpawnGuard(TruckVehicle, -1); 

            
            Passenger = SpawnGuard(TruckVehicle, 0);
            Passenger = SpawnGuard(TruckVehicle, 1);
            Passenger = SpawnGuard(TruckVehicle, 2);

            if (Driver != null && Driver.Pedestrian.Exists())
            {
                
                NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(
                    Driver.Pedestrian, TruckVehicle, 15f, 786603);
            }

            IsSpawned = true;
            SendTruckSpawnedMessage();
            EntryPoint.WriteToConsole($"SecurityTruck: spawned at {SpawnLocation?.Name}");
        }

        private PedExt SpawnGuard(Vehicle vehicle, int seat)
        {
            Ped ped = new Ped(GuardModel, vehicle.Position, vehicle.Heading);
            GameFiber.Yield();
            NativeFunction.Natives.SET_MODEL_AS_NO_LONGER_NEEDED(Game.GetHashKey(GuardModel));

            if (!ped.Exists()) return null;

            string guardName = Names.GetRandomName(true);
            PedExt guard = new PedExt(ped, Settings, Crimes, Weapons, guardName, "Guard", World);

            World.Pedestrians.AddEntity(guard);
            guard.WasEverSetPersistent = true;
            guard.CanBeAmbientTasked   = false;
            guard.CanBeTasked          = true;
            guard.WasModSpawned        = true;
            guard.IsManuallyDeleted    = true;

          
            NativeFunction.Natives.SET_PED_INTO_VEHICLE(ped, vehicle, seat);

            
            guard.WillFight             = true;
            guard.WillCallPolice        = true;
            guard.WillCallPoliceIntense = true;
            
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_CanFightArmedPedsWhenNotArmed, true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(ped, (int)eCombatAttributes.BF_AlwaysFight, false);
            NativeFunction.Natives.SET_PED_FLEE_ATTRIBUTES(ped, 0, false);

            
            WeaponInformation weapon = Weapons.GetRandomRegularWeapon(WeaponCategory.AR);
            if (weapon != null)
            {
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(ped, (uint)weapon.Hash, weapon.AmmoAmount, false, true);
            }

            if (Settings.SettingsManager.TaskSettings.ShowEntityBlips)
                guard.AddBlip();

            return guard;
        }

        private void DespawnTruck()
        {
            if (Driver != null && Driver.Pedestrian.Exists())
            {
                Driver.DeleteBlip();
                Driver.Pedestrian.IsPersistent = false;
                Driver.Pedestrian.Delete();
            }
            if (Passenger != null && Passenger.Pedestrian.Exists())
            {
                Passenger.DeleteBlip();
                Passenger.Pedestrian.IsPersistent = false;
                Passenger.Pedestrian.Delete();
            }
            if (TruckVehicle.Exists() && !TruckSecured)
            {
                TruckVehicle.IsPersistent = false;
                TruckVehicle.Delete();
            }
            IsSpawned = false;
        }


        private void FinishTask()
        {
            if (SpawnLocation != null)
                SpawnLocation.IsPlayerInterestedInLocation = false;
            if (DropOffLocation != null)
                DropOffLocation.IsPlayerInterestedInLocation = false;
            if (DropOffBlip.Exists())
                DropOffBlip.Delete();

            if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
                StartDeadDropPayment();
            else if (CurrentTask != null && CurrentTask.IsActive)
                SetFailed();
            else
                Dispose();
        }

        private void SetFailed()
        {
            SendFailMessage();
            PlayerTasks.FailTask(Contact);
        }

        private void StartDeadDropPayment()
        {
            MyDrop = PlacesOfInterest.GetUsableDeadDrop(World.IsMPMapLoaded, Player.CurrentLocation);
            if (MyDrop != null)
            {
                MyDrop.SetupDrop(MoneyToReceive, false);
                ActiveDrops.Add(MyDrop);
                SendDeadDropMessage();
                while (true)
                {
                    if (CurrentTask == null || !CurrentTask.IsActive) break;
                    if (MyDrop.InteractionComplete)
                    {
                        Game.DisplayHelp($"{Contact.Name}: Money picked up.");
                        break;
                    }
                    GameFiber.Sleep(1000);
                }
                if (CurrentTask != null && CurrentTask.IsActive && CurrentTask.IsReadyForPayment)
                    PlayerTasks.CompleteTask(Contact, true);
                MyDrop?.Reset();
                MyDrop?.Deactivate(true);
            }
            else
            {
                PlayerTasks.CompleteTask(Contact, true);
                SendQuickPaymentMessage();
            }
        }


        private void SendInitialMessage()
        {
            List<string> replies = new List<string>()
            {
                $"Got a job for you. There's a security truck making rounds near ~p~{SpawnLocation?.FullStreetAddress}~s~. Take out the guards, steal the truck, and bring it to ~p~{DropOffLocation?.FullStreetAddress}~s~. ${MoneyToReceive} on delivery.",
                $"Security truck job. The truck is near ~p~{SpawnLocation?.FullStreetAddress}~s~. Neutralise the guards and drive it to ~p~{DropOffLocation?.FullStreetAddress}~s~. ${MoneyToReceive}.",
                $"We need a security truck. It's doing a route near ~p~{SpawnLocation?.FullStreetAddress}~s~. Deal with the guards and bring it to ~p~{DropOffLocation?.FullStreetAddress}~s~. ${MoneyToReceive} when it arrives.",
                $"There's a Gruppe Sechs truck near ~p~{SpawnLocation?.FullStreetAddress}~s~. Take it. Drop it at ~p~{DropOffLocation?.FullStreetAddress}~s~. Watch out for the guards — they're armed. ${MoneyToReceive}.",
            };
            Player.CellPhone.AddPhoneResponse(Contact.Name, replies.PickRandom());
        }

        private void SendTruckSpawnedMessage()
        {
            List<string> replies = new List<string>()
            {
                $"The truck is in the area near ~p~{SpawnLocation?.FullStreetAddress}~s~. Take out the guards first.",
                $"Spotted the truck near ~p~{SpawnLocation?.FullStreetAddress}~s~. Two armed guards — be ready.",
                $"The truck is close to ~p~{SpawnLocation?.FullStreetAddress}~s~. Don't let it get away.",
            };
            Player.CellPhone.AddCustomScheduledText(Contact, replies.PickRandom(), Time.CurrentDateTime, null, true);
        }

        private void SendTruckSecuredMessage()
        {
            List<string> replies = new List<string>()
            {
                $"Nice work. Now get that truck to ~p~{DropOffLocation?.FullStreetAddress}~s~. Don't stop.",
                $"You've got it. Drive it to ~p~{DropOffLocation?.FullStreetAddress}~s~. Stay clean.",
                $"Good. Get that truck to ~p~{DropOffLocation?.FullStreetAddress}~s~ and don't draw attention.",
            };
            Player.CellPhone.AddCustomScheduledText(Contact, replies.PickRandom(), Time.CurrentDateTime, null, true);
        }

        private void SendDeadDropMessage()
        {
            List<string> replies = new List<string>()
            {
                $"Truck delivered. Pick up your ${MoneyToReceive} from {MyDrop?.Description} on {MyDrop?.FullStreetAddress}.",
                $"Good work. ${MoneyToReceive} is waiting at {MyDrop?.Description}, {MyDrop?.FullStreetAddress}.",
            };
            Player.CellPhone.AddScheduledText(Contact, replies.PickRandom(), 1, false);
        }

        private void SendQuickPaymentMessage()
        {
            List<string> replies = new List<string>()
            {
                $"Truck received. Sending your ${MoneyToReceive}.",
                $"Job done. ${MoneyToReceive} on its way.",
                $"Clean delivery. ${MoneyToReceive} sent.",
            };
            Player.CellPhone.AddScheduledText(Contact, replies.PickRandom(), 1, false);
        }

        private void SendAbortMessage()
        {
            List<string> replies = new List<string>()
            {
                "Can't set anything up right now. Check back later.",
                "Nothing available at the moment.",
                "Give it some time, I'll have something for you.",
            };
            Player.CellPhone.AddPhoneResponse(Contact.Name, replies.PickRandom());
        }

        private void SendFailMessage()
        {
            List<string> replies = new List<string>()
            {
                "You lost the truck. Useless.",
                "What happened? The truck never arrived.",
                "Job failed. Don't call for a while.",
            };
            Player.CellPhone.AddScheduledText(Contact, replies.PickRandom(), 1, false);
        }
    }
}
