// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingScreen : EditorScreen
    {
        private DependencyContainer dependencies = null!;
        private HitSoundingRoll? roll;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        public HitSoundingScreen()
            : base(EditorScreenMode.HitSounding)
        {
            ((Container)InternalChild).Padding = new MarginPadding(5);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuConfigManager config)
        {
            var model = new HitSoundingEditModel(EditorBeatmap);
            config.BindWith(OsuSetting.EditorHitSoundingNormalVisibility, model.NormalVisibility);
            dependencies.CacheAs(model);
            var playback = new HitSoundingSamplePlayback(model);
            dependencies.CacheAs(playback);
            var preview = new HitSoundingPreviewState(model);
            var library = new HitSoundingSampleLibrary();
            dependencies.CacheAs(library);
            dependencies.CacheAs(preview);
            roll = new HitSoundingRoll(model);
            dependencies.CacheAs(roll);
            AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background6, Alpha = 0.9f, Depth = float.MaxValue });
            Children = new Drawable[]
            {
                model,
                preview,
                new EditorSkinProvidingContainer(EditorBeatmap)
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        playback,
                        library,
                        new HitSoundingHotkeyHandler(model),
                        roll,
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 510 },
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    Width = 200, RelativeSizeAxes = Axes.Y,
                                    Child = new HitSoundingSelectionPanel(model)
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Left = 205 },
                                    Child = new HitSoundingPreviewPanel(model, preview)
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void PopOut()
        {
            roll?.CancelSeekPreview();
            base.PopOut();
        }
    }
}
