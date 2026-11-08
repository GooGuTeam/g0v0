// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public partial class LegacyModSelectOverlay : OsuFocusedOverlayContainer
    {
        public readonly Bindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();
        public readonly Bindable<WorkingBeatmap> Beatmap = new Bindable<WorkingBeatmap>();
        public readonly Bindable<IReadOnlyList<Mod>> SelectedMods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        protected override string? PopInSampleName => @"UI/overlay-pop-in";
        protected override string? PopOutSampleName => @"UI/overlay-pop-out";

        private Container canvas = null!;
        private OsuSpriteText multiplierText = null!;
        private Container buttonsContainer = null!;
        private LegacyDialogButton resetButton = null!;
        private LegacyDialogButton closeButton = null!;

        private readonly List<LegacyModButton> buttons = new List<LegacyModButton>();

        private Sample? checkOffSample;

        public LegacyModSelectOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            checkOffSample = audio.Samples.Get(@"UI/check-off");

            InternalChildren = new Drawable[]
            {
                new ClickableContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Action = () => Hide(),
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0, 0, 0, 0.85f),
                    }
                },
                new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(640, 480),
                    RelativeSizeAxes = Axes.Both,
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Child = canvas = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Children = new Drawable[]
                        {
                            new OsuTextFlowContainer(t => t.Font = OsuFont.Inter.With(size: 22))
                            {
                                Text = UserInterfaceStrings.LegacyModsDescription,
                                Position = new Vector2(5, 3),
                                RelativeSizeAxes = Axes.X,
                                Width = 0.98f,
                                AutoSizeAxes = Axes.Y,
                                Colour = Color4.White,
                            },
                            multiplierText = new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Position = new Vector2(0, 70),
                                Font = OsuFont.Inter.With(size: 28),
                                Colour = Color4.White,
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Position = new Vector2(24, 138),
                                Origin = Anchor.CentreLeft,
                                Text = UserInterfaceStrings.DifficultyReduction,
                                Font = OsuFont.Inter.With(size: 22),
                                Colour = Color4.LimeGreen,
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Position = new Vector2(24, 198),
                                Origin = Anchor.CentreLeft,
                                Text = UserInterfaceStrings.DifficultyIncrease,
                                Font = OsuFont.Inter.With(size: 22),
                                Colour = Color4.OrangeRed,
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Position = new Vector2(24, 258),
                                Origin = Anchor.CentreLeft,
                                Text = UserInterfaceStrings.SpecialMods,
                                Font = OsuFont.Inter.With(size: 22),
                                Colour = Color4.White,
                                Shadow = true,
                            },
                            buttonsContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            resetButton = new LegacyDialogButton(UserInterfaceStrings.ResetAllMods, Color4.OrangeRed, ResetAllMods)
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Y = 320,
                            },
                            closeButton = new LegacyDialogButton(UserInterfaceStrings.CloseDialog, Color4.Gray, () => Hide())
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Y = 370,
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Ruleset.BindValueChanged(_ => rebuildButtons(), true);

            // the mod icons come from the skin, so a skin change rebuilds them (scheduled: SourceChanged may fire off the update thread).
            skinSource.SourceChanged += onSkinChanged;
            SelectedMods.BindValueChanged(_ => refreshButtons(), true);
            Beatmap.BindValueChanged(_ => updateMultiplier(), true);
        }

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        private void onSkinChanged() => Schedule(rebuildButtons);

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource.IsNotNull())
                skinSource.SourceChanged -= onSkinChanged;

            base.Dispose(isDisposing);
        }

        private void rebuildButtons()
        {
            buttonsContainer.Clear();
            buttons.Clear();

            if (Ruleset.Value == null)
                return;

            var allMods = Ruleset.Value.CreateInstance().CreateAllMods().ToList();

            Mod? find(string acronym) => allMods.FirstOrDefault(m => string.Equals(m.Acronym, acronym, StringComparison.OrdinalIgnoreCase));

            Vector2 pos(int row, int col) => new Vector2(240 + col * 66, 138 + row * 60);

            void addSingle(int row, int col, string texture, string acronym)
            {
                var mod = find(acronym);
                if (mod == null)
                    return;

                var btn = new LegacyModButton(pos(row, col), (texture, mod))
                {
                    OnCycle = onButtonCycled,
                };
                buttons.Add(btn);
                buttonsContainer.Add(btn);
            }

            void addMulti(int row, int col, params (string texture, string acronym)[] states)
            {
                var validStates = states
                                  .Select(s => (s.texture, mod: find(s.acronym)))
                                  .Where(s => s.mod != null)
                                  .Select(s => (s.texture, s.mod!))
                                  .ToArray();

                if (validStates.Length == 0)
                    return;

                var btn = new LegacyModButton(pos(row, col), validStates)
                {
                    OnCycle = onButtonCycled,
                };
                buttons.Add(btn);
                buttonsContainer.Add(btn);
            }

            // Row 0: Reduction (stable: Easy, NoFail, HalfTime)
            addSingle(0, 0, "selection-mod-easy", "EZ");
            addSingle(0, 1, "selection-mod-nofail", "NF");
            addSingle(0, 2, "selection-mod-halftime", "HT");

            // Row 1: Increase (stable: HardRock, SuddenDeath/Perfect, DoubleTime/Nightcore, Hidden, Flashlight)
            addSingle(1, 0, "selection-mod-hardrock", "HR");
            addMulti(1, 1, ("selection-mod-suddendeath", "SD"), ("selection-mod-perfect", "PF"));
            addMulti(1, 2, ("selection-mod-doubletime", "DT"), ("selection-mod-nightcore", "NC"));

            string rulesetName = Ruleset.Value.ShortName;

            if (string.Equals(rulesetName, "mania", StringComparison.OrdinalIgnoreCase))
            {
                addMulti(1, 3, ("selection-mod-fadein", "FI"), ("selection-mod-hidden", "HD"));
            }
            else
            {
                addSingle(1, 3, "selection-mod-hidden", "HD");
            }

            addSingle(1, 4, "selection-mod-flashlight", "FL");

            // Row 2: Special (stable: Relax, Autopilot, SpunOut, Auto/Cinema, ScoreV2)
            if (string.Equals(rulesetName, "osu", StringComparison.OrdinalIgnoreCase))
            {
                addSingle(2, 0, "selection-mod-relax", "RX");
                addSingle(2, 1, "selection-mod-relax2", "AP");
                addSingle(2, 2, "selection-mod-spunout", "SO");
                addMulti(2, 3, ("selection-mod-autoplay", "AT"), ("selection-mod-cinema", "CN"));
                addSingle(2, 4, "selection-mod-scorev2", "SV2");
            }
            else if (string.Equals(rulesetName, "mania", StringComparison.OrdinalIgnoreCase))
            {
                addMulti(2, 0,
                    ("selection-mod-key4", "4K"),
                    ("selection-mod-key5", "5K"),
                    ("selection-mod-key6", "6K"),
                    ("selection-mod-key7", "7K"),
                    ("selection-mod-key8", "8K"),
                    ("selection-mod-key9", "9K"),
                    ("selection-mod-key1", "1K"),
                    ("selection-mod-key2", "2K"),
                    ("selection-mod-key3", "3K"));
                addSingle(2, 1, "selection-mod-keycoop", "DS");
                addSingle(2, 2, "selection-mod-random", "RD");
                addMulti(2, 3, ("selection-mod-autoplay", "AT"), ("selection-mod-cinema", "CN"));
                addSingle(2, 4, "selection-mod-scorev2", "SV2");
            }
            else
            {
                addSingle(2, 0, "selection-mod-relax", "RX");
                addMulti(2, 1, ("selection-mod-autoplay", "AT"), ("selection-mod-cinema", "CN"));
                addSingle(2, 2, "selection-mod-scorev2", "SV2");
            }

            refreshButtons();
        }

        private void refreshButtons()
        {
            foreach (var btn in buttons)
            {
                var activeMod = SelectedMods.Value.FirstOrDefault(m => btn.AvailableMods.Any(am => am.GetType() == m.GetType()));
                btn.UpdateState(activeMod);
            }

            updateMultiplier();
        }

        private void updateMultiplier()
        {
            if (Ruleset.Value == null || Beatmap.Value?.BeatmapInfo?.Difficulty == null)
            {
                multiplierText.Text = UserInterfaceStrings.LegacyScoreMultiplier("1.00x");
                multiplierText.Colour = Color4.White;
                return;
            }

            var rulesetInstance = Ruleset.Value.CreateInstance();
            var context = new ScoreMultiplierContext(Beatmap.Value.BeatmapInfo.Difficulty);
            var calculator = rulesetInstance.CreateScoreMultiplierCalculator(context);
            double multiplier = calculator?.CalculateFor(SelectedMods.Value) ?? 1.0;

            multiplierText.Text = UserInterfaceStrings.LegacyScoreMultiplier(ModUtils.FormatScoreMultiplier(multiplier).ToString());

            if (multiplier > 1.0001)
                multiplierText.FadeColour(Color4.GreenYellow, 200, Easing.OutQuint);
            else if (multiplier < 0.9999)
                multiplierText.FadeColour(Color4.OrangeRed, 200, Easing.OutQuint);
            else
                multiplierText.FadeColour(Color4.White, 200, Easing.OutQuint);
        }

        private void onButtonCycled(LegacyModButton button, bool reverse)
        {
            int currentIndex = -1;

            for (int i = 0; i < button.AvailableMods.Count; i++)
            {
                if (SelectedMods.Value.Any(m => m.GetType() == button.AvailableMods[i].GetType()))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex;

            if (reverse)
            {
                nextIndex = currentIndex == -1 ? button.AvailableMods.Count - 1 : currentIndex - 1;
            }
            else
            {
                nextIndex = currentIndex + 1;
                if (nextIndex >= button.AvailableMods.Count)
                    nextIndex = -1;
            }

            var currentList = SelectedMods.Value.ToList();
            currentList.RemoveAll(m => button.AvailableMods.Any(am => am.GetType() == m.GetType()));

            if (nextIndex >= 0)
            {
                var modToAdd = button.AvailableMods[nextIndex];
                var candidateList = currentList.Append(modToAdd).ToList();

                if (!ModUtils.CheckCompatibleSet(candidateList, out var invalidMods))
                {
                    candidateList = candidateList.Except(invalidMods).ToList();
                    candidateList.Add(modToAdd);
                }

                currentList = candidateList;
            }

            button.UpdateState(nextIndex >= 0 ? button.AvailableMods[nextIndex] : null, playSound: true);
            SelectedMods.Value = currentList;
        }

        private void setModByAcronym(string acronym)
        {
            var button = buttons.FirstOrDefault(b => b.AvailableMods.Any(m => string.Equals(m.Acronym, acronym, StringComparison.OrdinalIgnoreCase)));
            if (button == null)
                return;

            var mod = button.AvailableMods.FirstOrDefault(m => string.Equals(m.Acronym, acronym, StringComparison.OrdinalIgnoreCase));
            if (mod == null)
                return;

            bool isAlreadyActive = SelectedMods.Value.Any(m => m.GetType() == mod.GetType());
            var currentList = SelectedMods.Value.ToList();

            currentList.RemoveAll(m => button.AvailableMods.Any(am => am.GetType() == m.GetType()));

            if (!isAlreadyActive)
            {
                var candidateList = currentList.Append(mod).ToList();

                if (!ModUtils.CheckCompatibleSet(candidateList, out var invalidMods))
                {
                    candidateList = candidateList.Except(invalidMods).ToList();
                    candidateList.Add(mod);
                }

                currentList = candidateList;
            }

            button.UpdateState(isAlreadyActive ? null : mod, playSound: true);
            SelectedMods.Value = currentList;
        }

        private void cycleButtonByAcronym(string acronym)
        {
            var button = buttons.FirstOrDefault(b => b.AvailableMods.Any(m => string.Equals(m.Acronym, acronym, StringComparison.OrdinalIgnoreCase)));
            if (button != null)
                onButtonCycled(button, false);
        }

        public void ResetAllMods()
        {
            if (SelectedMods.Value.Count > 0)
            {
                checkOffSample?.Play();
                SelectedMods.Value = Array.Empty<Mod>();
            }
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.ToggleModSelection || e.Action == GlobalAction.Back || e.Action == GlobalAction.Select)
            {
                if (!e.Repeat)
                    Hide();
                return true;
            }

            return base.OnPressed(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return false;

            // stable's mod hotkeys are bare keys; leave modified combinations (e.g. Ctrl+F1) to their own bindings.
            if (e.ControlPressed || e.AltPressed || e.SuperPressed)
                return base.OnKeyDown(e);

            bool mania = string.Equals(Ruleset.Value?.ShortName, "mania", StringComparison.OrdinalIgnoreCase);
            if (mania && e.Key >= Key.Number3 && e.Key <= Key.Number9)
            {
                setModByAcronym($"{(int)e.Key - (int)Key.Number3 + 3}K");
                return true;
            }

            switch (e.Key)
            {
                case Key.Q:
                    setModByAcronym("EZ");
                    return true;

                case Key.W:
                    setModByAcronym("NF");
                    return true;

                case Key.E:
                    setModByAcronym("HT");
                    return true;

                case Key.A:
                    setModByAcronym("HR");
                    return true;

                case Key.S:
                    if (e.ShiftPressed)
                        setModByAcronym("PF");
                    else
                        cycleButtonByAcronym("SD");
                    return true;

                case Key.D:
                    if (e.ShiftPressed)
                        setModByAcronym("NC");
                    else
                        cycleButtonByAcronym("DT");
                    return true;

                case Key.F:
                    if (string.Equals(Ruleset.Value?.ShortName, "mania", StringComparison.OrdinalIgnoreCase) && !e.ShiftPressed)
                        setModByAcronym("FI");
                    else
                        setModByAcronym("HD");
                    return true;

                case Key.G:
                    setModByAcronym("FL");
                    return true;

                case Key.Z:
                    if (mania)
                        cycleButtonByAcronym("4K");
                    else
                        setModByAcronym("RX");
                    return true;

                case Key.X:
                    setModByAcronym(mania ? "RD" : "AP");
                    return true;

                case Key.C:
                    setModByAcronym("SO");
                    return true;

                case Key.V:
                    if (e.ShiftPressed)
                        setModByAcronym("CN");
                    else
                        cycleButtonByAcronym("AT");
                    return true;

                case Key.Number1:
                case Key.Keypad1:
                    ResetAllMods();
                    return true;

                case Key.Number2:
                case Key.Keypad2:
                    Hide();
                    return true;

                case Key.BackSpace:
                case Key.Delete:
                    ResetAllMods();
                    return true;

                case Key.Escape:
                    Hide();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void PopIn()
        {
            this.FadeIn(150, Easing.OutQuint);
            canvas.ScaleTo(1f, 250, Easing.OutBack);

            resetButton.MoveToX(-30).MoveToX(0, 300, Easing.OutQuint);
            closeButton.MoveToX(30).MoveToX(0, 300, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.FadeOut(120, Easing.InQuint);
            canvas.ScaleTo(0.96f, 120, Easing.InQuint);
        }
    }
}
