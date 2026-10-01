// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Database;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Overlays.Mods
{
    public partial class DeleteModPresetDialog : DeletionDialog
    {
        private readonly ModPreset modPreset;

        [Resolved]
        private IModPresetStore modPresetStore { get; set; } = null!;

        public DeleteModPresetDialog(ModPreset modPreset)
        {
            this.modPreset = modPreset;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BodyText = modPreset.Name;
            DangerousAction = () => modPresetStore.Delete(modPreset.ID);
        }
    }
}
