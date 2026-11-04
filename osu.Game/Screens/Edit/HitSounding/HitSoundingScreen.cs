// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
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
        private void load(OverlayColourProvider colours)
        {
            var model = new HitSoundingEditModel(EditorBeatmap);
            dependencies.CacheAs(model);
            var playback = new HitSoundingSamplePlayback(model);
            dependencies.CacheAs(playback);
            AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background6, Depth = float.MaxValue });
            Children = new Drawable[]
            {
                model,
                new EditorSkinProvidingContainer(EditorBeatmap)
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[] { playback, new HitSoundingHotkeyHandler(model), roll = new HitSoundingRoll(model) }
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
