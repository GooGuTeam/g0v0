// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.DebugSettings
{
    public partial class BatchImportSettings : SettingsSubsection
    {
        protected override LocalisableString Header => @"Batch Import";

        private ButtonEntry importBeatmapsButton = null!;
        private ButtonEntry importCollectionsButton = null!;
        private ButtonEntry importScoresButton = null!;
        private ButtonEntry importSkinsButton = null!;

        [BackgroundDependencyLoader]
        private void load(LegacyImportManager? legacyImportManager)
        {
            if (legacyImportManager?.SupportsImportFromStable != true)
                return;

            AddRange(new[]
            {
                CreateItem(importBeatmapsButton = new ButtonEntry
                {
                    Caption = @"Import beatmaps from stable",
                    Action = () =>
                    {
                        importBeatmapsButton.Enabled.Value = false;
                        legacyImportManager.ImportFromStableAsync(StableContent.Beatmaps).ContinueWith(_ => Schedule(() => importBeatmapsButton.Enabled.Value = true));
                    }
                }),
                CreateItem(importSkinsButton = new ButtonEntry
                {
                    Caption = @"Import skins from stable",
                    Action = () =>
                    {
                        importSkinsButton.Enabled.Value = false;
                        legacyImportManager.ImportFromStableAsync(StableContent.Skins).ContinueWith(_ => Schedule(() => importSkinsButton.Enabled.Value = true));
                    }
                }),
                CreateItem(importCollectionsButton = new ButtonEntry
                {
                    Caption = @"Import collections from stable",
                    Action = () =>
                    {
                        importCollectionsButton.Enabled.Value = false;
                        legacyImportManager.ImportFromStableAsync(StableContent.Collections).ContinueWith(_ => Schedule(() => importCollectionsButton.Enabled.Value = true));
                    }
                }),
                CreateItem(importScoresButton = new ButtonEntry
                {
                    Caption = @"Import scores from stable",
                    Action = () =>
                    {
                        importScoresButton.Enabled.Value = false;
                        legacyImportManager.ImportFromStableAsync(StableContent.Scores).ContinueWith(_ => Schedule(() => importScoresButton.Enabled.Value = true));
                    }
                }),
            });
        }
    }
}
