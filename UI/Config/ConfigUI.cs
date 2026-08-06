using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;
using System.ComponentModel;

namespace Emby.CycleImages.UI.Config
{
    public class ConfigUI : EditableOptionsBase
    {
        public override string EditorTitle => "Cycle Images - Configuration";

        public override string EditorDescription =>
            "Rebuilds collage images for tagged collections, playlists, and channels, plus individually enabled libraries, based on their most recently added members.";

        public CaptionItem GeneralHeading { get; set; } = new CaptionItem("General");

        [DisplayName("Enable Plugin")]
        [Description("When disabled, the scheduled task exits immediately without processing any items.")]
        [AutoPostBack("updateconfig", nameof(EnableCycleImages))]
        public bool EnableCycleImages { get; set; } = true;

        [DisplayName("Tag(s) to Cycle")]
        [Description("Comma-separated tags. Any collection, playlist, or channel carrying one of these tags will have its primary image rebuilt from its four most recently added members.")]
        [AutoPostBack("updateconfig", nameof(CycleTagString))]
        public string CycleTagString { get; set; } = "cyclepic";

        public CaptionItem LibrariesHeading { get; set; } = new CaptionItem("Libraries");

        [Description("Enable image cycling for individual libraries. Libraries are disabled by default.")]
        public GenericItemList LibraryList { get; set; } = new GenericItemList();

        public GenericItemList ScheduledTaskLink { get; set; } = new GenericItemList();

        public GenericItemList ForumLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Community Forum",
                SecondaryText = "Issues, Suggestions and Updates",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "https://emby.media/community/topic/116139-ginjaninja-tools-cycle-images-plugin-replace-collection-collage-image-based-on-latest-members/",
                HyperLinkTargetExternal = true
            }
        };

        public GenericItemList GithubLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Github repository",
                SecondaryText = string.Empty,
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "https://github.com/ginjaninja1/Emby.CycleImages",
                HyperLinkTargetExternal = true
            }
        };
    }
}
