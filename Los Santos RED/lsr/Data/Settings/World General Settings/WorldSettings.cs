using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

public class WorldSettings : ISettingsDefaultable
{
    [Description("Updates vehicle plates for the given state, plate style, and number format given in PlateTypes.xml.")]
    public bool UpdateVehiclePlates { get; set; }
    [Description("Percentage of vehicles that will get a plate type to match your current state (if not San Andreas).")]
    public float OutOfStateRandomVehiclePlatesPercent { get; set; }
    [Description("Percentage of vehicles that will get a random plate type (not dependant on state).")]
    public float RandomVehiclePlatesPercent { get; set; }
    [Description("Allow settings random vanity plates.")]
    public bool AllowRandomVanityPlates { get; set; }
    [Description("Percentage of vehicles that will get a random vanity plate.")]
    public float RandomVehicleVanityPlatesPercent { get; set; }
    [Description("Remove ambient vehicles that are empty from the game world. Not recommended to be disabled.")]
    public bool CleanupVehicles { get; set; }
    [Description("Delete the ambient shopkeeper peds as they spawn to not interfere with mod spawned merchant peds.")]
    public bool ReplaceVanillaShopKeepers { get; set; }
    [Description("If enabled all locations will be visible on the in game directoy (Messages or Player Info Menu). Disabled will show only legal locations.")]
    public bool ShowAllLocationsOnDirectory { get; set; }

    [Description("Sets the default spawn multiplier when LSR is active. Vanilla/Default is 1.0")]
    public float DefaultSpawnMultiplier { get; set; }
    [Description("If enabled, the civilian ped population will be lessened at 4+ stars.")]
    public bool LowerPedSpawnsAtHigherWantedLevels { get; set; }

    //[Description("Civilian ped density multiplier at 2 stars.")]
    //public float LowerPedSpawnsAtHigherWantedLevels_Wanted2Multiplier { get; set; }

    //[Description("Civilian ped density multiplier at 3 stars.")]
    //public float LowerPedSpawnsAtHigherWantedLevels_Wanted3Multiplier { get; set; }

    [Description("Civilian ped density multiplier at 4 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted4Multiplier { get; set; }
    [Description("Civilian ped density multiplier at 5 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted5Multiplier { get; set; }
    [Description("Civilian ped density multiplier at 6 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted6Multiplier { get; set; }
    [Description("Civilian ped density multiplier at 7 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted7Multiplier { get; set; }
    [Description("Civilian ped density multiplier at 8 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted8Multiplier { get; set; }
    [Description("Civilian ped density multiplier at 9 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted9Multiplier { get; set; }
    [Description("Civilian ped density multiplier at 10 stars.")]
    public float LowerPedSpawnsAtHigherWantedLevels_Wanted10Multiplier { get; set; }
    [Description("If enabled, ALL static blips will be added to the map.")]
    public bool ShowAllBlipsOnMap { get; set; }
    [Description("If enabled, there will be a 3D entrance marker around location entrances. Performance Intensive")]
    public bool ShowMarkersOnLocationEntrances { get; set; }
    [Description("If enabled, hotels will use specific rooms (if available).")]
    public bool HotelsUsesRooms { get; set; }
    [Description("If enabled, the blip showing where police are requsting backup will appear.")]
    public bool AllowPoliceBackupBlip { get; set; }

    [Description("If enabled, airports will require your owned vehicles to be nearby to use them for takeoff.")]
    public bool AirportsRequireOwnedPlanesLocal { get; set; }
    [Description("Distance (in meters) considered nearby in the case of owned planes being used for takeoff.")]
    public float AirportsOwnedPlanesLocalDistance { get; set; }
    [Description("If enabled, airports will require you to have a valid pilots license to take off.")]
    public bool AirportsRequireLicenseForPrivateFlights { get; set; }
    public bool AllowSettingDistantSirens { get; set; }
    public uint DeadBodyAlertTimeout { get; set; }
    public uint UnconsciousBodyAlertTimeout { get; set; }
    public uint GunshotAlertTimeout { get; set; }
    public uint HelpCryAlertTimeout { get; set; }
    public float OfficerMIACallInExpireDistance { get; set; }
    public float OfficerMIACallInDistance { get; set; }
    public uint OfficerMIACallInTimeMin { get; set; }
    public uint OfficerMIACallInTimeMax { get; set; }
    public bool AllowOfficerMIACallIn { get; set; }
    public float OfficerMIAStartPercentage_Alterted { get; set; }
    public float OfficerMIAStartPercentage_Regular { get; set; }
    [Description("If enabled, LSR set the siren state for any vehicle an AI Cop is in.")]
    public bool AllowSettingSirenState { get; set; }
    [Description("If enabled, LSR will set the vanilla taxi model as suppressed and only LSR will spawn them.")]
    public bool SetVanillaTaxiSuppressed { get; set; }
    public bool CreateObjectLocationsFromScanning { get; set; }
    public bool ShowMarkersInInteriors { get; set; }
    public int InteriorMarkerType { get; set; }
    public float InteriorMarkerZOffset { get; set; }
    public float InteriorMarkerScale { get; set; }
    [Description("If enabled, LSR will cancel a vehicle spawn if there is a mission entity within a certain radius")]
    public bool CheckAreaBeforeVehicleSpawn { get; set; }

    [Description("More aggressively remove abandoned or empty vehicle from the game world.")]
    public bool ExtendedVehicleCleanup { get; set; }
    public bool SuppressFEJVehiclesFromGenerators { get; set; }
    public bool SetMissionFlagOn { get; set; }
    [Description("Set if you would want MP map to load in by default")]
    public bool DefaultToMPMap { get; set; }

    [Description("If enabled, World.LoadMPMap will also request the GTAO Cayo Perico IPL set. Leave disabled if you use Seamless Cayo streaming or Liberty City can overlap the island.")]
    public bool LoadCayoIplsWithMPMap { get; set; }
    //public int MaxPedsBeforeDispatchPause { get; set; }
    //public int MaxVehiclesBeforeDispatchPause { get; set; }
    [Description("Select this option if you'd like to reveal the entire map on startup by default")]
    public bool LoadRevealMap { get; set; }


    [Description("Enable seamless (no cutscene) streaming of Cayo Perico while staying on the SP map. Loads Cayo IPLs gradually and enables island features only when near the island. Auto-disables when Liberty City is loaded (manhat06_slod).")]
    public bool EnableSeamlessCayoPerico { get; set; }
    [Description("Distance (meters) from the Cayo center at which island features turn ON (hysteresis enter).")]
    public float SeamlessCayoEnterDistance { get; set; }
    [Description("Distance (meters) from the Cayo center at which island features turn OFF (hysteresis exit). Should be greater than EnterDistance.")]
    public float SeamlessCayoExitDistance { get; set; }
    [Description("If enabled, applies the minimap/pause-map fix while near the island (radar interior trick using h4_fake_islandx).")]
    public bool SeamlessCayoEnableMapFix { get; set; }
    [Description("If enabled, unloads any Cayo IPLs that were loaded by LSR seamless streaming whenever Liberty City is detected as loaded (manhat06_slod). Recommended if Liberty City occupies the Cayo region.")]
    public bool SeamlessCayoUnloadIplsWhenLibertyCityLoaded { get; set; }
    [Description("If enabled, unloads any Cayo IPLs that were loaded by LSR seamless streaming when the feature is disabled. Keeps memory usage low.")]
    public bool SeamlessCayoUnloadIplsWhenDisabled { get; set; }

    [Description("Distance (meters) at which LSR begins requesting Cayo IPLs for seamless travel. Higher values preload earlier; lower values keep LS lighter. Default: 5000.")]
    public float SeamlessCayoIplLoadDistance { get; set; }
    [Description("Distance (meters) at which LSR unloads any Cayo IPLs it loaded for seamless travel. Must be greater than IplLoadDistance. Default: 6000.")]
    public float SeamlessCayoIplUnloadDistance { get; set; }




    [Description("Minimum distance gap (meters) enforced between IplLoadDistance and IplUnloadDistance when your XML is misconfigured (unload <= load). Default: 500.")]
    public float SeamlessCayoIplUnloadMinGapDistance { get; set; }

    [Description("Distance (meters) at which detail Cayo IPLs begin streaming (interleaved with base). This should be <= IplLoadDistance. Default: 5000.")]
    public float SeamlessCayoIplDetailStartDistance { get; set; }

    [Description("Distance (meters) at which the loader switches to the near batch size and prefers detail IPLs (higher fidelity while close). Default: 3500.")]
    public float SeamlessCayoIplNearDistance { get; set; }

    [Description("Number of IPLs to request per loader tick while in the mid/detail start band (distance <= DetailStartDistance but > NearDistance). Default: 4.")]
    public int SeamlessCayoIplBatchSizeMid { get; set; }

    [Description("Distance (meters) at which the Cayo minimap/pause-map fix becomes active during approach. This can start before EnterDistance so the island map appears while flying in. Default: 3500.")]
    public float SeamlessCayoMapFixStartDistance { get; set; }
    [Description("Distance (meters) at which the minimap fix will STOP after it has started. Used as hysteresis to prevent toggling near the boundary. Default: MapFixStartDistance + 750.")]
    public float SeamlessCayoMapFixStopDistance { get; set; }
    [Description("Distance (meters) at which SET_USE_ISLAND_MAP(true) is allowed (pause-map). Keep this smaller than MapFixStartDistance so Los Santos pause map returns quickly when leaving. Default: 3500.")]
    public float SeamlessCayoPauseMapDistance { get; set; }

    [Description("If enabled, toggles SET_USE_ISLAND_MAP(true) while within MapFixStartDistance to improve pause-map behavior. Set false if it conflicts with other world/map mods.")]
    public bool SeamlessCayoUseIslandMap { get; set; }
    [Description("If enabled, disables the PrLog zone while seamless Cayo is enabled (matches some FiveM loaders). Leave disabled if you use Liberty City/LCPP overlays to avoid zone-name conflicts.")]
    public bool SeamlessCayoDisablePrLogZone { get; set; }

    [Description("How long (ms) the player must remain within IplLoadDistance before streaming begins. Helps stabilize the first approach and prevents accidental preloads. Default: 1000.")]
    public uint SeamlessCayoIplLoadHoldMs { get; set; }
    [Description("Number of IPLs to request per loader tick while in the far/base phase (distance > IplNearDistance). Default: 4.")]
    public int SeamlessCayoIplBatchSizeFar { get; set; }
    [Description("Number of IPLs to request per loader tick while in the near/detail phase (distance <= IplNearDistance). Default: 8.")]
    public int SeamlessCayoIplBatchSizeNear { get; set; }
    [Description("Sleep (ms) between IPL request batches while loading. Lower loads faster but may pop/stutter; higher loads smoother but later. Default: 200.")]
    public int SeamlessCayoIplBatchDelayMs { get; set; }

    [Description("If enabled, uses an adaptive IPL scheduler (token-bucket + ETA-based ramp) instead of fixed batch sizes. This reduces load spikes and improves stability.")]
    public bool SeamlessCayoUseAdaptiveIplScheduler { get; set; }
    [Description("Adaptive scheduler tick interval (ms). Lower = more responsive but more CPU. Default: 50.")]
    public int SeamlessCayoIplAdaptiveTickMs { get; set; }
    [Description("Hard cap on IPL requests per adaptive scheduler tick (burst limiter). Default: 6.")]
    public int SeamlessCayoIplMaxRequestsPerTick { get; set; }
    [Description("Hard cap on adaptive token-bucket burst size (max queued IPL requests). Default: 12.")]
    public int SeamlessCayoIplMaxBurstRequests { get; set; }

    [Description("Adaptive scheduler MIN request rate (requests/sec) in the far/base phase. Default: 1.")]
    public int SeamlessCayoIplMinRequestsPerSecondFar { get; set; }
    [Description("Adaptive scheduler MIN request rate (requests/sec) in the mid/detail phase. Default: 2.")]
    public int SeamlessCayoIplMinRequestsPerSecondMid { get; set; }
    [Description("Adaptive scheduler MIN request rate (requests/sec) in the near phase. Default: 4.")]
    public int SeamlessCayoIplMinRequestsPerSecondNear { get; set; }

    [Description("Adaptive scheduler MAX request rate (requests/sec) in the far/base phase. Default: 4.")]
    public int SeamlessCayoIplMaxRequestsPerSecondFar { get; set; }
    [Description("Adaptive scheduler MAX request rate (requests/sec) in the mid/detail phase. Default: 8.")]
    public int SeamlessCayoIplMaxRequestsPerSecondMid { get; set; }
    [Description("Adaptive scheduler MAX request rate (requests/sec) in the near phase. Default: 12.")]
    public int SeamlessCayoIplMaxRequestsPerSecondNear { get; set; }

    [Description("If enabled, and h4_islandx is detected active while you are far from the island, the controller will attempt to unload the island even if it was loaded by another system. Use with caution if other mods intentionally keep Cayo loaded. Default: false.")]
    public bool SeamlessCayoForceUnloadIfIslandActiveWhileFar { get; set; }

    [Description("If enabled, unloading can remove the FULL Cayo IPL list (more aggressive). Leave disabled for maximum stability; only IPLs requested by the seamless controller will be removed unless Liberty City is loaded.")]
    public bool SeamlessCayoAggressiveUnloadAllIpls { get; set; }
    [Description("Delay (ms) that player must remain beyond the IPL unload distance before unloading begins. Helps avoid flicker/unload-load thrash when flying fast. Default: 5000.")]
    public uint SeamlessCayoUnloadDelayMs { get; set; }
    [Description("Number of IPLs to remove per unload batch (when unloading is enabled). Lower is safer, higher unloads faster. Default: 10.")]
    public int SeamlessCayoUnloadBatchSize { get; set; }
    [Description("Sleep (ms) between unload batches. Default: 200.")]
    public int SeamlessCayoUnloadBatchDelayMs { get; set; }


    [OnDeserialized()]
    private void SetValuesOnDeserialized(StreamingContext context)
    {
        SetDefault();
    }
    public WorldSettings()
    {
        SetDefault();

    }
    public void SetDefault()
    {
        UpdateVehiclePlates = true;
        CleanupVehicles = true;
        ReplaceVanillaShopKeepers = true;
        RandomVehiclePlatesPercent = 12f;// 7f;
        AllowRandomVanityPlates = true;
        RandomVehicleVanityPlatesPercent = 3f;// 5f;
        ShowAllLocationsOnDirectory = false;
        DefaultToMPMap = false;
        LoadCayoIplsWithMPMap = false;
        LoadRevealMap = false;

        EnableSeamlessCayoPerico = false;
        SeamlessCayoEnterDistance = 2000f;
        SeamlessCayoExitDistance = 3000f;
        SeamlessCayoEnableMapFix = true;
        SeamlessCayoUnloadIplsWhenLibertyCityLoaded = true;
        SeamlessCayoUnloadIplsWhenDisabled = true;
        SeamlessCayoIplLoadDistance = 6000f;
        SeamlessCayoIplUnloadDistance = 7000f;

        SeamlessCayoIplUnloadMinGapDistance = 500f;
        SeamlessCayoIplDetailStartDistance = 5000f;
        SeamlessCayoMapFixStopDistance = 5750f;
        SeamlessCayoPauseMapDistance = 3500f;
        SeamlessCayoIplBatchSizeMid = 4;

        SeamlessCayoMapFixStartDistance = 5000f;
        SeamlessCayoUseIslandMap = true;
        SeamlessCayoDisablePrLogZone = false;
        SeamlessCayoIplNearDistance = 3500f;
        SeamlessCayoIplLoadHoldMs = 1000;
        SeamlessCayoIplBatchSizeFar = 2;
        SeamlessCayoIplBatchSizeNear = 6;
        SeamlessCayoIplBatchDelayMs = 200;

        SeamlessCayoUseAdaptiveIplScheduler = true;
        SeamlessCayoIplAdaptiveTickMs = 50;
        SeamlessCayoIplMaxRequestsPerTick = 6;
        SeamlessCayoIplMaxBurstRequests = 12;

        SeamlessCayoIplMinRequestsPerSecondFar = 1;
        SeamlessCayoIplMinRequestsPerSecondMid = 2;
        SeamlessCayoIplMinRequestsPerSecondNear = 4;

        SeamlessCayoIplMaxRequestsPerSecondFar = 4;
        SeamlessCayoIplMaxRequestsPerSecondMid = 8;
        SeamlessCayoIplMaxRequestsPerSecondNear = 12;
        SeamlessCayoForceUnloadIfIslandActiveWhileFar = false;
        SeamlessCayoAggressiveUnloadAllIpls = false;
        SeamlessCayoUnloadDelayMs = 5000;
        SeamlessCayoUnloadBatchSize = 10;
        SeamlessCayoUnloadBatchDelayMs = 200;
        LowerPedSpawnsAtHigherWantedLevels = true;

        DefaultSpawnMultiplier = 1.0f;

        //LowerPedSpawnsAtHigherWantedLevels_Wanted2Multiplier = 0.9f;
        //LowerPedSpawnsAtHigherWantedLevels_Wanted3Multiplier = 0.75f;
        LowerPedSpawnsAtHigherWantedLevels_Wanted4Multiplier = 0.5f;
        LowerPedSpawnsAtHigherWantedLevels_Wanted5Multiplier = 0.3f;
        LowerPedSpawnsAtHigherWantedLevels_Wanted6Multiplier = 0.1f;

        LowerPedSpawnsAtHigherWantedLevels_Wanted7Multiplier = 0.1f;
        LowerPedSpawnsAtHigherWantedLevels_Wanted8Multiplier = 0.1f;
        LowerPedSpawnsAtHigherWantedLevels_Wanted9Multiplier = 0.1f;
        LowerPedSpawnsAtHigherWantedLevels_Wanted10Multiplier = 0.1f;

        // ShowAllBlipsOnMap = false;
        ShowMarkersOnLocationEntrances = false;
        HotelsUsesRooms = false;
        AllowPoliceBackupBlip = true;

        ShowAllBlipsOnMap = true;

        AirportsRequireOwnedPlanesLocal = true;
        AirportsOwnedPlanesLocalDistance = 1000f;
        AirportsRequireLicenseForPrivateFlights = true;
        AllowSettingDistantSirens = true;
        OutOfStateRandomVehiclePlatesPercent = 90f;
        DeadBodyAlertTimeout = 25000;
        UnconsciousBodyAlertTimeout = 25000;
        GunshotAlertTimeout = 35000;
        HelpCryAlertTimeout = 20000;

        OfficerMIACallInExpireDistance = 250f;

        OfficerMIACallInDistance = 150f;
        OfficerMIACallInTimeMin = 90000;
        OfficerMIACallInTimeMax = 150000;


        AllowOfficerMIACallIn = true;
        AllowSettingSirenState = true;
        OfficerMIAStartPercentage_Alterted = 80f;
        OfficerMIAStartPercentage_Regular = 40f;


        SetVanillaTaxiSuppressed = true;
        // SetReducedPropsOnMap = true;
        CreateObjectLocationsFromScanning = false;
        ShowMarkersInInteriors = true;
        InteriorMarkerType = 0;
        InteriorMarkerZOffset = 0.2f;
        InteriorMarkerScale = 0.25f;

        CheckAreaBeforeVehicleSpawn = true;
        ExtendedVehicleCleanup = true;

        SuppressFEJVehiclesFromGenerators = true;
        SetMissionFlagOn = true;

        //MaxPedsBeforeDispatchPause = 120;
        //MaxVehiclesBeforeDispatchPause = 180;

    }

}