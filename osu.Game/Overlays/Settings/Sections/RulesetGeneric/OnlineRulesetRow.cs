// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Private.Responses;
using osu.Game.Online.Chat;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.RulesetGeneric
{
    /// <summary>
    /// How a game mode provided by the server relates to what is installed locally.
    /// </summary>
    public enum OnlineRulesetState
    {
        /// <summary>
        /// The mode is an official ruleset which ships with the client.
        /// </summary>
        BuiltIn,

        /// <summary>
        /// The mode is a server-side variant of an official ruleset (relax/autopilot), which requires no local ruleset.
        /// </summary>
        ServerSide,

        /// <summary>
        /// The mode needs a custom ruleset which isn't present locally.
        /// </summary>
        NotInstalled,

        /// <summary>
        /// The locally installed ruleset matches the latest version the server provides.
        /// </summary>
        UpToDate,

        /// <summary>
        /// A newer version of the locally installed ruleset is available on the server.
        /// </summary>
        UpdateAvailable,

        /// <summary>
        /// The ruleset is installed but the server provides no version information to compare against.
        /// </summary>
        VersionUnknown,
    }

    /// <summary>
    /// A row describing a single game mode supported by the server, styled after <see cref="RulesetRow"/>.
    /// </summary>
    /// <remarks>
    /// Relax/autopilot variants are not given rows of their own; they are reported as tags on the row of the base mode
    /// they are derived from.
    /// </remarks>
    public partial class OnlineRulesetRow : Container
    {
        private const float spacing = 15;
        private const string separator = @" · ";

        private readonly APIRuleset mode;
        private readonly APIRulesetVersion? versionInfo;
        private readonly IReadOnlyList<string> variants;

        private FormControlBackground background = null!;
        private OsuTextFlowContainer titleFlow = null!;
        private OsuTextFlowContainer descriptionFlow = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private RulesetHashCache rulesetHashes { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        /// <summary>
        /// Creates a row for a server-provided game mode.
        /// </summary>
        /// <param name="mode">The mode being described.</param>
        /// <param name="versionInfo">The mode's version information, if the server provides any.</param>
        /// <param name="variants">
        /// The short names of the relax/autopilot variants of <paramref name="mode"/> which the server also offers.
        /// </param>
        public OnlineRulesetRow(APIRuleset mode, APIRulesetVersion? versionInfo, IReadOnlyList<string> variants)
        {
            this.mode = mode;
            this.versionInfo = versionInfo;
            this.variants = variants;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            var localRuleset = findLocalRuleset();

            OnlineRulesetState state = resolveState(localRuleset, out string? currentVersion);
            string? latestVersion = versionInfo?.LatestVersion;
            string? downloadUrl = versionInfo?.DownloadUrl;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(spacing),
                    Padding = new MarginPadding
                    {
                        Vertical = 5,
                        Left = 9,
                        Right = 5,
                    },
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable?[]
                                {
                                    new ConstrainedIconContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Icon = createIcon(localRuleset),
                                        Size = new Vector2(32),
                                        Margin = new MarginPadding
                                        {
                                            Vertical = 4,
                                            Right = 8,
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(4),
                                        Children = new Drawable[]
                                        {
                                            titleFlow = new OsuTextFlowContainer
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                            descriptionFlow = new OsuTextFlowContainer
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(5),
                                        Margin = new MarginPadding
                                        {
                                            Left = 5,
                                        },
                                        Children = new Drawable[]
                                        {
                                            new IconButton
                                            {
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                Icon = FontAwesome.Solid.Download,
                                                TooltipText = RulesetSettingsStrings.DownloadRuleset,
                                                Action = openDownloadPage,
                                                Alpha = downloadUrl == null ? 0 : 1,
                                                Enabled = { Value = downloadUrl != null },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            titleFlow.AddText($@"{mode.ReadableName} ", t => t.Font = OsuFont.Style.Heading2);
            titleFlow.AddText($@"[{mode.Name}]", t =>
            {
                t.Font = OsuFont.Style.Caption1;
                t.Colour = colours.Colour0;
            });

            // relax/autopilot variants the server offers alongside this mode.
            foreach (string variant in variants)
            {
                titleFlow.AddText($@" [{getVariantAcronym(variant)}]", t =>
                {
                    t.Font = OsuFont.Style.Caption1;
                    t.Colour = colours.Colour2;
                });
            }

            buildDescription(state, currentVersion, latestVersion, colours);
        }

        /// <summary>
        /// Derives the mod acronym ("RX"/"AP") from a variant mode's short name, following the same convention as
        /// <see cref="RulesetInfo.CreateSpecialRuleset"/>.
        /// </summary>
        private static string getVariantAcronym(string shortName)
            => shortName.Length > 2 ? shortName[^2..].ToUpperInvariant() : shortName.ToUpperInvariant();

        private RulesetInfo? findLocalRuleset()
            => rulesets.AllRulesets.FirstOrDefault(r => string.Equals(r.ShortName, mode.Name, StringComparison.OrdinalIgnoreCase));

        private Drawable createIcon(RulesetInfo? localRuleset)
        {
            if (localRuleset != null)
            {
                try
                {
                    return localRuleset.CreateInstance().CreateIcon();
                }
                catch (Exception)
                {
                    // the ruleset may be in a broken state; fall back to a placeholder icon below.
                }
            }

            return new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Icon = OsuIcon.Rulesets,
            };
        }

        /// <summary>
        /// Works out how the server mode relates to the local installation.
        /// </summary>
        /// <param name="localRuleset">The locally installed ruleset matching the server mode, if any.</param>
        /// <param name="currentVersion">
        /// The version of the locally installed ruleset, resolved by matching its assembly hash against the hashes
        /// the server provides. <c>null</c> if it doesn't correspond to any released version.
        /// </param>
        private OnlineRulesetState resolveState(RulesetInfo? localRuleset, out string? currentVersion)
        {
            currentVersion = null;

            if (isServerSideVariant(mode.Name))
                return OnlineRulesetState.ServerSide;

            if (localRuleset == null)
                return OnlineRulesetState.NotInstalled;

            if (mode.IsOfficial)
                return OnlineRulesetState.BuiltIn;

            string? latestVersion = versionInfo?.LatestVersion;

            if (string.IsNullOrEmpty(latestVersion))
                return OnlineRulesetState.VersionUnknown;

            string? localHash = rulesetHashes.GetHash(localRuleset);

            if (localHash == null)
                return OnlineRulesetState.VersionUnknown;

            foreach (var (version, hash) in versionInfo!.Versions)
            {
                if (!string.Equals(hash, localHash, StringComparison.OrdinalIgnoreCase))
                    continue;

                currentVersion = version;
                break;
            }

            return currentVersion == latestVersion ? OnlineRulesetState.UpToDate : OnlineRulesetState.UpdateAvailable;
        }

        private void buildDescription(OnlineRulesetState state, string? currentVersion, string? latestVersion, OverlayColourProvider colours)
        {
            void addDescription(LocalisableString text, Action<SpriteText>? style = null)
                => descriptionFlow.AddText(text, t =>
                {
                    t.Font = OsuFont.Style.Caption1;
                    style?.Invoke(t);
                });

            void addUpdateHighlight(LocalisableString text) => addDescription(text, t => t.Colour = colours.Colour0);

            switch (state)
            {
                case OnlineRulesetState.BuiltIn:
                    addDescription(RulesetSettingsStrings.RulesetBuiltin);
                    break;

                case OnlineRulesetState.ServerSide:
                    addDescription(RulesetSettingsStrings.RulesetProvidedByServer);
                    break;

                case OnlineRulesetState.VersionUnknown:
                    addDescription(RulesetSettingsStrings.RulesetNoVersionInformation);
                    break;

                case OnlineRulesetState.NotInstalled:
                    addDescription(RulesetSettingsStrings.RulesetNotInstalled);

                    if (!string.IsNullOrEmpty(latestVersion))
                    {
                        addDescription(separator);
                        addDescription(RulesetSettingsStrings.RulesetVersion(latestVersion));
                    }

                    break;

                case OnlineRulesetState.UpToDate:
                    addDescription(RulesetSettingsStrings.RulesetVersion(latestVersion!));
                    addDescription(separator);
                    addDescription(RulesetSettingsStrings.RulesetUpToDate);
                    break;

                case OnlineRulesetState.UpdateAvailable:
                    addDescription(currentVersion == null
                        ? RulesetSettingsStrings.UnknownVersionPlaceholder
                        : RulesetSettingsStrings.RulesetVersion(currentVersion));

                    addUpdateHighlight(separator);
                    addUpdateHighlight(RulesetSettingsStrings.RulesetVersion(latestVersion!));
                    addUpdateHighlight(separator);
                    addUpdateHighlight(RulesetSettingsStrings.RulesetUpdateAvailable);
                    break;
            }
        }

        private void openDownloadPage()
        {
            if (versionInfo?.DownloadUrl is not string url)
                return;

            game?.OpenUrlExternally(url, LinkWarnMode.NeverWarn);
        }

        private static bool isServerSideVariant(string shortName)
            => shortName.Equals(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, StringComparison.OrdinalIgnoreCase)
               || shortName.Equals(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, StringComparison.OrdinalIgnoreCase)
               || shortName.Equals(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, StringComparison.OrdinalIgnoreCase)
               || shortName.Equals(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, StringComparison.OrdinalIgnoreCase);

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        private void updateState()
            => background.VisualStyle = IsHovered ? VisualStyle.Hovered : VisualStyle.Normal;
    }
}
