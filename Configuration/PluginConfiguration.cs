using MediaBrowser.Model.Plugins;

namespace Emby.CycleImages.Configuration
{
    /// <summary>
    /// The plugin's persisted settings - and the only class involved in
    /// persistence. Uses Emby's standard BasePlugin&lt;T&gt; mechanism:
    /// Plugin.Instance.Configuration / SaveConfiguration() / UpdateConfiguration().
    ///
    /// This class has no UI/visual members by construction - the config page
    /// builds a separate view-model, ConfigUI, fresh from this class every
    /// time it's shown. See Emby.CycleImages.UI.Config.ConfigPageView.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableCycleImages { get; set; } = true;

        /// <summary>
        /// Comma-separated list of tags. Any collection, playlist, or channel carrying
        /// one of these tags has its primary image rebuilt as a collage of
        /// its four most recently added immediate members.
        /// </summary>
        public string CycleTagString { get; set; } = "cyclepic";

        public string[] EnabledLibraryIds { get; set; } = System.Array.Empty<string>();
    }
}
