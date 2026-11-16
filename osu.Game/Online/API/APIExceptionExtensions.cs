// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API
{
    public static class APIExceptionExtensions
    {
        /// <summary>
        /// Whether <paramref name="exception"/> indicates that the server answered with the given status code.
        /// </summary>
        public static bool HasStatusCode(this Exception? exception, HttpStatusCode statusCode)
            => exception switch
            {
                APIException apiException => apiException.StatusCode == statusCode,
                WebRequestException requestException => requestException.StatusCode == statusCode,
                _ => false,
            };
    }
}
