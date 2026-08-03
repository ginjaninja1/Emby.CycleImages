using System;
using System.Collections.Generic;
using System.IO;
using Emby.CycleImages.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.CycleImages
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public static Plugin Instance { get; set; }

        //You will need to generate a new GUID and paste it here - Tools => Create GUID
        private Guid _id = new Guid("600FF041-1129-441F-82D9-D3943F22C7BE");
        

        public override string Name => "Cycle Images";

        public override string Description => "Cycles Images on tagged media items";

        public override Guid Id => _id;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths,
            xmlSerializer)
        {
            Instance = this;
        }
        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        //Display Thumbnail image for Plugin Catalogue  - you will need to change build action for thumb.jpg to embedded Resource
        public Stream GetThumbImage()
        {
            Type type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        //Web pages for Server UI configuration
        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {

            new PluginPageInfo
            {
                //html File
                Name = "ConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.ConfigurationPage.html",
                EnableInMainMenu = true
                /*MenuSection = "server",*/
                //MenuIcon = "theaters"
            },
            new PluginPageInfo
            {
                //javascript file
                Name = "ConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.ConfigurationPage.js"
            },
        };





    }
}
