// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit.Native;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Visual.UserInterface;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneEditablePropertyInspector : ThemeComparisonTestScene
    {
        [Cached]
        private EditorBeatmap editorBeatmap { get; } = new EditorBeatmap(new OsuBeatmap
        {
            BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
        });

        public TestSceneEditablePropertyInspector()
            : base(false)
        {
            var objects = new[] { new TestHitObject(), new TestHitObject { StartTime = 1000 } };
            editorBeatmap.AddRange(objects);
            editorBeatmap.SelectedHitObjects.AddRange(objects);
        }

        protected override Drawable CreateContent() => new EditablePropertyInspector { Width = 0.5f };

        [TestCase("Integer", "2147483648")]
        [TestCase("Integer", "-2147483649")]
        [TestCase("Integer", "")]
        [TestCase("Float", "350000000000000000000000000000000000000")]
        [TestCase("Float", "-350000000000000000000000000000000000000")]
        [TestCase("Float", "")]
        public void TestInvalidInputRestoresLatestValue(string property, string invalid)
        {
            AddStep("create inspector", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("wait for fields", () => this.ChildrenOfType<FormNumberBox>().Count() == 2);
            commit("20");
            AddAssert("both properties updated", () => editorBeatmap.HitObjects.All(h => valueOf((TestHitObject)h) == 20));
            commit(invalid);
            AddAssert("properties unchanged", () => editorBeatmap.HitObjects.All(h => valueOf((TestHitObject)h) == 20));
            AddAssert("field restores latest value", () => box().Current.Value, () => Is.EqualTo("20"));

            FormNumberBox box() => this.ChildrenOfType<FormNumberBox>().Single(b => b.Caption.ToString() == property);

            double valueOf(TestHitObject obj) => property == "Integer" ? obj.Integer : obj.Float;

            void commit(string text)
            {
                AddStep($"enter '{text}'", () =>
                {
                    InputManager.MoveMouseTo(box().ChildrenOfType<OsuTextBox>().Single());
                    InputManager.Click(MouseButton.Left);
                    box().Current.Value = text;
                });
                AddStep("commit", () => InputManager.Key(Key.Enter));
            }
        }

        [Test]
        public void TestIntegerLimits()
        {
            AddStep("create inspector", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("wait for fields", () => this.ChildrenOfType<FormNumberBox>().Count() == 2);

            foreach (int value in new[] { int.MinValue, int.MaxValue })
            {
                AddStep($"enter {value}", () =>
                {
                    var box = this.ChildrenOfType<FormNumberBox>().Single(b => b.Caption.ToString() == "Integer");
                    InputManager.MoveMouseTo(box.ChildrenOfType<OsuTextBox>().Single());
                    InputManager.Click(MouseButton.Left);
                    box.Current.Value = value.ToString(CultureInfo.InvariantCulture);
                });
                AddStep("commit", () => InputManager.Key(Key.Enter));
                AddAssert("limit accepted", () => editorBeatmap.HitObjects.Cast<TestHitObject>().All(h => h.Integer == value));
            }
        }

        private class TestHitObject : HitCircle
        {
            [EditableProperty("Integer")]
            public int Integer { get; set; } = 10;

            [EditableProperty("Float")]
            public float Float { get; set; } = 10;
        }
    }
}
