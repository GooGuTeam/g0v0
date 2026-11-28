// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Specialized;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit.HitSounding
{
    public partial class HitSoundingPreviewState : Component
    {
        public BindableDouble Radius { get; } = new BindableDouble(3) { MinValue = 0.001, MaxValue = 60 };
        public BindableBool ShowHighlight { get; } = new BindableBool(true);
        public double? StartTime { get; private set; }
        public double? EndTime { get; private set; }

        private readonly HitSoundingEditModel model;

        public HitSoundingPreviewState(HitSoundingEditModel model)
        {
            this.model = model;
            model.SelectedNodes.CollectionChanged += selectionChanged;
            model.Changed += refresh;
            Radius.BindValueChanged(_ => refresh());
            refresh();
        }

        private void selectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => refresh();

        private void refresh()
        {
            double start = double.PositiveInfinity;
            double end = double.NegativeInfinity;

            foreach (var node in model.SelectedNodes)
            {
                start = Math.Min(start, node.IsBody ? node.HitObject.StartTime : node.Time);
                end = Math.Max(end, node.IsBody ? node.HitObject.GetEndTime() : node.Time);
            }

            StartTime = double.IsPositiveInfinity(start) ? null : start - Radius.Value * 1000;
            EndTime = double.IsNegativeInfinity(end) ? null : end + Radius.Value * 1000;
        }

        protected override void Dispose(bool isDisposing)
        {
            model.SelectedNodes.CollectionChanged -= selectionChanged;
            model.Changed -= refresh;
            base.Dispose(isDisposing);
        }
    }
}
