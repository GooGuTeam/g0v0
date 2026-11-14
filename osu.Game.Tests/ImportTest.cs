// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests
{
    public abstract partial class ImportTest
    {
        private readonly List<Task> hostTasks = new List<Task>();

        [TearDown]
        public void EnsureHostsCompletedSuccessfully()
        {
            try
            {
                Task.WhenAll(hostTasks).GetAwaiter().GetResult();
            }
            finally
            {
                hostTasks.Clear();
            }
        }

        protected virtual TestOsuGameBase LoadOsuIntoHost(GameHost host, bool withBeatmap = false)
        {
            var osu = new TestOsuGameBase(withBeatmap);
            Task hostTask = Task.Factory.StartNew(() => host.Run(osu), TaskCreationOptions.LongRunning);

            // Hosts are exited and disposed by the caller. Join their tasks in teardown so late failures
            // are reported on the owning test, rather than from a discarded, faulting continuation.
            hostTasks.Add(hostTask);

            waitForOrAssert(() => osu.IsLoaded, @"g0v0! failed to start in a reasonable amount of time", hostTask: hostTask);

            bool ready = false;
            // wait for two update frames to be executed. this ensures that all components have had a change to run LoadComplete and hopefully avoid
            // database access (GlobalActionContainer is one to do this).
            host.UpdateThread.Scheduler.Add(() => host.UpdateThread.Scheduler.Add(() => ready = true));

            waitForOrAssert(() => ready, @"g0v0! failed to start in a reasonable amount of time", hostTask: hostTask);

            return osu;
        }

        private void waitForOrAssert(Func<bool> result, string failureMessage, int timeout = 60000, Task? hostTask = null)
        {
            Task task = Task.Run(() =>
            {
                while (!result())
                {
                    // the condition can never become true if the host has already died, so fail with the real
                    // exception instead of blocking for the full timeout.
                    if (hostTask is { IsFaulted: true })
                        hostTask.GetAwaiter().GetResult();

                    Thread.Sleep(200);
                }
            });

            ClassicAssert.True(task.Wait(timeout), failureMessage);
        }

        public partial class TestOsuGameBase : OsuGameBase
        {
            public RealmAccess Realm => Dependencies.Get<RealmAccess>();
            public new IAPIProvider API => base.API;

            private readonly bool withBeatmap;

            public TestOsuGameBase(bool withBeatmap)
            {
                this.withBeatmap = withBeatmap;

                base.API = new DummyAPIAccess();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Beatmap must be imported before the collection manager is loaded.
                if (withBeatmap)
                    BeatmapManager.Import(TestResources.GetTestBeatmapForImport()).WaitSafely();

                // the logic for setting the initial ruleset exists in OsuGame rather than OsuGameBase.
                // the ruleset bindable is not meant to be nullable, so assign any ruleset in here.
                Ruleset.Value = RulesetStore.AvailableRulesets.First();
            }
        }
    }
}
