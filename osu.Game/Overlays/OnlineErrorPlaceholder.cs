// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Network;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osuTK;

namespace osu.Game.Overlays
{
    /// <summary>
    /// An action offered by an <see cref="OnlineErrorPlaceholder"/> in addition to (or instead of) retrying the failed
    /// retrieval, such as leaving a non-exist page.
    /// </summary>
    public readonly record struct OnlineErrorAction(LocalisableString Text, Action Action);

    /// <summary>
    /// Stands in for content which could not be retrieved from the server.
    /// </summary>
    public partial class OnlineErrorPlaceholder : CompositeDrawable
    {
        public const float HEIGHT = 250;

        private readonly LocalisableString title;
        private readonly LocalisableString description;
        private readonly Exception? exception;
        private readonly Action? retryAction;
        private readonly OnlineErrorAction[] additionalActions;

        [Resolved]
        private OsuGame? game { get; set; }

        public OnlineErrorPlaceholder(Action? retryAction = null)
            : this(OnlineErrorStrings.LoadFailedTitle, OnlineErrorStrings.LoadFailedDescription, null, retryAction, Array.Empty<OnlineErrorAction>())
        {
        }

        /// <summary>
        /// Creates a placeholder describing a failed request. Message from the server takes precedence.
        /// </summary>
        public OnlineErrorPlaceholder(Exception? exception, Action? retryAction = null, params OnlineErrorAction[] additionalActions)
            : this(exception, OnlineErrorStrings.LoadFailedDescription, retryAction, additionalActions)
        {
        }

        public OnlineErrorPlaceholder(Exception? exception, LocalisableString fallbackDescription, Action? retryAction = null, params OnlineErrorAction[] additionalActions)
            : this(OnlineErrorStrings.LoadFailedTitle, getDescription(exception, fallbackDescription), exception, retryAction, additionalActions)
        {
        }

        public OnlineErrorPlaceholder(LocalisableString title, LocalisableString description, Action? retryAction = null, params OnlineErrorAction[] additionalActions)
            : this(title, description, null, retryAction, additionalActions)
        {
        }

        private OnlineErrorPlaceholder(LocalisableString title, LocalisableString description, Exception? exception, Action? retryAction, OnlineErrorAction[] additionalActions)
        {
            this.title = title;
            this.description = description;
            this.exception = exception;
            this.retryAction = retryAction;
            this.additionalActions = additionalActions;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        private static LocalisableString getDescription(Exception? exception, LocalisableString fallback)
        {
            if (exception is APIException { Message.Length: > 0 } apiException)
                return apiException.Message;

            return fallback;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 40 },
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Icon = FontAwesome.Solid.ExclamationTriangle,
                            Size = new Vector2(28),
                            Colour = colours.Yellow,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = title,
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                        },
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            TextAnchor = Anchor.TopCentre,
                            Text = description,
                            Colour = colours.GrayC,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Margin = new MarginPadding { Top = 8 },
                            ChildrenEnumerable = buildButtons(),
                        },
                    },
                },
            };
        }

        private IEnumerable<RoundedButton> buildButtons()
        {
            if (retryAction != null)
                yield return createButton(OnlineErrorStrings.Retry, retryAction);

            foreach (var action in additionalActions)
                yield return createButton(action.Text, action.Action);

            if (game != null)
                yield return createButton(OnlineErrorStrings.CopyErrorDescription, () => game?.CopyToClipboard(BuildErrorDescription()));
        }

        /// <summary>
        /// Builds the report that is human-readable by users and is easy to copy.
        /// </summary>
        internal string BuildErrorDescription()
        {
            var builder = new StringBuilder();

            string descriptionText = description.ToString();

            builder.Append(descriptionText);

            HttpStatusCode? statusCode = null;
            string? url = null;

            switch (exception)
            {
                case APIException apiException:
                    statusCode = apiException.StatusCode;
                    url = (apiException.InnerException as WebRequestException)?.Url;
                    break;

                case WebRequestException requestException:
                    statusCode = requestException.StatusCode;
                    url = requestException.Url;
                    break;

                case WebException webException:
                    url = webException.Response?.ResponseUri.ToString();
                    break;
            }

            if (statusCode != null || url != null)
            {
                builder.Append('\n');

                if (statusCode != null)
                    builder.Append(@"HTTP ").Append((int)statusCode.Value).Append(' ').Append(statusCode.Value);

                if (url != null)
                {
                    if (statusCode != null)
                        builder.Append(@" · ");

                    builder.Append(url);
                }
            }

            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                builder.Append('\n').Append(current.GetType().Name);

                // the description is often the message of the outermost exception, in which case repeating it adds noise.
                if (!string.IsNullOrEmpty(current.Message) && current.Message != descriptionText)
                    builder.Append(@": ").Append(current.Message);
            }

            return builder.ToString();
        }

        private static RoundedButton createButton(LocalisableString text, Action action) => new RoundedButton
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Width = 160,
            Height = 36,
            Text = text,
            Action = action,
        };
    }
}
