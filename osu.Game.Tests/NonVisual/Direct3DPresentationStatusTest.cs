// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Rendering;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.Graphics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class Direct3DPresentationStatusTest
    {
        [TestCase(Direct3DPresentationModel.BitBltDiscard, false)]
        [TestCase(Direct3DPresentationModel.BitBltSequential, false)]
        [TestCase(Direct3DPresentationModel.FlipDiscard, true)]
        [TestCase(Direct3DPresentationModel.FlipSequential, true)]
        public void TestFlipModelClassification(Direct3DPresentationModel model, bool flip)
        {
            var status = new Direct3DPresentationStatus(model, false);
            Assert.That(status.UsesFlipModel, Is.EqualTo(flip));
            Assert.That(status.ExclusiveFullscreen, Is.False);
        }

        [Test]
        public void TestUnknownStateIsNotTreatedAsDisabled()
        {
            var status = new Direct3DPresentationStatus(null, null);
            Assert.That(status.UsesFlipModel, Is.Null);
            Assert.That(status.ExclusiveFullscreen, Is.Null);
            Assert.That(Direct3DPresentationNote.CreateNote(status)!.Text.ToString(), Does.Contain(GraphicsSettingsStrings.PresentationUnknown.ToString()));
        }

        [Test]
        public void TestOtherRenderersHaveNoNote()
        {
            Assert.That(Direct3DPresentationNote.CreateNote(null), Is.Null);
        }

        [TestCase(Direct3DPresentationModel.FlipDiscard, "FLIP_DISCARD")]
        [TestCase(Direct3DPresentationModel.FlipSequential, "FLIP_SEQUENTIAL")]
        [TestCase(Direct3DPresentationModel.BitBltDiscard, "DISCARD")]
        [TestCase(Direct3DPresentationModel.BitBltSequential, "SEQUENTIAL")]
        public void TestModelDescription(Direct3DPresentationModel model, string swapEffect)
        {
            string text = Direct3DPresentationNote.CreateNote(new Direct3DPresentationStatus(model, false))!.Text.ToString();
            Assert.That(text, Does.Contain(swapEffect));
            Assert.That(text, Does.Contain(GraphicsSettingsStrings.ExclusiveFullscreenInactive.ToString()));
        }

        [Test]
        public void TestPartialQueryFailure()
        {
            var modelUnknown = new Direct3DPresentationStatus(null, true);
            Assert.That(modelUnknown.UsesFlipModel, Is.Null);
            string text = Direct3DPresentationNote.CreateNote(modelUnknown)!.Text.ToString();
            Assert.That(text, Does.Contain(GraphicsSettingsStrings.Direct3DPresentationModel(GraphicsSettingsStrings.PresentationUnknown).ToString()));
            Assert.That(text, Does.Contain(GraphicsSettingsStrings.Direct3DExclusiveFullscreen(GraphicsSettingsStrings.ExclusiveFullscreenActive).ToString()));

            var fullscreenUnknown = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, null);
            Assert.That(fullscreenUnknown.UsesFlipModel, Is.True);
            text = Direct3DPresentationNote.CreateNote(fullscreenUnknown)!.Text.ToString();
            Assert.That(text, Does.Contain("FLIP_DISCARD"));
            Assert.That(text, Does.Contain(GraphicsSettingsStrings.Direct3DExclusiveFullscreen(GraphicsSettingsStrings.PresentationUnknown).ToString()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestExclusiveFullscreenIndependentFromFlipModel(bool exclusive)
        {
            var status = new Direct3DPresentationStatus(Direct3DPresentationModel.FlipDiscard, exclusive);
            string text = Direct3DPresentationNote.CreateNote(status)!.Text.ToString();
            Assert.That(status.UsesFlipModel, Is.True);
            Assert.That(text, Does.Contain((exclusive ? GraphicsSettingsStrings.ExclusiveFullscreenActive : GraphicsSettingsStrings.ExclusiveFullscreenInactive).ToString()));
        }
    }
}
