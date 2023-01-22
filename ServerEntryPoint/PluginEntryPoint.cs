using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emby.CycleImages.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using Emby.CycleImages.ScheduledTasks;
using System.Diagnostics;

namespace Emby.CycleImages
{
    public class CycleImagesPluginEntryPoint : IServerEntryPoint
    {
        public static CycleImagesPluginEntryPoint Instance { get; private set; }

        private readonly IServerConfigurationManager _config;

        private readonly ITaskManager TaskManager;
        private ILibraryMonitor LibraryMonitor { get; }
        private ILibraryManager LibraryManager { get; }
        private ILogger Log { get; }
        private IFileSystem FileSystem { get; }
        public IApplicationPaths ApplicationPaths { get; set; }

        

        public CycleImagesPluginEntryPoint(IServerConfigurationManager config, ITaskManager taskManager,
            IFileSystem fileSystem, ILogManager logManager, ILibraryMonitor libraryMonitor, ILibraryManager libraryManager)
        {
            _config = config;
            TaskManager = taskManager;
            FileSystem = fileSystem;
            LibraryMonitor = libraryMonitor;
            LibraryManager = libraryManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }

        public void Run()
        {
            MigratePluginConfig();

            Plugin.Instance.UpdateConfiguration(Plugin.Instance.Configuration);
            //LibraryManager.ItemUpdated += LibraryManagerItemAdded;
            LibraryManager.ItemAdded += LibraryManagerItemAdded;
            LibraryManager.ItemRemoved += LibraryManagerItemRemoved;
            //TaskManager.TaskCompleted += TaskManagerOnTaskCompleted;
        }

        private void LibraryManagerItemRemoved(object sender, ItemChangeEventArgs e)
        {
            var item = e.Item;
            Log.Info("Library Monitory has removed {0} from the library", item);
            var config = Plugin.Instance.Configuration;
            //do something on event item removed with item e
        }

        private async void LibraryManagerItemAdded(object sender, ItemChangeEventArgs e)
        {
            var config = Plugin.Instance.Configuration;
            var item = e.Item;
            //do something on vent ItemAdded with item e
        }
        
        public void Dispose()
        {
            Plugin.Instance.UpdateConfiguration(Plugin.Instance.Configuration);
            TaskManager.TaskCompleted -= TaskManagerOnTaskCompleted;
        }

        private void TaskManagerOnTaskCompleted(object sender, TaskCompletionEventArgs e)
        {
            var config = Plugin.Instance.Configuration;

            switch (e.Task.ScheduledTask.Key)
            {
                
            }
        }

        public void MigratePluginConfig()
        {
            //another method
        }
    }
}
