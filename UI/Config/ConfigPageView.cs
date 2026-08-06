using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.CycleImages.UIBaseClasses.Views;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

namespace Emby.CycleImages.UI.Config
{
    /// <summary>
    /// Config page. Deliberately kept to just: construction, page settings,
    /// and command handlers that read/write the persisted configuration via
    /// Plugin.Instance.
    /// </summary>
    internal class ConfigPageView : PluginPageView
    {
        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;
        private readonly ITaskManager taskManager;
        private readonly ILibraryManager libraryManager;

        private const string LibraryToggleCommandPrefix = "togglelibrary:";

        public ConfigPageView(
            PluginInfo pluginInfo,
            IServerApplicationHost applicationHost,
            ILogger logger)
            : base(pluginInfo.Id)
        {
            this.logger = logger;
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();
            this.taskManager = applicationHost.Resolve<ITaskManager>();
            this.libraryManager = applicationHost.Resolve<ILibraryManager>();
            this.ShowSave = false;
            this.ShowBack = false;
            this.AllowBack = false;

            RebuildContentData();
        }

        private void RebuildContentData()
        {
            var config = Plugin.Instance.Configuration;

            var myTaskWorker = this.taskManager.ScheduledTasks
                .FirstOrDefault(t => string.Equals(t.ScheduledTask.Key, "CycleImagesTask", StringComparison.Ordinal));

            string hyperlinkUrl = myTaskWorker != null
                ? $"/scheduledtask?id={myTaskWorker.Id}"
                : "/scheduledtasks";

            var enabledLibraryIds = new HashSet<string>(
                config.EnabledLibraryIds ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var libraryList = new GenericItemList(this.libraryManager.GetVirtualFolders()
                .Where(folder => !string.IsNullOrWhiteSpace(folder.ItemId))
                .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                .Select(folder =>
                {
                    var enabled = enabledLibraryIds.Contains(folder.ItemId);
                    return new GenericListItem
                    {
                        PrimaryText = folder.Name,
                        SecondaryText = string.IsNullOrWhiteSpace(folder.CollectionType) ? "Mixed content" : folder.CollectionType,
                        Icon = IconNames.video_library,
                        Status = enabled ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Toggle = new ToggleButtonItem("Cycle image")
                        {
                            IsChecked = enabled,
                            CommandId = LibraryToggleCommandPrefix + folder.ItemId
                        }
                    };
                }));

            this.ContentData = new ConfigUI
            {
                EnableCycleImages = config.EnableCycleImages,
                CycleTagString = config.CycleTagString,
                LibraryList = libraryList,

                ScheduledTaskLink = new GenericItemList
                {
                    new GenericListItem
                    {
                        PrimaryText = "Configure Scheduled Task",
                        SecondaryText = "Manage background execution rules and automation intervals",
                        Icon = IconNames.link,
                        Status = ItemStatus.Succeeded,
                        HyperLink = hyperlinkUrl,
                        HyperLinkTargetExternal = false
                    }
                }
            };
        }

        public override Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            return RunCommand(itemId, commandId, data);
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (!string.IsNullOrEmpty(commandId)
                && commandId.StartsWith(LibraryToggleCommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                ToggleLibrary(commandId.Substring(LibraryToggleCommandPrefix.Length));
                RebuildContentData();
                return Task.FromResult<IPluginUIView>(this);
            }

            if (!string.IsNullOrEmpty(data) && commandId == "updateconfig")
            {
                HandleSave(data);
            }

            return Task.FromResult<IPluginUIView>(this);
        }

        private void ToggleLibrary(string libraryId)
        {
            if (string.IsNullOrWhiteSpace(libraryId))
            {
                return;
            }

            var config = Plugin.Instance.Configuration;
            var enabled = new HashSet<string>(
                config.EnabledLibraryIds ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            if (!enabled.Add(libraryId))
            {
                enabled.Remove(libraryId);
            }

            config.EnabledLibraryIds = enabled.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
            Plugin.Instance.SaveConfiguration();
            this.logger.Info("Cycle Images library configuration saved");
        }

        private void HandleSave(string data)
        {
            var config = Plugin.Instance.Configuration;

            try
            {
                var incoming = this.jsonSerializer.DeserializeFromString<ConfigUI>(data);

                if (incoming != null)
                {
                    config.EnableCycleImages = incoming.EnableCycleImages;
                    config.CycleTagString = incoming.CycleTagString;

                    Plugin.Instance.SaveConfiguration();

                    this.logger.Info("Cycle Images configuration saved");
                }
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error saving Cycle Images configuration", ex);
            }

            RebuildContentData();
        }
    }
}
