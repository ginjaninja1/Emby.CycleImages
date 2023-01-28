using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.CycleImages.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
//using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

//mespace Emby.CycleImages.ScheduledTasks
namespace Emby.CycleImages
{
    //Use this section if you need to have Scheduled tasks run
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager LibraryManager;

        private readonly ILogger _log;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IUserDataManager _userDataManager;
        private IHttpClient _httpClient;
        private ISyncProvider syncProvider;

        public string Name => "Task Name";

        public string Key => nameof(Name);

        public string Description => "Cycle Images";

        public string Category => "GinjaNinja Tools";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        //Constructor
        public PluginScheduledTask(ILibraryManager libraryManager, ILogManager logManager, IServerApplicationHost serverApplicationHost, IHttpClient httpClient)
        {
            LibraryManager = libraryManager;
            _serverApplicationHost = serverApplicationHost;
            _httpClient = httpClient;
            _log = logManager.GetLogger(Plugin.Instance.Name);
        }

        //progressBar fields
        private double _totalProgress;
        private int _totalItems;

        //Get Library Item fields
        private BaseItem[] _taggeditems;
        private int _numberOfItemsInLibraries;
        


        //Task that will execute from the SheduleTask Menu
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            //Do work here for your Scheduled Task
            PluginConfiguration config = Plugin.Instance.Configuration;
            if (!config.EnableCycleImages)
            {
                _log.Info("Plugin is Not Enabled in Plugin Configuration: Exiting Now");
                return;
            }
            if (config.CycleTagString == "")
            {
                _log.Info("No Tag is Defined in Plugin Configuration: Exiting Now");
                return;
            }

            List<string> tags = config.CycleTagString.Split(',').ToList<string>();

            foreach (string t in tags)
            {
                _log.Info("Cycle Images Initializing: Tag is: " + t);

                await refreshitems(t);
            }
            

        }

        private async Task refreshitems(string tag)
        {
             PluginConfiguration config = Plugin.Instance.Configuration;
            //BaseItem[] _items = null;
            InternalItemsQuery queryList = new InternalItemsQuery
            {
                Recursive = true,
                Tags = new[] { tag },

            };

            //Cheese- it should work now you need to initialise the plugin config in every method. dont do a global one because it will only load once and any user changes after a server restart will not be read correctly
            //thanks
            //string cycleTagString = config.CycleTagString;

            //Cheese- are you try to tag thing so the show in UI?
            //Cheese- you should use the class LinkedItemInfo. this is what gives you hyperlinks. my mediainfo plugin makes good use of this
            //not following re linkediteminfo
           
            
            
            _taggeditems = LibraryManager.GetItemList(queryList);
            _numberOfItemsInLibraries = _taggeditems.Length;
            _log.Info("Total No. of Objects with Tag : " + tag + " : {0}", _numberOfItemsInLibraries.ToString());
            
            foreach (BaseItem item in _taggeditems)
            {
                //Remove the primary image
                //Perform a refresh metadata so the primary images gets generated again based on current content
                //In powershell
                // $ApiURL = $this.root + '/emby/items/' + $id + "/Refresh?Recursive=true&ImageRefreshMode=FullRefresh&MetadataRefreshMode=FullRefresh&ReplaceAllImages=true&ReplaceAllMetadata=true" + "&api_key=" + $this.apikey
            
                    //Cheese- TODO: show you how to use the Process Interface to launch external command line interfaces.
                    //Again  my MediaInfo plugin makes good use of this and often lol

            }

        }
        //Task Triggers - Currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }


    }
}
