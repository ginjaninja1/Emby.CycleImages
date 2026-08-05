using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.CycleImages.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;

namespace Emby.CycleImages.Tasks
{
    /// <summary>
    /// Finds collections (BoxSets) and playlists carrying one of the configured
    /// tags, and rebuilds their primary image as a collage of the four most
    /// recently added immediate members.
    ///
    /// Emby already auto-generates a similar collage natively (see
    /// Emby.Providers.Playlists.PlaylistDynamicImageProvider), but its member
    /// order comes from the item's default child query, not recency. This task
    /// exists purely to force that selection to be "most recently added" -
    /// dimensions, format, and local-image resolution below all mirror Emby's
    /// own BaseLazyCollageImageProvider so the result is indistinguishable
    /// from a native collage other than which four members it contains.
    ///
    /// Collage generation and persistence are delegated entirely to Emby's own
    /// IImageProcessor / IProviderManager - no image manipulation is done here.
    /// </summary>
    public class CycleImagesTask : IScheduledTask
    {
        private const int MembersToInclude = 4;

        private static readonly string[] CollageEligibleItemTypes =
        {
            nameof(BoxSet),
            nameof(Playlist)
        };

        private readonly ILibraryManager libraryManager;
        private readonly IApplicationPaths applicationPaths;
        private readonly IImageProcessor imageProcessor;
        private readonly IProviderManager providerManager;
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        public CycleImagesTask(
            ILibraryManager libraryManager,
            IApplicationPaths applicationPaths,
            IImageProcessor imageProcessor,
            IProviderManager providerManager,
            IFileSystem fileSystem,
            ILogManager logManager)
        {
            this.libraryManager = libraryManager;
            this.applicationPaths = applicationPaths;
            this.imageProcessor = imageProcessor;
            this.providerManager = providerManager;
            this.fileSystem = fileSystem;
            this.logger = logManager.GetLogger(Plugin.Instance.Name);
        }

        public string Name => "Cycle Images";

        public string Key => "CycleImagesTask";

        public string Description => "Rebuilds the primary image of tagged collections and playlists as a collage of their most recently added members.";

        public string Category => "GinjaNinja Tools";

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;

            if (!config.EnableCycleImages)
            {
                this.logger.Info("Cycle Images is not enabled in plugin configuration: exiting now");
                return;
            }

            if (string.IsNullOrWhiteSpace(config.CycleTagString))
            {
                this.logger.Info("No tag is defined in plugin configuration: exiting now");
                return;
            }

            if (!this.imageProcessor.SupportsImageCollageCreation)
            {
                this.logger.Warn("The active image processor does not support collage creation: exiting now");
                return;
            }

            var tags = config.CycleTagString
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            foreach (var tag in tags)
            {
                this.logger.Info("Cycle Images: processing tag '{0}'", tag);
                await ProcessTagAsync(tag, cancellationToken, progress).ConfigureAwait(false);
            }
        }

        private async Task ProcessTagAsync(string tag, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var taggedItems = this.libraryManager.GetItemList(new InternalItemsQuery
            {
                Recursive = true,
                Tags = new[] { tag },
                IncludeItemTypes = CollageEligibleItemTypes
            });

            var total = taggedItems.Length;
            var processed = 0;

            foreach (var item in taggedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item is Folder folder)
                {
                    await RebuildCollageAsync(folder, cancellationToken).ConfigureAwait(false);
                }

                processed++;
                progress.Report(total == 0 ? 100.0 : (processed / (double)total) * 100.0);
            }
        }

        private async Task RebuildCollageAsync(Folder folder, CancellationToken cancellationToken)
        {
            var objectType = folder is BoxSet ? "collection" : "playlist";

            // Playlist and BoxSet membership is stored as linked/list items, not
            // as ordinary Folder children.  This is the same API used by Emby's
            // PlaylistDynamicImageProvider for both object types.
            var grouping = folder as IHasFolderGrouping;
            if (grouping == null)
            {
                this.logger.Warn("{0} '{1}': does not expose list membership - skipping", objectType, folder.Name);
                return;
            }

            var members = grouping.GetItems(
                new InternalItemsQuery
                {
                    EnableTotalRecordCount = false,
                    QueryName = "CycleImagesTask",
                    OrderBy = new[] { (ItemSortBy.DateCreated, SortOrder.Descending) }
                },
                cancellationToken).Items;

            var memberImages = await ResolveLocalMemberImagesAsync(members, cancellationToken).ConfigureAwait(false);

            if (memberImages.Count == 0)
            {
                this.logger.Info("{0} '{1}': no members with a usable primary image were found among {2} member(s) - skipping", objectType, folder.Name, members.Length);
                return;
            }

            var sourceHash = ComputeSourceHash(memberImages.Select(m => m.MemberId));
            var hashPath = GetHashPath(folder, objectType);
            var currentImage = folder.GetImageInfo(ImageType.Primary, 0);

            if (File.Exists(hashPath)
                && currentImage != null
                && currentImage.IsLocalFile
                && File.Exists(currentImage.Path)
                && string.Equals(File.ReadAllText(hashPath).Trim(), sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("{0} '{1}': newest {2} source member(s) are unchanged - skipping", objectType, folder.Name, memberImages.Count);
                return;
            }

            // Matches Emby's own BaseLazyCollageImageProvider.CreateImage: BoxSet
            // uses a 400x600 poster-shaped canvas, Playlist a 600x600 square -
            // both fall through Skia's aspect-ratio dispatch to the same 2x2
            // grid builder either way (there is no distinct native "poster"
            // layout - see Emby.Drawing.Skia.StripCollageBuilder).
            var (width, height) = folder is BoxSet ? (400, 600) : (600, 600);
            var outputPath = Path.Combine(this.applicationPaths.TempDirectory, $"{Guid.NewGuid()}.jpg");
            Directory.CreateDirectory(this.applicationPaths.TempDirectory);

            try
            {
                await this.imageProcessor.CreateImageCollage(
                    new ImageCollageOptions
                    {
                        Images = memberImages.Select(m => m.Image).ToArray(),
                        OutputPath = outputPath,
                        Width = width,
                        Height = height
                    },
                    cancellationToken).ConfigureAwait(false);

                var directoryService = new DirectoryService(this.logger, this.fileSystem);

                await this.providerManager.SaveImage(
                    folder,
                    this.libraryManager.GetLibraryOptions(folder),
                    outputPath,
                    "image/jpeg".AsMemory(),
                    ImageType.Primary,
                    null,
                    // Keep this as a plugin-managed poster. If source IDs are
                    // supplied, Emby names it auto_poster_<ids>.jpg and its
                    // PlaylistDynamicImageProvider subsequently replaces our
                    // newest-first collage using Emby's normal display order.
                    saveLocallyWithMedia: false,
                    generatedFromItemIds: Array.Empty<long>(),
                    directoryService: directoryService,
                    updateImageCache: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                // SaveImage updates this BaseItem instance but does not persist
                // the image path. Without this write, a later request reloads
                // the previous path from SQLite.
                folder.UpdateToRepository(ItemUpdateType.ImageUpdate);

                Directory.CreateDirectory(Path.GetDirectoryName(hashPath));
                File.WriteAllText(hashPath, sourceHash);

                this.logger.Info("{0} '{1}': primary image rebuilt from {2} member(s)", objectType, folder.Name, memberImages.Count);
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch (IOException ex)
                {
                    this.logger.Warn("Could not delete temporary collage file '{0}': {1}", outputPath, ex.Message);
                }
            }
        }

        /// <summary>
        /// Resolves each member's primary collage source image. Episodes are
        /// represented by their series, while directly imageable members are
        /// represented by themselves. Remote images are converted to local
        /// files and paths are rewritten to Emby's cached copies because Skia
        /// can only read the collage inputs from disk. Duplicate image owners,
        /// missing images, and failed remote conversions are skipped.
        /// </summary>
        private async Task<List<(ItemImageInfo Image, long MemberId)>> ResolveLocalMemberImagesAsync(
            BaseItem[] members,
            CancellationToken cancellationToken)
        {
            var resolved = new List<(ItemImageInfo Image, long MemberId)>();

            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // An episode contributes its show's poster. Movies (and any
                // other directly-imageable list item) contribute their own.
                BaseItem imageOwner = member;
                if (member is Episode episode)
                {
                    imageOwner = episode.Series;
                }

                if (imageOwner == null || resolved.Any(i => i.MemberId == imageOwner.InternalId))
                {
                    continue;
                }

                var image = imageOwner.GetImageInfo(ImageType.Primary, 0);

                if (image == null)
                {
                    continue;
                }

                if (!image.IsLocalFile)
                {
                    try
                    {
                        image = await this.libraryManager.ConvertImageToLocal(imageOwner, image, 0, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warn("'{0}': could not convert remote image to local, skipping this member: {1}", imageOwner.Name, ex.Message);
                        continue;
                    }
                }

                var cachedPath = this.libraryManager.GetCachedImage(imageOwner, image.Path);

                resolved.Add((new ItemImageInfo(image) { Path = cachedPath }, imageOwner.InternalId));

                if (resolved.Count == MembersToInclude)
                {
                    break;
                }
            }

            return resolved;
        }

        private string GetHashPath(BaseItem item, string objectType)
        {
            return Path.Combine(
                this.applicationPaths.DataPath,
                "CycleImages",
                $"{objectType}_{item.InternalId}.sha256");
        }

        private static string ComputeSourceHash(IEnumerable<long> memberIds)
        {
            var orderedIds = string.Join(",", memberIds.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(orderedIds));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
