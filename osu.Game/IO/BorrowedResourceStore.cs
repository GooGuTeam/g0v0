// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace osu.Game.IO
{
    /// <summary>
    /// Prevents disposal of a worker's resource wrapper from closing the client's store.
    /// </summary>
    public sealed class BorrowedResourceStore<T> : IResourceStore<T> where T : class
    {
        private readonly IResourceStore<T> source;

        public BorrowedResourceStore(IResourceStore<T> source) => this.source = source;
        public T Get(string name) => source.Get(name);
        public Task<T> GetAsync(string name, CancellationToken cancellationToken = default) => source.GetAsync(name, cancellationToken);
        public Stream GetStream(string name) => source.GetStream(name);
        public IEnumerable<string> GetAvailableResources() => source.GetAvailableResources();
        public void Dispose()
        {
        }
    }
}
