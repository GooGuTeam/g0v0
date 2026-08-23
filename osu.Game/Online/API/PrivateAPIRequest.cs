// This file is originally created by GooGuTeam.

namespace osu.Game.Online.API
{
    /// <summary>
    /// An API request targeting the "private" route of a private server.
    /// </summary>
    public abstract class PrivateAPIRequest : APIRequest
    {
        protected override string Route => @"private";
    }

    /// <summary>
    /// An API request with a well-defined response type, targeting the "private" route of a private server.
    /// </summary>
    /// <typeparam name="T">Type of the response (used for deserialisation).</typeparam>
    public abstract class PrivateAPIRequest<T> : APIRequest<T> where T : class
    {
        protected override string Route => @"private";
    }

    /// <summary>
    /// A download request targeting the "private" route of a private server.
    /// </summary>
    public abstract class PrivateAPIDownloadRequest : APIDownloadRequest
    {
        protected override string Route => @"private";
    }
}
