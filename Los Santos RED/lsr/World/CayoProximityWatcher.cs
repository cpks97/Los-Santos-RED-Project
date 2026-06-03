using LosSantosRED.lsr.Interface;
using Rage;
using System;
using System.Linq;

/// <summary>
/// Watches the player's distance to Cayo Perico and automatically triggers
/// the same OnDepart/OnArrive map swap that the airport system uses.
///
/// Trigger distance: 3000m from island centre (4964, -4649).
/// Inbound:  fires LS -> Cayo swap (same as CayoPericoAirport.OnArrive)
/// Outbound: fires Cayo -> LS swap (same as CayoPericoAirport.OnDepart)
///
/// Hook up:
///   1. Construct in World.Setup() or wherever World is initialised.
///   2. Call Setup() once.
///   3. Call Update() from World.Update() every tick.
///   4. Call Dispose() from World.Dispose().
/// </summary>
public class CayoProximityWatcher
{
    private static readonly Vector3 CayoCentre = new Vector3(4964.835f, -4649.445f, 0f);

    private const float TriggerDistance   = 3000f;
    private const float HysteresisPadding = 300f;  // must travel this far past threshold to reverse
    private const uint  CheckInterval     = 3000;  // ms between distance polls

    private ILocationInteractable Player;
    private IPlacesOfInterest     PlacesOfInterest;
    private IEntityProvideable    World;
    private IModItems             ModItems;
    private ISettingsProvideable  Settings;
    private IWeapons              Weapons;
    private ITimeControllable     Time;

    private bool IsWatching      = false;
    private bool SwapInProgress  = false;
    private uint GameTimeLastCheck = 0;

    public CayoProximityWatcher(
        ILocationInteractable player,
        IPlacesOfInterest     placesOfInterest,
        IEntityProvideable    world,
        IModItems             modItems,
        ISettingsProvideable  settings,
        IWeapons              weapons,
        ITimeControllable     time)
    {
        Player           = player;
        PlacesOfInterest = placesOfInterest;
        World            = world;
        ModItems         = modItems;
        Settings         = settings;
        Weapons          = weapons;
        Time             = time;
    }

    public void Setup()
    {
        IsWatching = true;
        EntryPoint.WriteToConsole("CayoProximityWatcher: started, trigger distance = " + TriggerDistance + "m");
    }

    public void Dispose()
    {
        IsWatching = false;
    }

    /// <summary>
    /// Call every tick from World.Update().
    /// </summary>
    public void Update()
    {
        if (!IsWatching || SwapInProgress) return;
        if (Game.GameTime - GameTimeLastCheck < CheckInterval) return;
        GameTimeLastCheck = Game.GameTime;

        if (!Game.LocalPlayer.Character.Exists()) return;

        float dist      = Game.LocalPlayer.Character.Position.DistanceTo2D(CayoCentre);
        bool  mpLoaded  = World.IsMPMapLoaded;

        if (!mpLoaded && dist < TriggerDistance)
        {
            EntryPoint.WriteToConsole($"CayoProximityWatcher: {dist:F0}m from Cayo — loading island");
            TriggerCayoLoad();
        }
        else if (mpLoaded && dist > TriggerDistance + HysteresisPadding)
        {
            EntryPoint.WriteToConsole($"CayoProximityWatcher: {dist:F0}m from Cayo — unloading island");
            TriggerCayoUnload();
        }
    }

    // ── Inbound: load Cayo ────────────────────────────────────────────────────

    private void TriggerCayoLoad()
    {
        SwapInProgress = true;
        GameFiber.StartNew(delegate
        {
            try
            {
                CayoPericoAirport cayoAirport = GetCayoAirport();
                Airport           lsAirport   = GetNearestLSAirport();

                if (cayoAirport == null)
                {
                    EntryPoint.WriteToConsole("CayoProximityWatcher: CayoPericoAirport not found in locations — cannot load");
                    SwapInProgress = false;
                    return;
                }

                Game.FadeScreenOut(1500, true);

                // Depart LS — cleans up any LS-specific IPLs/zones on the source airport
                lsAirport?.OnDepart(Player);

                // SetDestination primes the airport with all the dependencies it needs
                // then OnArrive runs EnableCayo() — same as using the airport menu
                cayoAirport.OnSetDestination(Player, ModItems, World, Settings, Weapons, Time, PlacesOfInterest);
                cayoAirport.OnArrive(Player, false); // false = don't move the player

                // LoadMPMap sets IsMPMapLoaded = true and any additional MP state
                // Guard against double-load since OnArrive may have already triggered some of this
                if (!World.IsMPMapLoaded)
                {
                    World.LoadMPMap();
                }

                GameFiber.Sleep(1000);
                Game.FadeScreenIn(1500, true);
                EntryPoint.WriteToConsole("CayoProximityWatcher: Cayo load complete");
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole($"CayoProximityWatcher TriggerCayoLoad error: {ex.Message} {ex.StackTrace}", 0);
                try { Game.FadeScreenIn(500, true); } catch { }
            }
            finally
            {
                SwapInProgress = false;
            }
        }, "CayoProximityLoad");
    }

    // ── Outbound: restore LS ─────────────────────────────────────────────────

    private void TriggerCayoUnload()
    {
        SwapInProgress = true;
        GameFiber.StartNew(delegate
        {
            try
            {
                CayoPericoAirport cayoAirport = GetCayoAirport();
                Airport           lsAirport   = GetNearestLSAirport();

                Game.FadeScreenOut(1500, true);

                // Depart Cayo — runs DisableCayo(), clears IPLs, resets zones
                cayoAirport?.OnDepart(Player);

                // Arrive at nearest LS airport — setPos = false, player stays in flight
                if (lsAirport != null)
                {
                    lsAirport.OnSetDestination(Player, ModItems, World, Settings, Weapons, Time, PlacesOfInterest);
                    lsAirport.OnArrive(Player, false);
                }

                // LoadSPMap restores SP state and sets IsMPMapLoaded = false
                if (World.IsMPMapLoaded)
                {
                    World.LoadSPMap();
                }

                GameFiber.Sleep(1000);
                Game.FadeScreenIn(1500, true);
                EntryPoint.WriteToConsole("CayoProximityWatcher: LS restore complete");
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole($"CayoProximityWatcher TriggerCayoUnload error: {ex.Message} {ex.StackTrace}", 0);
                try { Game.FadeScreenIn(500, true); } catch { }
            }
            finally
            {
                SwapInProgress = false;
            }
        }, "CayoProximityUnload");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CayoPericoAirport GetCayoAirport()
    {
        return PlacesOfInterest.PossibleLocations.Airports
            .OfType<CayoPericoAirport>()
            .FirstOrDefault();
    }

    private Airport GetNearestLSAirport()
    {
        return PlacesOfInterest.PossibleLocations.Airports
            .Where(x => !(x is CayoPericoAirport) && x.IsEnabled)
            .OrderBy(x => x.EntrancePosition.DistanceTo2D(Game.LocalPlayer.Character.Position))
            .FirstOrDefault();
    }
}
