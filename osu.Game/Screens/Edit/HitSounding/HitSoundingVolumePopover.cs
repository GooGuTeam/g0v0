// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// A small popover for adjusting the volume of a run of <see cref="HitSoundingNode"/>s.
    /// </summary>
    /// <remarks>
    /// Opened by double-clicking anywhere on a run (editing the whole run) or by Ctrl-clicking a sample point inside
    /// a run (editing the run from that point onwards). No node is inserted or removed when the value stays the same.
    /// </remarks>
    public partial class HitSoundingVolumePopover : OsuPopover
    {
        private readonly HitSoundingEditModel model;
        private readonly HitSoundingNode[] targets;
        private readonly double startTime;
        private readonly double endTime;
        private readonly int initialVolume;
        private readonly bool hasMultipleVolumes;

        private SamplePointPiece.VolumeControl volume = null!;

        public HitSoundingVolumePopover(HitSoundingEditModel model, IEnumerable<HitSoundingNode> nodes, double startTime, double endTime)
        {
            this.model = model;
            this.startTime = startTime;
            this.endTime = endTime;

            // freeze the edit targets immediately; the model may rebuild its node instances after any change.
            targets = nodes.ToArray();

            int volumeValue = targets.Length == 0 ? 100 : targets[0].Volume;

            hasMultipleVolumes = targets.Any(n => n.Volume != volumeValue);
            initialVolume = hasMultipleVolumes ? 100 : volumeValue;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            FillFlowContainer flow;

            Children = new Drawable[]
            {
                flow = new FillFlowContainer
                {
                    Width = 283,
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Font = OsuFont.Style.Caption1,
                            Colour = colourProvider.Light4,
                            Text = HitSoundingStrings.TimeRange(startTime.ToEditorFormattedString(), endTime.ToEditorFormattedString()),
                        },
                        volume = new SamplePointPiece.VolumeControl
                        {
                            Caption = EditorStrings.SampleVolume,
                            Current = new BindableInt(initialVolume)
                            {
                                MinValue = DrawableHitObject.MINIMUM_SAMPLE_VOLUME,
                                MaxValue = 100,
                            }
                        }
                    }
                }
            };

            volume.TabbableContentContainer = flow;
            volume.IsMultipleValues = hasMultipleVolumes;

            volume.Current.BindValueChanged(v => model.SetVolume(targets, v.NewValue));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ScheduleAfterChildren(() => volume.TakeFocus());
        }
    }
}
