// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Rooms;
using osu.Game.Online.Solo;
using osu.Game.Scoring;
using osu.Game.Rulesets;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Screens.Play
{
    public partial class SoloPlayer : SubmittingPlayer
    {
        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly SoloGameplayLeaderboardProvider leaderboardProvider = new SoloGameplayLeaderboardProvider();

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        private GetBeatmapUserScoresRequest personalBestRequest;
        private ScoreInfo previousBest;
        private readonly Lock personalBestLock = new Lock();
        private bool scorePreparationStarted;

        public SoloPlayer([CanBeNull] PlayerConfiguration configuration = null)
            : base(configuration)
        {
            Configuration.ShowLeaderboard = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(leaderboardProvider);
        }

        protected override APIRequest<APIScoreToken> CreateTokenRequest()
        {
            int beatmapId = Beatmap.Value.BeatmapInfo.OnlineID;
            int rulesetId = Ruleset.Value.OnlineID;

            if (beatmapId <= 0)
                return null;

            if (Beatmap.Value.BeatmapInfo.Status == BeatmapOnlineStatus.LocallyModified)
                return null;

            if (!Ruleset.Value.IsLegacyRuleset())
                return null;

            return new CreateSoloScoreRequest(Beatmap.Value.BeatmapInfo, rulesetId, Game.VersionHash, RulesetHashCache?.GetHash(Ruleset.Value));
        }

        protected override void StartGameplay()
        {
            base.StartGameplay();

            if (!api.IsLoggedIn || Beatmap.Value.BeatmapInfo.OnlineID <= 0 || !Ruleset.Value.IsLegacyRuleset())
                return;

            // Fetch before submission: querying on the results screen could return this play as its own PB.
            var ruleset = Ruleset.Value.CreateSpecialRulesetByScore(Score.ScoreInfo) ?? Ruleset.Value;
            personalBestRequest = new GetBeatmapUserScoresRequest(Beatmap.Value.BeatmapInfo.OnlineID, api.LocalUser.Value.OnlineID, ruleset);
            personalBestRequest.Success += response =>
            {
                lock (personalBestLock)
                {
                    if (scorePreparationStarted)
                        return;

                    previousBest = response.Scores.Where(s => s.Passed && s.PP is { } pp && double.IsFinite(pp))
                                           .OrderByDescending(s => s.PP).FirstOrDefault()?.ToScoreInfo(rulesets, Beatmap.Value.BeatmapInfo);
                }
            };
            api.Queue(personalBestRequest);
        }

        protected override Task PrepareScoreForResultsAsync(Score score)
        {
            // Never let a late response after submission overwrite the pre-play snapshot.
            lock (personalBestLock)
                scorePreparationStarted = true;

            personalBestRequest?.Cancel();
            return base.PrepareScoreForResultsAsync(score);
        }

        protected override ResultsScreen CreateResults(ScoreInfo score) => new SoloResultsScreen(score)
        {
            AllowRetry = true,
            IsLocalPlay = true,
            ComparisonScore = { Value = previousBest },
        };

        protected override void Dispose(bool isDisposing)
        {
            personalBestRequest?.Cancel();
            base.Dispose(isDisposing);
        }

        protected override bool ShouldExitOnTokenRetrievalFailure(Exception exception) => false;

        protected override APIRequest<MultiplayerScore> CreateSubmissionRequest(Score score, long token)
        {
            IBeatmapInfo beatmap = score.ScoreInfo.BeatmapInfo!;

            Debug.Assert(beatmap.OnlineID > 0);

            return new SubmitSoloScoreRequest(score.ScoreInfo, token, beatmap.OnlineID);
        }
    }
}
