// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The compose timeline's object visual, without its sample controls or time-changing drag handles.
    /// </summary>
    public partial class HitSoundingObject : TimelineHitObjectBlueprint
    {
        private readonly Action<HitObject> selected;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        public HitSoundingObject(HitObject hitObject, Action<HitObject> selected)
            : base(hitObject, false)
        {
            this.selected = selected;
        }

        internal void BindObject(HitObject hitObject) => RebindItem(hitObject);

        protected override bool ShouldBeConsideredForInput(Drawable child) => false;

        protected override void Update()
        {
            base.Update();
            if (beatmap.SelectedHitObjects.Contains(Item))
                Select();
            else
                Deselect();
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            selected(Item);
            return true;
        }
    }
}
