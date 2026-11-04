// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Desktop.Performance
{
    internal partial class ProcessorPerformanceNotice : Component
    {
        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Logger.Log(osu.Framework.Platform.ProcessorDiagnostics.GetDescription());
            var architecture = RuntimeInformation.ProcessArchitecture;
            Logger.Log($@"Processor diagnostics: process={architecture}, OS={RuntimeInformation.OSArchitecture}, managed SSE4.1={Sse41.IsSupported}, AVX2={Avx2.IsSupported}, AdvSimd={AdvSimd.IsSupported}.");

            // Runtime capability flags also reflect OS support and emulation, not just the physical CPU.
            bool limited = architecture switch
            {
                Architecture.X86 or Architecture.X64 => !Sse41.IsSupported,
                Architecture.Arm64 => !AdvSimd.IsSupported,
                _ => false,
            };

            if (limited)
                notifications.Post(new SimpleNotification { Text = NotificationsStrings.LimitedProcessorAcceleration });
        }
    }
}
