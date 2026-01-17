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

        public World(IAgencies agencies, IZones zones, IJurisdictions jurisdictions, ISettingsProvideable settings, IPlacesOfInterest placesOfInterest, IPlateTypes plateTypes, INameProvideable names, IPedGroups relationshipGroups,
            IWeapons weapons, ICrimes crimes, ITimeControllable time, IShopMenus shopMenus, IInteriors interiors, IAudioPlayable audio, IGangs gangs, IGangTerritories gangTerritories, IStreets streets, IModItems modItems, IPedGroups pedGroups, ILocationTypes locationTypes,
            IOrganizations associations, IContacts contacts, ModDataFileManager modDataFileManager)
        {
            PlacesOfInterest = placesOfInterest;
            Zones = zones;
            Jurisdictions = jurisdictions;
            Settings = settings;
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
            EntryPoint.WriteToConsole($"FEJ Installed: {IsFEJInstalled}",0);

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
            if(TotalWantedLevel != totalWantedLevel)
            {
                OnTotalWantedLevelChanged();
            }
            if (Settings.SettingsManager.WorldSettings.AllowSettingDistantSirens)
            {
                NativeFunction.Natives.DISTANT_COP_CAR_SIRENS(false);
            }
            int numFires = NativeFunction.Natives.GET_NUMBER_OF_FIRES_IN_RANGE<int>(Game.LocalPlayer.Character.Position, 150f);
            AnyFiresNearPlayer = numFires > 0;
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
                Game.FadeScreenOut(1500, true);
                NativeFunction.Natives.SET_INSTANCE_PRIORITY_MODE(1);
                NativeFunction.Natives.x0888C3502DBBEEF5();// ON_ENTER_MP();
                LoadCayoIPLs();
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
                UnloadCayoIPLs();
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
            if(isTrafficDisabled)
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
            if(TotalWantedLevel == 0)
            {
                OnTotalWantedLevelRemoved();
            }
            else if(totalWantedLevel == 0)
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
}