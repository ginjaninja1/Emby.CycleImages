using System;
using System.Linq;
using System.Threading.Tasks;
using Emby.CycleImages.UIBaseClasses.Views;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller;
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

        public ConfigPageView(
            PluginInfo pluginInfo,
            IServerApplicationHost applicationHost,
            ILogger logger)
            : base(pluginInfo.Id)
        {
            this.logger = logger;
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();
            this.taskManager = applicationHost.Resolve<ITaskManager>();
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

            this.ContentData = new ConfigUI
            {
                EnableCycleImages = config.EnableCycleImages,
                CycleTagString = config.CycleTagString,

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
            if (!string.IsNullOrEmpty(data) && commandId == "updateconfig")
            {
                HandleSave(data);
            }

            return Task.FromResult<IPluginUIView>(this);
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
