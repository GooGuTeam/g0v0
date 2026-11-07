// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// osu!stable's in-game scoreboard (<c>Scoreboard</c> and <c>ScoreboardEntry</c>): up to six <c>menu-button-background</c> plates down the
    /// left of the screen, keeping the leader and a window around the local player, shown during breaks or while toggled on.
    /// </summary>
    public partial class LegacyGameplayLeaderboard : CompositeDrawable, ISerialisableDrawable
    {
        /// <summary>
        /// Whether legacy skins' default HUD uses this leaderboard instead of the standard one.
        /// Updated by <see cref="OsuGameBase"/> from <see cref="OsuSetting.ToriiLegacyGameplayLeaderboard"/>, applying from the next play.
        /// </summary>
        public static bool UseInLegacySkins { get; set; }

        // stable lays its HUD out in 640x480 units, which g0v0 draws at 1.6x.
        private const float scale = 1.6f;

        private const int display_count = 6;
        private const float entry_spacing = 36 * scale;

        /// <summary>
        /// The top of stable's scoreboard for a ruleset (<c>Ruleset.ScoreboardPosition</c>), in g0v0's units.
        /// </summary>
        public static float TopFor(string? rulesetShortName) => rulesetShortName switch
        {
            @"taiko" => 264,
            @"fruits" => 100,
            @"mania" => 135,
            _ => 205,
        } * scale;

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private IGameplayLeaderboardProvider leaderboardProvider { get; set; } = null!;

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        private readonly IBindableList<GameplayLeaderboardScore> scores = new BindableList<GameplayLeaderboardScore>();
        private readonly Bindable<bool> alwaysVisible = new Bindable<bool>();
        private readonly IBindable<LocalUserPlayingState> playingState = new Bindable<LocalUserPlayingState>();

        private readonly List<Entry> entries = new List<Entry>();
        private Container<Entry> entryContainer = null!;

        private int lastTrackedIndex = -1;

        public LegacyGameplayLeaderboard()
        {
            Size = new Vector2(90 * scale, entry_spacing * display_count);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.GameplayLeaderboard, alwaysVisible);

            if (gameplayState != null)
                playingState.BindTo(gameplayState.PlayingState);

            InternalChild = entryContainer = new Container<Entry>
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scores.BindTo(leaderboardProvider.Scores);
            scores.BindCollectionChanged((_, _) =>
            {
                entryContainer.Clear();
                entries.Clear();
                lastTrackedIndex = -1;

                foreach (var score in scores)
                {
                    var entry = new Entry(score);
                    entries.Add(entry);
                    entryContainer.Add(entry);

                    entry.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(reorder));
                    entry.Rank.BindValueChanged(_ => Scheduler.AddOnce(reorder));
                    entry.HasQuit.BindValueChanged(_ => Scheduler.AddOnce(reorder));
                }

                Scheduler.AddOnce(reorder);
                FinishTransforms(true);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            // stable shows its scoreboard during breaks, or throughout while toggled on, and takes it away once the play is over.
            bool over = gameplayState?.HasPassed == true || gameplayState?.HasFailed == true;
            bool visible = player?.Configuration.ShowLeaderboard != false && !over && (alwaysVisible.Value || playingState.Value != LocalUserPlayingState.Playing);

            // stable steps the alpha by 0.08 every 60th of a second.
            float target = visible ? 1 : 0;
            float step = (float)(Time.Elapsed / (1000 / 60.0) * 0.08);
            entryContainer.Alpha = target > entryContainer.Alpha
                ? System.Math.Min(target, entryContainer.Alpha + step)
                : System.Math.Max(target, entryContainer.Alpha - step);
        }

        /// <summary>
        /// stable's <c>Reorder</c> and <c>Reposition</c>.
        /// </summary>
        private void reorder()
        {
            var ordered = entries.OrderBy(e => e.Score.DisplayOrder.Value).ToList();

            int trackedIndex = ordered.FindIndex(e => e.Score.Tracked);

            // overtaking someone flashes the local player's plate.
            if (trackedIndex >= 0 && lastTrackedIndex >= 0 && trackedIndex < lastTrackedIndex)
                ordered[trackedIndex].Overtook();

            lastTrackedIndex = trackedIndex;

            // the leader always keeps the top slot, and the rest of the window follows the local player.
            int firstShown = trackedIndex > display_count - 1 ? trackedIndex - (display_count - 1) : 0;
            if (firstShown > 0)
                firstShown++;

            int others = entries.Count(e => !e.Score.Tracked);
            int slot = 0;

            for (int i = 0; i < ordered.Count; i++)
            {
                var entry = ordered[i];
                bool shown = (i >= firstShown && slot < display_count) || i == 0;

                // stable fades the other players' plates down the board.
                float alpha = entry.Score.Tracked || others == 0 ? 1 : 0.8f - (float)i / others * 0.3f;

                entry.SetRank(entry.Score.Position.Value ?? (entry.Score.Tracked ? null : i + 1));
                entry.SetLeader(i == 0);
                entry.MoveTo(slot * entry_spacing, shown, alpha);

                if (shown)
                    slot++;
            }
        }

        private partial class Entry : CompositeDrawable
        {
            public readonly GameplayLeaderboardScore Score;

            // bound copies, so that the entries' subscriptions go with them when the scores are replaced.
            public readonly IBindable<long> DisplayOrder;
            public readonly IBindable<int?> Rank;
            public readonly IBindable<bool> HasQuit;
            private readonly IBindable<long> totalScore;
            private readonly IBindable<int> combo;

            private static readonly Color4 leader_colour = new Color4(97, 190, 255, 150);
            private static readonly Color4 others_colour = new Color4(31, 115, 153, 150);
            private static readonly Color4 tracked_colour = new Color4(250, 250, 250, 100);
            private static readonly Color4 quit_name_colour = new Color4(236, 39, 81, 255);

            private Sprite background = null!;
            private TruncatingSpriteText nameText = null!;
            private LegacySpriteText scoreText = null!;
            private LegacySpriteText comboText = null!;
            private LegacySpriteText rankText = null!;

            private bool? shown;
            private bool leader;

            private Bindable<ScoringMode> scoringMode = null!;

            public Entry(GameplayLeaderboardScore score)
            {
                Score = score;

                DisplayOrder = score.DisplayOrder.GetBoundCopy();
                Rank = score.Position.GetBoundCopy();
                HasQuit = score.HasQuit.GetBoundCopy();
                totalScore = score.TotalScore.GetBoundCopy();
                combo = score.Combo.GetBoundCopy();

                AutoSizeAxes = Axes.Both;
                Alpha = 0;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin, SkinManager skins, OsuConfigManager config)
            {
                scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);

                var texture = skin.GetStableTexture(@"menu-button-background") ?? skins.DefaultClassicSkin.GetStableTexture(@"menu-button-background");

                InternalChildren = new Drawable[]
                {
                    background = new Sprite
                    {
                        Texture = texture,
                        Scale = new Vector2(0.62f),
                        Colour = backgroundColour,
                        BypassAutoSizeAxes = Axes.Both,
                    },
                    // drawn behind the name and score, as a large faint number.
                    rankText = new LegacySpriteText(LegacyFont.ScoreEntry)
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(82, -2) * scale,
                        Scale = new Vector2(2.2f),
                        Colour = new Color4(255, 255, 255, 80),
                        FixedWidth = true,
                    },
                    nameText = new TruncatingSpriteText
                    {
                        Position = new Vector2(0, -2) * scale,
                        Text = Score.User.Username,
                        Font = LegacyFonts.Get(14 * scale),
                        // stable cuts names wider than 90 units down and adds an ellipsis.
                        MaxWidth = 90 * scale,
                    },
                    scoreText = new LegacySpriteText(LegacyFont.ScoreEntry)
                    {
                        Position = new Vector2(2, 18) * scale,
                        FixedWidth = true,
                    },
                    comboText = new LegacySpriteText(LegacyFont.ScoreEntry)
                    {
                        Origin = Anchor.TopRight,
                        Position = new Vector2(82, 18) * scale,
                        Colour = new Color4(153, 251, 255, 255),
                        FixedWidth = true,
                    },
                };

                // the plate's texture is anchored by a point inside it, leaving its left part off-screen.
                if (texture != null)
                    background.OriginPosition = new Vector2(470, 20);
            }

            private Color4 backgroundColour => Score.Tracked ? tracked_colour : leader ? leader_colour : others_colour;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // stable's scoreboard digits overlap by 2.5 (3 for the rank) at 1024x768.
                scoreText.Spacing = comboText.Spacing = new Vector2(-2.5f, 0);
                rankText.Spacing = new Vector2(-3, 0);

                totalScore.BindValueChanged(_ => updateScore());
                scoringMode.BindValueChanged(_ => updateScore());
                combo.BindValueChanged(c => comboText.Text = c.NewValue > 0 ? $@"{c.NewValue.ToString(@"#,#", CultureInfo.InvariantCulture)}x" : string.Empty, true);
                HasQuit.BindValueChanged(q => nameText.FadeColour(q.NewValue ? quit_name_colour : Color4.White, 150), true);

                updateScore();
            }

            private void updateScore()
            {
                long value = Score.GetDisplayScore(scoringMode.Value);
                scoreText.Text = value > 0 ? value.ToString(@"#,#", CultureInfo.InvariantCulture) : @"-";
            }

            public void SetRank(int? rank) => rankText.Text = rank > 0 ? rank.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

            public void SetLeader(bool isLeader)
            {
                if (leader == isLeader)
                    return;

                leader = isLeader;
                background.FadeColour(backgroundColour, 50);
            }

            public void MoveTo(float y, bool show, float alpha)
            {
                bool instant = shown == null;

                if (instant)
                    Y = y;
                else
                    this.MoveToY(y, 600, Easing.Out);

                if (shown != show || (show && Alpha != alpha))
                    this.FadeTo(show ? alpha : 0, instant ? 0 : 600);

                shown = show;
            }

            public void Overtook() => background.FlashColour(Color4.White, 200);
        }
    }
}
