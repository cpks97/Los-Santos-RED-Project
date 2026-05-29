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

    private MenuPool MenuPool;
    private UIMenu FixerMenu;
    private IGangs Gangs;
    private IPlacesOfInterest PlacesOfInterest;
    private ISettingsProvideable Settings;
    //private UIMenuListScrollerItem<Gang> StartGangHitMenu;
    private UIMenuItem TaskCancel;
    //private UIMenuItem StartWitnessEliminationMenu;
    //private UIMenuItem StartCopHitMenu;
    private UIMenu JobsSubMenu;
    private FixerContact Contact;
    private IAgencies Agencies;

    public JobFixerInteraction(IContactInteractable player, IGangs gangs, IPlacesOfInterest placesOfInterest, ISettingsProvideable settings, FixerContact contact, IAgencies agencies)
    {
        Player = player;
        Gangs = gangs;
        PlacesOfInterest = placesOfInterest;
        Settings = settings;
        Contact = contact;
        Agencies = agencies;
        MenuPool = new MenuPool();
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
                {
                    GameFiber.Yield();
                }
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
            TaskCancel = new UIMenuItem("Cancel Task", "Tell the fixer you cannot complete the task.") { RightLabel = "~o~$?~s~" };
            TaskCancel.Activated += (sender, e) =>
            {
                Player.PlayerTasks.CancelTask(Contact);
                sender.Visible = false;
            };
            JobsSubMenu.AddItem(TaskCancel);
            return;
        }
        AddContractKillerSubMenu();
    }

    private void AddContractKillerSubMenu()
    {
        UIMenu contractKillerSubMenu = MenuPool.AddSubMenu(JobsSubMenu, "Contract Killer");
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].Description = $"Take a contract and get rid of the target for some cash";
        JobsSubMenu.MenuItems[JobsSubMenu.MenuItems.Count() - 1].RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMin:C0}-{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMax:C0}~s~";
        contractKillerSubMenu.RemoveBanner();
        UIMenuItem StartTaskMenu = new UIMenuItem("Start contract", "Start the contract.") { RightLabel = $"~HUD_COLOUR_GREENDARK~{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMin:C0}-{Settings.SettingsManager.TaskSettings.ContractKillerPaymentMax:C0}~s~" };
        StartTaskMenu.Activated += (sender, e) =>
        {
            Player.PlayerTasks.FixerTasks.StartContractKillerTask(Contact);
            sender.Visible = false;
        };
        contractKillerSubMenu.AddItem(StartTaskMenu);
    }

    public void Update()
    {
        MenuPool.ProcessMenus();
    }
}

