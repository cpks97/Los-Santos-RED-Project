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
    public class HookerDeliveryTask : IPlayerTask
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

        private const int   TotalPassengers = 3;
        private const float DropOffRadius   = 30f;

        private readonly List<string> HookerModels = new List<string>()
        {
            "S_F_Y_Hooker_01",
            "S_F_Y_Hooker_02",
            "S_F_Y_Hooker_03",
        };

        
        private GameLocation         PickupLocation;
        private Vector3              PickupPosition;
        private float                PickupHeading;
        private int                  PickupCellX;
        private int                  PickupCellY;
        private bool                 PickupLocationIsInterested;
        private bool                 DropOffsActivated;

        private List<PassengerData>  Passengers = new List<PassengerData>();

        private bool HasSpawnPosition => PickupPosition != Vector3.Zero && Passengers.Any(p => p.DropOffLocation != null);

        private bool IsPlayerNearPickup =>
            PickupCellX != -1 && PickupCellY != -1 &&
            NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, PickupCellX, PickupCellY, 12);

        private class PassengerData
        {
            public string        Name;
            public string        Model;
            public GameLocation  DropOffLocation;
            public Blip          DropOffBlip;   
            public PedExt        Ped;
            public PedVariation  Variation;
            public bool          IsSpawned;
            public bool          IsPickedUp;
            public bool          IsDelivered;
            public bool          IsTaskedToEnterVehicle;
            public int           AssignedSeat;
        }

        public HookerDeliveryTask(ITaskAssignable player, ITimeReportable time, IGangs gangs,
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
            foreach (PassengerData p in Passengers)
            {
                if (p.Ped != null && p.Ped.Pedestrian.Exists())
                {
                    p.Ped.DeleteBlip();
                    p.Ped.Pedestrian.IsPersistent = false;
                    p.Ped.Pedestrian.Delete();
                }
                if (p.DropOffLocation != null)
                    p.DropOffLocation.IsPlayerInterestedInLocation = false;
                if (p.DropOffBlip.Exists())
                    p.DropOffBlip.Delete();
            }
            if (PickupLocation != null)
                PickupLocation.IsPlayerInterestedInLocation = false;
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
            }, "HookerDeliveryFiber");
        }

        

        private void GetInformation()
        {
            Passengers.Clear();

            
            PickupLocation = PlacesOfInterest.PossibleLocations.GenericTaskLocations()
                .Where(x => x.IsCorrectMap(World.IsMPMapLoaded) &&
                            x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState))
                .PickRandom();

            if (PickupLocation == null) return;

            PickupPosition = PickupLocation.EntrancePosition;
            PickupHeading  = PickupLocation.EntranceHeading;
            PickupCellX    = (int)(PickupPosition.X / EntryPoint.CellSize);
            PickupCellY    = (int)(PickupPosition.Y / EntryPoint.CellSize);

            
            List<Residence> chosenDropOffs = new List<Residence>();
            for (int i = 0; i < TotalPassengers; i++)
            {
                Residence dropOff = PlacesOfInterest.PossibleLocations.Residences
                    .Where(x => !x.IsOwnedOrRented &&
                                x.IsCorrectMap(World.IsMPMapLoaded) &&
                                x.IsSameState(Player.CurrentLocation?.CurrentZone?.GameState) &&
                                !chosenDropOffs.Contains(x))
                    .PickRandom();

                if (dropOff == null) break;
                chosenDropOffs.Add(dropOff);

                PassengerData p = new PassengerData();
                p.Name            = Names.GetRandomName(false);
                p.Model           = HookerModels[i % HookerModels.Count];
                p.DropOffLocation = dropOff;
                p.AssignedSeat    = i;
                Passengers.Add(p);
            }
            EntryPoint.WriteToConsole($"HookerDelivery: {Passengers.Count} passengers with drop-offs assigned");
            foreach (PassengerData p in Passengers)
                EntryPoint.WriteToConsole($"HookerDelivery: {p.Name} drop-off = {p.DropOffLocation?.FullStreetAddress ?? "NULL"}");
        }

        private void GetPayment()
        {
            MoneyToReceive = RandomItems.GetRandomNumberInt(
                Settings.SettingsManager.TaskSettings.EscortDriverPaymentMin,
                Settings.SettingsManager.TaskSettings.EscortDriverPaymentMax).Round(500);
            if (MoneyToReceive <= 0) MoneyToReceive = 500;
        }

        private void AddTask()
        {
            PlayerTasks.AddTask(Contact, 0, 2000, 0, -500, 7, "Escort Driver");
            CurrentTask = PlayerTasks.GetTask(Contact.Name);

            if (PickupLocation != null)
                PickupLocation.IsPlayerInterestedInLocation = true;

            DropOffsActivated = false;

            foreach (PassengerData p in Passengers)
            {
                p.IsSpawned              = false;
                p.IsPickedUp             = false;
                p.IsDelivered            = false;
                p.IsTaskedToEnterVehicle = false;
                // Set interested from the start so blip system activates and colours them
                // when DropOffsActivated flips them blue — same pattern as WitnessEliminationTask
                if (p.DropOffLocation != null)
                    p.DropOffLocation.IsPlayerInterestedInLocation = false;
                if (p.DropOffBlip.Exists())
                    p.DropOffBlip.Delete();
            }
        }

        

        private void Loop()
        {
            while (true)
            {
                if (CurrentTask == null || !CurrentTask.IsActive) break;

                // Fail if player dies
                if (!Player.IsAliveAndFree)
                {
                    EntryPoint.WriteToConsole("HookerDelivery: player died — failing task");
                    SetFailed();
                    return;
                }

                // Fail if all picked-up passengers have died
                bool anyAlivePassenger = Passengers.Any(p =>
                    p.Ped != null && p.Ped.Pedestrian.Exists() && !p.Ped.Pedestrian.IsDead);
                if (Passengers.Any(p => p.IsSpawned || p.IsPickedUp) && !anyAlivePassenger)
                {
                    EntryPoint.WriteToConsole("HookerDelivery: all passengers died — failing task");
                    SetFailed();
                    return;
                }

               
                bool allPickedUp = Passengers.All(p => p.IsPickedUp || p.IsDelivered);
                if (!allPickedUp)
                {
                    if (IsPlayerNearPickup)
                    {
                        foreach (PassengerData p in Passengers.Where(p => !p.IsSpawned && !p.IsPickedUp))
                        {
                            SpawnPassenger(p);
                        }
                    }
                    else
                    {
                        
                        foreach (PassengerData p in Passengers.Where(p => p.IsSpawned && !p.IsPickedUp))
                        {
                            if (PickupPosition != Vector3.Zero &&
                                !NativeHelper.IsNearby(EntryPoint.FocusCellX, EntryPoint.FocusCellY, PickupCellX, PickupCellY, 10) &&
                                Player.Character.DistanceTo2D(PickupPosition) >= 850f)
                            {
                                DespawnPassenger(p);
                            }
                        }
                    }

                    foreach (PassengerData p in Passengers.Where(p => p.IsSpawned && !p.IsPickedUp))
                    {
                        if (p.Ped == null || !p.Ped.Pedestrian.Exists()) continue;

                        if (p.Ped.Pedestrian.IsDead)
                        {
                            DespawnPassenger(p);
                            p.IsDelivered = true;
                            EntryPoint.WriteToConsole($"HookerDelivery: {p.Name} died");
                            continue;
                        }

                        bool pedInVehicle = NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(p.Ped.Pedestrian, false);
                        bool playerInVehicle = NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Player.Character, false);

                       
                        if (!pedInVehicle && playerInVehicle &&
                            !p.IsTaskedToEnterVehicle &&
                            Player.Character.CurrentVehicle.Exists() &&
                            p.Ped.Pedestrian.Position.DistanceTo2D(Player.Character.CurrentVehicle.Position) <= 10f)
                        {
                            
                            if (p.AssignedSeat < Player.Character.CurrentVehicle.PassengerCapacity)
                            {
                                NativeFunction.CallByName<bool>("TASK_ENTER_VEHICLE",
                                    p.Ped.Pedestrian, Player.Character.CurrentVehicle,
                                    -1, p.AssignedSeat, 2f,
                                    (int)(eEnter_Exit_Vehicle_Flags.ECF_RESUME_IF_INTERRUPTED |
                                          eEnter_Exit_Vehicle_Flags.ECF_BLOCK_SEAT_SHUFFLING));
                                p.IsTaskedToEnterVehicle = true;
                                EntryPoint.WriteToConsole($"HookerDelivery: tasked {p.Name} to enter vehicle seat {p.AssignedSeat}");
                            }
                        }

                       
                        if (pedInVehicle && playerInVehicle &&
                            p.Ped.Pedestrian.CurrentVehicle.Exists() &&
                            Player.Character.CurrentVehicle.Exists() &&
                            p.Ped.Pedestrian.CurrentVehicle.Handle == Player.Character.CurrentVehicle.Handle)
                        {
                            p.IsPickedUp = true;
                            EntryPoint.WriteToConsole($"HookerDelivery: {p.Name} picked up");
                        }
                    }

                }

                
                if (!DropOffsActivated && Passengers.Any() && Passengers.All(p => p.IsPickedUp || p.IsDelivered))
                {
                    DropOffsActivated = true;
                    if (PickupLocation != null)
                        PickupLocation.IsPlayerInterestedInLocation = false;
                    foreach (PassengerData p in Passengers.Where(p => p.IsPickedUp && p.DropOffLocation != null))
                    {
                        
                        if (!p.DropOffBlip.Exists())
                        {
                            p.DropOffBlip = new Blip(p.DropOffLocation.EntrancePosition)
                            {
                                Sprite = BlipSprite.PointOfInterest,
                                Color  = System.Drawing.Color.Blue,
                                Scale  = 1.0f,
                                Name   = p.DropOffLocation.Name
                            };
                        }
                        EntryPoint.WriteToConsole($"HookerDelivery: created drop-off blip for {p.Name} at {p.DropOffLocation.FullStreetAddress}");
                    }
                    SendAllPickedUpMessage();
                }

                if (Passengers.All(p => p.IsPickedUp || p.IsDelivered))
                {
                    
                    bool playerInVehicle = NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Player.Character, false);

                    foreach (PassengerData p in Passengers.Where(p => p.IsPickedUp && !p.IsDelivered))
                    {
                        if (p.Ped == null || !p.Ped.Pedestrian.Exists()) { p.IsDelivered = true; continue; }
                        if (p.DropOffLocation == null) { p.IsDelivered = true; continue; }

                        if (p.Ped.Pedestrian.IsDead) { p.IsDelivered = true; continue; }

                        bool pedInVehicle = NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(p.Ped.Pedestrian, false);

                        
                        if (playerInVehicle && pedInVehicle &&
                            Player.Character.CurrentVehicle.Exists() &&
                            Player.Character.CurrentVehicle.DistanceTo2D(p.DropOffLocation.EntrancePosition) <= DropOffRadius)
                        {
                            NativeFunction.Natives.TASK_LEAVE_VEHICLE(p.Ped.Pedestrian, Player.Character.CurrentVehicle, 0);
                            p.IsDelivered = true;
                            p.DropOffLocation.IsPlayerInterestedInLocation = false;
                            if (p.DropOffBlip.Exists())
                                p.DropOffBlip.Delete();
                            SendDeliveredMessage(p);
                            EntryPoint.WriteToConsole($"HookerDelivery: {p.Name} delivered");
                        }
                    }
                }

                
                if (Passengers.All(p => p.IsDelivered))
                {
                    bool anySuccessful = Passengers.Any(p =>
                        p.Ped != null && p.Ped.Pedestrian.Exists() && !p.Ped.Pedestrian.IsDead);
                    if (anySuccessful)
                        CurrentTask.OnReadyForPayment(true);
                    break;
                }

                GameFiber.Sleep(1000);
            }
        }

       

        private void SpawnPassenger(PassengerData p)
        {
            if (PickupPosition == Vector3.Zero) return;

            
            Vector3 spawnPos = PickupPosition.Around2D(0.5f, 2f);

            World.Pedestrians.CleanupAmbient();
            Ped ped = new Ped(p.Model, spawnPos, PickupHeading);
            GameFiber.Yield();
            NativeFunction.Natives.SET_MODEL_AS_NO_LONGER_NEEDED(Game.GetHashKey(p.Model));
            if (!ped.Exists()) return;

            p.Ped = new PedExt(ped, Settings, Crimes, Weapons, p.Name, "Woman", World);

            if (Settings.SettingsManager.TaskSettings.ShowEntityBlips)
                p.Ped.AddBlip();

            World.Pedestrians.AddEntity(p.Ped);
            p.Ped.WasEverSetPersistent = true;
            p.Ped.CanBeAmbientTasked   = true;
            p.Ped.CanBeTasked          = true;
            p.Ped.WasModSpawned        = true;
            p.Ped.IsManuallyDeleted    = true;

            if (p.Variation == null)
            {
                ped.RandomizeVariation();
                p.Variation = NativeHelper.GetPedVariation(ped);
            }
            else
            {
                p.Variation.ApplyToPed(ped, true);
            }

            
            NativeFunction.Natives.TASK_STAND_STILL(ped, -1);

            p.IsSpawned = true;
            EntryPoint.WriteToConsole($"HookerDelivery: spawned {p.Name} at {PickupLocation?.Name}");
        }

        private void DespawnPassenger(PassengerData p)
        {
            if (p.Ped != null && p.Ped.Pedestrian.Exists())
            {
                p.Ped.DeleteBlip();
                p.Ped.Pedestrian.IsPersistent = false;
                p.Ped.Pedestrian.Delete();
            }
            p.IsSpawned = false;
        }

        

        private void FinishTask()
        {
            if (PickupLocation != null)
                PickupLocation.IsPlayerInterestedInLocation = false;
            foreach (PassengerData p in Passengers)
            {
                if (p.DropOffLocation != null)
                    p.DropOffLocation.IsPlayerInterestedInLocation = false;
                if (p.DropOffBlip.Exists())
                    p.DropOffBlip.Delete();
            }

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
            string dropOffList = string.Join(", ", Passengers.Select(p =>
                $"~y~{p.Name}~s~ (~p~{p.DropOffLocation?.FullStreetAddress}~s~)"));

            List<string> replies = new List<string>()
            {
                $"Got a driving job for you. You'll need a 4-door car for this one. Pick up {TotalPassengers} girls from ~p~{PickupLocation?.FullStreetAddress}~s~ and drop each one at their address: {dropOffList}. ${MoneyToReceive} when they're all delivered.",
                $"{TotalPassengers} girls need picking up from ~p~{PickupLocation?.FullStreetAddress}~s~. Make sure you've got a 4-door, they all need a seat. Drop each one off: {dropOffList}. ${MoneyToReceive} on completion.",
                $"Driving job. Head to ~p~{PickupLocation?.FullStreetAddress}~s~ in a 4-door car, pick up the girls, and drop them each home. Addresses: {dropOffList}. ${MoneyToReceive}.",
            };
            Player.CellPhone.AddPhoneResponse(Contact.Name, replies.PickRandom());
        }

        private void SendAllPickedUpMessage()
        {
           
            string dropOffList = string.Join(", ", Passengers
                .Where(p => p.IsPickedUp && p.DropOffLocation != null)
                .Select(p => $"~y~{p.Name}~s~ to ~p~{p.DropOffLocation.FullStreetAddress}~s~"));

            List<string> replies = new List<string>()
            {
                $"Good, all {TotalPassengers} are in. Drop-offs: {dropOffList}.",
                $"All picked up. Make the drops: {dropOffList}.",
                $"They're all in. Deliver them: {dropOffList}.",
            };
            Player.CellPhone.AddCustomScheduledText(Contact, replies.PickRandom(), Time.CurrentDateTime, null, true);
        }

        private void SendDeliveredMessage(PassengerData p)
        {
            int remaining = Passengers.Count(x => x.IsPickedUp && !x.IsDelivered);
            List<string> replies = remaining > 0
                ? new List<string>()
                {
                    $"~y~{p.Name}~s~ delivered. {remaining} still to drop off.",
                    $"Good drop. {remaining} more to go.",
                    $"~y~{p.Name}~s~ done. {remaining} left.",
                }
                : new List<string>()
                {
                    $"Last one done. Nice work, payment incoming.",
                    $"That's all of them. Sending your ${MoneyToReceive}.",
                    $"All delivered. You'll get your money shortly.",
                };
            Player.CellPhone.AddCustomScheduledText(Contact, replies.PickRandom(), Time.CurrentDateTime, null, true);
        }

        private void SendDeadDropMessage()
        {
            List<string> replies = new List<string>()
            {
                $"All deliveries done. Pick up your ${MoneyToReceive} from {MyDrop?.Description} on {MyDrop?.FullStreetAddress}.",
                $"Good work. ${MoneyToReceive} is waiting at {MyDrop?.Description}, {MyDrop?.FullStreetAddress}.",
            };
            Player.CellPhone.AddScheduledText(Contact, replies.PickRandom(), 1, false);
        }

        private void SendQuickPaymentMessage()
        {
            List<string> replies = new List<string>()
            {
                $"All delivered. Sending your ${MoneyToReceive}.",
                $"Job done. ${MoneyToReceive} on its way.",
                $"Nice driving. ${MoneyToReceive} sent.",
            };
            Player.CellPhone.AddScheduledText(Contact, replies.PickRandom(), 1, false);
        }

        private void SendAbortMessage()
        {
            List<string> replies = new List<string>()
            {
                "Nothing doing right now, check back later.",
                "Can't set anything up at the moment. Try again soon.",
                "No jobs available yet.",
            };
            Player.CellPhone.AddPhoneResponse(Contact.Name, replies.PickRandom());
        }

        private void SendFailMessage()
        {
            List<string> replies = new List<string>()
            {
                "You lost all of them. Useless.",
                "Not one delivery made. What happened?",
                "That was a disaster. Don't bother calling for a while.",
            };
            Player.CellPhone.AddScheduledText(Contact, replies.PickRandom(), 1, false);
        }
    }
}
