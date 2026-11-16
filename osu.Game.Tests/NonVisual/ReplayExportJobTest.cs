// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Recording.Export;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayExportJobTest
    {
        private static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

        [Test]
        public void TestFinalProgressFailureFaultsCompletion()
        {
            var session = new TestSession();
            var expected = new InvalidOperationException(@"Progress failed.");
            var progress = new CallbackProgress(value =>
            {
                if (value == 1)
                {
                    Assert.That(session.Disposed, Is.True);
                    throw expected;
                }
            });

            Task completion = createJob().RunAsync(_ => Task.FromResult<IReplayExportSession>(session), progress, CancellationToken.None);
            var actual = Assert.ThrowsAsync<InvalidOperationException>(async () => await completion.WaitAsync(timeout, CancellationToken.None));

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(session.Completed, Is.True);
            Assert.That(session.Disposed, Is.True);
        }

        [Test]
        public void TestFinalProgressCancellationSettlesCompletion()
        {
            using var cancellation = new CancellationTokenSource();
            var session = new TestSession();
            var progress = new CallbackProgress(value =>
            {
                if (value == 1)
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                }
            });

            Task completion = createJob().RunAsync(_ => Task.FromResult<IReplayExportSession>(session), progress, cancellation.Token);
            Assert.CatchAsync<OperationCanceledException>(async () => await completion.WaitAsync(timeout, CancellationToken.None));

            Assert.That(completion.IsCanceled, Is.True);
            Assert.That(session.Disposed, Is.True);
        }

        [Test]
        public async Task TestSuccessIsReportedAfterDisposal()
        {
            var session = new TestSession();
            bool reportedCompletion = false;
            var progress = new CallbackProgress(value =>
            {
                if (value == 1)
                {
                    Assert.That(session.Completed, Is.True);
                    Assert.That(session.Disposed, Is.True);
                    reportedCompletion = true;
                }
            });

            await createJob().RunAsync(_ => Task.FromResult<IReplayExportSession>(session), progress, CancellationToken.None).WaitAsync(timeout, CancellationToken.None);

            Assert.That(session.EncodedFrames, Is.EqualTo(1));
            Assert.That(reportedCompletion, Is.True);
        }

        private static ReplayExportJob createJob() => new ReplayExportJob(new ReplayExportTimeline(TimeSpan.FromMilliseconds(10)));

        private sealed class CallbackProgress : IProgress<double>
        {
            private readonly Action<double> report;

            public CallbackProgress(Action<double> report) => this.report = report;

            public void Report(double value) => report(value);
        }

        private sealed class TestSession : IReplayExportSession
        {
            public int EncodedFrames { get; private set; }
            public bool Completed { get; private set; }
            public bool Disposed { get; private set; }

            public Task EncodeFrameAsync(long frameIndex, long timestamp, long audioStartSample, int audioSampleCount, CancellationToken cancellationToken)
            {
                EncodedFrames++;
                return Task.CompletedTask;
            }

            public Task CompleteAsync(CancellationToken cancellationToken)
            {
                Completed = true;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }
}
