using System;
using System.Collections.Generic;
using System.IO;
using Emby.CycleImages.Configuration;
using Emby.CycleImages.UI;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Serialization;

namespace Emby.CycleImages
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasUIPages
    {
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

        private List<IPluginUIPageController> pages;

        public Plugin(
            IServerApplicationHost applicationHost,
            ILogManager logManager)
            : base(
                applicationHost.Resolve<IApplicationPaths>(),
                applicationHost.Resolve<IXmlSerializer>())
        {
            this.applicationHost = applicationHost;

            this.logger = logManager.GetLogger(this.Name);

            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        public override string Name => "Cycle Images";

        public override string Description =>
            "Rebuilds collage images for collections, playlists, channels, and enabled libraries based on their most recently added members.";

        public override Guid Id =>
            new Guid("600FF041-1129-441F-82D9-D3943F22C7BE");

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public Stream GetThumbImage()
            => this.GetType()
                .Assembly
                .GetManifestResourceStream(this.GetType().Namespace + ".thumb.png");

        public IReadOnlyCollection<IPluginUIPageController> UIPageControllers
        {
            get
            {
                if (this.pages == null)
                {
                    this.pages = new List<IPluginUIPageController>
                    {
                        new MainPageController(
                            this.GetPluginInfo(),
                            this.applicationHost,
                            this.logger)
                    };
                }

                return this.pages.AsReadOnly();
            }
        }
    }
}
