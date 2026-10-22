// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Scoring;

namespace osu.Game.Online
{
    public partial class ScoreDownloadTracker : DownloadTracker<ScoreInfo>
    {
        [Resolved(CanBeNull = true)]
        protected ScoreModelDownloader? Downloader { get; private set; }

        [Resolved]
        private IScoreStore scoreStore { get; set; } = null!;

        private ArchiveDownloadRequest<IScoreInfo>? attachedRequest;

        private IDisposable? storeSubscription;

        /// <summary>
        /// Used to interact with manager classes that don't support interface types. Will eventually be replaced.
        /// </summary>
        private ScoreInfo? downloaderScoreInfo;

        public ScoreDownloadTracker(ScoreInfo trackedItem)
            : base(trackedItem)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Downloader == null)
                return;

            downloaderScoreInfo = new ScoreInfo
            {
                ID = TrackedItem.ID,
                OnlineID = TrackedItem.OnlineID,
                LegacyOnlineID = TrackedItem.LegacyOnlineID
            };

            Downloader.DownloadBegan += downloadBegan;
            Downloader.DownloadFailed += downloadFailed;

            storeSubscription = scoreStore.Subscribe(() => Schedule(updateLocalAvailability));
        }

        private void updateLocalAvailability()
        {
            if (isLocallyAvailable(TrackedItem))
                UpdateState(DownloadState.LocallyAvailable);
            else
            {
                UpdateState(DownloadState.NotDownloaded);
                attachDownload(downloaderScoreInfo == null ? null : Downloader?.GetExistingDownload(downloaderScoreInfo));
            }
        }

        private bool isLocallyAvailable(ScoreInfo score)
            => (score.OnlineID > 0 && scoreStore.FindByOnlineId(score.OnlineID) != null)
               || (score.LegacyOnlineID > 0 && scoreStore.FindByLegacyOnlineId(score.LegacyOnlineID) != null)
               // FindByHash may return a score pending deletion (so imports can reuse the row), which is not locally available.
               || (!string.IsNullOrEmpty(score.Hash) && scoreStore.FindByHash(score.Hash)?.DeletePending == false);

        private void downloadBegan(ArchiveDownloadRequest<IScoreInfo> request) => Schedule(() =>
        {
            if (checkEquality(request.Model, TrackedItem))
                attachDownload(request);
        });

        private void downloadFailed(ArchiveDownloadRequest<IScoreInfo> request) => Schedule(() =>
        {
            if (checkEquality(request.Model, TrackedItem))
                attachDownload(null);
        });

        private void attachDownload(ArchiveDownloadRequest<IScoreInfo>? request)
        {
            if (attachedRequest != null)
            {
                attachedRequest.Failure -= onRequestFailure;
                attachedRequest.DownloadProgressed -= onRequestProgress;
                attachedRequest.Success -= onRequestSuccess;
            }

            attachedRequest = request;

            if (attachedRequest != null)
            {
                if (attachedRequest.Progress == 1)
                {
                    UpdateProgress(1);
                    UpdateState(DownloadState.Importing);
                }
                else
                {
                    UpdateProgress(attachedRequest.Progress);
                    UpdateState(DownloadState.Downloading);

                    attachedRequest.Failure += onRequestFailure;
                    attachedRequest.DownloadProgressed += onRequestProgress;
                    attachedRequest.Success += onRequestSuccess;
                }
            }
            else
            {
                UpdateState(DownloadState.NotDownloaded);
            }
        }

        private void onRequestSuccess(string _) => Schedule(() => UpdateState(DownloadState.Importing));

        private void onRequestProgress(float progress) => Schedule(() => UpdateProgress(progress));

        private void onRequestFailure(Exception e) => Schedule(() => attachDownload(null));

        private bool checkEquality(IScoreInfo x, IScoreInfo y) => x.MatchesOnlineID(y);

        #region Disposal

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            attachDownload(null);

            storeSubscription?.Dispose();

            if (Downloader != null)
            {
                Downloader.DownloadBegan -= downloadBegan;
                Downloader.DownloadFailed -= downloadFailed;
            }
        }

        #endregion
    }
}
