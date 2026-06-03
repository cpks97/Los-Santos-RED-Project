using ExtensionsMethods;
using LosSantosRED.lsr.Interface;
using Rage;
using RAGENativeUI;
using RAGENativeUI.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class JobFixerInteraction : IContactMenuInteraction
{
    private IContactInteractable Player;
    private MenuPool             MenuPool;
    private UIMenu               FixerMenu;
    private IGangs               Gangs;
    private IPlacesOfInterest    PlacesOfInterest;
    private ISettingsProvideable Settings;
    private UIMenuItem           TaskCancel;
    private UIMenu               JobsSubMenu;
    private FixerContact         Contact;
    private IAgencies            Agencies;

    public JobFixerInteraction(IContactInteractable player, IGangs gangs, IPlacesOfInterest placesOfInterest,
        ISettingsProvideable settings, FixerContact contact, IAgencies agencies)
    {
        Player          = player;
        Gangs           = gangs;
        PlacesOfInterest = placesOfInterest;
        Settings        = settings;
        Contact         = contact;
        Agencies        = agencies;
        MenuPool        = new MenuPool();
    }

    public void Start(PhoneContact contact)
    {
        FixerMenu = new UIMenu("", "Select an Option");
        FixerMenu.RemoveBanner();
        MenuPool.Add(FixerMenu);
        AddJobs();
        FixerMenu.Visible = true;
        GameFiber.StartNew(delegate
        {
            try
            {
                while (MenuPool.IsAnyMenuOpen())
                    GameFiber.Yield();
                Player.CellPhone.Close(250);
            }
            catch (Exception ex)
            {
                EntryPoint.WriteToConsole(ex.Message + " " + ex.StackTrace, 0);
                EntryPoint.ModController.CrashUnload();
            }
        }, "CellPhone");
    }

    private void AddJobs()
    {
        JobsSubMenu = MenuPool.AddSubMenu(FixerMenu, "Jobs");
        JobsSubMenu.RemoveBanner();

        if (Player.PlayerTasks.HasTask(Contact.Name))
        {
            TaskCancel = new UIMenuItem("Cancel Task", "Tell the fixer you cannot complete the task.")
                { RightLabel = "~o~$?~s~" };
            TaskCancel.Activated += (sender, e) =>
            {
                Player.PlayerTasks.CancelTask(Contact);
                sender.Visible = false;
            };
            JobsSubMenu.AddItem(TaskCancel);
            return;
        }

        AddContractKillerSubMenu();
        AddHookerDeliverySubMenu();
        AddSecurityTruckSubMenu();
    }

    private void AddContractKillerSubMenu()
    {
        UIMenu contractKillerSubMenu = MenuPool.AddSubMenu(JobsSubMenu, "Contract Killer");
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].Description =
            "Take a contract and get rid of the target for some cash";
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].RightLabel =
            $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMin:C0}" +
            $"-{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMax:C0}~s~";
        contractKillerSubMenu.RemoveBanner();

        UIMenuItem startTask = new UIMenuItem("Start contract", "Start the contract.")
        {
            RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMin:C0}" +
                         $"-{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMax:C0}~s~"
        };
        startTask.Activated += (sender, e) =>
        {
            Player.PlayerTasks.FixerTasks.StartContractKillerTask(Contact);
            sender.Visible = false;
        };
        contractKillerSubMenu.AddItem(startTask);
    }

    private void AddHookerDeliverySubMenu()
    {
        UIMenu hookerSubMenu = MenuPool.AddSubMenu(JobsSubMenu, "Escort Driver");
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].Description =
            "Pick up our street workers and drop them off at their cliets properties.";
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].RightLabel =
            $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.EscortDriverPaymentMin:C0}" +
            $"-{Settings.SettingsManager.TaskSettings.EscortDriverPaymentMax:C0}~s~";
        hookerSubMenu.RemoveBanner();

        UIMenuItem startTask = new UIMenuItem("Accept job", "Pick up 3 of our hookers and take them to their clients place of residence.")
        {
            RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.EscortDriverPaymentMin:C0}" +
                         $"-{Settings.SettingsManager.TaskSettings.EscortDriverPaymentMax:C0}~s~"
        };
        startTask.Activated += (sender, e) =>
        {
            Player.PlayerTasks.FixerTasks.StartHookerDeliveryTask(Contact);
            sender.Visible = false;
        };
        hookerSubMenu.AddItem(startTask);
    }

    private void AddSecurityTruckSubMenu()
    {
        UIMenu truckSubMenu = MenuPool.AddSubMenu(JobsSubMenu, "Security Truck Robbery");
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].Description =
            "Steal a security truck and deliver it to a drop-off point.";
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].RightLabel =
            $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.SecurityTruckTheftPaymentMin:C0}" +
            $"-{Settings.SettingsManager.TaskSettings.SecurityTruckTheftPaymentMax:C0}~s~";
        truckSubMenu.RemoveBanner();

        UIMenuItem startTask = new UIMenuItem("Accept job", "Steal the security truck and deliver it.")
        {
            RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.SecurityTruckTheftPaymentMin:C0}" +
                         $"-{Settings.SettingsManager.TaskSettings.SecurityTruckTheftPaymentMax:C0}~s~"
        };
        startTask.Activated += (sender, e) =>
        {
            Player.PlayerTasks.FixerTasks.StartSecurityTruckTask(Contact);
            sender.Visible = false;
        };
        truckSubMenu.AddItem(startTask);
    }

    public void Update()
    {
        MenuPool.ProcessMenus();
    }
}
