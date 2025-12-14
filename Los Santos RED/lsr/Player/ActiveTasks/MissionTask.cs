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
using System.Xml.Serialization;

namespace LosSantosRED.lsr.Player.ActiveTasks
{
    [XmlInclude(typeof(DeliveryTask))]
    public class MissionTask : IPlayerTask
    {
        [XmlIgnore]
        public bool IsActive { get; protected set; } 
        public string Description { get; set; }
        public virtual void Dispose()
        {
            throw new NotImplementedException();
        }

        public virtual void Setup()
        {
            throw new NotImplementedException();
        }
    }
}
