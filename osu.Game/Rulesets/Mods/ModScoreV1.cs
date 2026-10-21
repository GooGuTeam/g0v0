// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Mods
{
    public class ModScoreV1 : Mod
    {
        public override string Name => "Score V1";
        public override string Acronym => @"SV1";
        public override IconUsage? Icon => null;
        public override ModType Type => ModType.System;
        public override LocalisableString Description => CommonModsStrings.ScoreV1Description;
        public override bool UserPlayable => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;
        public override Type[] IncompatibleMods => new[] { typeof(ModScoreV2) };
    }
}
