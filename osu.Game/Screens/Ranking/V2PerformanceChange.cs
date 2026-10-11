// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    /// <summary>
    /// The change in profile PP and global rank caused by this score, not a beatmap PB comparison.
    /// </summary>
    public partial class V2PerformanceChange : CompositeDrawable
    {
        public readonly Bindable<ScoreBasedUserStatisticsUpdate?> LatestUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();
        public bool WaitForUpdate { get; init; }

        private readonly ScoreInfo score;
        private LoadingSpinner spinner = null!;
        private FillFlowContainer content = null!;
        private OsuSpriteText ppText = null!;
        private OsuSpriteText rankText = null!;
        private bool waiting;

        public V2PerformanceChange(ScoreInfo score)
        {
            this.score = score;
            Size = new Vector2(190, 14);
        }

        [BackgroundDependencyLoader]
        private void load(UserStatisticsWatcher? watcher, ResultsScreen? resultsScreen, IAPIProvider api)
        {
            waiting = WaitForUpdate || (resultsScreen?.IsLocalPlay == true && watcher != null && api.IsLoggedIn
                            && score.UserID == api.LocalUser.Value.OnlineID && score.OnlineID > 0 && score.Ruleset.IsLegacyRuleset());
            InternalChildren = new Drawable[]
            {
                spinner = new LoadingSpinner
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(10),
                },
                content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new[]
                    {
                        ppText = createText("Profile PP change"),
                        rankText = createText("Profile rank change"),
                    },
                },
            };

            if (watcher != null)
                ((IBindable<ScoreBasedUserStatisticsUpdate?>)LatestUpdate).BindTo(watcher.LatestUpdate);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Alpha = waiting ? 1 : 0;

            if (waiting)
            {
                spinner.Show();
                Scheduler.AddDelayed(() =>
                {
                    if (waiting)
                    {
                        spinner.Hide();
                        ppText.Text = "—";
                        waiting = false;
                    }
                }, 60000);
            }

            LatestUpdate.BindValueChanged(update =>
            {
                var value = update.NewValue;
                if (value == null || value.Score.UserID != score.UserID || value.Score.RulesetID != score.RulesetID
                    || !(value.Score.Equals(score) || value.Score.MatchesOnlineID(score)))
                    return;

                Alpha = 1;
                waiting = false;
                spinner.Hide();

                if (value.Before.PP is { } before && value.After.PP is { } after)
                {
                    decimal change = after - before;
                    ppText.Text = $"{change:+0.00;-0.00;+0.00}pp";
                    ppText.Colour = changeColour(change);
                }
                else
                    ppText.Text = "—";

                if (value.Before.GlobalRank is > 0 and var oldRank && value.After.GlobalRank is > 0 and var newRank)
                {
                    int change = oldRank - newRank;
                    rankText.Text = $"#{newRank:N0} ({change:+0;-0;0})";
                    rankText.Colour = changeColour(change);
                }
                else if (value.After.GlobalRank is > 0 and var rank)
                {
                    rankText.Text = $"#{rank:N0}";
                    rankText.Colour = Colour4.White;
                }
                else
                    rankText.Text = string.Empty;
            }, true);
        }

        private static Colour4 changeColour(decimal change) => change > 0 ? Colour4.FromHex("#B6FF86") : change < 0 ? Colour4.FromHex("#ED1B53") : Colour4.White;

        private static OsuSpriteText createText(string name) => new OsuSpriteText
        {
            Name = name,
            Font = OsuFont.Numeric.With(size: 11),
            Shadow = false,
        };

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            content.Scale = new Vector2(System.Math.Min(1, DrawWidth / System.Math.Max(1, content.DrawWidth)));
        }
    }
}
