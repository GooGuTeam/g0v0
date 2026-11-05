// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Runs the entire export on one dedicated thread, including native finalisation.
    /// </summary>
    public sealed class ReplayExportJob
    {
        private readonly ReplayExportTimeline timeline;
        private int started;

        public ReplayExportJob(ReplayExportTimeline timeline)
        {
            this.timeline = timeline;
        }

        public Task RunAsync(Func<CancellationToken, Task<IReplayExportSession>> createSession, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
                throw new InvalidOperationException(@"An export job can only be run once.");
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            new Thread(() =>
            {
                IReplayExportSession? session = null;
                Exception? failure = null;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    session = createSession(cancellationToken).GetAwaiter().GetResult();
                    progress?.Report(0);
                    while (!timeline.IsComplete)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        session.EncodeFrameAsync(timeline.FrameIndex, timeline.Timestamp, timeline.AudioStartSample,
                            timeline.AudioSampleCount, cancellationToken).GetAwaiter().GetResult();
                        timeline.AcknowledgeFrame(timeline.FrameIndex);
                        progress?.Report(timeline.Progress * 0.99);
                    }
                    session.CompleteAsync(cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    try { session?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                    catch (Exception ex) { failure ??= ex; }
                }
                if (failure is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else if (failure != null)
                    completion.TrySetException(failure);
                else
                {
                    progress?.Report(1);
                    completion.TrySetResult();
                }
            })
            { IsBackground = true, Name = @"Replay video export" }.Start();
            return completion.Task;
        }
    }
}
