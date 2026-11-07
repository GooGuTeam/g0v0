// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingScreen : EditorScreen
    {
        private DependencyContainer dependencies = null!;
        private HitSoundingRoll? roll;
        private Container workspace = null!;
        private WorkspaceSplitter splitter = null!;
        private float requestedRollHeight = HitSoundingLayout.ROLL_HEIGHT;
        private float lastAvailableHeight;

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
                        workspace = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = HitSoundingLayout.ROLL_HEIGHT + 5 },
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
                        },
                        splitter = new WorkspaceSplitter(changeSplit)
                    }
                }
            };
        }

        protected override void Update()
        {
            base.Update();
            if (roll?.Parent == null)
                return;

            float available = roll.Parent.ChildSize.Y;
            if (available != lastAvailableHeight)
            {
                lastAvailableHeight = available;
                changeSplit(requestedRollHeight);
            }

            workspace.Padding = new MarginPadding { Top = roll.Height + 8 };
            splitter.Y = roll.Height;
        }

        private void changeSplit(float height)
        {
            if (roll == null)
                return;

            const float minimum_roll_height = HitSoundingLayout.OBJECTS_HEIGHT + 40 + 5 + HitSoundingLayout.LANE_COUNT * (HitSoundingLayout.SAMPLE_HEIGHT + HitSoundingLayout.LANE_SPACING);
            requestedRollHeight = Math.Clamp(height, minimum_roll_height, Math.Max(minimum_roll_height, lastAvailableHeight - 160));
            roll.ResizeHeightTo(requestedRollHeight, 160, Easing.OutQuint);
        }

        private partial class WorkspaceSplitter : CompositeDrawable
        {
            private readonly Action<float> resize;
            private readonly Box grip;
            private float startHeight;

            public WorkspaceSplitter(Action<float> resize)
            {
                this.resize = resize;
                Name = "Hitsounding workspace splitter";
                RelativeSizeAxes = Axes.X;
                Height = 8;
                Depth = -10;
                InternalChild = grip = new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 60,
                    Height = 2,
                    Alpha = 0.3f,
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours) => grip.Colour = colours.Highlight1;

            protected override bool OnHover(HoverEvent e)
            {
                grip.FadeTo(0.9f, 120);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e) => grip.FadeTo(0.3f, 120);

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return false;

                startHeight = Y;
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                float delta = Parent!.ToLocalSpace(e.ScreenSpaceMousePosition).Y - Parent.ToLocalSpace(e.ScreenSpaceMouseDownPosition).Y;
                resize(startHeight + delta);
            }
        }

        protected override void PopOut()
        {
            roll?.CancelSeekPreview();
            base.PopOut();
        }
    }
}
