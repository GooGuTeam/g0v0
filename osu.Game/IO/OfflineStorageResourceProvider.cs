// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;

namespace osu.Game.IO
{
    /// <summary>
    /// Shares immutable asset bytes while allocating textures and audio in the export worker.
    /// </summary>
    public sealed class OfflineStorageResourceProvider : IStorageResourceProvider
    {
        private readonly IStorageResourceProvider parent;

        public OfflineStorageResourceProvider(IRenderer renderer, AudioManager audio, IStorageResourceProvider parent)
        {
            Renderer = renderer;
            AudioManager = audio;
            this.parent = parent;
            Files = new BorrowedResourceStore<byte[]>(parent.Files);
            Resources = new BorrowedResourceStore<byte[]>(parent.Resources);
        }

        public IRenderer Renderer { get; }
        public AudioManager AudioManager { get; }
        public IResourceStore<byte[]> Files { get; }
        public IResourceStore<byte[]> Resources { get; }
        public RealmAccess RealmAccess => parent.RealmAccess;

        public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> store) => new TextureLoaderStore(store);
    }
}
