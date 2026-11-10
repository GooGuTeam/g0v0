// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Skinning
{
    /// <summary>
    /// osu!stable's in-game mod icons (<c>Player.OnLoadComplete</c>): the skin's <c>selection-mod-*</c> textures fanned out leftwards from the
    /// top-right, each popping in half a second after the last and fading away over five seconds. Replays keep them up, spread further apart.
    /// </summary>
    public partial class LegacyModDisplay : CompositeDrawable, ISerialisableDrawable
    {
        // stable lays its HUD out in 640x480 units, which g0v0 draws at 1.6x.
        private const float scale = 1.6f;

        /// <summary>
        /// Where stable centres the first icon, from the top-right of the screen.
        /// </summary>
        public static readonly Vector2 FIRST_ICON_CENTRE = new Vector2(30, 94) * scale;

        /// <summary>
        /// The largest icon's size, which the display is sized to.
        /// </summary>
        public Vector2 IconSize { get; private set; }

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> selectedRuleset { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private readonly IBindable<IReadOnlyList<Mod>> mods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        private HUDOverlay? hud;

        // gameplay shows the play's mods; elsewhere, such as the skin editor's preview, the selected ones.
        private IReadOnlyList<Mod> currentMods => gameplayState?.Mods ?? mods.Value;

        public LegacyModDisplay()
        {
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // stable shows only its own icons.
            hud = this.FindClosestParent<HUDOverlay>();
            if (hud != null)
                hud.HideStandardModDisplay = true;

            mods.BindTo(selectedMods);
            mods.BindValueChanged(_ => Scheduler.AddOnce(updateIcons), true);
            skin.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => Scheduler.AddOnce(updateIcons);

        private void updateIcons()
        {
            ClearInternal();

            // stable steps 10 units per icon, or 50 in replays, where they stay up.
            bool replay = player is ReplayPlayer;
            float step = (replay ? 50 : 10) * scale;

            var textures = orderedForDisplay(currentMods)
                           .Select(m => getTexture(m))
                           .OfType<Texture>()
                           .ToList();

            IconSize = textures.Count == 0
                ? Vector2.Zero
                : new Vector2(textures.Max(t => t.DisplayWidth), textures.Max(t => t.DisplayHeight));

            Size = textures.Count == 0 ? Vector2.Zero : new Vector2(IconSize.X + (textures.Count - 1) * step, IconSize.Y);

            for (int i = 0; i < textures.Count; i++)
            {
                var icon = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-(IconSize.X / 2 + i * step), IconSize.Y / 2),
                    Texture = textures[i],
                    Alpha = 0,
                    // later icons go behind the earlier ones.
                    Depth = i,
                };

                AddInternal(icon);

                using (icon.BeginDelayedSequence(i * 500))
                {
                    icon.ScaleTo(2).ScaleTo(1, 400, Easing.Out);

                    if (replay)
                        icon.FadeIn(400);
                    else
                    {
                        // stable's fade in and its five second fade out start together, the fade out taking over once the fade in ends.
                        icon.FadeIn(400).Then().FadeTo(1 - 400f / 5000).FadeOut(5000 - 400);
                    }
                }
            }
        }

        /// <summary>
        /// stable lists mods in the order of its <c>Mods</c> flags, leaving out double time under nightcore and sudden death under perfect.
        /// Mods stable doesn't have come last, in the order they were selected.
        /// </summary>
        private IEnumerable<Mod> orderedForDisplay(IReadOnlyList<Mod> modList)
        {
            var ruleset = (gameplayState?.Ruleset ?? selectedRuleset.Value?.CreateInstance());

            LegacyMods legacyFlag(Mod mod)
            {
                try
                {
                    return ruleset?.ConvertToLegacyMods(new[] { mod }) ?? LegacyMods.None;
                }
                catch (NotImplementedException)
                {
                    return LegacyMods.None;
                }
            }

            bool hasNightcore = modList.Any(m => m is ModNightcore);
            bool hasPerfect = modList.Any(m => m is ModPerfect);

            return modList.Where(m => !(hasNightcore && m is ModDoubleTime) && !(hasPerfect && m is ModSuddenDeath))
                          .Select((m, i) => (mod: m, flag: legacyFlag(m), index: i))
                          .OrderBy(e => e.flag == LegacyMods.None ? long.MaxValue : (long)e.flag)
                          .ThenBy(e => e.index)
                          .Select(e => e.mod);
        }

        private Texture? getTexture(Mod mod)
        {
            string name = $@"selection-mod-{stableName(mod)}";
            return skin.GetStableTexture(name) ?? skins.DefaultClassicSkin.GetStableTexture(name);
        }

        /// <summary>
        /// The name stable gives a mod's texture: its class name without the ruleset prefix, lower-cased.
        /// </summary>
        private static string stableName(Mod mod)
        {
            string name = mod.GetType().Name;
            int idx = name.IndexOf(@"Mod", StringComparison.Ordinal);
            name = (idx >= 0 ? name[(idx + 3)..] : name).ToLowerInvariant();

            return name switch
            {
                @"autopilot" => @"relax2",
                @"targetpractice" => @"target",
                @"daycore" => @"halftime",
                _ => name,
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (hud != null)
                hud.HideStandardModDisplay = false;

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }
}
