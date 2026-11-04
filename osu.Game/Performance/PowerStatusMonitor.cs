// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Utils;

namespace osu.Game.Performance
{
    public partial class PowerStatusMonitor : Component
    {
        [Resolved]
        private BatteryInfo batteryInfo { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        private bool? wasOnBattery;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            checkPowerStatus();
            Scheduler.AddDelayed(checkPowerStatus, 1000, true);
        }

        private void checkPowerStatus()
        {
            bool onBattery = batteryInfo.OnBattery;

            if (onBattery && wasOnBattery != true)
                notifications.Post(new BatteryPowerNotification(wasOnBattery == null));

            wasOnBattery = onBattery;
        }

        private partial class BatteryPowerNotification : SimpleNotification
        {
            public BatteryPowerNotification(bool initialCheck)
            {
                Text = initialCheck ? NotificationsStrings.PowerNotConnected : NotificationsStrings.PowerDisconnected;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Icon = FontAwesome.Solid.Plug;
                IconContent.Colour = colours.YellowDark;
            }
        }
    }
}
