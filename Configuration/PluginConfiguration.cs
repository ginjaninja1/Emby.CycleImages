using MediaBrowser.Model.Plugins;
using System.Collections.Generic;
using System;
using MediaBrowser.Controller.Entities;

namespace Emby.CycleImages.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        //Configuration Properties
        public bool EnableCycleImages { get; set; }
        public string CycleTagString { get; set; }
        public CycleItem[] CycleItems { get; set; }
        public PluginConfiguration()
        {
            //Default values
            EnableCycleImages = true;
            CycleTagString = "cyclepic";
            CycleItems = Array.Empty<CycleItem>();
        }   
    }
    public class CycleItem
    {
        public string Name { get; set; }
        public long Id { get; set; }
        public long Hash { get; set; }

        public DateTime Datetime { get; set; }
    }
}
