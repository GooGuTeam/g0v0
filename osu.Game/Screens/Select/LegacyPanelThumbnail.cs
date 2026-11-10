// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Drawing;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Imaging;
using osu.Framework.Logging;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The thumbnail on the left of an osu!stable song list panel (<c>BeatmapTreeItem.thumbnail</c>). Stable downloads the set's thumb from the
    /// website, which covers the 114 x 85.5 box; this shows the beatmap's own full background the same way, scaled uniformly to fill the box
    /// and cropped at its centre.
    /// Backgrounds load once the panel has been on screen for a moment, nearest the centre first, as <see cref="PanelSetBackground"/> does.
    /// </summary>
    public partial class LegacyPanelThumbnail : Container
    {
        [Resolved]
        private BeatmapCarousel? beatmapCarousel { get; set; }

        private ThumbnailSprite? sprite;
        private WorkingBeatmap? working;
        private CancellationTokenSource? loadCancellation;
        private double timeSinceUnpool;

        public WorkingBeatmap? Beatmap
        {
            get => working;
            set
            {
                if (working == null && value == null)
                    return;

                if (getBackgroundFileHash(working) == getBackgroundFileHash(value) && (value == null) == (working == null))
                    return;

                working = value;

                loadCancellation?.Cancel();
                loadCancellation = null;

                sprite?.Expire();
                sprite = null;

                timeSinceUnpool = 0;
            }
        }

        public LegacyPanelThumbnail()
        {
            Masking = true;
        }

        /// <summary>
        /// The full background (lazer's panel background is a pre-cropped strip).
        /// </summary>
        private partial class ThumbnailSprite : Sprite
        {
            // twice the 114 x 85.5 box, so it stays sharp on screens up to twice stable's 768 px height.
            private const float target_width = 114 * 2;
            private const float target_height = 85.5f * 2;

            private readonly WorkingBeatmap working;
            private Texture? ownTexture;

            public ThumbnailSprite(WorkingBeatmap working)
            {
                this.working = working;
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer)
            {
                Texture = ownTexture = createThumbnail(renderer);
                Texture ??= working.GetBackground();

                if (Texture != null)
                    FillAspectRatio = Texture.Width / (float)Texture.Height;
            }

            /// <summary>
            /// A copy of the background shrunk to about the box's size. Drawn straight into the box, a full background (usually 1920 x 1080) is
            /// sampled at a few of its pixels each, which leaves its edges jagged; stable's thumbnails are small images to begin with.
            /// </summary>
            private Texture? createThumbnail(IRenderer renderer)
            {
                string? file = working.Metadata.BackgroundFile;

                if (string.IsNullOrEmpty(file))
                    return null;

                try
                {
                    string? path = working.BeatmapSetInfo.GetPathForFile(file);

                    if (path == null)
                        return null;

                    using var stream = working.GetStream(path);

                    if (stream == null)
                        return null;

                    var image = Image.Load<Rgba32>(stream);

                    float scale = Math.Max(target_width / image.Width, target_height / image.Height);

                    if (scale >= 1)
                        return null;

                    int width = Math.Max(1, (int)Math.Ceiling(image.Width * scale));
                    int height = Math.Max(1, (int)Math.Ceiling(image.Height * scale));

                    // halving averages each 2 x 2 block, so every source pixel counts; one bilinear step down this far would skip most of them.
                    while (image.Width / 2 >= width && image.Height / 2 >= height)
                    {
                        var half = new Size(image.Width / 2, image.Height / 2);
                        image.Mutate(i => i.Resize(half));
                    }

                    if (image.Width != width || image.Height != height)
                        image.Mutate(i => i.Resize(new Size(width, height)));

                    var texture = renderer.CreateTexture(width, height);
                    texture.SetData(new TextureUpload(image));
                    return texture;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Could not create a song select thumbnail for {working.BeatmapInfo}");
                    return null;
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                ownTexture?.Dispose();
            }
        }

        private static string? getBackgroundFileHash(WorkingBeatmap? working)
            => working?.BeatmapSetInfo.GetFile(working.Metadata.BackgroundFile)?.File.Hash;

        protected override void Update()
        {
            base.Update();

            if (loadCancellation != null || working == null)
                return;

            if (beatmapCarousel != null)
            {
                var carouselQuad = beatmapCarousel.ScreenSpaceDrawQuad;
                float delay = Math.Abs(carouselQuad.Centre.Y - ScreenSpaceDrawQuad.Centre.Y) / carouselQuad.Height * 100;

                timeSinceUnpool += Time.Elapsed;

                if (timeSinceUnpool <= delay)
                    return;
            }

            loadCancellation = new CancellationTokenSource();

            LoadComponentAsync(new ThumbnailSprite(working)
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            }, s =>
            {
                Add(sprite = s);

                // stable fades a newly loaded thumbnail in over 400ms.
                sprite.FadeInFromZero(400);
            }, loadCancellation.Token);
        }
    }
}
