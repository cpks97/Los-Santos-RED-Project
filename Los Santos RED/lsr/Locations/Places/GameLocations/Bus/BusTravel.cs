using LosSantosRED.lsr.Interface;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public class BusTravel
{
    public string ToBusID { get; set; }
    public string CarrierID { get; set; }





    public string Description { get; set; }
    public int Cost { get; set; }
    public int TravelTime { get; set; }






    public BusTravel()
    {

    }

    public BusTravel(string busID, string busline, string description, int cost, int travelTime)
    {
        ToBusID = busID;
        CarrierID = busline;
        Description = description;
        Cost = cost;
        TravelTime = travelTime;
    }
}

