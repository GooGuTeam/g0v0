// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Notifications.WebSocket;
using osu.Game.Tests;

namespace osu.Game.Online.API
{
    public partial class DummyAPIAccess : Component, IAPIProvider
    {
        public const int DUMMY_USER_ID = 1001;

        public DummyLocalUserState LocalUserState { get; } = new DummyLocalUserState();

        public string ScoreProcessingNoticeUrl { get; set; } = string.Empty;

        public Bindable<APIUser> LocalUser => LocalUserState.User;

        ILocalUserState IAPIProvider.LocalUserState => LocalUserState;
        IBindable<APIUser> IAPIProvider.LocalUser => LocalUser;

        public DummyNotificationsClient NotificationsClient { get; } = new DummyNotificationsClient();
        INotificationsClient IAPIProvider.NotificationsClient => NotificationsClient;

        public Language Language => Language.en;

        public string AccessToken => "token";

        public Guid SessionIdentifier { get; } = Guid.NewGuid();

        /// <seealso cref="APIAccess.IsLoggedIn"/>
        public bool IsLoggedIn => State.Value > APIState.Offline;

        public string ProvidedUsername => LocalUser.Value.Username;

        public EndpointConfiguration Endpoints { get; } = new EndpointConfiguration
        {
            APIUrl = "http://localhost",
            WebsiteUrl = "http://localhost",
        };

        public int APIVersion => int.Parse(DateTime.Now.ToString("yyyyMMdd"));

        public Exception? LastLoginError { get; private set; }

        /// <summary>
        /// Registered request handlers, in registration order.
        /// </summary>
        private readonly List<Func<APIRequest, bool>> requestHandlers = new List<Func<APIRequest, bool>>();

        /// <summary>
        /// Registers handling logic for an arbitrary API request.
        /// </summary>
        /// <remarks>
        /// Handlers are consulted in reverse registration order, so the most recently added one takes precedence and
        /// can override a handler installed by shared test infrastructure.
        /// </remarks>
        /// <param name="handler">The handler to add.</param>
        public void AddHandler(Func<APIRequest, bool> handler) => requestHandlers.Add(handler);

        /// <summary>
        /// Removes a previously registered request handler.
        /// </summary>
        /// <param name="handler">The handler to remove.</param>
        /// <returns>Whether the handler was registered and has been removed.</returns>
        public bool RemoveHandler(Func<APIRequest, bool> handler) => requestHandlers.Remove(handler);

        /// <summary>
        /// Removes all registered request handlers.
        /// </summary>
        public void ClearHandlers() => requestHandlers.Clear();

        private bool handleRequest(APIRequest request)
        {
            // a plain reverse loop rather than LINQ so a throwing handler surfaces as itself instead of being buried in an enumerator.
            for (int i = requestHandlers.Count - 1; i >= 0; i--)
            {
                if (requestHandlers[i](request))
                    return true;
            }

            return false;
        }

        private readonly Bindable<APIState> state = new Bindable<APIState>(APIState.Online);

        private bool shouldFailNextLogin;
        private bool stayConnectingNextLogin;

        public SessionVerificationMethod? SessionVerificationMethod { get; set; } = Requests.Responses.SessionVerificationMethod.EmailMessage;

        /// <summary>
        /// The current connectivity state of the API.
        /// </summary>
        public IBindable<APIState> State => state;

        public IBindable<LocalisableString?> UserFacingOutageMessage { get; } = new Bindable<LocalisableString?>();

        public virtual void Queue(APIRequest request)
        {
            request.AttachAPI(this);

            Schedule(() =>
            {
                if (!handleRequest(request))
                {
                    // Noisy so let's silently allow these to succeed.
                    if (request is ChatAckRequest ack)
                    {
                        ack.TriggerSuccess(new ChatAckResponse());
                        return;
                    }

                    request.Fail(new InvalidOperationException($@"{nameof(DummyAPIAccess)} cannot process this request."));
                }
            });
        }

        void IAPIProvider.Schedule(Action action) => base.Schedule(action);

        public void Perform(APIRequest request)
        {
            request.AttachAPI(this);
            handleRequest(request);
        }

        public Task PerformAsync(APIRequest request)
        {
            request.AttachAPI(this);
            handleRequest(request);
            return Task.CompletedTask;
        }

        public void Login(string username, string password)
        {
            state.Value = APIState.Connecting;

            if (stayConnectingNextLogin)
            {
                stayConnectingNextLogin = false;
                return;
            }

            if (shouldFailNextLogin)
            {
                LastLoginError = new APIException("Not powerful enough to login.", new ArgumentException(nameof(shouldFailNextLogin)));

                state.Value = APIState.Offline;
                shouldFailNextLogin = false;
                return;
            }

            LastLoginError = null;
            LocalUser.Value = new APIUser
            {
                Username = username,
                Id = DUMMY_USER_ID,
            };

            if (SessionVerificationMethod != null)
            {
                state.Value = APIState.RequiresSecondFactorAuth;
            }
            else
            {
                onSuccessfulLogin();
                SessionVerificationMethod = null;
            }
        }

        public void AuthenticateSecondFactor(string code)
        {
            var request = new VerifySessionRequest(code);
            request.Failure += e =>
            {
                state.Value = APIState.RequiresSecondFactorAuth;

                if (request.RequiredVerificationMethod != null)
                {
                    SessionVerificationMethod = request.RequiredVerificationMethod;
                    LastLoginError = new APIException($"Must use {SessionVerificationMethod.GetDescription().ToLowerInvariant()} to complete verification.", e);
                }
                else
                {
                    LastLoginError = e;
                }
            };

            state.Value = APIState.Connecting;
            LastLoginError = null;

            request.AttachAPI(this);

            // if no handler installed / handler can't handle verification, just assume that the server would verify for simplicity.
            if (!handleRequest(request))
                onSuccessfulLogin();

            // if a handler did handle this, make sure the verification actually passed.
            if (request.CompletionState == APIRequestCompletionState.Completed)
                onSuccessfulLogin();
        }

        private void onSuccessfulLogin()
        {
            state.Value = APIState.Online;
        }

        public void Logout()
        {
            state.Value = APIState.Offline;
            // must happen after `state.Value` is changed such that subscribers to that bindable's value changes see the correct user.
            // compare: `APIAccess.Logout()`.
            LocalUser.Value = new GuestUser();
        }

        public void UpdateLocalFriends()
        {
        }

        public void UpdateLocalBlocks()
        {
        }

        public IHubClientConnector? GetHubConnector(string clientName, string endpoint) => null;

        public IChatClient GetChatClient() => new TestChatClientConnector(this);

        public RegistrationRequest.RegistrationRequestErrors? CreateAccount(string email, string username, string password)
        {
            Thread.Sleep(200);
            return null;
        }

        public void SetState(APIState newState) => state.Value = newState;

        /// <summary>
        /// Skip 2FA requirement for next login.
        /// </summary>
        public void SkipSecondFactor() => SessionVerificationMethod = null;

        /// <summary>
        /// During the next simulated login, the process will fail immediately.
        /// </summary>
        public void FailNextLogin() => shouldFailNextLogin = true;

        /// <summary>
        /// During the next simulated login, the process will pause indefinitely at "connecting".
        /// </summary>
        public void PauseOnConnectingNextLogin() => stayConnectingNextLogin = true;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // Ensure (as much as we can) that any pending tasks are run.
            Scheduler.Update();
        }

        public class DummyLocalUserState : ILocalUserState
        {
            public Bindable<APIUser> User { get; } = new Bindable<APIUser>(new APIUser
            {
                Username = @"Local user",
                Id = DUMMY_USER_ID,
            });

            public BindableList<APIRelation> Friends { get; } = new BindableList<APIRelation>();
            public BindableList<APIRelation> Blocks { get; } = new BindableList<APIRelation>();
            public BindableList<int> FavouriteBeatmapSets { get; } = new BindableList<int>();

            IBindable<APIUser> ILocalUserState.User => User;
            IBindableList<APIRelation> ILocalUserState.Friends => Friends;
            IBindableList<APIRelation> ILocalUserState.Blocks => Blocks;
            IBindableList<int> ILocalUserState.FavouriteBeatmapSets => FavouriteBeatmapSets;

            public void UpdateFriends()
            {
            }

            public void UpdateBlocks()
            {
            }

            public void UpdateFavouriteBeatmapSets()
            {
            }
        }
    }
}
