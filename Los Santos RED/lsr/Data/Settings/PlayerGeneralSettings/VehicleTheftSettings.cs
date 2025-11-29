using System.Runtime.Serialization;

public class VehicleTheftSettings : ISettingsDefaultable
{
    public float CompactsMultiplier { get; set; }
    public float SedansMultiplier { get; set; }
    public float SUVsMultiplier { get; set; }
    public float CoupesMultiplier { get; set; }
    public float MuscleMultiplier { get; set; }
    public float SportsClassicsMultiplier { get; set; }
    public float SportsMultiplier { get; set; }
    public float SuperMultiplier { get; set; }
    public float MotorcyclesMultiplier { get; set; }
    public float OffRoadMultiplier { get; set; }
    public float IndustrialMultiplier { get; set; }
    public float UtilityMultiplier { get; set; }
    public float VansMultiplier { get; set; }
    public float CyclesMultiplier { get; set; }
    public float BoatsMultiplier { get; set; }
    public float HelicoptersMultiplier { get; set; }
    public float PlanesMultiplier { get; set; }
    public float ServiceMultiplier { get; set; }
    public float EmergencyMultiplier { get; set; }
    public float MilitaryMultiplier { get; set; }
    public float CommercialMultiplier { get; set; }
    public float BaseCost { get; set; }

    [OnDeserialized()]
    private void SetValuesOnDeserialized(StreamingContext context)
    {
        SetDefault();
    }

    public VehicleTheftSettings()
    {
        SetDefault();
    }
    public void SetDefault()
    {
        CompactsMultiplier = 0.6f;
        SedansMultiplier = 0.8f;
        SUVsMultiplier = 1;
        CoupesMultiplier = 1.1f;
        MuscleMultiplier = 1.2f;
        SportsClassicsMultiplier = 2;
        SportsMultiplier = 2.5f;
        SuperMultiplier = 4.5f;
        MotorcyclesMultiplier = 0.7f;
        OffRoadMultiplier = 0.9f;
        IndustrialMultiplier = 1.4f;
        UtilityMultiplier = 1.2f;
        VansMultiplier = 1;
        CyclesMultiplier = 0.2f;
        BoatsMultiplier = 1.8f;
        HelicoptersMultiplier = 6;
        PlanesMultiplier = 7.5f;
        ServiceMultiplier = 2.2f;
        EmergencyMultiplier = 5;
        MilitaryMultiplier = 8;
        CommercialMultiplier = 3;
        BaseCost = 5000;
    }
}
