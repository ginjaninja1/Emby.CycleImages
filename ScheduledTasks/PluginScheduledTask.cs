using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.CycleImages.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Dto;
//using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
//using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Sync;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Controller.Collections;


namespace Emby.CycleImages
{
    //Use this section if you need to have Scheduled tasks run
    public class PluginScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private readonly ILibraryManager LibraryManager;
        private readonly IItemRepository ItemRepository;
        private readonly ILogger _log;
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        //private readonly IItemRepository itemrepository;
        //private readonly ICollectionManager _collectionManager;
        private IHttpClient _httpClient;
        private ISyncProvider syncProvider;

        public string Name => "Cycle Images";

        public string Key => nameof(Name);

        public string Description => "Cycle Images";

        public string Category => "GinjaNinja Tools";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        //Constructor
        public PluginScheduledTask(ILibraryManager libraryManager, ILogManager logManager, 
            IServerApplicationHost serverApplicationHost, IHttpClient httpClient, IUserManager userManager, IItemRepository itemRepository)
        {
            LibraryManager = libraryManager;
            ItemRepository = itemRepository;
            _serverApplicationHost = serverApplicationHost;
            _httpClient = httpClient;
            _log = logManager.GetLogger(Plugin.Instance.Name);
            _userManager = userManager;
            //_collectionManager = collectionManager;
            
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
                _log.Info("Working on tag: " + t);

                await refreshitems(t);
            }



        }

        private async Task refreshitems(string tag)
        {
            //Get Collections, later functions only work with collections.
            InternalItemsQuery queryList = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { nameof(BoxSet) },
                Tags = new[] { tag }
            };
            _taggeditems = LibraryManager.GetItemList(queryList);
            _numberOfItemsInLibraries = _taggeditems.Length;
            _log.Info("Total No. of Objects with Tag : " + tag + " : {0}", _numberOfItemsInLibraries.ToString());

            foreach (BaseItem item in _taggeditems)
            {
                
                if (NeedUpdate(item))
                {
                    //Remove the primary image from baseitem
                    //Perform a refresh metadata on the baseitem so the primary images gets generated again based on current content
                    item.DeleteImage(ImageType.Primary, 0);
                    await item.RefreshMetadata(CancellationToken.None);
                    _log.Info("Refreshed Image for ID:{0} Name:{1} Type:{2}", item.InternalId, item.Name, item.GetType().Name);
                    UpdateHash(item);

                } else
                {
                    _log.Info("No Update Neccessary for ID:{0} Name:{1} Type:{2}", item.InternalId, item.Name, item.GetType().Name);
                }
                
                
            }

        }

        private bool NeedUpdate(BaseItem item)
        {
            //Relies on date last saved but if that doesnt work can switch to MakeHash
            PluginConfiguration config = Plugin.Instance.Configuration;
            
            List<CycleItem> CItems = config.CycleItems.ToList();
            
            //var cmatch = CItems.Where(c => c.Id == item.InternalId).ToList();
            var cmatch = CItems.Where(c => c.Id == item.InternalId).ToList();
            if (cmatch.Count == 0 || MakeHash(item) != cmatch[0].Hash) {
            //if (cmatch.Count == 0 || item.DateLastSaved >= cmatch[0].Datetime) {
                return true;
            } 
            return false;
                          
        }

        private long MakeHash(BaseItem parentitem)
        {
            
            var queryList = new InternalItemsQuery
            {
                ParentIds = new[] { parentitem.InternalId },
                //CollectionIds = new[] { parentitem.InternalId },
                DtoOptions = new DtoOptions(true)
                //Recursive = true
            };
           
            //var children = LibraryManager.QueryItems(queryList);
            var children = ItemRepository.GetItems(queryList);
            
            
            long hash = 100000;
            //
            //
               for (var i = 0; i < children.TotalRecordCount; i++)
            {
                if (i%2 != 0)
                {
                    hash = hash * children.Items[i].InternalId;
                    //hash = hash * children[i];
                } else
                {
                    //hash = hash * children.Items[i].InternalId;
                    hash = hash / children.Items[i].InternalId;
                }
            }

            return hash;
          
        }

        private void UpdateHash(BaseItem item)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            List<CycleItem> CItems = config.CycleItems.ToList();
            DateTime localnow = DateTime.Now;
            var obj = CItems.FirstOrDefault(x => x.Id == item.InternalId);
            if (obj != null)
            {
                //CItems.Where(x => x.Id == item.InternalId).ToList().ForEach(x => x.Hash = MakeHash(item));
                CItems.Where(x => x.Id == item.InternalId).ToList().ForEach(x => { x.Datetime = localnow; x.Hash = MakeHash(item); x.Name = item.Name; });
                _log.Info("Updated Item in  Config.XML for ID:{0} Name:{1} Type:{2} Date:{3}", item.InternalId, item.Name, item.GetType().Name, localnow.ToString() );

            }
            else
            {
                CItems.Add(new CycleItem { Id = item.InternalId, Datetime = localnow, Hash = MakeHash(item), Name = item.Name });
                _log.Info("Updated Item in  Config.XML for ID:{0} Name:{1} Type:{2} Date:{3}", item.InternalId, item.Name, item.GetType().Name, localnow.ToString()); 
            }

            config.CycleItems = CItems.ToArray();
            Plugin.Instance.UpdateConfiguration(Plugin.Instance.Configuration);

        }

        /*
        public async Task<List<ItemInfoModel>> GetLatestItems(string parentId)
        {
            JsonSerializerOptions _jsonSerializerOptions = new()
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };


            List<ItemInfoModel> baseItems = new List<ItemInfoModel>();

            string url = string.Format(_apiCalls.GetLatestItemsApi(parentId));

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                try
                {
                    baseItems = JsonSerializer.Deserialize<List<ItemInfoModel>>(json, _jsonSerializerOptions);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
            return baseItems;
        }
        */

        //Task Triggers - Currently unset, user can set these themselves in the menu.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }


    }
}
