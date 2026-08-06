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
using MediaBrowser.Controller.Channels;
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
    /// Finds collections, playlists, and channels carrying a configured tag,
    /// plus explicitly enabled libraries, and rebuilds their primary image as
    /// a collage of the four most recently added members.
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
            nameof(Playlist),
            nameof(Channel)
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

        public string Description => "Rebuilds collage images for tagged collections, playlists, and channels, plus enabled libraries, from their most recently added members.";

        public string Category => "GinjaNinja Tools";

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;

            if (!config.EnableCycleImages)
            {
                this.logger.Info("Cycle Images is not enabled in plugin configuration: exiting now");
                return;
            }

            var enabledLibraryIds = config.EnabledLibraryIds ?? Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(config.CycleTagString) && enabledLibraryIds.Length == 0)
            {
                this.logger.Info("No tag or library is enabled in plugin configuration: exiting now");
                return;
            }

            if (!this.imageProcessor.SupportsImageCollageCreation)
            {
                this.logger.Warn("The active image processor does not support collage creation: exiting now");
                return;
            }

            var tags = (config.CycleTagString ?? string.Empty)
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            foreach (var tag in tags)
            {
                this.logger.Info("Cycle Images: processing tag '{0}'", tag);
                await ProcessTagAsync(tag, cancellationToken, progress).ConfigureAwait(false);
            }

            await ProcessLibrariesAsync(enabledLibraryIds, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task ProcessLibrariesAsync(string[] libraryIds, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var libraries = libraryIds
                .Select(ResolveLibrary)
                .OfType<CollectionFolder>()
                .ToArray();

            for (var index = 0; index < libraries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await RebuildCollageAsync(libraries[index], cancellationToken).ConfigureAwait(false);
                progress.Report(((index + 1) / (double)libraries.Length) * 100.0);
            }
        }

        private BaseItem ResolveLibrary(string value)
        {
            if (long.TryParse(value, out var internalId))
            {
                return this.libraryManager.GetItemById(internalId);
            }

            return Guid.TryParse(value, out var id)
                ? this.libraryManager.GetItemById(id)
                : null;
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
            var objectType = folder is BoxSet
                ? "collection"
                : folder is Playlist
                    ? "playlist"
                    : folder is Channel ? "channel" : "library";

            // Playlist and BoxSet membership is stored as linked/list items;
            // channels and libraries expose ordinary folder queries.
            BaseItem[] members;
            if (folder is IHasFolderGrouping grouping)
            {
                members = grouping.GetItems(
                    new InternalItemsQuery
                    {
                        EnableTotalRecordCount = false,
                        QueryName = "CycleImagesTask",
                        OrderBy = new[] { (ItemSortBy.DateCreated, SortOrder.Descending) }
                    },
                    cancellationToken).Items;
            }
            else
            {
                members = folder.GetItemList(CreateFolderQuery(folder));
            }

            var memberImages = await ResolveLocalMemberImagesAsync(members, cancellationToken).ConfigureAwait(false);

            if (memberImages.Count == 0)
            {
                this.logger.Info("{0} '{1}': no members with a usable image were found among {2} member(s) - skipping", objectType, folder.Name, members.Length);
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

            // Matches BaseLazyCollageImageProvider: libraries use a 640x360
            // PNG, BoxSets a 400x600 JPEG, and playlists a 600x600 JPEG.
            // Channels are also top-level landscape folders; their native
            // image normally comes from the channel provider rather than a
            // collage, so the library form factor is used for cycling.
            var isLandscape = folder is CollectionFolder || folder is Channel;
            var (width, height) = isLandscape
                ? (640, 360)
                : folder is BoxSet ? (400, 600) : (600, 600);
            var imageFormat = isLandscape ? "png" : "jpg";
            var mimeType = isLandscape ? "image/png" : "image/jpeg";
            var outputPath = Path.Combine(this.applicationPaths.TempDirectory, $"{Guid.NewGuid()}.{imageFormat}");
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
                    mimeType.AsMemory(),
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

                // Channels are retained in Emby's in-memory library-item
                // cache. SaveImage updates this instance and the repository,
                // but the channel API can continue serving the separately
                // cached ImageInfos until the server restarts. UpdateImages
                // synchronizes that live cached instance immediately.
                if (folder is Channel)
                {
                    this.libraryManager.UpdateImages(folder);
                }

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

        private static InternalItemsQuery CreateFolderQuery(Folder folder)
        {
            var query = new InternalItemsQuery
            {
                Recursive = true,
                EnableTotalRecordCount = false,
                QueryName = "CycleImagesTask",
                OrderBy = new[] { (ItemSortBy.DateCreated, SortOrder.Descending) }
            };

            if (!(folder is CollectionFolder library))
            {
                return query;
            }

            var contentType = library.CollectionType ?? string.Empty;
            if (IsCollectionType(contentType, CollectionType.Movies))
                query.IncludeItemTypes = new[] { "Movie" };
            else if (IsCollectionType(contentType, CollectionType.TvShows))
                query.IncludeItemTypes = new[] { "Series" };
            else if (IsCollectionType(contentType, CollectionType.Music)
                || IsCollectionType(contentType, CollectionType.AudioBooks))
            {
                query.IncludeItemTypes = new[] { "Audio", "MusicVideo" };
                query.GroupByAlbumId = true;
            }
            else if (IsCollectionType(contentType, CollectionType.MusicVideos))
                query.IncludeItemTypes = new[] { "MusicVideo" };
            else if (IsCollectionType(contentType, CollectionType.Books))
                query.IncludeItemTypes = new[] { "Book" };
            else if (IsCollectionType(contentType, CollectionType.Games))
                query.IncludeItemTypes = new[] { "Game" };
            else if (IsCollectionType(contentType, CollectionType.BoxSets))
            {
                query.IncludeItemTypes = new[] { "BoxSet" };
                query.Recursive = false;
            }
            else if (IsCollectionType(contentType, CollectionType.Playlists))
            {
                query.IncludeItemTypes = new[] { "Playlist" };
                query.Recursive = false;
            }
            else if (IsCollectionType(contentType, CollectionType.HomeVideos))
                query.IncludeItemTypes = new[] { "Video", "Photo" };
            else
                query.IncludeItemTypes = new[] { "Video", "Audio", "Photo", "Movie", "Series", "MusicVideo", "Game" };

            return query;
        }

        private static bool IsCollectionType(string actual, ReadOnlyMemory<char> expected)
        {
            return string.Equals(actual, expected.ToString(), StringComparison.OrdinalIgnoreCase);
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

                var image = imageOwner.GetImageInfo(ImageType.Primary, 0)
                    ?? imageOwner.GetImageInfo(ImageType.Thumb, 0);

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
