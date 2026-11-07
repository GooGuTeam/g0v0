// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering;
using osu.Game.Overlays.Settings.Rendering.Stable;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Audio;
using osu.Game.Overlays.Settings.Sections.Input;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Renders the same settings entries with the lazer and the osu!stable renderers side by side.
    /// Both columns are bound to the same bindables, so changing one must update the other.
    /// </summary>
    public partial class TestSceneSettingsRenderers : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly BindableBool toggle = new BindableBool(true) { Default = true };
        private readonly BindableBool disabledToggle = new BindableBool { Disabled = true };
        private readonly BindableFloat slider = new BindableFloat(0.5f) { MinValue = 0, MaxValue = 1, Precision = 0.01f, Default = 0.5f };
        private readonly Bindable<SampleMode> mode = new Bindable<SampleMode>();
        private readonly Bindable<string> text = new Bindable<string>(string.Empty);
        private readonly Bindable<SettingsNote.Data?> note = new Bindable<SettingsNote.Data?>();
        private int buttonClicks;

        private Container lazerColumn = null!;
        private Container stableColumn = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset bindables", () =>
            {
                toggle.SetDefault();
                slider.SetDefault();
                mode.SetDefault();
                text.SetDefault();
                note.Value = null;
                buttonClicks = 0;
            });

            AddStep("create columns", () =>
            {
                Child = new OsuContextMenuContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new FillFlowContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(40),
                            Children = new Drawable[]
                            {
                                lazerColumn = createColumn(LazerSettingsRenderer.DEFAULT, colourProvider.Background4),
                                stableColumn = createColumn(StableSettingsRenderer.DEFAULT, new Color4(8, 8, 8, 220)),
                            }
                        }
                    }
                };
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDropdownRefreshPreservesSelection(bool stable)
        {
            DropdownEntry<string> entry = null!;
            int changes = 0;

            AddStep("create dynamic dropdown", () =>
            {
                entry = new DropdownEntry<string>
                {
                    Items = new[] { "first", "selected" },
                    Current = new Bindable<string>("selected"),
                };
                ISettingsRenderer renderer = stable ? StableSettingsRenderer.DEFAULT : LazerSettingsRenderer.DEFAULT;
                stableColumn.Add(entry.CreateDrawable(renderer));
            });
            AddStep("observe selection", () => entry.Current.BindValueChanged(_ => changes++));
            AddStep("replace items", () => entry.SetItems(new[] { "new", "selected", "first" }));
            AddAssert("selection retained without transient changes", () => entry.Current.Value == "selected" && changes == 0);
            AddStep("refresh unchanged identities", () => entry.SetItems(new[] { "new", "selected", "first" }));
            AddAssert("refresh does not change selection", () => entry.Current.Value == "selected" && changes == 0);
        }

        [Test]
        public void TestSliderTooltipTracksDisabledState()
        {
            var value = new BindableFloat(0.5f) { MinValue = 0, MaxValue = 1 };
            StableSliderBar<float> control = null!;
            AddStep("create conditional tooltip", () => stableColumn.Add(control = new StableSliderBar<float>(v => value.Disabled ? "disabled" : v.ToString())
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Current = value,
                TransferValueOnCommit = true,
            }));
            AddAssert("numeric tooltip", () => control.TooltipContent.Equals(value.Value.ToString()));
            AddStep("disable without changing value", () => value.Disabled = true);
            AddAssert("disabled tooltip", () => control.TooltipContent.Equals("disabled"));
            AddStep("enable without changing value", () => value.Disabled = false);
            AddAssert("numeric tooltip restored", () => control.TooltipContent.Equals(value.Value.ToString()));
        }

        [Test]
        public void TestAllEntryTypes()
        {
            AddAssert("lazer uses form controls", () => lazerColumn.ChildrenOfType<FormCheckBox>().Any() && !lazerColumn.ChildrenOfType<StableSettingsItem>().Any());
            AddAssert("stable uses stable controls", () => stableColumn.ChildrenOfType<StableSettingsItem>().Count() == 7 && !stableColumn.ChildrenOfType<FormCheckBox>().Any());
            AddStep("modify values", () =>
            {
                toggle.Value = false;
                slider.Value = 0.8f;
                mode.Value = SampleMode.Second;
                text.Value = @"hello";
                note.Value = new SettingsNote.Data(@"This note is shown in both styles.", SettingsNote.Type.Warning);
            });
        }

        [Test]
        public void TestStableCheckboxToggles()
        {
            AddStep("click stable checkbox", () =>
            {
                InputManager.MoveMouseTo(stableColumn.ChildrenOfType<StableCheckbox>().First());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("bindable toggled", () => !toggle.Value);
            AddAssert("lazer checkbox follows", () => !lazerColumn.ChildrenOfType<FormCheckBox>().First().Current.Value);
        }

        [Test]
        public void TestStableDefaultIndicatorRestoresDefault()
        {
            AddStep("modify slider", () => slider.Value = 0.9f);
            AddStep("click indicator", () =>
            {
                var item = stableColumn.ChildrenOfType<StableSlider<float>>().First();
                InputManager.MoveMouseTo(item.ScreenSpaceDrawQuad.TopLeft + new Vector2(StableOptionsMetrics.INDICATOR_X + 4, item.DrawHeight / 2));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("slider back at default", () => slider.IsDefault);
        }

        [Test]
        public void TestStableButton()
        {
            AddStep("click stable button", () =>
            {
                InputManager.MoveMouseTo(stableColumn.ChildrenOfType<StableButton>().First());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("action invoked", () => buttonClicks == 1);
        }

        [Test]
        public void TestStableOverlay()
        {
            SettingsOverlay overlay = null!;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Any());
            AddAssert("stable renderer used", () => overlay.Renderer is StableSettingsRenderer);
            AddAssert("no lazer checkboxes in subsections", () => !overlay.SectionsContainer.ChildrenOfType<SettingsItemV2>().Any(i => i.Control is FormCheckBox));
            AddUntilStep("sidebar buttons loaded", () => overlay.ChildrenOfType<StableSidebarButton>().Any());
            AddAssert("no ruleset tabs", () => overlay.ChildrenOfType<StableSidebarButton>().All(b => b.Section is not RulesetSection));
            AddAssert("stable back button shown", () => overlay.ChildrenOfType<StableBackButton>().Any(b => b.FindClosestParent<SettingsPanel>() == overlay));
        }

        [Test]
        public void TestStableBackButton()
        {
            SettingsOverlay overlay = null!;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Any());
            AddStep("hover back button", () => hoverBackButton(overlay));
            AddWaitStep("wait for expand", 10);
            AddStep("click back button", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestStableDropdownFloats()
        {
            SettingsOverlay overlay = null!;
            Drawable header = null!;
            Drawable next = null!;
            float nextY = 0;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Count(i => i.IsLoaded) > 5);
            AddWaitStep("wait for layout", 5);
            AddStep("find the first dropdown", () =>
            {
                var dropdown = overlay.ChildrenOfType<StableSettingsItem>().First(i => i.GetType().Name.StartsWith("StableDropdown", StringComparison.Ordinal));
                header = dropdown.ChildrenOfType<DropdownHeader>().Single();

                var siblings = ((Container<Drawable>)dropdown.Parent!).Children;
                next = siblings[siblings.ToList().IndexOf(dropdown) + 1];
                nextY = next.ScreenSpaceDrawQuad.TopLeft.Y;
            });
            AddStep("open it", () =>
            {
                InputManager.MoveMouseTo(header);
                InputManager.Click(MouseButton.Left);
            });
            AddWaitStep("wait for the list", 10);
            AddAssert("list is open", () => header.Parent!.ChildrenOfType<Menu>().Single().State == MenuState.Open);
            AddAssert("list drawn in the flyout layer", () => header.FindClosestParent<SettingsPanel.SettingsSectionsContainer>()!.FlyoutLayer.Children.Any());
            AddAssert("following option did not move", () => Precision.AlmostEquals(next.ScreenSpaceDrawQuad.TopLeft.Y, nextY, 1));
            AddAssert("options do not overlap", () =>
            {
                var rows = header.Parent!.ChildrenOfType<Menu.DrawableMenuItem>().Take(2).ToArray();
                return rows.Length == 2 && rows[0].DrawHeight > 0 && rows[1].ScreenSpaceDrawQuad.TopLeft.Y >= rows[0].ScreenSpaceDrawQuad.BottomLeft.Y - 1;
            });

            Menu.DrawableMenuItem overNext = null!;

            AddStep("hover the option over the next setting", () =>
            {
                overNext = header.Parent!.ChildrenOfType<Menu.DrawableMenuItem>()
                                 .First(r => next.ScreenSpaceDrawQuad.Contains(r.ScreenSpaceDrawQuad.Centre));
                InputManager.MoveMouseTo(overNext.ScreenSpaceDrawQuad.Centre);
            });
            AddAssert("option takes the hover", () => overNext.IsHovered && !next.IsHovered);

            AddStep("shrink viewport while menu is open", () =>
            {
                var viewport = overlay.SectionsContainer;
                float bottom = viewport.ToLocalSpace(header.ScreenSpaceDrawQuad.BottomLeft).Y + 30;
                viewport.RelativeSizeAxes = Axes.X;
                viewport.Height = bottom;
            });
            AddAssert("menu immediately fits viewport", () =>
            {
                var menu = header.Parent!.ChildrenOfType<Menu>().First();
                var viewport = overlay.SectionsContainer;
                Assert.That(menu.ScreenSpaceDrawQuad.BottomLeft.Y, Is.LessThanOrEqualTo(viewport.ScreenSpaceDrawQuad.BottomLeft.Y + 1), "menu bottom");
                Assert.That(menu.ScreenSpaceDrawQuad.TopLeft.Y, Is.GreaterThanOrEqualTo(viewport.FixedHeader!.ScreenSpaceDrawQuad.BottomLeft.Y - 1), "menu top");
                return true;
            });
            AddWaitStep("allow previous size animation to finish", 3);
            AddAssert("obsolete animation does not restore overflow", () =>
            {
                var menu = header.Parent!.ChildrenOfType<Menu>().First();
                return menu.ScreenSpaceDrawQuad.BottomLeft.Y <= overlay.SectionsContainer.ScreenSpaceDrawQuad.BottomLeft.Y + 1;
            });
        }

        [Test]
        public void TestStableSkinSection() => showStableSetting(o => o.ChildrenOfType<SkinSection>().First());

        [Test]
        public void TestStableInputSection() => showStableSetting(o => o.ChildrenOfType<InputSection>().First());

        [Test]
        public void TestStableAudioSection() => showStableSetting(o => o.ChildrenOfType<AudioOffsetAdjustControl>().First());

        /// <summary>
        /// Opens the stable overlay scrolled to a setting.
        /// </summary>
        private void showStableSetting(Func<SettingsOverlay, Drawable> target)
        {
            SettingsOverlay overlay = null!;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Count(i => i.IsLoaded) > 20);
            AddWaitStep("wait for layout", 5);
            AddStep("scroll to setting", () => overlay.SectionsContainer.ScrollTo(target(overlay)));
            AddWaitStep("wait for scroll", 20);
        }

        [Test]
        public void TestStableToggleableHeader()
        {
            StableToggleableSubsectionHeader header = null!;

            AddStep("create headers", () => Child = new Container
            {
                Width = SettingsPanel.PANEL_WIDTH,
                AutoSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = StableOptionsMetrics.PANEL_BACKGROUND },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            header = new StableToggleableSubsectionHeader("Input device: Tablet", true),
                            new StableToggleableSubsectionHeader("Input device: Mouse", true) { Current = { Value = false } },
                            new StableToggleableSubsectionHeader("Input device: Keyboard", false),
                        },
                    },
                },
            });
            AddUntilStep("headers loaded", () => header.IsLoaded);
            AddWaitStep("wait for layout", 3);
            // the header's layout rectangle includes its top margin, so aim at what is drawn.
            AddStep("hover the first", () => InputManager.MoveMouseTo(header.ScreenSpaceDrawQuad.Centre));
            AddStep("toggle the first off", () => InputManager.Click(MouseButton.Left));
            AddUntilStep("first switched off", () => !header.Current.Value);
            AddStep("toggle back", () => InputManager.Click(MouseButton.Left));
            AddAssert("first switched on", () => header.Current.Value);
        }

        [Test]
        public void TestStableTooltip()
        {
            SettingsOverlay overlay = null!;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Any());
            AddWaitStep("wait for layout", 5);
            AddStep("hover item with hint", () => InputManager.MoveMouseTo(overlay.ChildrenOfType<StableSettingsItem>()
                                                                                  .First(i => i.IsPresent && !LocalisableString.IsNullOrEmpty(i.TooltipContent))));
            AddUntilStep("stable tooltip shown", () => (this.FindClosestParent<TooltipContainer>()?.ChildrenOfType<StableTooltip>() ?? Enumerable.Empty<StableTooltip>()).Any(t => t.State.Value == Visibility.Visible));
            AddWaitStep("wait for fade", 5);
        }

        [Test]
        public void TestStableKeyBindingRows()
        {
            SettingsOverlay overlay = null!;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Any());
            AddStep("open key bindings", () => overlay.ChildrenOfType<KeyBindingPanel>().Single().Show());
            AddUntilStep("rows loaded", () => overlay.ChildrenOfType<KeyBindingRow>().Count(r => r.IsLoaded) > 3);
            AddWaitStep("wait for transition", 10);
            AddAssert("stable headers used", () => overlay.ChildrenOfType<KeyBindingPanel>().Single().ChildrenOfType<StableSubsectionHeader>().Any());
            AddStep("start binding third row", () =>
            {
                var row = overlay.ChildrenOfType<KeyBindingRow>().ElementAt(2);
                InputManager.MoveMouseTo(row.ChildrenOfType<KeyBindingRow.KeyButton>().First());
                // the overlay's held search focus wins over a click here, so focus the row as a click would.
                GetContainingFocusManager()!.ChangeFocus(row);
            });
            AddUntilStep("row focused", () => overlay.ChildrenOfType<KeyBindingRow>().ElementAt(2).HasFocus);
            AddWaitStep("wait for expand", 5);
            AddStep("cancel binding", () => InputManager.Key(Key.Escape));
        }

        [Test]
        public void TestStableSubPanel()
        {
            SettingsOverlay overlay = null!;

            AddStep("enable stable options", () => config.SetValue(OsuSetting.ToriiLegacyOptions, true));
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<StableSettingsItem>().Any());
            AddStep("open key bindings", () => overlay.ChildrenOfType<KeyBindingPanel>().Single().Show());
            AddUntilStep("key bindings loaded", () => overlay.ChildrenOfType<KeyBindingPanel>().Single().ChildrenOfType<SettingsSection>().Any(s => s.IsLoaded));
            AddWaitStep("wait for transition", 10);
            AddAssert("single back button", () => overlay.ChildrenOfType<StableBackButton>().Count() == 1);
            AddStep("click back button", () =>
            {
                hoverBackButton(overlay);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("sub-panel closed", () => overlay.ChildrenOfType<KeyBindingPanel>().Single().State.Value == Visibility.Hidden);
            AddAssert("overlay still open", () => overlay.State.Value == Visibility.Visible);
        }

        /// <summary>
        /// Moves the mouse onto the body of the root panel's back button, near the icon (the button's own bounds extend past its slanted layers).
        /// </summary>
        private void hoverBackButton(SettingsOverlay overlay)
        {
            var button = overlay.ChildrenOfType<StableBackButton>().Single(b => b.FindClosestParent<SettingsPanel>() == overlay);
            InputManager.MoveMouseTo(button.ToScreenSpace(new Vector2(StableOptionsMetrics.BACK_LEFT_CONTRACTED + 10, button.DrawHeight - StableOptionsMetrics.BACK_LAYER_BOTTOM - 10)));
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("restore options style", () => config.SetValue(OsuSetting.ToriiLegacyOptions, false));
        }

        private Container createColumn(ISettingsRenderer renderer, Color4 background) => new Container
        {
            Width = SettingsPanel.PANEL_WIDTH,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = background,
                },
                new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CachedDependencies = new (Type, object)[] { (typeof(ISettingsRenderer), renderer) },
                    Child = new SampleSubsection(this),
                },
            }
        };

        private enum SampleMode
        {
            First,
            Second,
            Third,
        }

        private partial class SampleSubsection : SettingsSubsection
        {
            private readonly TestSceneSettingsRenderers scene;

            protected override LocalisableString Header => @"Sample subsection";

            public SampleSubsection(TestSceneSettingsRenderers scene)
            {
                this.scene = scene;
                Padding = new MarginPadding { Vertical = 10 };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new[]
                {
                    CreateItem(new CheckboxEntry
                    {
                        Caption = @"A checkbox",
                        HintText = @"Hint text, shown in a tooltip",
                        Current = scene.toggle,
                    }),
                    CreateItem(new CheckboxEntry
                    {
                        Caption = @"A disabled checkbox",
                        Current = scene.disabledToggle,
                    }),
                    CreateItem(new SliderEntry<float>
                    {
                        Caption = @"A slider",
                        Current = scene.slider,
                        DisplayAsPercentage = true,
                    }),
                    CreateItem(new EnumDropdownEntry<SampleMode>
                    {
                        Caption = @"An enum dropdown",
                        Current = scene.mode,
                        Note = { BindTarget = scene.note },
                    }),
                    CreateItem(new TextBoxEntry
                    {
                        Caption = @"A text box",
                        PlaceholderText = @"type here",
                        Current = scene.text,
                    }),
                    CreateItem(new ButtonEntry
                    {
                        Caption = @"A button",
                        Action = () => scene.buttonClicks++,
                    }),
                    CreateItem(new ButtonEntry
                    {
                        Caption = @"A dangerous button",
                        Dangerous = true,
                    }),
                };
            }
        }
    }
}
