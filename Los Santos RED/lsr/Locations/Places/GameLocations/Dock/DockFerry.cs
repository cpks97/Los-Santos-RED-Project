using LosSantosRED.lsr.Interface;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public class DockFerry
{
    public string ToDockID { get; set; }
    public string CarrierID { get; set; }





    public string Description { get; set; }
    public int Cost { get; set; }
    public int FerryTime { get; set; }






    public DockFerry()
    {

    }

    public DockFerry(string dockID, string ferryline, string description, int cost, int ferryTime)
    {
        ToDockID = dockID;
        CarrierID = ferryline;
        Description = description;
        Cost = cost;
        FerryTime = ferryTime;
    }
}

