// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Online
{
    [HeadlessTest]
    public partial class TestDummyAPIRequestHandling : OsuTestScene
    {
        [Test]
        public void TestGenericRequestHandling()
        {
            AddStep("register request handling", () => ((DummyAPIAccess)API).AddHandler(req =>
            {
                switch (req)
                {
                    case CommentVoteRequest cRequest:
                        cRequest.TriggerSuccess(new CommentBundle());
                        return true;
                }

                return false;
            }));

            CommentVoteRequest request = null;
            CommentBundle response = null;

            AddStep("fire request", () =>
            {
                response = null;
                request = new CommentVoteRequest(1, CommentVoteAction.Vote);
                request.Success += res => response = res;
                API.Queue(request);
            });

            AddAssert("response event fired", () => response != null);

            AddAssert("request has response", () => request.Response == response);
        }

        [Test]
        public void TestQueueRequestHandling()
        {
            registerHandler();

            LeaveChannelRequest request;
            bool gotResponse = false;

            AddStep("fire request", () =>
            {
                gotResponse = false;
                request = new LeaveChannelRequest(new Channel());
                request.Success += () => gotResponse = true;
                API.Queue(request);
            });

            AddAssert("response event fired", () => gotResponse);
        }

        [Test]
        public void TestPerformRequestHandling()
        {
            registerHandler();

            LeaveChannelRequest request;
            bool gotResponse = false;

            AddStep("fire request", () =>
            {
                gotResponse = false;
                request = new LeaveChannelRequest(new Channel());
                request.Success += () => gotResponse = true;
                API.Perform(request);
            });

            AddAssert("response event fired", () => gotResponse);
        }

        [Test]
        public void TestPerformAsyncRequestHandling()
        {
            registerHandler();

            LeaveChannelRequest request;
            bool gotResponse = false;

            AddStep("fire request", () =>
            {
                gotResponse = false;
                request = new LeaveChannelRequest(new Channel());
                request.Success += () => gotResponse = true;
                API.PerformAsync(request);
            });

            AddAssert("response event fired", () => gotResponse);
        }

        [Test]
        public void TestHandlersAreComposed()
        {
            AddStep("register handler for one request type", () => ((DummyAPIAccess)API).AddHandler(req =>
            {
                if (req is LeaveChannelRequest leave)
                {
                    leave.TriggerSuccess();
                    return true;
                }

                return false;
            }));

            AddStep("register handler for another request type", () => ((DummyAPIAccess)API).AddHandler(req =>
            {
                if (req is CommentVoteRequest vote)
                {
                    vote.TriggerSuccess(new CommentBundle());
                    return true;
                }

                return false;
            }));

            LeaveChannelRequest leaveRequest = null;
            CommentVoteRequest voteRequest = null;

            AddStep("fire both requests", () =>
            {
                leaveRequest = new LeaveChannelRequest(new Channel());
                API.Queue(leaveRequest);

                voteRequest = new CommentVoteRequest(1, CommentVoteAction.Vote);
                API.Queue(voteRequest);
            });

            AddUntilStep("both requests handled", () => leaveRequest.CompletionState == APIRequestCompletionState.Completed
                                                        && voteRequest.CompletionState == APIRequestCompletionState.Completed);
        }

        [Test]
        public void TestLastRegisteredHandlerTakesPrecedence()
        {
            bool firstHandlerRan = false;

            AddStep("register first handler", () => ((DummyAPIAccess)API).AddHandler(req =>
            {
                if (req is LeaveChannelRequest leave)
                {
                    firstHandlerRan = true;
                    leave.TriggerSuccess();
                    return true;
                }

                return false;
            }));

            AddStep("register overriding handler", () => ((DummyAPIAccess)API).AddHandler(req =>
            {
                if (req is LeaveChannelRequest leave)
                {
                    leave.TriggerSuccess();
                    return true;
                }

                return false;
            }));

            LeaveChannelRequest request = null;

            AddStep("fire request", () =>
            {
                firstHandlerRan = false;
                request = new LeaveChannelRequest(new Channel());
                API.Queue(request);
            });

            AddUntilStep("request handled", () => request.CompletionState == APIRequestCompletionState.Completed);
            AddAssert("overridden handler was not consulted", () => !firstHandlerRan);
        }

        [Test]
        public void TestUnhandledRequestFails()
        {
            LeaveChannelRequest request = null;

            AddStep("remove all handlers", () => ((DummyAPIAccess)API).ClearHandlers());

            AddStep("fire request", () =>
            {
                request = new LeaveChannelRequest(new Channel());
                API.Queue(request);
            });

            AddUntilStep("request failed", () => request.CompletionState == APIRequestCompletionState.Failed);
        }

        private void registerHandler()
        {
            AddStep("register request handling", () => ((DummyAPIAccess)API).AddHandler(req =>
            {
                switch (req)
                {
                    case LeaveChannelRequest cRequest:
                        cRequest.TriggerSuccess();
                        return true;
                }

                return false;
            }));
        }
    }
}
