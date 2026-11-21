// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Recording.Live;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class FfmpegLiveCaptureTest
    {
        private static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

        [TestCase("")]
        [TestCase("frame=0\nprogress=continue\n")]
        [TestCase("frame=N/A\nframe=-1\nprogress=end\n")]
        [TestCase("fps=60\nout_time_us=1000000\nprogress=continue\n")]
        public async Task TestStartupRequiresAnOutputFrame(string output)
        {
            using var reader = createReader(output);
            bool published = false;

            await FfmpegLiveCapture.ReadProgressAsync(reader, () => published = true, CancellationToken.None).WaitAsync(timeout, CancellationToken.None);

            Assert.That(published, Is.False);
        }

        [Test]
        public async Task TestProgressAnnouncesOnceAndDrainsThePipe()
        {
            using var reader = createReader("frame=0\nprogress=continue\nframe=1\nprogress=continue\nframe=2\nprogress=end\n");
            int announcements = 0;

            await FfmpegLiveCapture.ReadProgressAsync(reader, () => announcements++, CancellationToken.None).WaitAsync(timeout, CancellationToken.None);

            Assert.That(announcements, Is.EqualTo(1));
            Assert.That(await reader.ReadLineAsync(), Is.Null);
        }

        [Test]
        public async Task TestCancelledReaderDoesNotAnnounceStartup()
        {
            using var reader = createReader("frame=1\nprogress=continue\n");
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            bool published = false;

            await FfmpegLiveCapture.ReadProgressAsync(reader, () => published = true, cancellation.Token).WaitAsync(timeout, CancellationToken.None);

            Assert.That(published, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestInheritedOutputPipeCannotBlockDrain(bool holdStderr)
        {
            using var cancellation = new CancellationTokenSource();
            var pendingTail = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingProgress = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<string> tail = holdStderr ? pendingTail.Task : Task.FromResult(@"Saved diagnostic");
            Task? progress = holdStderr ? null : pendingProgress.Task;

            string result = await FfmpegLiveCapture.DrainOutputAsync(tail, progress, cancellation, TimeSpan.FromMilliseconds(50)).WaitAsync(timeout, CancellationToken.None);

            Assert.That(cancellation.IsCancellationRequested, Is.True);
            Assert.That(result, Is.EqualTo(holdStderr ? string.Empty : @"Saved diagnostic"));
        }

        [Test]
        public async Task TestCompletedDrainPreservesDiagnostics()
        {
            using var cancellation = new CancellationTokenSource();

            string result = await FfmpegLiveCapture.DrainOutputAsync(Task.FromResult(@"Encoder failed"), Task.CompletedTask, cancellation, timeout);

            Assert.That(result, Is.EqualTo(@"Encoder failed"));
            Assert.That(cancellation.IsCancellationRequested, Is.False);
        }

        [Test]
        public void TestReadFailureIsObserved()
        {
            using var cancellation = new CancellationTokenSource();
            var expected = new IOException(@"Broken diagnostic pipe");
            Task drain = FfmpegLiveCapture.DrainOutputAsync(Task.FromException<string>(expected), Task.CompletedTask, cancellation, timeout);

            var actual = Assert.ThrowsAsync<IOException>(async () => await drain.WaitAsync(timeout, CancellationToken.None));

            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public async Task TestDiagnosticTailIsBounded()
        {
            string output = new string('a', 3000) + @"last diagnostic";
            using var reader = createReader(output);

            string tail = await FfmpegLiveCapture.ReadTailAsync(reader, CancellationToken.None).WaitAsync(timeout, CancellationToken.None);

            Assert.That(tail, Is.EqualTo(output[^2048..]));
        }

        private static StreamReader createReader(string content) => new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }
}
