using Emby.CycleImages.UI.Config;
using Emby.CycleImages.UIBaseClasses;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using System.Threading.Tasks;

namespace Emby.CycleImages.UI
{
    internal class MainPageController : ControllerBase
    {
        private readonly PluginInfo pluginInfo;
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

        public MainPageController(
            PluginInfo pluginInfo,
            IServerApplicationHost applicationHost,
            ILogger logger)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.applicationHost = applicationHost;
            this.logger = logger;

            this.PageInfo = new PluginPageInfo
            {
                Name = "CycleImages",
                EnableInMainMenu = false,
                DisplayName = "Cycle Images",
                MenuIcon = "image",
                IsMainConfigPage = true
            };
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new ConfigPageView(
                this.pluginInfo,
                this.applicationHost,
                this.logger);

            return Task.FromResult(view);
        }
    }
}
