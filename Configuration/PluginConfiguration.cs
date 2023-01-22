using MediaBrowser.Model.Plugins;

namespace Emby.CycleImages.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        //User Configuration Files
        public bool EnableCycleImages { get; set; }
        public string CycleTagString { get; set; }

        public PluginConfiguration()
        {
            //add default values here to use
            EnableCycleImages = true;
            CycleTagString = "cyclepic";

        }
    }
}
