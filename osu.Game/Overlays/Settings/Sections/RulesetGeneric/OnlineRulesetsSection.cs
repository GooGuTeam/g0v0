// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Private;
using osu.Game.Online.API.Requests.Private.Responses;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.RulesetGeneric
{
    /// <summary>
    /// Lists the game modes provided by the server the client is currently connected to, and reports how the locally
    /// installed rulesets compare to the versions the server hosts.
    /// </summary>
    /// <remarks>
    /// This section runs its own request chain and owns a dedicated loading indicator, so the (potentially slow)
    /// online portion never blocks the rest of the settings panel.
    /// </remarks>
    public partial class OnlineRulesetsSection : SettingsSection
    {
        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = FontAwesome.Solid.Globe,
        };

        public override LocalisableString Header => RulesetSettingsStrings.OnlineRulesets;

        public override IEnumerable<LocalisableString> FilterTerms => [@"online", @"server", @"update", @"download", @"ruleset", @"mode"];

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private SettingsNote statusNote = null!;
        private FillFlowContainer rulesetFlow = null!;
        private IconButton refreshButton = null!;
        private OsuTextFlowContainer sourceFlow = null!;

        private bool fetching;

        /// <summary>
        /// The known relax/autopilot variants, paired with the base mode each of them is derived from.
        /// </summary>
        /// <remarks>
        /// The server lists variants as standalone modes; they are folded into the row of their base mode instead of
        /// being displayed separately.
        /// </remarks>
        private static readonly (string BaseMode, string VariantMode)[] known_variants =
        {
            (RulesetInfo.OSU_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_MODE_SHORTNAME),
            (RulesetInfo.OSU_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME),
            (RulesetInfo.TAIKO_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME),
            (RulesetInfo.CATCH_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_MODE_SHORTNAME),
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[]
                        {
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
                                sourceFlow = new OsuTextFlowContainer(t => t.Font = OsuFont.Style.Caption1)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                },
                                refreshButton = new IconButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Icon = FontAwesome.Solid.Sync,
                                    TooltipText = RulesetSettingsStrings.RefreshOnlineRulesets,
                                    Action = fetch,
                                },
                            },
                        },
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            statusNote = new SettingsNote
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                            rulesetFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(5),
                            },
                        },
                    },
                },
            });

            string apiUrl = api.Endpoints.APIUrl;

            if (!string.IsNullOrEmpty(apiUrl))
                sourceFlow.AddText(RulesetSettingsStrings.OnlineRulesetsSource(apiUrl));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            fetch();
        }

        /// <summary>
        /// Retrieves the game modes supported by the server, followed by their version information.
        /// </summary>
        private void fetch()
        {
            if (fetching)
                return;

            fetching = true;
            refreshButton.Enabled.Value = false;

            statusNote.Current.Value = null;
            rulesetFlow.Clear();
            rulesetFlow.Add(createLoadingBox());

            var request = new SupportedRulesetsRequest();

            request.Success += onModesReceived;
            request.Failure += onModesFailed;

            api.Queue(request);
        }

        private void onModesReceived(SupportedRulesetsResponse response)
        {
            var request = new SupportedRulesetVersionsRequest();

            // version information is supplementary; failing to retrieve it should still leave the mode list usable.
            request.Success += versions => onCompleted(response, versions);
            request.Failure += _ => onCompleted(response, null);

            api.Queue(request);
        }

        private void onModesFailed(Exception exception)
        {
            fetching = false;
            refreshButton.Enabled.Value = true;
            rulesetFlow.Clear();

            if (!api.IsLoggedIn)
            {
                setNote(RulesetSettingsStrings.OnlineRulesetsLoginRequired, SettingsNote.Type.Informational);
                return;
            }

            Logger.Log($@"Failed to retrieve ruleset information from the server: {exception}", level: LogLevel.Important);

            setNote(exception is APIException ? RulesetSettingsStrings.OnlineRulesetsUnsupportedServer : RulesetSettingsStrings.OnlineRulesetsFetchFailed,
                SettingsNote.Type.Warning);
        }

        private void onCompleted(SupportedRulesetsResponse modes, SupportedRulesetVersionsResponse? versions)
        {
            fetching = false;
            refreshButton.Enabled.Value = true;

            rulesetFlow.Clear();

            var versionLookup = new Dictionary<string, APIRulesetVersion>(StringComparer.OrdinalIgnoreCase);

            if (versions != null)
            {
                foreach (var version in versions.Rulesets)
                    versionLookup[version.Name] = version;
            }

            var supportedModes = modes.Rulesets.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var mode in modes.Rulesets)
            {
                // relax/autopilot variants are folded into the row of the base mode they are derived from, as long as
                // that base mode was returned as well.
                if (findBaseMode(mode.Name) is string baseMode && supportedModes.Contains(baseMode))
                    continue;

                versionLookup.TryGetValue(mode.Name, out var version);

                rulesetFlow.Add(new OnlineRulesetRow(mode, version, findVariants(mode.Name, supportedModes)));
            }

            if (modes.Rulesets.Length == 0)
                setNote(RulesetSettingsStrings.OnlineRulesetsUnsupportedServer, SettingsNote.Type.Warning);
        }

        private void setNote(LocalisableString text, SettingsNote.Type type)
            => statusNote.Current.Value = new SettingsNote.Data(text, type);

        /// <summary>
        /// Finds the base mode a known relax/autopilot variant is derived from.
        /// </summary>
        /// <param name="shortName">The short name of the mode to look up.</param>
        /// <returns>The short name of the base mode, or <c>null</c> if <paramref name="shortName"/> isn't a known variant.</returns>
        private static string? findBaseMode(string shortName)
        {
            foreach (var (baseMode, variantMode) in known_variants)
            {
                if (string.Equals(variantMode, shortName, StringComparison.OrdinalIgnoreCase))
                    return baseMode;
            }

            return null;
        }

        /// <summary>
        /// Finds the relax/autopilot variants of a base mode which the server also offers.
        /// </summary>
        /// <param name="shortName">The short name of the base mode.</param>
        /// <param name="supportedModes">The short names of every mode the server offers.</param>
        private static string[] findVariants(string shortName, IReadOnlySet<string> supportedModes)
        {
            var variants = new List<string>();

            foreach (var (baseMode, variantMode) in known_variants)
            {
                if (string.Equals(baseMode, shortName, StringComparison.OrdinalIgnoreCase) && supportedModes.Contains(variantMode))
                    variants.Add(variantMode);
            }

            return variants.ToArray();
        }

        /// <summary>
        /// Creates the dedicated loading box shown in place of the mode list while a request is in flight.
        /// </summary>
        private static Drawable createLoadingBox() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new FormControlBackground(),
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Padding = new MarginPadding
                    {
                        Vertical = 8,
                        Left = 9,
                        Right = 9,
                    },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Size = new Vector2(24),
                            Child = new LoadingSpinner
                            {
                                Size = new Vector2(24),
                                State = { Value = Visibility.Visible },
                            },
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.Style.Caption1,
                            Text = RulesetSettingsStrings.OnlineRulesetsFetching,
                        },
                    },
                },
            },
        };
    }
}
