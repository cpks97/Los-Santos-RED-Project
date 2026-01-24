using ExtensionsMethods;
using LosSantosRED.lsr;
using LosSantosRED.lsr.Helper;
using LosSantosRED.lsr.Interface;
//using LosSantosRED.lsr.Util.Locations;
using LSR.Vehicles;
using Rage;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;


namespace Mod
{
    public class World : IEntityLoggable, IEntityProvideable
    {
        private int totalWantedLevel;
        private IJurisdictions Jurisdictions;
        private ISettingsProvideable Settings;
        private ICrimes Crimes;
        private IWeapons Weapons;
        private ITimeControllable Time;
        private IInteriors Interiors;
        private IShopMenus ShopMenus;
        private IGangs Gangs;
        private IStreets Streets;
        private IPlacesOfInterest PlacesOfInterest;
        private List<Blip> CreatedBlips = new List<Blip>();
        private Blip TotalWantedBlip;
        private float CurrentSpawnMultiplier;
        private bool isSettingDensity;
        private bool isTrafficDisabled;

        private SeamlessCayoPericoController SeamlessCayoPerico;

        public World(IAgencies agencies, IZones zones, IJurisdictions jurisdictions, ISettingsProvideable settings, IPlacesOfInterest placesOfInterest, IPlateTypes plateTypes, INameProvideable names, IPedGroups relationshipGroups,
            IWeapons weapons, ICrimes crimes, ITimeControllable time, IShopMenus shopMenus, IInteriors interiors, IAudioPlayable audio, IGangs gangs, IGangTerritories gangTerritories, IStreets streets, IModItems modItems, IPedGroups pedGroups, ILocationTypes locationTypes,
            IOrganizations associations, IContacts contacts, ModDataFileManager modDataFileManager)
        {
            PlacesOfInterest = placesOfInterest;
            Zones = zones;
            Jurisdictions = jurisdictions;
            Settings = settings;
            SeamlessCayoPerico = new SeamlessCayoPericoController(Settings);
            Weapons = weapons;
            Crimes = crimes;
            Time = time;
            Interiors = interiors;
            ShopMenus = shopMenus;
            Gangs = gangs;
            GangTerritories = gangTerritories;
            Streets = streets;
            ModDataFileManager = modDataFileManager;
            Pedestrians = new Pedestrians(agencies, zones, jurisdictions, settings, names, relationshipGroups, weapons, crimes, shopMenus, Gangs, GangTerritories, this);
            Vehicles = new Vehicles(agencies, zones, jurisdictions, settings, plateTypes, modItems, this, associations);
            Places = new Places(this, zones, jurisdictions, settings, placesOfInterest, weapons, crimes, time, shopMenus, interiors, gangs, gangTerritories, streets, agencies, names, pedGroups, locationTypes, plateTypes, associations, contacts, ModDataFileManager.ModItems, modDataFileManager.IssueableWeapons, modDataFileManager.Heads, modDataFileManager.DispatchablePeople, modDataFileManager.ClothesNames);
            SpawnErrors = new List<SpawnError>();
        }
        public bool IsMPMapLoaded { get; private set; }
        public bool IsZombieApocalypse { get; set; } = false;
        public Vehicles Vehicles { get; private set; }
        public Pedestrians Pedestrians { get; private set; }
        public Places Places { get; private set; }
        public IZones Zones { get; set; }
        public IGangTerritories GangTerritories { get; set; }
        public int CitizenWantedLevel { get; set; }
        public int TotalWantedLevel { get; set; } = 0;
        public Vector3 PoliceBackupPoint { get; set; }
        public bool AnyFiresNearPlayer { get; private set; }
        public List<SpawnError> SpawnErrors { get; private set; }
        public ModDataFileManager ModDataFileManager { get; private set; }
        public ILocationInteractable LocationInteractable { get; private set; }
        public bool IsFEJInstalled { get; private set; }
        public bool IsFMTInstalled { get; private set; }
        public bool IsFEWInstalled { get; private set; }
        public string DebugString => "";


        public bool IsTrafficDisabled => isTrafficDisabled;
        public void Setup(IInteractionable player, ILocationInteractable locationInteractable)
        {
            DetermineMap();
            Pedestrians.Setup();
            LocationInteractable = locationInteractable;
            Places.Setup(player, locationInteractable);
            Vehicles.Setup();
            AddBlipsToMap();
            SetMemoryItems();
            CheckSpecialCircumstances();
        }
        private void CheckSpecialCircumstances()
        {
            IsFEJInstalled = NativeFunction.Natives.IS_DLC_PRESENT<bool>(Game.GetHashKey("greskfej"));
            EntryPoint.WriteToConsole($"FEJ Installed: {IsFEJInstalled}", 0);

            IsFMTInstalled = NativeFunction.Natives.IS_DLC_PRESENT<bool>(Game.GetHashKey("greskfmt"));
            EntryPoint.WriteToConsole($"FMT Installed: {IsFMTInstalled}", 0);

            IsFEWInstalled = NativeFunction.Natives.IS_DLC_PRESENT<bool>(Game.GetHashKey("greskfew"));
            EntryPoint.WriteToConsole($"FEW Installed: {IsFEWInstalled}", 0);

            //if (Settings.SettingsManager.WorldSettings.SetMissionFlagOn)
            //{
            //    NativeFunction.Natives.SET_MINIGAME_IN_PROGRESS(true);
            //}
        }
        private void SetMemoryItems()
        {
            if (Settings.SettingsManager.PlayerOtherSettings.AllowDLCVehicles)
            {
                NativeMemory.SetMPGlobals();
            }
        }
        public void Update()
        {

            SetDensity();

            if (Settings.SettingsManager.WorldSettings.AllowPoliceBackupBlip)
            {
                if (PoliceBackupPoint == Vector3.Zero)
                {
                    if (TotalWantedBlip.Exists())
                    {
                        TotalWantedBlip.Delete();
                    }
                }
                else
                {
                    if (!TotalWantedBlip.Exists())
                    {
                        CreateTotalWantedBlip();
                    }
                    else
                    {
                        TotalWantedBlip.Position = PoliceBackupPoint;
                    }
                }
            }
            else
            {
                if (TotalWantedBlip.Exists())
                {
                    TotalWantedBlip.Delete();
                }
            }
            if (TotalWantedLevel != totalWantedLevel)
            {
                OnTotalWantedLevelChanged();
            }
            if (Settings.SettingsManager.WorldSettings.AllowSettingDistantSirens)
            {
                NativeFunction.Natives.DISTANT_COP_CAR_SIRENS(false);
            }
            int numFires = NativeFunction.Natives.GET_NUMBER_OF_FIRES_IN_RANGE<int>(Game.LocalPlayer.Character.Position, 150f);
            AnyFiresNearPlayer = numFires > 0;

            try
            {
                SeamlessCayoPerico?.Update(IsMPMapLoaded);
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole($"SeamlessCayoPerico.Update Error: {ex.Message} {ex.StackTrace}", 0);
            }
        }
        public void Dispose()
        {
            Places.Dispose();
            Pedestrians.Dispose();
            Vehicles.Dispose();
            RemoveBlips();
            if (Settings.SettingsManager.WorldSettings.SetMissionFlagOn)
            {
                NativeFunction.Natives.SET_MINIGAME_IN_PROGRESS(false);
            }
        }
        public void ClearSpawned(bool includeCivilians)
        {
            Pedestrians.ClearSpawned();
            Vehicles.ClearSpawned(includeCivilians);
        }
        public void LoadMPMap()
        {
            if (!IsMPMapLoaded)
            {
                // Safety: Liberty City (LCPP/WorldTravel) occupies the Cayo region for some installs.
                // If Liberty City is currently loaded, do NOT load MP map/Cayo IPLs.
                if (SeamlessCayoPericoController.IsLibertyCityLoaded())
                {
                    Game.DisplayNotification("~r~Cannot load MP Map/Cayo while Liberty City is loaded.");
                    EntryPoint.WriteToConsole("World.LoadMPMap aborted: Liberty City detected (manhat06_slod)", 0);
                    return;
                }

                Game.FadeScreenOut(1500, true);
                NativeFunction.Natives.SET_INSTANCE_PRIORITY_MODE(1);
                NativeFunction.Natives.x0888C3502DBBEEF5();// ON_ENTER_MP();
                if (Settings?.SettingsManager?.WorldSettings?.LoadCayoIplsWithMPMap == true)
                {
                    LoadCayoIPLs();
                }
                LoadAirstripIPLs();
                Game.FadeScreenIn(1500, true);
                IsMPMapLoaded = true;
            }
        }
        public void LoadSPMap()
        {
            if (IsMPMapLoaded)
            {
                Game.FadeScreenOut(1500, true);
                NativeFunction.Natives.SET_INSTANCE_PRIORITY_MODE(0);
                NativeFunction.Natives.xD7C10C4A637992C9();// ON_ENTER_SP();
                if (Settings?.SettingsManager?.WorldSettings?.LoadCayoIplsWithMPMap == true)
                {
                    UnloadCayoIPLs();
                }
                UnloadAirstripIPLs();
                Game.FadeScreenIn(1500, true);
                IsMPMapLoaded = false;
            }
        }
        public void AddBlip(Blip myBlip)
        {
            if (myBlip.Exists())
            {
                CreatedBlips.Add(myBlip);
            }
        }
        public void AddBlipsToMap()
        {
            CreatedBlips = new List<Blip>();
        }
        public void RemoveBlips()
        {
            foreach (Blip MyBlip in CreatedBlips)
            {
                if (MyBlip.Exists())
                {
                    MyBlip.Delete();
                }
            }
            if (TotalWantedBlip.Exists())
            {
                TotalWantedBlip.Delete();
            }
        }
        public void SetDensity()
        {
            CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.DefaultSpawnMultiplier;// 1.0f;
            if (Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels)
            {
                if (TotalWantedLevel >= 10)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted10Multiplier;
                }
                else if (TotalWantedLevel >= 9)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted9Multiplier;
                }
                else if (TotalWantedLevel >= 8)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted8Multiplier;
                }
                else if (TotalWantedLevel >= 7)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted7Multiplier;
                }
                else if (TotalWantedLevel >= 6)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted6Multiplier;
                }
                else if (TotalWantedLevel == 5)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted5Multiplier;
                }
                else if (TotalWantedLevel == 4)
                {
                    CurrentSpawnMultiplier = Settings.SettingsManager.WorldSettings.LowerPedSpawnsAtHigherWantedLevels_Wanted4Multiplier;
                }
            }
            if (isTrafficDisabled)
            {
                CurrentSpawnMultiplier = 0.0f;
            }
            if (CurrentSpawnMultiplier != 1.0f && !isSettingDensity)
            {
                isSettingDensity = true;
                EntryPoint.WriteToConsole($"World - START Setting Population Density {CurrentSpawnMultiplier}");
                GameFiber.StartNew(delegate
                {
                    try
                    {
                        while (CurrentSpawnMultiplier != 1.0f && EntryPoint.ModController?.IsRunning == true)
                        {
                            NativeFunction.Natives.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(CurrentSpawnMultiplier);
                            NativeFunction.Natives.SET_PED_DENSITY_MULTIPLIER_THIS_FRAME(CurrentSpawnMultiplier);
                            NativeFunction.Natives.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(CurrentSpawnMultiplier);
                            NativeFunction.Natives.SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME(CurrentSpawnMultiplier);
                            NativeFunction.Natives.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(CurrentSpawnMultiplier);
                            GameFiber.Yield();
                        }
                        isSettingDensity = false;
                        EntryPoint.WriteToConsole($"World - DONE Setting Population Density {CurrentSpawnMultiplier}");
                    }
                    catch (Exception ex)
                    {
                        EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                        //EntryPoint.ModController.CrashUnload();
                    }
                }, $"Density Runner");
            }
        }
        private void DetermineMap()
        {
            string iplName = "bkr_bi_hw1_13_int";
            NativeFunction.Natives.REQUEST_IPL(iplName);
            GameFiber.Sleep(100);
            IsMPMapLoaded = NativeFunction.Natives.IS_IPL_ACTIVE<bool>(iplName);
            EntryPoint.WriteToConsole($"MP Map Loaded: {IsMPMapLoaded}");
        }
        private void CreateTotalWantedBlip()
        {
            TotalWantedBlip = new Blip(PoliceBackupPoint, 50f)
            {
                Name = "Police Requesting Assistance",
                Color = Color.Purple,
                Alpha = 0.25f
            };
            EntryPoint.WriteToConsole($"TOTAL WANTED BLIP CREATED");
            if (TotalWantedBlip.Exists())
            {
                NativeFunction.Natives.BEGIN_TEXT_COMMAND_SET_BLIP_NAME("STRING");
                NativeFunction.Natives.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME("Police Requesting Assistance");
                NativeFunction.Natives.END_TEXT_COMMAND_SET_BLIP_NAME(TotalWantedBlip);
                NativeFunction.Natives.SET_BLIP_AS_SHORT_RANGE((uint)TotalWantedBlip.Handle, true);
            }
        }
        private void OnTotalWantedLevelChanged()
        {
            if (TotalWantedLevel == 0)
            {
                OnTotalWantedLevelRemoved();
            }
            else if (totalWantedLevel == 0)
            {
                OnTotalWantedLevelAdded();
            }
            else
            {
                //EntryPoint.WriteToConsoleTestLong($"OnTotalWantedLevelChanged {TotalWantedLevel}");
            }
            totalWantedLevel = TotalWantedLevel;
        }
        private void OnTotalWantedLevelRemoved()
        {
            if (Settings.SettingsManager.WorldSettings.AllowSettingDistantSirens)
            {
                NativeFunction.Natives.DISTANT_COP_CAR_SIRENS(false);
                //EntryPoint.WriteToConsoleTestLong($"OnTotalWantedLevelRemoved Distant Sirens Removed");
            }
        }
        private void OnTotalWantedLevelAdded()
        {
            //EntryPoint.WriteToConsoleTestLong($"OnTotalWantedLevelAdded {TotalWantedLevel}");
        }
        public void SetTrafficDisabled()
        {
            isTrafficDisabled = true;
            Vehicles.ClearPolice();
            Pedestrians.ClearPolice();
        }
        public void SetTrafficEnabled()
        {
            isTrafficDisabled = false;
        }

        private void LoadAirstripIPLs()
        {
            foreach (string ipl in airstripMPIPLs)
            {
                NativeFunction.Natives.REQUEST_IPL(ipl);
            }
            foreach (string ipl in airstripSPIPLs)
            {
                NativeFunction.Natives.REMOVE_IPL(ipl);
            }
        }
        private void UnloadAirstripIPLs()
        {
            foreach (string ipl in airstripMPIPLs)
            {
                NativeFunction.Natives.REMOVE_IPL(ipl);
            }
            foreach (string ipl in airstripSPIPLs)
            {
                NativeFunction.Natives.REQUEST_IPL(ipl);
            }
        }
        private List<string> airstripMPIPLs = new List<string>()
        {
        "m24_2_airstrip",
        "m24_2_hanger_additions",
        "m24_2_mp2024_02_additions",
        "m24_2_legacy_fixes"
        };
        private List<string> airstripSPIPLs = new List<string>()
        {
        };
        private void LoadCayoIPLs()
        {
            foreach (string ipl in CayoMPIPLs)
            {
                NativeFunction.Natives.REQUEST_IPL(ipl);
            }
            foreach (string ipl in CayoSPIPLs)
            {
            }
        }
        private void UnloadCayoIPLs()
        {
            foreach (string ipl in CayoMPIPLs)
            {
                NativeFunction.Natives.REMOVE_IPL(ipl);
            }
            foreach (string ipl in CayoSPIPLs)
            {
                NativeFunction.Natives.REQUEST_IPL(ipl);
            }
        }
        private List<string> CayoMPIPLs = new List<string>()
        {
        "h4_aa_guns",
        "h4_aa_guns_lod",
        "h4_beach",
        "h4_beach_lod",
        "h4_beach_slod",
        "h4_beach_props",
        "h4_beach_props_lod",
        "h4_beach_props_slod",
        "h4_beach_party",
        "h4_beach_props_party",
        "h4_beach_party_lod",
        "h4_beach_bar_props",
        "h4_island_padlock_props",
        "h4_islandx_barrack_hatch",
        "h4_islandx_barrack_props",
        "h4_islandx_barrack_props_lod",
        "h4_islandx_barrack_props_slod",
        "h4_islandxcanal_props",
        "h4_islandxcanal_props_lod",
        "h4_islandxcanal_props_slod",
        "h4_islandx_mansion_entrance_fence",
        "h4_islandx_mansion_guardfence",
        "h4_islandx_mansion_lights",
        "h4_islandx_mansion_lod",
        "h4_islandx_mansion_slod",
        "h4_islandx_mansion_b",
        "h4_islandx_mansion_b_side_fence",
        "h4_islandx_mansion_b_lod",
        "h4_islandx_mansion_b_slod",
        "h4_islandx_mansion_props",
        "h4_islandx_mansion_props_lod",
        "h4_islandx_mansion_props_slod",
        "h4_islandx_mansion_vault",
        "h4_islandx_mansion_vault_lod",
        "h4_islandx_mansion_lockup_01",
        "h4_islandx_mansion_lockup_01_lod",
        "h4_islandx_mansion_lockup_02",
        "h4_islandx_mansion_lockup_02_lod",
        "h4_islandx_mansion_lockup_03",
        "h4_islandx_mansion_lockup_03_lod",
        "h4_islandx_mansion_office",
        "h4_islandx_mansion_office_lod",
        "h4_islandx",
        "h4_islandx_props",
        "h4_islandx_props_lod",
        "h4_islandx_terrain_01",
        "h4_islandx_terrain_02",
        "h4_islandx_terrain_03",
        "h4_islandx_terrain_04",
        "h4_islandx_terrain_05",
        "h4_islandx_terrain_06",
        "h4_islandx_terrain_props_05_a",
        "h4_islandx_terrain_props_05_b",
        "h4_islandx_terrain_props_05_c",
        "h4_islandx_terrain_props_05_d",
        "h4_islandx_terrain_props_05_e",
        "h4_islandx_terrain_props_05_f",
        "h4_islandx_terrain_props_06_a",
        "h4_islandx_terrain_props_06_b",
        "h4_islandx_terrain_props_06_c",
        "h4_islandx_terrain_01_lod",
        "h4_islandx_terrain_02_lod",
        "h4_islandx_terrain_03_lod",
        "h4_islandx_terrain_04_lod",
        "h4_islandx_terrain_05_lod",
        "h4_islandx_terrain_06_lod",
        "h4_islandx_terrain_props_05_a_lod",
        "h4_islandx_terrain_props_05_b_lod",
        "h4_islandx_terrain_props_05_c_lod",
        "h4_islandx_terrain_props_05_d_lod",
        "h4_islandx_terrain_props_05_e_lod",
        "h4_islandx_terrain_props_05_f_lod",
        "h4_islandx_terrain_props_06_a_lod",
        "h4_islandx_terrain_props_06_b_lod",
        "h4_islandx_terrain_props_06_c_lod",
        "h4_islandx_terrain_01_slod",
        "h4_islandx_terrain_02_slod",
        "h4_islandx_terrain_04_slod",
        "h4_islandx_terrain_05_slod",
        "h4_islandx_terrain_06_slod",
        "h4_islandx_terrain_props_05_d_slod",
        "h4_islandx_terrain_props_05_e_slod",
        "h4_islandx_terrain_props_05_f_slod",
        "h4_islandx_terrain_props_06_a_slod",
        "h4_islandx_terrain_props_06_b_slod",
        "h4_islandx_terrain_props_06_c_slod",
        "h4_mph4_airstrip",
        "h4_mph4_airstrip_interior_0_airstrip_hanger",
        "h4_airstrip_hanger",
        "h4_mph4_beach",
        "h4_mph4_dock",
        "h4_mph4_island",
        "h4_mph4_island_long_0",
        "h4_mph4_island_strm_0",
        "h4_mph4_island_placement",
        "h4_mph4_island_ne_placement",
        "h4_mph4_island_nw_placement",
        "h4_mph4_island_se_placement",
        "h4_mph4_island_sw_placement",
        "h4_mph4_mansion",
        "h4_mph4_mansion_strm_0",
        "h4_mph4_mansion_b",
        "h4_mph4_mansion_b_strm_0",
        "h4_mph4_wtowers",
        "h4_mph4_terrain_01",
        "h4_mph4_terrain_01_grass_0",
        "h4_mph4_terrain_01_grass_1",
        "h4_mph4_terrain_01_long_0",
        "h4_mph4_terrain_02",
        "h4_mph4_terrain_02_grass_0",
        "h4_mph4_terrain_02_grass_1",
        "h4_mph4_terrain_02_grass_2",
        "h4_mph4_terrain_02_grass_3",
        "h4_mph4_terrain_03",
        "h4_mph4_terrain_04",
        "h4_mph4_terrain_04_grass_0",
        "h4_mph4_terrain_04_grass_1",
        "h4_mph4_terrain_05",
        "h4_mph4_terrain_05_grass_0",
        "h4_mph4_terrain_06",
        "h4_mph4_terrain_06_strm_0",
        "h4_mph4_terrain_lod",
        "h4_mph4_terrain_occ_00",
        "h4_mph4_terrain_occ_01",
        "h4_mph4_terrain_occ_02",
        "h4_mph4_terrain_occ_03",
        "h4_mph4_terrain_occ_04",
        "h4_mph4_terrain_occ_05",
        "h4_mph4_terrain_occ_06",
        "h4_mph4_terrain_occ_07",
        "h4_mph4_terrain_occ_08",
        "h4_mph4_terrain_occ_09",
        "h4_ne_ipl_00",
        "h4_ne_ipl_01",
        "h4_ne_ipl_02",
        "h4_ne_ipl_03",
        "h4_ne_ipl_04",
        "h4_ne_ipl_05",
        "h4_ne_ipl_06",
        "h4_ne_ipl_07",
        "h4_ne_ipl_08",
        "h4_ne_ipl_09",
        "h4_ne_ipl_00_lod",
        "h4_ne_ipl_01_lod",
        "h4_ne_ipl_02_lod",
        "h4_ne_ipl_03_lod",
        "h4_ne_ipl_04_lod",
        "h4_ne_ipl_05_lod",
        "h4_ne_ipl_06_lod",
        "h4_ne_ipl_07_lod",
        "h4_ne_ipl_08_lod",
        "h4_ne_ipl_09_lod",
        "h4_ne_ipl_00_slod",
        "h4_ne_ipl_01_slod",
        "h4_ne_ipl_02_slod",
        "h4_ne_ipl_03_slod",
        "h4_ne_ipl_04_slod",
        "h4_ne_ipl_05_slod",
        "h4_ne_ipl_06_slod",
        "h4_ne_ipl_07_slod",
        "h4_ne_ipl_08_slod",
        "h4_ne_ipl_09_slod",
        "h4_nw_ipl_00",
        "h4_nw_ipl_01",
        "h4_nw_ipl_02",
        "h4_nw_ipl_03",
        "h4_nw_ipl_04",
        "h4_nw_ipl_05",
        "h4_nw_ipl_06",
        "h4_nw_ipl_07",
        "h4_nw_ipl_08",
        "h4_nw_ipl_09",
        "h4_nw_ipl_00_lod",
        "h4_nw_ipl_01_lod",
        "h4_nw_ipl_02_lod",
        "h4_nw_ipl_03_lod",
        "h4_nw_ipl_04_lod",
        "h4_nw_ipl_05_lod",
        "h4_nw_ipl_06_lod",
        "h4_nw_ipl_07_lod",
        "h4_nw_ipl_08_lod",
        "h4_nw_ipl_09_lod",
        "h4_nw_ipl_00_slod",
        "h4_nw_ipl_01_slod",
        "h4_nw_ipl_02_slod",
        "h4_nw_ipl_03_slod",
        "h4_nw_ipl_04_slod",
        "h4_nw_ipl_05_slod",
        "h4_nw_ipl_06_slod",
        "h4_nw_ipl_07_slod",
        "h4_nw_ipl_08_slod",
        "h4_nw_ipl_09_slod",
        "h4_se_ipl_00",
        "h4_se_ipl_01",
        "h4_se_ipl_02",
        "h4_se_ipl_03",
        "h4_se_ipl_04",
        "h4_se_ipl_05",
        "h4_se_ipl_06",
        "h4_se_ipl_07",
        "h4_se_ipl_08",
        "h4_se_ipl_09",
        "h4_se_ipl_00_lod",
        "h4_se_ipl_01_lod",
        "h4_se_ipl_02_lod",
        "h4_se_ipl_03_lod",
        "h4_se_ipl_04_lod",
        "h4_se_ipl_05_lod",
        "h4_se_ipl_06_lod",
        "h4_se_ipl_07_lod",
        "h4_se_ipl_08_lod",
        "h4_se_ipl_09_lod",
        "h4_se_ipl_00_slod",
        "h4_se_ipl_01_slod",
        "h4_se_ipl_02_slod",
        "h4_se_ipl_03_slod",
        "h4_se_ipl_04_slod",
        "h4_se_ipl_05_slod",
        "h4_se_ipl_06_slod",
        "h4_se_ipl_07_slod",
        "h4_se_ipl_08_slod",
        "h4_se_ipl_09_slod",
        "h4_sw_ipl_00",
        "h4_sw_ipl_01",
        "h4_sw_ipl_02",
        "h4_sw_ipl_03",
        "h4_sw_ipl_04",
        "h4_sw_ipl_05",
        "h4_sw_ipl_06",
        "h4_sw_ipl_07",
        "h4_sw_ipl_08",
        "h4_sw_ipl_09",
        "h4_sw_ipl_00_lod",
        "h4_sw_ipl_01_lod",
        "h4_sw_ipl_02_lod",
        "h4_sw_ipl_03_lod",
        "h4_sw_ipl_04_lod",
        "h4_sw_ipl_05_lod",
        "h4_sw_ipl_06_lod",
        "h4_sw_ipl_07_lod",
        "h4_sw_ipl_08_lod",
        "h4_sw_ipl_09_lod",
        "h4_sw_ipl_00_slod",
        "h4_sw_ipl_01_slod",
        "h4_sw_ipl_02_slod",
        "h4_sw_ipl_03_slod",
        "h4_sw_ipl_04_slod",
        "h4_sw_ipl_05_slod",
        "h4_sw_ipl_06_slod",
        "h4_sw_ipl_07_slod",
        "h4_sw_ipl_08_slod",
        "h4_sw_ipl_09_slod",
        "h4_ch2_mansion_final",
        "h4_islandx_checkpoint",
        "h4_islandx_checkpoint_lod",
        "h4_islandx_checkpoint_props",
        "h4_islandx_checkpoint_props_lod",
        "h4_islandx_checkpoint_props_slod",
        "h4_islandairstrip",
        "h4_islandairstrip_props",
        "h4_islandairstrip_props_lod",
        "h4_islandairstrip_props_slod",
        "h4_islandairstrip_propsb",
        "h4_islandairstrip_propsb_lod",
        "h4_islandairstrip_propsb_slod",
        "h4_islandairstrip_hangar_props",
        "h4_islandx_maindock",
        "h4_islandx_maindock_lod",
        "h4_islandx_maindock_slod",
        "h4_islandx_maindock_props",
        "h4_islandx_maindock_props_lod",
        "h4_islandx_maindock_props_slod",
        "h4_islandx_maindock_props_2",
        "h4_islandx_maindock_props_2_lod",
        "h4_islandx_maindock_props_2_slod",
        "h4_islandxdock",
        "h4_islandxdock_lod",
        "h4_islandxdock_slod",
        "h4_islandxdock_props",
        "h4_islandxdock_props_lod",
        "h4_islandxdock_props_slod",
        "h4_islandxdock_props_2",
        "h4_islandxdock_props_2_lod",
        "h4_islandxdock_props_2_slod",
        "h4_islandxdock_water_hatch",
        "h4_islandxtower",
        "h4_islandxtower_veg",
        "h4_islandxtower_veg_lod",
        "h4_islandxtower_veg_slod",
        "island_lodlights",
        "island_distantlights",
        "h4_islandx_mansion",
        "h4_mansion_gate_closed",
        "h4_islandairstrip_doorsopen",
        "h4_islandairstrip_doorsopen_lod"
        };
        private List<string> CayoSPIPLs = new List<string>()
        {
        };
    }

    /// <summary>
    /// Seamless (no-cutscene) Cayo Perico streaming controller for Story Mode.
    ///
    /// Design goals for LSRP + WorldTravel/LCPP:
    /// - Off by default (opt-in) so it cannot be blamed for crashes/perf unless enabled.
    /// - Never run when Liberty City is loaded (detected via manhat06_slod IPL).
    /// - Stream Cayo IPLs in phases + small batches to reduce "all at once" pop-in.
    /// - Avoid unload/load thrash (delay + cancel unload on re-approach).
    /// - Avoid fighting WorldTravel helpers: delay the IPL that WorldTravel uses as "external Cayo loaded" trigger (h4_islandairstrip_slod).
    /// - Apply minimap + (optional) pause-map support on approach (not only after landing).
    /// </summary>
    internal sealed class SeamlessCayoPericoController : IDisposable
    {
        private readonly ISettingsProvideable Settings;

        // Track IPLs we explicitly REQUEST_IPL'ed while enabled, so we can optionally unload ONLY our own requests.
        private readonly HashSet<string> IplsLoadedByThis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // IPLs that were already active before seamless Cayo streaming was enabled (e.g., MP-map mode or other plugins).
        // In non-aggressive unload mode we avoid removing these to prevent unload/load thrash and flicker.
        private readonly HashSet<string> PreexistingActiveIpls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool CapturedPreexistingActiveIpls = false;

        private bool IslandFeaturesEnabledByThis = false;
        private bool NearIsland = false;
        private bool LastIsMPMapLoaded = false;

        private GameFiber MapFixFiber;
        private GameFiber IplLoaderFiber;
        private GameFiber IplUnloaderFiber;

        private Blip DummyMapBlip;

        private bool HasStartedIplLoading = false;

        // Timing / stability
        private uint GameTimeLastStatusLog = 0;
        private uint FarBeyondUnloadSince = 0;
        private uint WithinLoadSince = 0;

        // Optional zone tweak (disabled by default because Liberty City can overlap in the same region)
        private bool PrLogZoneDisabled = false;

        // Pause-map toggling (optional)
        private bool UseIslandMapByThis = false;

        // First-approach diagnostics
        private bool LoggedStartLoadThisSession = false;
        private bool LoggedIslandxActiveThisSession = false;
        private float StartLoadDistanceThisSession = -1f;
        private float FirstIslandxActiveDistanceThisSession = -1f;

        // IPL phase lists (built once)
        private List<string> PhaseBaseIpls;
        private List<string> PhaseDetailIpls;
        private int NextBaseIndex = 0;
        private int NextDetailIndex = 0;

        // Adaptive IPL scheduler (token-bucket + ETA-based ramp).
        // Goal: avoid load spikes that can cause stutter/crashes, while still finishing before the player reaches the island.
        private double IplTokenBucket = 0.0;
        private uint GameTimeLastIplTokenUpdate = 0;
        private float LastAdaptiveDesiredRps = 0f;
        private float LastAdaptiveClosingSpeed = 0f;
        private float LastAdaptiveEtaSeconds = 0f;

        private bool WarnedMpMap = false;

        private enum ControllerState
        {
            Disabled,
            BlockedByLibertyCity,
            Far,
            PreloadingBase,
            PreloadingDetail,
            NearIsland,
            Unloading
        }

        private ControllerState CurrentState = ControllerState.Disabled;
        private string CurrentStateReason = "";

        private void Log(string message, int level = 1)
        {
            try
            {
                EntryPoint.WriteToConsole($"[SeamlessCayo] {message}", level);
                try { Game.LogTrivial($"[SeamlessCayo] {message}"); } catch { }
            }
            catch { }
        }

        private void SetState(ControllerState newState, string reason = null)
        {
            try
            {
                // Reduce spam: only log on state changes (not on reason changes).
                if (newState != CurrentState)
                {
                    string old = $"{CurrentState}({CurrentStateReason})";
                    CurrentState = newState;
                    CurrentStateReason = reason ?? "";
                    Log($"STATE -> {CurrentState} ({CurrentStateReason}) [from {old}]", 0);
                }
                else if (reason != null)
                {
                    CurrentStateReason = reason;
                }
            }
            catch { }
        }

        private bool IsSafeToApplyRadarInteriorTrick()
        {
            try
            {
                if (IsLibertyCityLoaded())
                {
                    return false;
                }

                bool pauseMenu = false;
                try { pauseMenu = NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>(); } catch { }
                if (pauseMenu) return false;

                int interior = 0;
                try { interior = NativeFunction.Natives.GET_INTERIOR_FROM_ENTITY<int>(Game.LocalPlayer.Character); } catch { }
                return interior == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsH4IslandxActive()
        {
            try { return NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_islandx"); } catch { return false; }
        }

        private bool IsCayoVisuallyLoaded()
        {
            // IMPORTANT: do NOT treat h4_mph4_island as "Cayo loaded".
            // In LSRP MP-map mode, h4_mph4_island can be active even when the island is not streamed.
            return IsH4IslandxActive();
        }

        private void LogStatusThrottled(bool enabled, bool isMPMapLoaded, float distance, float iplLoadDist, float iplUnloadDist, float detailStartDist, float nearDist, float mapFixStartDist, float mapFixStopDist, float pauseMapDist)
        {
            try
            {
                uint now = Game.GameTime;
                if (GameTimeLastStatusLog != 0 && now - GameTimeLastStatusLog < 5000)
                {
                    return;
                }
                GameTimeLastStatusLog = now;

                bool h4_islandx = false;
                bool h4_mph4_island = false;
                bool manhat06_slod = false;
                bool h4_islandairstrip_slod = false;
                try { h4_islandx = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_islandx"); } catch { }
                try { h4_mph4_island = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_mph4_island"); } catch { }
                try { h4_islandairstrip_slod = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_islandairstrip_slod"); } catch { }
                try { manhat06_slod = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("manhat06_slod"); } catch { }

                bool loaderAlive = false;
                bool mapFixAlive = false;
                bool unloaderAlive = false;
                try { loaderAlive = IplLoaderFiber != null && IplLoaderFiber.IsAlive; } catch { }
                try { mapFixAlive = MapFixFiber != null && MapFixFiber.IsAlive; } catch { }
                try { unloaderAlive = IplUnloaderFiber != null && IplUnloaderFiber.IsAlive; } catch { }

                int baseCount = PhaseBaseIpls?.Count ?? 0;
                int detailCount = PhaseDetailIpls?.Count ?? 0;

                Log($"enabled={enabled} mpMap={isMPMapLoaded} lc={manhat06_slod} dist={distance:0} load={iplLoadDist:0} detailStart={detailStartDist:0} nearBatch={nearDist:0} unload={iplUnloadDist:0} mapFixOn={mapFixStartDist:0} mapFixOff={mapFixStopDist:0} pauseMap={pauseMapDist:0} near={NearIsland} startedLoad={HasStartedIplLoading} loaderAlive={loaderAlive} unloaderAlive={unloaderAlive} mapFixAlive={mapFixAlive} p1={NextBaseIndex}/{baseCount} p2={NextDetailIndex}/{detailCount} loadedByThis={IplsLoadedByThis.Count} preexisting={PreexistingActiveIpls.Count} h4_islandx={h4_islandx} h4_mph4_island={h4_mph4_island} h4_islandairstrip_slod={h4_islandairstrip_slod} useIslandMapByThis={UseIslandMapByThis} rps={LastAdaptiveDesiredRps:0.0} eta={LastAdaptiveEtaSeconds:0.0} close={LastAdaptiveClosingSpeed:0.0} state={CurrentState} reason={CurrentStateReason}", 0);
            }
            catch { }
        }

        // Same coords as scully_cayoperico config.lua
        private static readonly Vector3 IslandCenter = new Vector3(4840.571f, -5174.425f, 2.0f);

        // Dummy blip coords used by common island minimap fixes
        private static readonly Vector3 DummyBlipCoords = new Vector3(6146.53f, -6107.55f, 0.0f);

        // Radar interior trick (common minimap fix)
        private static readonly int FakeIslandInteriorHash = unchecked((int)Game.GetHashKey("h4_fake_islandx"));
        private const float RadarInteriorX = 4700.0f;
        private const float RadarInteriorY = -5150.0f;

        // --- IPL list (from scully_cayoperico data/ipl.lua) ---
        private static readonly string[] CayoIpls = new string[]
        {
        "h4_mph4_terrain_occ_00",
        "h4_mph4_terrain_occ_01",
        "h4_mph4_terrain_occ_02",
        "h4_mph4_terrain_occ_03",
        "h4_mph4_terrain_occ_04",
        "h4_mph4_terrain_occ_05",
        "h4_mph4_terrain_occ_06",
        "h4_mph4_terrain_occ_07",
        "h4_mph4_terrain_occ_08",
        "h4_mph4_terrain_occ_09",
        "h4_mph4_terrain_01_grass_0",
        "h4_mph4_terrain_01_grass_1",
        "h4_mph4_terrain_02_grass_0",
        "h4_mph4_terrain_02_grass_1",
        "h4_mph4_terrain_02_grass_2",
        "h4_mph4_terrain_02_grass_3",
        "h4_mph4_terrain_04_grass_0",
        "h4_mph4_terrain_04_grass_1",
        "h4_mph4_terrain_05_grass_0",
        "h4_mph4_terrain_06_grass_0",
        "h4_islandx_terrain_01",
        "h4_islandx_terrain_01_lod",
        "h4_islandx_terrain_01_slod",
        "h4_islandx_terrain_02",
        "h4_islandx_terrain_02_lod",
        "h4_islandx_terrain_02_slod",
        "h4_islandx_terrain_03",
        "h4_islandx_terrain_03_lod",
        "h4_islandx_terrain_04",
        "h4_islandx_terrain_04_lod",
        "h4_islandx_terrain_04_slod",
        "h4_islandx_terrain_05",
        "h4_islandx_terrain_05_lod",
        "h4_islandx_terrain_05_slod",
        "h4_islandx_terrain_06",
        "h4_islandx_terrain_06_lod",
        "h4_islandx_terrain_06_slod",
        "h4_islandx_terrain_props_05_a",
        "h4_islandx_terrain_props_05_a_lod",
        "h4_islandx_terrain_props_05_b",
        "h4_islandx_terrain_props_05_b_lod",
        "h4_islandx_terrain_props_05_c",
        "h4_islandx_terrain_props_05_c_lod",
        "h4_islandx_terrain_props_05_d",
        "h4_islandx_terrain_props_05_d_lod",
        "h4_islandx_terrain_props_05_d_slod",
        "h4_islandx_terrain_props_05_e",
        "h4_islandx_terrain_props_05_e_lod",
        "h4_islandx_terrain_props_05_e_slod",
        "h4_islandx_terrain_props_05_f",
        "h4_islandx_terrain_props_05_f_lod",
        "h4_islandx_terrain_props_05_f_slod",
        "h4_islandx_terrain_props_06_a",
        "h4_islandx_terrain_props_06_a_lod",
        "h4_islandx_terrain_props_06_a_slod",
        "h4_islandx_terrain_props_06_b",
        "h4_islandx_terrain_props_06_b_lod",
        "h4_islandx_terrain_props_06_b_slod",
        "h4_islandx_terrain_props_06_c",
        "h4_islandx_terrain_props_06_c_lod",
        "h4_islandx_terrain_props_06_c_slod",
        "h4_mph4_terrain_01",
        "h4_mph4_terrain_01_long_0",
        "h4_mph4_terrain_02",
        "h4_mph4_terrain_03",
        "h4_mph4_terrain_04",
        "h4_mph4_terrain_05",
        "h4_mph4_terrain_06",
        "h4_mph4_terrain_06_strm_0",
        "h4_mph4_terrain_lod",
        "h4_islandx",
        "h4_islandx_disc_strandedshark",
        "h4_islandx_disc_strandedshark_lod",
        "h4_islandx_disc_strandedwhale",
        "h4_islandx_disc_strandedwhale_lod",
        "h4_islandx_props",
        "h4_islandx_props_lod",
        "h4_mph4_island",
        "h4_mph4_island_long_0",
        "h4_mph4_island_strm_0",
        "h4_beach",
        "h4_beach_bar_props",
        "h4_beach_lod",
        "h4_beach_party",
        "h4_beach_party_lod",
        "h4_beach_props",
        "h4_beach_props_lod",
        "h4_beach_props_party",
        "h4_beach_props_slod",
        "h4_beach_slod",
        "h4_islandairstrip",
        "h4_islandairstrip_doorsopen",
        "h4_islandairstrip_doorsopen_lod",
        "h4_islandairstrip_hangar_props",
        "h4_islandairstrip_hangar_props_lod",
        "h4_islandairstrip_hangar_props_slod",
        "h4_islandairstrip_lod",
        "h4_islandairstrip_props",
        "h4_islandairstrip_propsb",
        "h4_islandairstrip_propsb_lod",
        "h4_islandairstrip_propsb_slod",
        "h4_islandairstrip_props_lod",
        "h4_islandairstrip_props_slod",
        "h4_islandairstrip_slod",
        "h4_islandxcanal_props",
        "h4_islandxcanal_props_lod",
        "h4_islandxcanal_props_slod",
        "h4_islandxdock",
        "h4_islandxdock_lod",
        "h4_islandxdock_props",
        "h4_islandxdock_props_2",
        "h4_islandxdock_props_2_lod",
        "h4_islandxdock_props_2_slod",
        "h4_islandxdock_props_lod",
        "h4_islandxdock_props_slod",
        "h4_islandxdock_slod",
        "h4_islandxdock_water_hatch",
        "h4_islandxtower",
        "h4_islandxtower_lod",
        "h4_islandxtower_slod",
        "h4_islandxtower_veg",
        "h4_islandxtower_veg_lod",
        "h4_islandxtower_veg_slod",
        "h4_islandx_barrack_hatch",
        "h4_islandx_barrack_props",
        "h4_islandx_barrack_props_lod",
        "h4_islandx_barrack_props_slod",
        "h4_islandx_checkpoint",
        "h4_islandx_checkpoint_lod",
        "h4_islandx_checkpoint_props",
        "h4_islandx_checkpoint_props_lod",
        "h4_islandx_checkpoint_props_slod",
        "h4_islandx_maindock",
        "h4_islandx_maindock_lod",
        "h4_islandx_maindock_props",
        "h4_islandx_maindock_props_2",
        "h4_islandx_maindock_props_2_lod",
        "h4_islandx_maindock_props_2_slod",
        "h4_islandx_maindock_props_lod",
        "h4_islandx_maindock_props_slod",
        "h4_islandx_maindock_slod",
        "h4_islandx_mansion",
        "h4_islandx_mansion_b",
        "h4_islandx_mansion_b_lod",
        "h4_islandx_mansion_b_side_fence",
        "h4_islandx_mansion_b_slod",
        "h4_islandx_mansion_entrance_fence",
        "h4_islandx_mansion_guardfence",
        "h4_islandx_mansion_lights",
        "h4_islandx_mansion_lockup_01",
        "h4_islandx_mansion_lockup_01_lod",
        "h4_islandx_mansion_lockup_02",
        "h4_islandx_mansion_lockup_02_lod",
        "h4_islandx_mansion_lockup_03",
        "h4_islandx_mansion_lockup_03_lod",
        "h4_islandx_mansion_lod",
        "h4_islandx_mansion_office",
        "h4_islandx_mansion_office_lod",
        "h4_islandx_mansion_props",
        "h4_islandx_mansion_props_lod",
        "h4_islandx_mansion_props_slod",
        "h4_islandx_mansion_slod",
        "h4_islandx_mansion_vault",
        "h4_islandx_mansion_vault_lod",
        "h4_island_padlock_props",
        "h4_mansion_gate_closed",
        "h4_mansion_remains_cage",
        "h4_mph4_airstrip",
        "h4_mph4_airstrip_interior_0_airstrip_hanger",
        "h4_mph4_beach",
        "h4_mph4_dock",
        "h4_mph4_island_lod",
        "h4_mph4_island_ne_placement",
        "h4_mph4_island_nw_placement",
        "h4_mph4_island_se_placement",
        "h4_mph4_island_sw_placement",
        "h4_mph4_mansion",
        "h4_mph4_mansion_b",
        "h4_mph4_mansion_b_strm_0",
        "h4_mph4_mansion_strm_0",
        "h4_mph4_wtowers",
        "h4_ne_ipl_00",
        "h4_ne_ipl_00_lod",
        "h4_ne_ipl_00_slod",
        "h4_ne_ipl_01",
        "h4_ne_ipl_01_lod",
        "h4_ne_ipl_01_slod",
        "h4_ne_ipl_02",
        "h4_ne_ipl_02_lod",
        "h4_ne_ipl_02_slod",
        "h4_ne_ipl_03",
        "h4_ne_ipl_03_lod",
        "h4_ne_ipl_03_slod",
        "h4_ne_ipl_04",
        "h4_ne_ipl_04_lod",
        "h4_ne_ipl_04_slod",
        "h4_ne_ipl_05",
        "h4_ne_ipl_05_lod",
        "h4_ne_ipl_05_slod",
        "h4_ne_ipl_06",
        "h4_ne_ipl_06_lod",
        "h4_ne_ipl_06_slod",
        "h4_ne_ipl_07",
        "h4_ne_ipl_07_lod",
        "h4_ne_ipl_07_slod",
        "h4_ne_ipl_08",
        "h4_ne_ipl_08_lod",
        "h4_ne_ipl_08_slod",
        "h4_ne_ipl_09",
        "h4_ne_ipl_09_lod",
        "h4_ne_ipl_09_slod",
        "h4_nw_ipl_00",
        "h4_nw_ipl_00_lod",
        "h4_nw_ipl_00_slod",
        "h4_nw_ipl_01",
        "h4_nw_ipl_01_lod",
        "h4_nw_ipl_01_slod",
        "h4_nw_ipl_02",
        "h4_nw_ipl_02_lod",
        "h4_nw_ipl_02_slod",
        "h4_nw_ipl_03",
        "h4_nw_ipl_03_lod",
        "h4_nw_ipl_03_slod",
        "h4_nw_ipl_04",
        "h4_nw_ipl_04_lod",
        "h4_nw_ipl_04_slod",
        "h4_nw_ipl_05",
        "h4_nw_ipl_05_lod",
        "h4_nw_ipl_05_slod",
        "h4_nw_ipl_06",
        "h4_nw_ipl_06_lod",
        "h4_nw_ipl_06_slod",
        "h4_nw_ipl_07",
        "h4_nw_ipl_07_lod",
        "h4_nw_ipl_07_slod",
        "h4_nw_ipl_08",
        "h4_nw_ipl_08_lod",
        "h4_nw_ipl_08_slod",
        "h4_nw_ipl_09",
        "h4_nw_ipl_09_lod",
        "h4_nw_ipl_09_slod",
        "h4_se_ipl_00",
        "h4_se_ipl_00_lod",
        "h4_se_ipl_00_slod",
        "h4_se_ipl_01",
        "h4_se_ipl_01_lod",
        "h4_se_ipl_01_slod",
        "h4_se_ipl_02",
        "h4_se_ipl_02_lod",
        "h4_se_ipl_02_slod",
        "h4_se_ipl_03",
        "h4_se_ipl_03_lod",
        "h4_se_ipl_03_slod",
        "h4_se_ipl_04",
        "h4_se_ipl_04_lod",
        "h4_se_ipl_04_slod",
        "h4_se_ipl_05",
        "h4_se_ipl_05_lod",
        "h4_se_ipl_05_slod",
        "h4_se_ipl_06",
        "h4_se_ipl_06_lod",
        "h4_se_ipl_06_slod",
        "h4_se_ipl_07",
        "h4_se_ipl_07_lod",
        "h4_se_ipl_07_slod",
        "h4_se_ipl_08",
        "h4_se_ipl_08_lod",
        "h4_se_ipl_08_slod",
        "h4_se_ipl_09",
        "h4_se_ipl_09_lod",
        "h4_se_ipl_09_slod",
        "h4_sw_ipl_00",
        "h4_sw_ipl_00_lod",
        "h4_sw_ipl_00_slod",
        "h4_sw_ipl_01",
        "h4_sw_ipl_01_lod",
        "h4_sw_ipl_01_slod",
        "h4_sw_ipl_02",
        "h4_sw_ipl_02_lod",
        "h4_sw_ipl_02_slod",
        "h4_sw_ipl_03",
        "h4_sw_ipl_03_lod",
        "h4_sw_ipl_03_slod",
        "h4_sw_ipl_04",
        "h4_sw_ipl_04_lod",
        "h4_sw_ipl_04_slod",
        "h4_sw_ipl_05",
        "h4_sw_ipl_05_lod",
        "h4_sw_ipl_05_slod",
        "h4_sw_ipl_06",
        "h4_sw_ipl_06_lod",
        "h4_sw_ipl_06_slod",
        "h4_sw_ipl_07",
        "h4_sw_ipl_07_lod",
        "h4_sw_ipl_07_slod",
        "h4_sw_ipl_08",
        "h4_sw_ipl_08_lod",
        "h4_sw_ipl_08_slod",
        "h4_sw_ipl_09",
        "h4_sw_ipl_09_lod",
        "h4_sw_ipl_09_slod",
        "h4_islandx_placement_01",
        "h4_islandx_placement_02",
        "h4_islandx_placement_03",
        "h4_islandx_placement_04",
        "h4_islandx_placement_05",
        "h4_islandx_placement_06",
        "h4_islandx_placement_07",
        "h4_islandx_placement_08",
        "h4_islandx_placement_09",
        "h4_islandx_placement_10",
        "h4_mph4_island_placement",
        };

        // Prefer to load early to avoid WorldTravel helper spikes:
        // WorldTravel's helper thread checks h4_islandairstrip_slod and then REQUEST_IPL's these sets when it detects "Cayo loaded externally".
        // We stream these ourselves first so WorldTravel does not have to "catch up" in one frame.
        private static readonly HashSet<string> PreferEarly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // From WorldTravel Helpers.cpp (cayoInteriors)
        "h4_islandx_mansion_vault",
        "h4_islandx_mansion_lockup_03",
        "h4_islandx_mansion_lockup_02",
        "h4_islandx_mansion_lockup_01",
        "h4_islandx_mansion_office",
        "h4_mph4_airstrip_interior_0_airstrip_hanger",

        // From WorldTravel Helpers.cpp (cayoInstancePlacement)
        "h4_mph4_terrain_01_grass_0",
        "h4_mph4_terrain_01_grass_1",
        "h4_mph4_terrain_02_grass_0",
        "h4_mph4_terrain_02_grass_1",
        "h4_mph4_terrain_02_grass_2",
        "h4_mph4_terrain_02_grass_3",
        "h4_mph4_terrain_04_grass_0",
        "h4_mph4_terrain_04_grass_1",
        "h4_mph4_terrain_05_grass_0",
        "h4_mph4_terrain_06_grass_0",

        // From WorldTravel Helpers.cpp (cayoOccl)
        "h4_mph4_terrain_occ_00",
        "h4_mph4_terrain_occ_01",
        "h4_mph4_terrain_occ_02",
        "h4_mph4_terrain_occ_03",
        "h4_mph4_terrain_occ_04",
        "h4_mph4_terrain_occ_05",
        "h4_mph4_terrain_occ_06",
        "h4_mph4_terrain_occ_07",
        "h4_mph4_terrain_occ_08",
        "h4_mph4_terrain_occ_09",

        // Core island sentinel should go early in our base phase
        "h4_islandx",

        // Base MP heist island scaffolding
        "h4_mph4_island",
        "h4_mph4_island_long_0",
        "h4_mph4_island_strm_0",
        "h4_mph4_island_lod",
        "h4_mph4_terrain_lod",
    };

        private void EnsurePhaseListsBuilt()
        {
            if (PhaseBaseIpls != null && PhaseDetailIpls != null)
            {
                return;
            }
            PhaseBaseIpls = new List<string>(256);
            PhaseDetailIpls = new List<string>(256);

            foreach (string ipl in CayoIpls)
            {
                if (IsBasePhaseIpl(ipl))
                {
                    PhaseBaseIpls.Add(ipl);
                }
                else
                {
                    PhaseDetailIpls.Add(ipl);
                }
            }

            // Defer the WorldTravel "external Cayo loaded" trigger IPL until the very end of detail phase.
            string trigger = "h4_islandairstrip_slod";
            int idx = PhaseDetailIpls.FindIndex(x => string.Equals(x, trigger, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                PhaseDetailIpls.RemoveAt(idx);
                PhaseDetailIpls.Add(trigger);
            }
        }

        private static bool IsBasePhaseIpl(string ipl)
        {
            if (string.IsNullOrEmpty(ipl)) return false;
            if (PreferEarly.Contains(ipl)) return true;

            // Base phase should be LIGHT and stable:
            // - MP heist island terrain occlusion + grass + terrain LODs
            // - Island terrain meshes + terrain props + their lod/slod
            // - Distant lights
            // Everything else is deferred to detail phase to avoid "all at once" and reduce LS unload pressure.

            // WorldTravel helper "cayoOccl" list is always base
            if (ipl.StartsWith("h4_mph4_terrain_occ_", StringComparison.OrdinalIgnoreCase)) return true;

            // MP heist island terrain/grass/lod
            if (ipl.StartsWith("h4_mph4_terrain_", StringComparison.OrdinalIgnoreCase)) return true;

            // Island terrain + terrain props (and their lod/slod variants)
            if (ipl.StartsWith("h4_islandx_terrain_", StringComparison.OrdinalIgnoreCase)) return true;
            if (ipl.StartsWith("h4_islandx_terrain_props_", StringComparison.OrdinalIgnoreCase)) return true;

            // Lighting LODs are cheap and help with distant silhouette
            if (ipl.IndexOf("distantlights", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (ipl.IndexOf("lodlights", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // Keep the main island + mph4 base island early
            if (string.Equals(ipl, "h4_islandx", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(ipl, "h4_mph4_island", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        public SeamlessCayoPericoController(ISettingsProvideable settings)
        {
            Settings = settings;
        }

        public void Update(bool isMPMapLoaded)
        {
            try
            {
                var worldSettings = Settings?.SettingsManager?.WorldSettings;
                if (worldSettings == null)
                {
                    return;
                }

                EnsurePhaseListsBuilt();
                LastIsMPMapLoaded = isMPMapLoaded;

                // If not enabled: cleanup (optional) and exit.
                if (!worldSettings.EnableSeamlessCayoPerico)
                {
                    SetState(ControllerState.Disabled, "EnableSeamlessCayoPerico=false");
                    Cleanup(worldSettings, unloadIpls: worldSettings.SeamlessCayoUnloadIplsWhenDisabled, aggressiveUnload: worldSettings.SeamlessCayoAggressiveUnloadAllIpls, reason: "disabled");
                    return;
                }

                // Hard safety gate: never run when Liberty City is loaded.
                if (IsLibertyCityLoaded())
                {
                    SetState(ControllerState.BlockedByLibertyCity, "manhat06_slod active");
                    Cleanup(worldSettings, unloadIpls: worldSettings.SeamlessCayoUnloadIplsWhenLibertyCityLoaded, aggressiveUnload: true, reason: "libertycity");
                    return;
                }

                if (isMPMapLoaded && !WarnedMpMap)
                {
                    WarnedMpMap = true;
                    Log("MP map mode detected (IsMPMapLoaded=true). Seamless Cayo streaming remains active; controller restores global nav nodes to MP defaults when leaving the island.", 0);
                }

                Vector3 playerPos = Game.LocalPlayer.Character.Position;
                float distance = playerPos.DistanceTo(IslandCenter);

                float iplLoadDist = worldSettings.SeamlessCayoIplLoadDistance <= 0f ? 6000f : worldSettings.SeamlessCayoIplLoadDistance;
                float iplUnloadDist = worldSettings.SeamlessCayoIplUnloadDistance <= 0f ? 7000f : worldSettings.SeamlessCayoIplUnloadDistance;
                float unloadMinGap = worldSettings.SeamlessCayoIplUnloadMinGapDistance <= 0f ? 500f : worldSettings.SeamlessCayoIplUnloadMinGapDistance;
                if (iplUnloadDist <= iplLoadDist) iplUnloadDist = iplLoadDist + unloadMinGap;

                // Detail streaming starts before "near batch" so the island feels gradual while flying in.
                float detailStartDist = worldSettings.SeamlessCayoIplDetailStartDistance <= 0f ? Math.Min(iplLoadDist, 5000f) : worldSettings.SeamlessCayoIplDetailStartDistance;
                if (detailStartDist > iplLoadDist) detailStartDist = iplLoadDist;

                float nearLoadDist = worldSettings.SeamlessCayoIplNearDistance <= 0f ? 3500f : worldSettings.SeamlessCayoIplNearDistance;
                if (nearLoadDist > detailStartDist) nearLoadDist = detailStartDist;
                if (nearLoadDist > iplLoadDist) nearLoadDist = iplLoadDist;

                float enterDist = worldSettings.SeamlessCayoEnterDistance <= 0f ? 2000f : worldSettings.SeamlessCayoEnterDistance;
                float exitDist = worldSettings.SeamlessCayoExitDistance <= 0f ? 3000f : worldSettings.SeamlessCayoExitDistance;
                if (exitDist <= enterDist) exitDist = enterDist + 1000f;

                float mapFixStartDist = worldSettings.SeamlessCayoMapFixStartDistance <= 0f ? 5000f : worldSettings.SeamlessCayoMapFixStartDistance;
                float mapFixStopDist = worldSettings.SeamlessCayoMapFixStopDistance <= 0f ? (mapFixStartDist + 750f) : worldSettings.SeamlessCayoMapFixStopDistance;
                if (mapFixStopDist < mapFixStartDist) mapFixStopDist = mapFixStartDist;

                float pauseMapDist = worldSettings.SeamlessCayoPauseMapDistance <= 0f ? 3500f : worldSettings.SeamlessCayoPauseMapDistance;


                // Optional: disable PrLog zone while enabled (default is false to avoid Liberty City overlap quirks).
                if (worldSettings.SeamlessCayoDisablePrLogZone)
                {
                    if (!PrLogZoneDisabled)
                    {
                        SetPrLogZoneEnabled(false);
                    }
                }
                else
                {
                    // If toggled off, restore zone immediately.
                    if (PrLogZoneDisabled)
                    {
                        SetPrLogZoneEnabled(true);
                    }
                }

                // Capture preexisting IPLs once so we can avoid removing them in non-aggressive unload mode.
                CapturePreexistingIplsIfNeeded();

                LogStatusThrottled(true, isMPMapLoaded, distance, iplLoadDist, iplUnloadDist, detailStartDist, nearLoadDist, mapFixStartDist, mapFixStopDist, pauseMapDist);

                // --- Unload decision (delayed, cancelable) ---
                bool unloaderAliveNow = false;
                try { unloaderAliveNow = IplUnloaderFiber != null && IplUnloaderFiber.IsAlive; } catch { }

                // If the player re-approaches while an unload is running, abort the unload so we don't flicker.
                if (unloaderAliveNow && distance <= iplLoadDist)
                {
                    Log("Re-approach detected while unload is in progress; aborting unloader and resuming load.", 0);
                    StopIplUnloader();
                    unloaderAliveNow = false;
                }

                bool aggressiveUnload = worldSettings.SeamlessCayoAggressiveUnloadAllIpls;
                uint unloadDelayMs = worldSettings.SeamlessCayoUnloadDelayMs == 0 ? 5000u : worldSettings.SeamlessCayoUnloadDelayMs;

                // Consider unload only if we engaged the island this session OR if user explicitly wants to force-unload an externally loaded island.
                bool shouldConsiderUnload = (HasStartedIplLoading || IplsLoadedByThis.Count > 0 || IslandFeaturesEnabledByThis || NearIsland);
                bool islandVisuallyLoaded = IsCayoVisuallyLoaded();

                if (!shouldConsiderUnload && worldSettings.SeamlessCayoForceUnloadIfIslandActiveWhileFar && islandVisuallyLoaded)
                {
                    // This is an explicit opt-in safety valve.
                    shouldConsiderUnload = true;
                    aggressiveUnload = true;
                }

                if (shouldConsiderUnload && distance >= iplUnloadDist)
                {
                    if (FarBeyondUnloadSince == 0)
                    {
                        FarBeyondUnloadSince = Game.GameTime;
                    }
                    else if (Game.GameTime - FarBeyondUnloadSince >= unloadDelayMs)
                    {
                        SetState(ControllerState.Unloading, $"dist={distance:0}>=unload={iplUnloadDist:0} for {unloadDelayMs}ms");
                        Cleanup(worldSettings, unloadIpls: true, aggressiveUnload: aggressiveUnload, reason: "distance");
                        return;
                    }
                }
                else
                {
                    FarBeyondUnloadSince = 0;
                }

                // --- Load decision (stable first approach) ---
                uint loadHoldMs = worldSettings.SeamlessCayoIplLoadHoldMs == 0 ? 1000u : worldSettings.SeamlessCayoIplLoadHoldMs;
                bool withinLoad = distance <= iplLoadDist;

                if (withinLoad)
                {
                    if (WithinLoadSince == 0)
                    {
                        WithinLoadSince = Game.GameTime;
                    }
                }
                else
                {
                    WithinLoadSince = 0;
                }

                bool allowStartLoadingNow = withinLoad && (loadHoldMs == 0 || (WithinLoadSince != 0 && (Game.GameTime - WithinLoadSince) >= loadHoldMs));
                bool inDetailPhase = distance <= detailStartDist;
                bool inNearBatchPhase = distance <= nearLoadDist;

                // Start / maintain a loader fiber once we're close AND held long enough, but do not fight an active unloader.
                if (!unloaderAliveNow && (allowStartLoadingNow || HasStartedIplLoading))
                {
                    StartIplLoader();
                }
                else
                {
                    StopIplLoader();
                }

                // Island features (scully behavior): only near the island with hysteresis.
                if (!NearIsland && distance <= enterDist)
                {
                    EnableIslandFeatures();
                    NearIsland = true;
                }
                else if (NearIsland && distance >= exitDist)
                {
                    DisableIslandFeatures();
                    NearIsland = false;
                }

                // Minimap/pause-map support:
                // - Radar interior trick is per-frame and should ONLY run when approaching/near the island.
                // - Pause-map (SET_USE_ISLAND_MAP) is persistent and MUST be forced back OFF when leaving,
                //   otherwise the Los Santos pause map can stay stuck on the island.
                bool mapFixRunning = false;
                try { mapFixRunning = MapFixFiber != null && MapFixFiber.IsAlive; } catch { }

                bool wantMapFixNow = worldSettings.SeamlessCayoEnableMapFix && (distance <= mapFixStartDist || NearIsland);
                bool wantMapFixHysteresis = mapFixRunning && distance <= mapFixStopDist;
                bool safeMapFix = (wantMapFixNow || wantMapFixHysteresis) && IsSafeToApplyRadarInteriorTrick();

                if (safeMapFix)
                {
                    StartMapFix();
                }
                else
                {
                    StopMapFix();
                }

                // Pause-map: keep this MUCH tighter than radar fix so LS comes back immediately when you leave.
                bool pauseMenu = false;
                try { pauseMenu = NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>(); } catch { }

                bool wantPauseMap = worldSettings.SeamlessCayoUseIslandMap && !pauseMenu && distance <= pauseMapDist && !IsLibertyCityLoaded();
                if (wantPauseMap)
                {
                    SetUseIslandMap(true);
                }
                else
                {
                    SetUseIslandMap(false);
                }

                // Controller state for diagnostics.
                bool loaderAlive = false;
                try { loaderAlive = IplLoaderFiber != null && IplLoaderFiber.IsAlive; } catch { }

                if (NearIsland)
                {
                    SetState(ControllerState.NearIsland, $"dist={distance:0}");
                }
                else if (HasStartedIplLoading || loaderAlive)
                {
                    SetState(inDetailPhase ? ControllerState.PreloadingDetail : ControllerState.PreloadingBase, $"dist={distance:0}");
                }
                else
                {
                    SetState(ControllerState.Far, $"dist={distance:0}");
                }

                // First-approach diagnostics: if island becomes active, log it once.
                if (!LoggedIslandxActiveThisSession && IsH4IslandxActive())
                {
                    LoggedIslandxActiveThisSession = true;
                    FirstIslandxActiveDistanceThisSession = distance;
                    Log($"h4_islandx became active (first this session) at dist={distance:0}. StartLoadDist={StartLoadDistanceThisSession:0}", 0);
                }
            }
            catch (Exception ex)
            {
                // Never let this feature crash the mod.
                EntryPoint.WriteToConsole("SeamlessCayoPericoController.Update " + ex.Message + " " + ex.StackTrace, 0);
            }
        }

        public void Dispose()
        {
            try { StopMapFix(); } catch { }
            try { StopIplLoader(); } catch { }
            try { StopIplUnloader(); } catch { }

            try
            {
                if (IslandFeaturesEnabledByThis || NearIsland)
                {
                    DisableIslandFeatures();
                    NearIsland = false;
                }
            }
            catch { }

            try { SetUseIslandMap(false); } catch { }

            try
            {
                if (PrLogZoneDisabled)
                {
                    SetPrLogZoneEnabled(true);
                }
            }
            catch { }
        }

        private void Cleanup(WorldSettings worldSettings, bool unloadIpls, bool aggressiveUnload, string reason)
        {
            try { StopIplLoader(); } catch { }
            try { StopMapFix(); } catch { }
            try { StopIplUnloader(); } catch { }

            try { SetUseIslandMap(false); } catch { }

            // Avoid unload/load thrash when flying quickly.
            FarBeyondUnloadSince = 0;
            WithinLoadSince = 0;

            // Turn off island-only features if we enabled them.
            try
            {
                if (IslandFeaturesEnabledByThis || NearIsland)
                {
                    DisableIslandFeatures();
                    NearIsland = false;
                }
            }
            catch { }

            // Restore PrLog zone only if we were managing it.
            try
            {
                if (PrLogZoneDisabled)
                {
                    SetPrLogZoneEnabled(true);
                }
            }
            catch { }

            if (unloadIpls)
            {
                try { RequestUnload(worldSettings, aggressiveUnload, reason); } catch { }
            }

            // Reset loader progress so a future approach can stream again cleanly.
            HasStartedIplLoading = false;
            LoggedStartLoadThisSession = false;
            LoggedIslandxActiveThisSession = false;
            StartLoadDistanceThisSession = -1f;
            FirstIslandxActiveDistanceThisSession = -1f;

            NextBaseIndex = 0;
            NextDetailIndex = 0;

            IplTokenBucket = 0.0;
            GameTimeLastIplTokenUpdate = 0;
            LastAdaptiveDesiredRps = 0f;
            LastAdaptiveClosingSpeed = 0f;
            LastAdaptiveEtaSeconds = 0f;

            WarnedMpMap = false;
        }

        private void SetPrLogZoneEnabled(bool enabled)
        {
            try
            {
                int zone = NativeFunction.Natives.GET_ZONE_FROM_NAME_ID<int>("PrLog");
                NativeFunction.Natives.SET_ZONE_ENABLED(zone, enabled);
                PrLogZoneDisabled = !enabled;
            }
            catch
            {
                // best-effort
            }
        }

        private void CapturePreexistingIplsIfNeeded()
        {
            if (CapturedPreexistingActiveIpls)
            {
                return;
            }
            CapturedPreexistingActiveIpls = true;

            int count = 0;
            try
            {
                foreach (string ipl in CayoIpls)
                {
                    try
                    {
                        if (NativeFunction.Natives.IS_IPL_ACTIVE<bool>(ipl))
                        {
                            PreexistingActiveIpls.Add(ipl);
                            count++;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            bool h4_islandx = false;
            bool h4_mph4_island = false;
            bool h4_islandairstrip_slod = false;
            try { h4_islandx = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_islandx"); } catch { }
            try { h4_mph4_island = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_mph4_island"); } catch { }
            try { h4_islandairstrip_slod = NativeFunction.Natives.IS_IPL_ACTIVE<bool>("h4_islandairstrip_slod"); } catch { }
            Log($"Captured preexisting Cayo IPLs active at enable: {count}. Sentinels: h4_islandx={h4_islandx} h4_mph4_island={h4_mph4_island} h4_islandairstrip_slod={h4_islandairstrip_slod}", 0);

            // If Cayo is already visually loaded while far away, call it out explicitly (common source of confusion).
            try
            {
                float dist = Game.LocalPlayer.Character.Position.DistanceTo(IslandCenter);
                if (dist > 6000f && h4_islandx)
                {
                    Log("WARNING: h4_islandx is active while far from island. This usually means another plugin/mod loaded Cayo. If you want LSR to force-unload it while far, enable SeamlessCayoForceUnloadIfIslandActiveWhileFar.", 0);
                }
            }
            catch { }
        }

        private void StopIplUnloader()
        {
            try
            {
                if (IplUnloaderFiber != null)
                {
                    IplUnloaderFiber.Abort();
                }
            }
            catch { }
            IplUnloaderFiber = null;
        }

        private void RequestUnload(WorldSettings ws, bool aggressiveUnload, string reason)
        {
            try
            {
                if (IplUnloaderFiber != null && IplUnloaderFiber.IsAlive)
                {
                    return;
                }

                // Ensure loader isn't fighting us.
                StopIplLoader();

                // Snapshot targets.
                string[] targets;
                if (aggressiveUnload)
                {
                    targets = CayoIpls.ToArray();
                }
                else
                {
                    targets = IplsLoadedByThis.ToArray();
                }

                if (targets == null || targets.Length == 0)
                {
                    IplsLoadedByThis.Clear();
                    return;
                }

                int batchSize = ws.SeamlessCayoUnloadBatchSize <= 0 ? 10 : ws.SeamlessCayoUnloadBatchSize;
                int batchDelay = ws.SeamlessCayoUnloadBatchDelayMs <= 0 ? 200 : ws.SeamlessCayoUnloadBatchDelayMs;

                Log($"Unload begin (aggressive={aggressiveUnload}) reason={reason} targets={targets.Length} batchSize={batchSize} delayMs={batchDelay}", 0);

                IplUnloaderFiber = GameFiber.StartNew(delegate
                {
                    try
                    {
                        int removedAttempted = 0;
                        for (int i = 0; i < targets.Length; i++)
                        {
                            string ipl = targets[i];
                            try
                            {
                                // In non-aggressive mode, never remove IPLs that were already active before we enabled.
                                if (!aggressiveUnload && PreexistingActiveIpls.Contains(ipl))
                                {
                                    continue;
                                }

                                if (NativeFunction.Natives.IS_IPL_ACTIVE<bool>(ipl))
                                {
                                    NativeFunction.Natives.REMOVE_IPL(ipl);
                                }
                                removedAttempted++;
                            }
                            catch { }

                            if (batchSize > 0 && removedAttempted > 0 && (removedAttempted % batchSize) == 0)
                            {
                                GameFiber.Sleep(batchDelay);
                            }
                        }

                        IplsLoadedByThis.Clear();
                        Log($"Unload end (aggressive={aggressiveUnload}) reason={reason} attemptedRemovals={removedAttempted}", 0);
                    }
                    catch
                    {
                        // swallow
                    }
                    finally
                    {
                        try { IplUnloaderFiber = null; } catch { IplUnloaderFiber = null; }
                    }
                }, "LSR-SeamlessCayo-IPLUnloader");
            }
            catch { }
        }

        internal static bool IsLibertyCityLoaded()
        {
            try
            {
                // WorldTravel/LCPP ASI uses this IPL as its Liberty City presence check.
                return NativeFunction.Natives.IS_IPL_ACTIVE<bool>("manhat06_slod");
            }
            catch
            {
                return false;
            }
        }

        private void StartIplLoader()
        {
            try
            {
                if (IplLoaderFiber != null && IplLoaderFiber.IsAlive)
                {
                    return;
                }
                try { if (IplUnloaderFiber != null && IplUnloaderFiber.IsAlive) return; } catch { }

                HasStartedIplLoading = true;

                if (!LoggedStartLoadThisSession)
                {
                    LoggedStartLoadThisSession = true;
                    try
                    {
                        StartLoadDistanceThisSession = Game.LocalPlayer.Character.Position.DistanceTo(IslandCenter);
                    }
                    catch { StartLoadDistanceThisSession = -1f; }
                    Log($"StartLoad session: dist={StartLoadDistanceThisSession:0} (p1={NextBaseIndex}/{PhaseBaseIpls.Count} p2={NextDetailIndex}/{PhaseDetailIpls.Count})", 0);
                }

                IplLoaderFiber = GameFiber.StartNew(delegate
                {
                    try
                    {
                        // Reset adaptive scheduler state at the start of a load session.
                        IplTokenBucket = 0.0;
                        GameTimeLastIplTokenUpdate = 0;
                        LastAdaptiveDesiredRps = 0f;
                        LastAdaptiveClosingSpeed = 0f;
                        LastAdaptiveEtaSeconds = 0f;

                        while (EntryPoint.ModController?.IsRunning == true)
                        {
                            var ws = Settings?.SettingsManager?.WorldSettings;
                            if (ws == null || !ws.EnableSeamlessCayoPerico)
                            {
                                break;
                            }
                            if (IsLibertyCityLoaded())
                            {
                                break;
                            }

                            Vector3 playerPos = Game.LocalPlayer.Character.Position;
                            float distance = playerPos.DistanceTo(IslandCenter);

                            float loadDist = ws.SeamlessCayoIplLoadDistance <= 0f ? 6000f : ws.SeamlessCayoIplLoadDistance;
                            float unloadDist = ws.SeamlessCayoIplUnloadDistance <= 0f ? 7000f : ws.SeamlessCayoIplUnloadDistance;
                            float unloadMinGap = ws.SeamlessCayoIplUnloadMinGapDistance <= 0f ? 500f : ws.SeamlessCayoIplUnloadMinGapDistance;
                            if (unloadDist <= loadDist) unloadDist = loadDist + unloadMinGap;

                            float nearDist = ws.SeamlessCayoIplNearDistance <= 0f ? 3500f : ws.SeamlessCayoIplNearDistance;
                            if (nearDist > loadDist) nearDist = loadDist;

                            float detailStartDist = ws.SeamlessCayoIplDetailStartDistance <= 0f ? Math.Min(loadDist, 5000f) : ws.SeamlessCayoIplDetailStartDistance;
                            if (detailStartDist > loadDist) detailStartDist = loadDist;
                            if (detailStartDist < nearDist) detailStartDist = nearDist;

                            // Stop loader if we are far away again.
                            if (distance >= unloadDist || distance > loadDist)
                            {
                                break;
                            }

                            bool allowDetail = distance <= detailStartDist;

                            if (ws.SeamlessCayoUseAdaptiveIplScheduler)
                            {
                                RequestIplsAdaptive(ws, playerPos, distance, allowDetail, nearDist, detailStartDist);
                                int tick = ws.SeamlessCayoIplAdaptiveTickMs <= 0 ? 50 : ws.SeamlessCayoIplAdaptiveTickMs;
                                if (tick < 10) tick = 10;
                                if (tick > 1000) tick = 1000;
                                GameFiber.Sleep(tick);
                            }
                            else
                            {
                                RequestIplsBatched(ws, distance, allowDetail, nearDist, detailStartDist);
                                int delay = ws.SeamlessCayoIplBatchDelayMs <= 0 ? 200 : ws.SeamlessCayoIplBatchDelayMs;
                                GameFiber.Sleep(delay);
                            }
                        }
                    }
                    catch
                    {
                        // Best-effort: never crash LSR.
                    }
                }, "LSR-SeamlessCayo-IPLLoader");
            }
            catch
            {
                // ignore
            }
        }

        private void StopIplLoader()
        {
            try
            {
                if (IplLoaderFiber != null)
                {
                    IplLoaderFiber.Abort();
                }
            }
            catch { }
            IplLoaderFiber = null;
        }

        private void RequestIplsAdaptive(WorldSettings ws, Vector3 playerPos, float distance, bool allowDetailPhase, float nearDist, float detailStartDist)
        {
            try
            {
                EnsurePhaseListsBuilt();

                int baseCount = PhaseBaseIpls.Count;
                int detailCount = PhaseDetailIpls.Count;

                int baseRemaining = Math.Max(0, baseCount - NextBaseIndex);
                int detailRemaining = allowDetailPhase ? Math.Max(0, detailCount - NextDetailIndex) : 0;
                int remaining = baseRemaining + detailRemaining;
                if (remaining <= 0)
                {
                    return;
                }

                // --- Stage selection (affects min/max request rates) ---
                bool isNear = distance <= nearDist;
                bool isMid = !isNear && allowDetailPhase;

                // Max request rates (requests/sec). If unset (<=0), derive from legacy batch settings.
                int delayMs = ws.SeamlessCayoIplBatchDelayMs <= 0 ? 200 : ws.SeamlessCayoIplBatchDelayMs;
                if (delayMs < 10) delayMs = 10;

                float derivedFarMax = ((ws.SeamlessCayoIplBatchSizeFar <= 0 ? 2 : ws.SeamlessCayoIplBatchSizeFar) * 1000f) / delayMs;
                float derivedMidMax = ((ws.SeamlessCayoIplBatchSizeMid <= 0 ? 4 : ws.SeamlessCayoIplBatchSizeMid) * 1000f) / delayMs;
                float derivedNearMax = ((ws.SeamlessCayoIplBatchSizeNear <= 0 ? 6 : ws.SeamlessCayoIplBatchSizeNear) * 1000f) / delayMs;

                float maxRps;
                if (isNear)
                {
                    maxRps = ws.SeamlessCayoIplMaxRequestsPerSecondNear > 0 ? ws.SeamlessCayoIplMaxRequestsPerSecondNear : derivedNearMax;
                }
                else if (isMid)
                {
                    maxRps = ws.SeamlessCayoIplMaxRequestsPerSecondMid > 0 ? ws.SeamlessCayoIplMaxRequestsPerSecondMid : derivedMidMax;
                }
                else
                {
                    maxRps = ws.SeamlessCayoIplMaxRequestsPerSecondFar > 0 ? ws.SeamlessCayoIplMaxRequestsPerSecondFar : derivedFarMax;
                }

                float minRps;
                if (isNear)
                {
                    minRps = ws.SeamlessCayoIplMinRequestsPerSecondNear > 0 ? ws.SeamlessCayoIplMinRequestsPerSecondNear : 4f;
                }
                else if (isMid)
                {
                    minRps = ws.SeamlessCayoIplMinRequestsPerSecondMid > 0 ? ws.SeamlessCayoIplMinRequestsPerSecondMid : 2f;
                }
                else
                {
                    minRps = ws.SeamlessCayoIplMinRequestsPerSecondFar > 0 ? ws.SeamlessCayoIplMinRequestsPerSecondFar : 1f;
                }

                if (maxRps < minRps) maxRps = minRps;

                // --- ETA-based ramp ---
                // Compute "closing speed" toward the island so we can estimate time-to-threshold.
                float closingSpeed = 0f;
                try
                {
                    Vector3 vel = Game.LocalPlayer.Character.Velocity;
                    Vector3 toIsland = IslandCenter - playerPos;
                    float d = toIsland.Length();
                    if (d > 1f)
                    {
                        Vector3 dir = toIsland / d;
                        closingSpeed = (vel.X * dir.X) + (vel.Y * dir.Y) + (vel.Z * dir.Z);
                    }
                }
                catch { }

                if (closingSpeed < 0f) closingSpeed = 0f;

                float targetDist = allowDetailPhase ? nearDist : detailStartDist;
                float remainingDist = distance - targetDist;
                if (remainingDist < 0f) remainingDist = 0f;

                float etaSeconds = float.PositiveInfinity;
                if (closingSpeed > 2f)
                {
                    etaSeconds = remainingDist / closingSpeed;
                }

                float desiredRps = minRps;
                if (!float.IsInfinity(etaSeconds))
                {
                    // If ETA is very large (loitering), stick to minRps.
                    if (etaSeconds <= 120f)
                    {
                        float requiredRps = remaining / Math.Max(etaSeconds, 1f);
                        desiredRps = Math.Max(minRps, Math.Min(maxRps, requiredRps));
                    }
                }

                LastAdaptiveDesiredRps = desiredRps;
                LastAdaptiveClosingSpeed = closingSpeed;
                LastAdaptiveEtaSeconds = float.IsInfinity(etaSeconds) ? -1f : etaSeconds;

                // --- Token bucket ---
                uint now = Game.GameTime;
                if (GameTimeLastIplTokenUpdate == 0)
                {
                    GameTimeLastIplTokenUpdate = now;
                    return;
                }

                float dt = (now - GameTimeLastIplTokenUpdate) / 1000f;
                if (dt < 0f) dt = 0f;
                GameTimeLastIplTokenUpdate = now;

                IplTokenBucket += desiredRps * dt;

                int maxBurst = ws.SeamlessCayoIplMaxBurstRequests <= 0 ? 12 : ws.SeamlessCayoIplMaxBurstRequests;
                if (maxBurst < 1) maxBurst = 1;
                if (maxBurst > 100) maxBurst = 100;

                if (IplTokenBucket > maxBurst)
                {
                    IplTokenBucket = maxBurst;
                }

                int budget = (int)Math.Floor(IplTokenBucket);
                if (budget <= 0)
                {
                    return;
                }

                int perTick = ws.SeamlessCayoIplMaxRequestsPerTick <= 0 ? 6 : ws.SeamlessCayoIplMaxRequestsPerTick;
                if (perTick < 1) perTick = 1;
                if (perTick > 50) perTick = 50;

                int toAttempt = Math.Min(budget, perTick);

                int attempted = 0;
                int requestedThisTick = 0;

                for (int i = 0; i < toAttempt; i++)
                {
                    string ipl = null;

                    bool preferDetail = distance <= nearDist;

                    bool pickDetailNow = allowDetailPhase && NextDetailIndex < detailCount &&
                                         (NextBaseIndex >= baseCount || (preferDetail ? (i % 2 == 1) : (i % 4 == 3)));

                    if (!pickDetailNow && NextBaseIndex < baseCount)
                    {
                        ipl = PhaseBaseIpls[NextBaseIndex];
                        NextBaseIndex++;
                    }
                    else if (allowDetailPhase && NextDetailIndex < detailCount)
                    {
                        ipl = PhaseDetailIpls[NextDetailIndex];
                        NextDetailIndex++;
                    }
                    else
                    {
                        break;
                    }

                    attempted++;

                    if (string.IsNullOrEmpty(ipl))
                    {
                        continue;
                    }

                    try
                    {
                        if (!NativeFunction.Natives.IS_IPL_ACTIVE<bool>(ipl))
                        {
                            NativeFunction.Natives.REQUEST_IPL(ipl);
                            IplsLoadedByThis.Add(ipl);
                            requestedThisTick++;
                        }
                    }
                    catch { }
                }

                IplTokenBucket -= attempted;
                if (IplTokenBucket < 0.0) IplTokenBucket = 0.0;

                if (requestedThisTick >= 20)
                {
                    Log($"Loader burst: requested={requestedThisTick} (adaptive) remaining={remaining} p1={NextBaseIndex}/{baseCount} p2={NextDetailIndex}/{detailCount}", 0);
                }
            }
            catch
            {
                // Best-effort: never crash LSR.
            }
        }

        private void RequestIplsBatched(WorldSettings ws, float distance, bool allowDetailPhase, float nearDist, float detailStartDist)
        {
            EnsurePhaseListsBuilt();

            int baseCount = PhaseBaseIpls.Count;
            int detailCount = PhaseDetailIpls.Count;

            if (NextBaseIndex >= baseCount && (!allowDetailPhase || NextDetailIndex >= detailCount))
            {
                // Done for the currently-allowed phases.
                return;
            }

            HasStartedIplLoading = true;

            int batchSize;

            // Batch ramp:
            // - Far/Base only (distance > detailStartDist): use far batch size
            // - Mid detail (distance <= detailStartDist but > nearDist): use mid batch size
            // - Near (distance <= nearDist): use near batch size
            if (distance <= nearDist)
            {
                batchSize = ws.SeamlessCayoIplBatchSizeNear;
                if (batchSize <= 0) batchSize = 6;
            }
            else if (allowDetailPhase)
            {
                batchSize = ws.SeamlessCayoIplBatchSizeMid;
                if (batchSize <= 0) batchSize = 4;
            }
            else
            {
                batchSize = ws.SeamlessCayoIplBatchSizeFar;
                if (batchSize <= 0) batchSize = 2;
            }

            if (batchSize > 50) batchSize = 50;

            int requestedThisTick = 0;

            for (int i = 0; i < batchSize; i++)
            {
                string ipl = null;

                bool preferDetail = distance <= nearDist;

                // Interleave base + detail once detail is allowed so streaming feels gradual:
                // - While base is still loading, we still occasionally request detail IPLs.
                // - Near the island, prefer detail more often.
                bool pickDetailNow = allowDetailPhase && NextDetailIndex < detailCount &&
                                     (NextBaseIndex >= baseCount || (preferDetail ? (i % 2 == 1) : (i % 4 == 3)));

                if (!pickDetailNow && NextBaseIndex < baseCount)
                {
                    ipl = PhaseBaseIpls[NextBaseIndex];
                    NextBaseIndex++;
                }
                else if (allowDetailPhase && NextDetailIndex < detailCount)
                {
                    ipl = PhaseDetailIpls[NextDetailIndex];
                    NextDetailIndex++;
                }
                else
                {
                    break;
                }

                if (string.IsNullOrEmpty(ipl))
                {
                    continue;
                }

                try
                {
                    if (!NativeFunction.Natives.IS_IPL_ACTIVE<bool>(ipl))
                    {
                        NativeFunction.Natives.REQUEST_IPL(ipl);
                        IplsLoadedByThis.Add(ipl);
                        requestedThisTick++;
                    }
                }
                catch { }
            }

            // Helpful one-time diagnostic: if we requested a lot very quickly, call it out.
            if (requestedThisTick >= 20)
            {
                Log($"Loader burst: requested={requestedThisTick} (allowDetail={allowDetailPhase}) p1={NextBaseIndex}/{baseCount} p2={NextDetailIndex}/{detailCount}", 0);
            }
        }

        private void SetUseIslandMap(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    if (!UseIslandMapByThis)
                    {
                        SafeCall("SET_USE_ISLAND_MAP", true);
                        UseIslandMapByThis = true;
                        Log("SET_USE_ISLAND_MAP(true)", 0);
                    }
                }
                else
                {
                    if (UseIslandMapByThis)
                    {
                        SafeCall("SET_USE_ISLAND_MAP", false);
                        UseIslandMapByThis = false;
                        Log("SET_USE_ISLAND_MAP(false)", 0);
                    }
                }
            }
            catch { }
        }

        private void EnableIslandFeatures()
        {
            try
            {
                // In LSRP, MP map is typically enabled. Avoid breaking MP nav by restoring to MP defaults when leaving.
                SafeCall("SET_AI_GLOBAL_PATH_NODES_TYPE", 1);
                SafeCall("_LOAD_GLOBAL_WATER_TYPE", 1); // 1 = HeistIsland (introduced around v2189)

                SafeCall("SET_ISLAND_ENABLED", "HeistIsland", true);
                SafeCall("SET_ALLOW_STREAM_HEIST_ISLAND_NODES", true);

                SafeCall("SET_SCENARIO_GROUP_ENABLED", "Heist_Island_Peds", true);

                SetAmbientZonePersistent("AZL_DLC_Hei4_Island_Zones", true, true);
                SetAmbientZonePersistent("AZL_DLC_Hei4_Island_Disabled_Zones", false, true);

                SafeCall("SET_DEEP_OCEAN_SCALER", 0.0f);
                SafeCall("SET_AUDIO_FLAG", "PlayerOnDLCHeist4Island", true);

                IslandFeaturesEnabledByThis = true;
            }
            catch { }
        }

        private void DisableIslandFeatures()
        {
            try
            {
                // Restore global nav nodes based on MP-map state to avoid interfering with LSRP MP world behavior.
                SafeCall("SET_AI_GLOBAL_PATH_NODES_TYPE", LastIsMPMapLoaded ? 1 : 0);
                SafeCall("_LOAD_GLOBAL_WATER_TYPE", 0);

                SafeCall("SET_ALLOW_STREAM_HEIST_ISLAND_NODES", false);
                SafeCall("SET_ISLAND_ENABLED", "HeistIsland", false);

                SafeCall("SET_SCENARIO_GROUP_ENABLED", "Heist_Island_Peds", false);

                SetAmbientZonePersistent("AZL_DLC_Hei4_Island_Zones", false, false);
                SetAmbientZonePersistent("AZL_DLC_Hei4_Island_Disabled_Zones", true, false);

                SafeCall("SET_AUDIO_FLAG", "PlayerOnDLCHeist4Island", false);
                SafeCall("RESET_DEEP_OCEAN_SCALER");
            }
            catch { }
            IslandFeaturesEnabledByThis = false;
        }

        private static void SetAmbientZonePersistent(string zoneListName, bool state, bool p2)
        {
            SafeCall("SET_AMBIENT_ZONE_LIST_STATE_PERSISTENT", zoneListName, state, p2);
            SafeCall("SET_AMBIENT_ZONE_STATE_PERSISTENT", zoneListName, state, p2);
            SafeCall("SET_AMBIENT_ZONE_LIST_STATE", zoneListName, state, p2);
        }

        private void StartMapFix()
        {
            if (MapFixFiber != null && MapFixFiber.IsAlive)
                return;

            EnsureDummyBlip();

            MapFixFiber = GameFiber.StartNew(delegate
            {
                try
                {
                    while (EntryPoint.ModController?.IsRunning == true &&
                           Settings?.SettingsManager?.WorldSettings != null &&
                           Settings.SettingsManager.WorldSettings.EnableSeamlessCayoPerico &&
                           Settings.SettingsManager.WorldSettings.SeamlessCayoEnableMapFix &&
                           !IsLibertyCityLoaded())
                    {
                        // Avoid breaking interior minimaps: only apply when player is not inside an interior.
                        int interior = 0;
                        try { interior = NativeFunction.Natives.GET_INTERIOR_FROM_ENTITY<int>(Game.LocalPlayer.Character); } catch { }

                        bool pauseMenu = false;
                        try { pauseMenu = NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>(); } catch { }

                        if (!pauseMenu && interior == 0)
                        {
                            SafeCall("SET_RADAR_AS_EXTERIOR_THIS_FRAME");
                            SafeCall("SET_RADAR_AS_INTERIOR_THIS_FRAME", FakeIslandInteriorHash, RadarInteriorX, RadarInteriorY, 0.0f, 0.0f);
                        }
                        GameFiber.Yield();
                    }
                }
                catch
                {
                    // Never crash LSR because of this fiber.
                }
            }, "LSR-SeamlessCayo-MapFix");
        }

        private void StopMapFix()
        {
            try
            {
                if (MapFixFiber != null)
                {
                    MapFixFiber.Abort();
                    MapFixFiber = null;
                }
            }
            catch { MapFixFiber = null; }

            try
            {
                if (DummyMapBlip != null && DummyMapBlip.Exists())
                {
                    DummyMapBlip.Delete();
                }
            }
            catch { }

            DummyMapBlip = null;
        }

        private void EnsureDummyBlip()
        {
            if (DummyMapBlip != null && DummyMapBlip.Exists())
                return;

            try
            {
                DummyMapBlip = new Blip(DummyBlipCoords);
                SafeCall("SET_BLIP_DISPLAY", DummyMapBlip.Handle, 4);
                SafeCall("SET_BLIP_ALPHA", DummyMapBlip.Handle, 0);
            }
            catch
            {
                DummyMapBlip = null;
            }
        }

        private static void SafeCall(string nativeName, params object[] args)
        {
            try
            {
                switch (nativeName)
                {
                    case "SET_AI_GLOBAL_PATH_NODES_TYPE":
                        NativeFunction.Natives.SET_AI_GLOBAL_PATH_NODES_TYPE((int)args[0]);
                        return;
                    case "_LOAD_GLOBAL_WATER_TYPE":
                        NativeFunction.Natives._LOAD_GLOBAL_WATER_TYPE((int)args[0]);
                        return;
                    case "SET_SCENARIO_GROUP_ENABLED":
                        NativeFunction.Natives.SET_SCENARIO_GROUP_ENABLED((string)args[0], (bool)args[1]);
                        return;
                    case "SET_ISLAND_ENABLED":
                        NativeFunction.Natives.SET_ISLAND_ENABLED((string)args[0], (bool)args[1]);
                        return;
                    case "SET_USE_ISLAND_MAP":
                        NativeFunction.Natives.SET_USE_ISLAND_MAP((bool)args[0]);
                        return;
                    case "SET_ALLOW_STREAM_HEIST_ISLAND_NODES":
                        NativeFunction.Natives.SET_ALLOW_STREAM_HEIST_ISLAND_NODES((bool)args[0]);
                        return;
                    case "SET_AUDIO_FLAG":
                        NativeFunction.Natives.SET_AUDIO_FLAG((string)args[0], (bool)args[1]);
                        return;
                    case "SET_DEEP_OCEAN_SCALER":
                        NativeFunction.Natives.SET_DEEP_OCEAN_SCALER((float)args[0]);
                        return;
                    case "RESET_DEEP_OCEAN_SCALER":
                        NativeFunction.Natives.RESET_DEEP_OCEAN_SCALER();
                        return;
                    case "SET_AMBIENT_ZONE_LIST_STATE_PERSISTENT":
                        NativeFunction.Natives.SET_AMBIENT_ZONE_LIST_STATE_PERSISTENT((string)args[0], (bool)args[1], (bool)args[2]);
                        return;
                    case "SET_AMBIENT_ZONE_STATE_PERSISTENT":
                        NativeFunction.Natives.SET_AMBIENT_ZONE_STATE_PERSISTENT((string)args[0], (bool)args[1], (bool)args[2]);
                        return;
                    case "SET_AMBIENT_ZONE_LIST_STATE":
                        NativeFunction.Natives.SET_AMBIENT_ZONE_LIST_STATE((string)args[0], (bool)args[1], (bool)args[2]);
                        return;
                    case "SET_RADAR_AS_EXTERIOR_THIS_FRAME":
                        NativeFunction.Natives.SET_RADAR_AS_EXTERIOR_THIS_FRAME();
                        return;
                    case "SET_RADAR_AS_INTERIOR_THIS_FRAME":
                        NativeFunction.Natives.SET_RADAR_AS_INTERIOR_THIS_FRAME((int)args[0], (float)args[1], (float)args[2], (float)args[3], (float)args[4]);
                        return;
                    case "SET_BLIP_DISPLAY":
                        NativeFunction.Natives.SET_BLIP_DISPLAY((int)args[0], (int)args[1]);
                        return;
                    case "SET_BLIP_ALPHA":
                        NativeFunction.Natives.SET_BLIP_ALPHA((int)args[0], (int)args[1]);
                        return;
                    default:
                        return;
                }
            }
            catch
            {
                // swallow
            }
        }
    }

}