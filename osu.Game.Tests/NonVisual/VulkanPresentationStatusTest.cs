// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Rendering;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.Graphics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class VulkanPresentationStatusTest
    {
        [TestCase(VulkanPresentMode.Immediate, "Immediate")]
        [TestCase(VulkanPresentMode.Mailbox, "Mailbox")]
        [TestCase(VulkanPresentMode.Fifo, "FIFO")]
        [TestCase(VulkanPresentMode.FifoRelaxed, "FIFO Relaxed")]
        public void TestPresentModeDescription(VulkanPresentMode mode, string expectedSubstring)
        {
            var status = new VulkanPresentationStatus(mode, true, false, 2);
            string text = VulkanPresentationNote.CreateNote(status)!.Text.ToString();
            Assert.That(text, Does.Contain(expectedSubstring));
            Assert.That(text, Does.Contain(GraphicsSettingsStrings.ExclusiveFullscreenInactive.ToString()));
        }

        [Test]
        public void TestUnknownStateIsNotTreatedAsDisabled()
        {
            var status = new VulkanPresentationStatus(null, null, null, null);
            Assert.That(status.PresentMode, Is.Null);
            Assert.That(status.ExclusiveFullscreen, Is.Null);
            Assert.That(VulkanPresentationNote.CreateNote(status)!.Text.ToString(), Does.Contain(GraphicsSettingsStrings.PresentationUnknown.ToString()));
        }

        [Test]
        public void TestOtherRenderersHaveNoNote()
        {
            Assert.That(VulkanPresentationNote.CreateNote(null), Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestExclusiveFullscreenStatus(bool exclusive)
        {
            var status = new VulkanPresentationStatus(VulkanPresentMode.Immediate, true, exclusive, 2);
            string text = VulkanPresentationNote.CreateNote(status)!.Text.ToString();
            Assert.That(text, Does.Contain((exclusive ? GraphicsSettingsStrings.ExclusiveFullscreenActive : GraphicsSettingsStrings.ExclusiveFullscreenInactive).ToString()));
        }
    }
}
