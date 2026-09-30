// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;

namespace osu.Game.Database
{
    /// <summary>
    /// Hash-addressed file blob storage.
    /// Backend of <see cref="IHasRealmFiles"/> models: the database stores only hash references,
    /// file contents live here, deduplicated by hash and reference-counted.
    /// </summary>
    public interface IFileStore
    {
        /// <summary>
        /// Open a read stream for the blob with the given hash.
        /// </summary>
        /// <returns>The stream, or null if no blob with that hash exists.</returns>
        Stream? GetStream(string hash);

        /// <summary>
        /// Store a blob, incrementing its reference count.
        /// </summary>
        /// <returns>The hash of the stored blob.</returns>
        string Add(Stream contents);

        /// <summary>
        /// Decrement the reference count of a blob, deleting it when it reaches zero.
        /// </summary>
        void Release(string hash);

        /// <summary>
        /// Whether a blob with the given hash exists.
        /// </summary>
        bool Exists(string hash);
    }
}
