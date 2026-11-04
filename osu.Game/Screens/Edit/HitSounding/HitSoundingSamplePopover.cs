// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Chooses one bank and shared set for the nodes selected when the popover opens.
    /// </summary>
    public partial class HitSoundingSamplePopover : OsuPopover
    {
        private readonly HitSoundingEditModel model;
        private readonly HitSoundingNode[] targets;
        private readonly bool additionsOnly;
        private readonly string initialBank;
        private readonly int initialSet;

        public HitSoundingSamplePopover(HitSoundingNode node, HitSoundingEditModel model, bool additionsOnly)
        {
            this.model = model;
            this.additionsOnly = additionsOnly;
            targets = model.SelectedNodes.Contains(node) ? model.SelectedNodes.ToArray() : new[] { node };
            initialBank = node.GetBank(additionsOnly);
            initialSet = node.SetIndex;
        }

        [BackgroundDependencyLoader]
        private void load(EditorBeatmap beatmap)
        {
            string[] banks = additionsOnly
                ? new[] { HitObjectComposer.HIT_BANK_AUTO, HitSampleInfo.BANK_NORMAL, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM }
                : HitSampleInfo.ALL_BANKS;
            var indices = new SortedSet<int> { 0, 1 };

            if (beatmap.BeatmapSkin != null)
            {
                foreach (var set in beatmap.BeatmapSkin.GetAvailableSampleSets())
                    indices.Add(set.SampleSetIndex);
            }

            foreach (var node in targets)
                indices.Add(node.SetIndex);

            var bankDropdown = new BankDropdown
            {
                Caption = additionsOnly ? EditorStrings.AdditionBank : EditorStrings.NormalBank,
                Items = banks,
                Current = { Value = initialBank }
            };
            var setDropdown = new SetDropdown
            {
                Caption = EditorStrings.SampleSet,
                Items = indices,
                Current = { Value = initialSet }
            };
            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    Width = 283, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        bankDropdown,
                        setDropdown,
                        new SaveButton
                        {
                            RelativeSizeAxes = Axes.X, Height = 35, Text = Resources.Localisation.Web.CommonStrings.ButtonsSave,
                            Action = () =>
                            {
                                model.SetBank(targets, bankDropdown.Current.Value, setDropdown.Current.Value, additionsOnly);
                                this.HidePopover();
                            }
                        }
                    }
                }
            };
        }

        private partial class SaveButton : RoundedButton
        {
            protected override void LoadComplete()
            {
                base.LoadComplete();

                SpriteText.Font = OsuFont.GetFont(size: 21, weight: FontWeight.SemiBold);

                Triangles?.Hide();
            }
        }

        private partial class BankDropdown : FormDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item) => item switch
            {
                HitSampleInfo.BANK_NORMAL => HitSoundingStrings.Normal,
                HitSampleInfo.BANK_SOFT => HitSoundingStrings.Soft,
                HitSampleInfo.BANK_DRUM => HitSoundingStrings.Drum,
                _ => HitSoundingStrings.Auto
            };
        }

        private partial class SetDropdown : FormDropdown<int>
        {
            protected override LocalisableString GenerateItemText(int item)
                => item == 0 ? HitSoundingStrings.UserSkin : HitSoundingStrings.Set(item);
        }
    }
}
