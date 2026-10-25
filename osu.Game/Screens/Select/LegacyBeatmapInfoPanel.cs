// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The osu!stable style beatmap info block (top-left of the legacy song select).
    /// Replicates stable's exact layout (SongSelection.cs:261-289): ranked status icon in the corner,
    /// title hugging the very top, then mapper / length-bpm-objects / object counts / difficulty stats.
    /// Coordinates are stable's 480-space ones scaled by 1.6 so they line up with the 1366x768
    /// legacy space. Length/BPM and the stats are tinted by the active mods (red = harder,
    /// green = easier), just like stable with DT/HT/HR/EZ.
    /// </summary>
    public partial class LegacyBeatmapInfoPanel : CompositeDrawable
    {
        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private Sprite statusIcon = null!;
        private SpriteIcon statusGlyph = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText mapperText = null!;
        private FillFlowContainer lengthLine = null!;
        private OsuSpriteText countsText = null!;
        private FillFlowContainer statsLine = null!;

        private ISkinSource skin = null!;

        private CancellationTokenSource? countsCancellation;

        private IBindable<StarDifficulty>? starDifficulty;
        private CancellationTokenSource? starCancellation;

        // The SR WITHOUT mods, calculated live, used as the comparison baseline for the tint. Comparing
        // against info.StarRating (the stored value) tints randomly: the current calculator usually
        // differs by a few hundredths from the stored value even with no mods applied.
        private double? baselineStars;
        private CancellationTokenSource? baselineCancellation;

        private Ruleset? rulesetInstance;
        private RulesetInfo? rulesetInstanceSource;

        // Value raised by mods = harder = warmer; lowered = easier = cooler.
        private static readonly Color4 harder_colour = new Color4(255, 102, 102, 255);
        private static readonly Color4 easier_colour = new Color4(130, 220, 130, 255);

        private static string formatCounts(WorkingBeatmap working)
        {
            try
            {
                int circles = 0, sliders = 0, spinners = 0;

                foreach (var hitObject in working.Beatmap.HitObjects)
                {
                    switch (hitObject)
                    {
                        case IHasPath:
                            sliders++;
                            break;

                        case IHasDuration:
                            spinners++;
                            break;

                        default:
                            circles++;
                            break;
                    }
                }

                return $"Circles: {circles}   Sliders: {sliders}   Spinners: {spinners}";
            }
            catch
            {
                return @"Circles: 0   Sliders: 0   Spinners: 0";
                // Failed decode (corrupt / missing file); stable shows zeroes here too.
            }
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skinSource)
        {
            skin = skinSource;
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                statusIcon = new Sprite
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(19, 19),
                    Size = new Vector2(28),
                },
                // Fallback glyph for when neither the skin nor the bundled classic one provides the
                // status icon (selection-ranked / selection-loved / etc).
                statusGlyph = new SpriteIcon
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.Centre,
                    Position = new Vector2(19, 19),
                    Size = new Vector2(22),
                    Shadow = true,
                    Alpha = 0,
                },
                // The title hugs the very top, offset to clear the status icon (stable: 21,-3).
                // Higher depth keeps it BEHIND the detail lines whenever a long/tall title overlaps them.
                titleText = line(new Vector2(34, -3), 28, FontWeight.Light, depth: 1),
                mapperText = line(new Vector2(37, 19), 18, FontWeight.Light),
                lengthLine = flowLine(new Vector2(2, 38)),
                countsText = line(new Vector2(2, 58), 18, FontWeight.Bold),
                statsLine = flowLine(new Vector2(2, 78)),
            };

            static OsuSpriteText line(Vector2 position, float size, FontWeight weight, float depth = 0) => new OsuSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = position,
                Font = LegacyFonts.Get(size, weight),
                Shadow = true,
                Depth = depth,
            };

            static FillFlowContainer flowLine(Vector2 position) => new FillFlowContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = position,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            };
        }

        private static void setSegments(FillFlowContainer flow, float size, FontWeight weight, IEnumerable<(string text, Color4 colour)> segments)
        {
            flow.Clear();

            foreach (var (text, colour) in segments)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = text,
                    Font = LegacyFonts.Get(size, weight),
                    Shadow = true,
                    Colour = colour,
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(_ => updateDisplay(), true);
            // Mods and ruleset both affect the length/BPM line and the stats line; the star rating
            // bindable below already tracks mod/ruleset changes on its own.
            mods.BindValueChanged(_ => Scheduler.AddOnce(refreshModSensitiveLines));
            ruleset.BindValueChanged(_ =>
            {
                // The mod-free baseline depends on the ruleset (converts change SR).
                refreshBaselineStars();
                Scheduler.AddOnce(refreshModSensitiveLines);
            });
        }

        private void refreshBaselineStars()
        {
            baselineCancellation?.Cancel();
            baselineStars = null;

            var info = beatmap.Value.BeatmapInfo;
            var token = (baselineCancellation = new CancellationTokenSource()).Token;

            difficultyCache.GetDifficultyAsync(info, ruleset.Value, null, token).ContinueWith(task =>
            {
                if (task.GetResultSafely() is StarDifficulty diff)
                {
                    Schedule(() =>
                    {
                        if (token.IsCancellationRequested)
                            return;

                        baselineStars = diff.Stars;
                        Scheduler.AddOnce(refreshStatsLine);
                    });
                }
            }, token);
        }

        private void updateDisplay()
        {
            var working = beatmap.Value;
            var info = working.BeatmapInfo;
            var metadata = info.Metadata;

            titleText.Text = $"{metadata.Artist} - {metadata.Title} [{info.DifficultyName}]";
            mapperText.Text = $"Mapped by {metadata.Author.Username}";

            // Star rating with the current mods applied; the bindable also updates itself on
            // mod/ruleset changes.
            starCancellation?.Cancel();
            starDifficulty?.UnbindAll();
            starDifficulty = difficultyCache.GetBindableDifficulty(info, (starCancellation = new CancellationTokenSource()).Token);
            starDifficulty.BindValueChanged(_ => Scheduler.AddOnce(refreshStatsLine));

            refreshBaselineStars();

            refreshLengthLine();
            refreshStatsLine();

            // The circle/slider/spinner counts aren't in the local database (only the totals are), so we
            // derive them from the actual hit objects like stable does. If the beatmap is already decoded
            // in memory we use it directly; if not, we decode on a background task (large maps take a few
            // tens of ms) and fill the line in once it finishes.
            countsCancellation?.Cancel();

            if (working.BeatmapLoaded)
                countsText.Text = formatCounts(working);
            else
            {
                countsText.Text = string.Empty;

                var cts = countsCancellation = new CancellationTokenSource();

                Task.Run(() =>
                {
                    string text = formatCounts(working);

                    Schedule(() =>
                    {
                        if (!cts.IsCancellationRequested)
                            countsText.Text = text;
                    });
                }, cts.Token);
            }

            var status = info.BeatmapSet?.Status ?? info.Status;

            var tex = skin.GetTexture(statusTextureName(status)) ?? skins.DefaultClassicSkin.GetTexture(statusTextureName(status));
            statusIcon.Texture = tex;

            if (tex != null)
            {
                // Fit the 28px slot while preserving aspect; forcing a square stretched the non-square
                // icons (e.g. selection-question is 15x26).
                float fit = Math.Min(28f / tex.DisplayWidth, 28f / tex.DisplayHeight);
                statusIcon.Size = new Vector2(tex.DisplayWidth, tex.DisplayHeight) * fit;
                statusIcon.Alpha = 1;
                statusGlyph.Alpha = 0;
            }
            else
            {
                statusIcon.Alpha = 0;
                statusGlyph.Alpha = 1;
                statusGlyph.Icon = statusFallbackIcon(status);
            }
        }

        private void refreshModSensitiveLines()
        {
            refreshLengthLine();
            refreshStatsLine();
        }

        private void refreshLengthLine()
        {
            var info = beatmap.Value.BeatmapInfo;
            int total = info.TotalObjectCount >= 0 ? info.TotalObjectCount : 0;

            double rate = ModUtils.CalculateRateWithMods(mods.Value);
            int lengthSeconds = (int)(info.Length / rate / 1000);
            int bpm = FormatUtils.RoundBPM(info.BPM, rate);

            var rateColour = rate > 1.0005 ? harder_colour : rate < 0.9995 ? easier_colour : Color4.White;

            setSegments(lengthLine, 18, FontWeight.Bold, new[]
            {
                ($"Length: {lengthSeconds / 60:00}:{lengthSeconds % 60:00}   BPM: {bpm}", rateColour),
                ($"   Objects: {total}", Color4.White),
            });
        }

        private void refreshStatsLine()
        {
            var info = beatmap.Value.BeatmapInfo;
            var segments = new List<(string, Color4)>();

            if (rulesetInstance == null || !ruleset.Value.Equals(rulesetInstanceSource))
            {
                rulesetInstance = ruleset.Value.CreateInstance();
                rulesetInstanceSource = ruleset.Value;
            }

            // Stable's stat order first (CS AR OD HP), then the ruleset's extras.
            string[] preferredOrder = { @"CS", @"AR", @"OD", @"HP" };

            var attributes = rulesetInstance.GetBeatmapAttributesForDisplay(info, mods.Value)
                                            .OrderBy(a =>
                                            {
                                                int i = Array.IndexOf(preferredOrder, a.Acronym);
                                                return i < 0 ? int.MaxValue : i;
                                            });

            bool first = true;

            foreach (var attribute in attributes)
            {
                var colour = attribute.AdjustedValue > attribute.OriginalValue + 0.005f ? harder_colour
                    : attribute.AdjustedValue < attribute.OriginalValue - 0.005f ? easier_colour
                    : Color4.White;

                segments.Add(($"{(first ? string.Empty : @" ")}{attribute.Acronym}:{attribute.AdjustedValue:0.##}", colour));
                first = false;
            }

            // Tint against the live mod-free baseline only (same calculator version); white until it
            // arrives. The displayed value may still fall back to the stored one while calculating.
            double stars = starDifficulty?.Value.Stars ?? info.StarRating;
            var starColour = Color4.White;

            if (baselineStars is double baseline)
            {
                starColour = stars > baseline + 0.005 ? harder_colour
                    : stars < baseline - 0.005 ? easier_colour
                    : Color4.White;
            }

            segments.Add(($"   Star Rating: {stars:0.0}", starColour));

            setSegments(statsLine, 13, FontWeight.Bold, segments);
        }

        protected override void Dispose(bool isDisposing)
        {
            countsCancellation?.Cancel();
            starCancellation?.Cancel();
            baselineCancellation?.Cancel();
            base.Dispose(isDisposing);
        }

        // lazer's own (newer) icons, from osu-resources.
        private static IconUsage statusFallbackIcon(BeatmapOnlineStatus status)
        {
            switch (status)
            {
                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Qualified:
                    return OsuIcon.CheckCircle;

                case BeatmapOnlineStatus.Loved:
                    return OsuIcon.Heart;

                case BeatmapOnlineStatus.Approved:
                    return OsuIcon.Crown;

                default:
                    return OsuIcon.EditCircle;
            }
        }

        private static string statusTextureName(BeatmapOnlineStatus status)
        {
            switch (status)
            {
                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Qualified:
                    return @"selection-ranked";

                case BeatmapOnlineStatus.Loved:
                    return @"selection-loved";

                case BeatmapOnlineStatus.Approved:
                case BeatmapOnlineStatus.Pending:
                case BeatmapOnlineStatus.WIP:
                    return @"selection-approved";

                default:
                    return @"selection-question";
            }
        }
    }
}
