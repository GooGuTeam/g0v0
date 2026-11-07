// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public partial class LegacyBeatmapOptionsOverlay : OsuFocusedOverlayContainer
    {
        private readonly List<Action> actions = new List<Action>();
        private FillFlowContainer buttons = null!;
        private OsuTextFlowContainer heading = null!;

        public LegacyBeatmapOptionsOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.85f },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(640, 480),
                    Children = new Drawable[]
                    {
                        heading = new OsuTextFlowContainer(t => t.Font = OsuFont.Inter.With(size: 24))
                        {
                            RelativeSizeAxes = Axes.X,
                            Width = 0.98f,
                            AutoSizeAxes = Axes.Y,
                            Position = new Vector2(6, 2),
                        },
                        buttons = new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 105,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 15),
                        },
                    },
                },
            };
        }

        public void ShowFor(ISongSelect songSelect, BeatmapInfo beatmap)
        {
            buttons.Clear();
            actions.Clear();
            heading.Text = $"{beatmap.Metadata.Artist} - {beatmap.Metadata.Title}";
            heading.NewLine();
            heading.AddText(UserInterfaceStrings.LegacyBeatmapOptionsPrompt);

            var destructive = new Color4(233, 49, 0, 255);
            var purple = new Color4(187, 104, 194, 255);

            add(CollectionsStrings.ManageCollectionsTitle, new Color4(133, 184, 31, 255), songSelect.ManageCollections);
            if (beatmap.BeatmapSet != null)
                add(CommonStrings.DeleteWithConfirmation, destructive, () => songSelect.Delete(beatmap.BeatmapSet));

            if (songSelect is SoloSongSelect solo)
            {
                add(UserInterfaceStrings.LegacyBeatmapOptionsRemoveFromUnplayed, purple, () => solo.RemoveFromUnplayed(beatmap));
                add(UserInterfaceStrings.LegacyBeatmapOptionsClearLocalScores, purple, () => solo.ClearLocalScores(beatmap));
                add(ButtonSystemStrings.Edit.ToSentence(), destructive, () => solo.Edit(beatmap));
            }

            add(WebCommonStrings.ButtonsCancel, OsuColour.Gray((byte)107), () => { });
            Show();
        }

        private void add(LocalisableString text, Color4 colour, Action action)
        {
            actions.Add(action);
            int index = actions.Count - 1;
            buttons.Add(new LegacyDialogButton(LocalisableString.Interpolate($"{index + 1}. {text}"), colour, () => invoke(index))
            {
                Origin = Anchor.TopLeft,
                LabelSize = 31,
            });
        }

        private void invoke(int index)
        {
            Hide();
            actions[index]();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!e.Repeat && e.Key >= Key.Number1 && e.Key <= Key.Number9)
            {
                int index = e.Key - Key.Number1;
                if (index < actions.Count)
                    invoke(index);
                return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void PopIn() => this.FadeIn(200);
        protected override void PopOut() => this.FadeOut(200);
    }
}
