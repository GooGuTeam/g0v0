// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    /// <summary>
    /// Displays actual Vulkan presentation diagnostics without offering a setting to change them.
    /// </summary>
    public partial class VulkanPresentationNote : CompositeDrawable, IConditionalFilterable
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly SettingsNote note;
        private VulkanPresentationStatus? lastStatus;

        public IEnumerable<LocalisableString> FilterTerms => new[]
        {
            GraphicsSettingsStrings.Renderer,
            "Vulkan", "VK", "FSE", "Mailbox", "FIFO", "Immediate", "fullscreen",
        };

        private bool matchingFilter = true;

        public bool MatchingFilter
        {
            get => matchingFilter;
            set
            {
                if (matchingFilter == value)
                    return;

                matchingFilter = value;
                Invalidate(Invalidation.Presence);
            }
        }

        public bool FilteringActive { get; set; }

        private readonly BindableBool canBeShown = new BindableBool();
        public IBindable<bool> CanBeShown => canBeShown;

        public override bool IsPresent => base.IsPresent && MatchingFilter;

        public VulkanPresentationNote()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Alpha = 0;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = note = new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateStatus();
        }

        public override bool UpdateSubTree()
        {
            // Update() is skipped for absent drawables. Poll before that check so a hidden note
            // can become present again without reserving space in the settings flow.
            if (IsLoaded)
                updateStatus();

            return base.UpdateSubTree();
        }

        private void updateStatus()
        {
            var status = GetPresentationStatus();
            if (status == lastStatus)
                return;

            lastStatus = status;
            note.Current.Value = CreateNote(status);
            Alpha = status == null ? 0 : 1;
            canBeShown.Value = status != null;
        }

        protected virtual VulkanPresentationStatus? GetPresentationStatus() =>
            (host.Renderer as IVulkanRenderer)?.PresentationStatus;

        internal static SettingsNote.Data? CreateNote(VulkanPresentationStatus? status)
        {
            if (status == null)
                return null;

            LocalisableString mode = status.PresentMode switch
            {
                VulkanPresentMode.Immediate => GraphicsSettingsStrings.VulkanPresentModeImmediate,
                VulkanPresentMode.Mailbox => GraphicsSettingsStrings.VulkanPresentModeMailbox,
                VulkanPresentMode.Fifo => GraphicsSettingsStrings.VulkanPresentModeFifo,
                VulkanPresentMode.FifoRelaxed => GraphicsSettingsStrings.VulkanPresentModeFifoRelaxed,
                _ => GraphicsSettingsStrings.PresentationUnknown,
            };

            LocalisableString fullscreen = status.ExclusiveFullscreen switch
            {
                true => GraphicsSettingsStrings.ExclusiveFullscreenActive,
                false => GraphicsSettingsStrings.ExclusiveFullscreenInactive,
                _ => GraphicsSettingsStrings.PresentationUnknown,
            };

            return new SettingsNote.Data(LocalisableString.Interpolate($"{GraphicsSettingsStrings.VulkanPresentationMode(mode)}\n{GraphicsSettingsStrings.VulkanExclusiveFullscreen(fullscreen)}"), SettingsNote.Type.Informational);
        }
    }
}
