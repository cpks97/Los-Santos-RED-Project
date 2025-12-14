using ExtensionsMethods;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
using LSR.Vehicles;
using Rage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Media.Animation;

namespace LosSantosRED.lsr.Player.ActiveTasks
{
    public class DeliveryTask : MissionTask
    {
        public string ItemToDeliver { get; set; }
        public Vector3 DeliverTo { get; set; }
        public string Zone { get; set; }
        public float Payout { get; set; }
        public string VehicleToSpawn { get; set; }
        public Vector3 VehicleSpawnPosition { get; set; }
        public float VehicleHeading { get; set; }
        public string LicenseRequired { get; set; }
        public override void Dispose()
        {
            return;
        }

        public override void Setup()
        {
            GameFiber PayoffFiber = GameFiber.StartNew(delegate
            {
                try
                {
                    IsActive = true;
                    Game.Console.Print($"Mission started at Setup");
                    Loop();
                }
                catch (Exception ex)
                {
                    EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                    EntryPoint.ModController.CrashUnload();
                }
            }, "DeliveryTaskFiber");
        }
        private void Loop()
        {
            while (IsActive)
            {
                if(Game.LocalPlayer.Character.Position.DistanceTo(DeliverTo) <= 5f)
                {
                    CompleteTask();
                }    
                GameFiber.Sleep(1000);
            }
        }
        private void CompleteTask()
        {
            Game.DisplayNotification("Completed mission");
            IsActive = false;
        }
    }
}
