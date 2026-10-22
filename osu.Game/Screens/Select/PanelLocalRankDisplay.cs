// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class PanelLocalRankDisplay : CompositeDrawable
    {
        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap?.Equals(value) == true)
                    return;

                beatmap = value;

                if (IsLoaded)
                    updateSubscription();
            }
        }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IScoreStore scoreStore { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        private IDisposable? scoreSubscription;

        private readonly UpdateableRank updateable;

        public bool HasRank => updateable.Rank != null;

        public PanelLocalRankDisplay(BeatmapInfo? beatmap = null)
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = updateable = new UpdateableRank(animate: false)
            {
                Size = new Vector2(40, 20),
                Alpha = 0,
            };

            Beatmap = beatmap;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            localUser.BindTo(api.LocalUser);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateSubscription());
            localUser.BindValueChanged(_ => updateSubscription(), true);
        }

        private void updateSubscription()
        {
            scoreSubscription?.Dispose();
            scoreSubscription = null;
            setRankFromScore(null);

            if (beatmap == null)
                return;

            // The store callback only signals that scores changed and may arrive on a background thread.
            // Fetch the current beatmap/ruleset/user on the update thread so stale callbacks can't
            // publish a rank for outdated criteria.
            scoreSubscription = scoreStore.Subscribe(() => Schedule(updateRank));
        }

        private void updateRank()
        {
            var currentBeatmap = beatmap;

            if (currentBeatmap == null)
                return;

            ScoreInfo? topScore = scoreStore.GetForBeatmap(currentBeatmap.Hash)
                                            // doing these post-store filters is most efficient.
                                            .Where(s => s.UserID == localUser.Value.Id || s.UserID <= 1)
                                            .Where(s => ruleset.Value.Equals(s.Ruleset))
                                            .MaxBy(info => (info.TotalScore, -info.Date.UtcDateTime.Ticks));

            setRankFromScore(topScore);
        }

        private void setRankFromScore(ScoreInfo? topScore)
        {
            updateable.Rank = topScore?.Rank;
            updateable.Alpha = topScore != null ? 1 : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            scoreSubscription?.Dispose();
        }
    }
}
