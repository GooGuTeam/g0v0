// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// Resolves a skin through its selected store for each operation.
    /// </summary>
    public sealed class SkinStoreLive : Live<SkinInfo>
    {
        private readonly ISkinStore store;

        public SkinStoreLive(ISkinStore store, Guid id)
            : base(id)
        {
            this.store = store;
        }

        public override bool IsManaged => store.GetDetached(ID) != null;

        public override SkinInfo Value => store.GetDetached(ID) ?? throw new InvalidOperationException($"Skin {ID} no longer exists.");

        public override void PerformRead(Action<SkinInfo> perform) => perform(Value);

        public override TReturn PerformRead<TReturn>(Func<SkinInfo, TReturn> perform) => perform(Value);

        public override void PerformWrite(Action<SkinInfo> perform) => store.Update(ID, perform);
    }
}
