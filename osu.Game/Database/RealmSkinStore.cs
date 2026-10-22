// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Models;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="ISkinStore"/>.
    /// </summary>
    public class RealmSkinStore : RealmSoftDeletableStore<SkinInfo>, ISkinStore
    {
        public RealmSkinStore(RealmAccess realm)
            : base(realm)
        {
        }

        public string FileStorageDirectory => @"files";

        public override List<SkinInfo> GetAllUsableDetached() => Realm.Run(r =>
            r.All<SkinInfo>()
             .Where(s => !s.DeletePending && !s.Protected)
             .AsEnumerable()
             .Detach());

        /// <summary>
        /// Add a skin, reusing the realm-managed <see cref="RealmFile"/> instances referenced by
        /// it rather than inserting duplicate copies.
        /// </summary>
        public override void Add(SkinInfo item)
        {
            ArgumentNullException.ThrowIfNull(item);

            Realm.Write(r =>
            {
                resolveFiles(r, item);
                r.Add(item);
            });
        }

        public SkinInfo? FindByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            // Deleted skins are included so the importer can reuse and restore an existing row;
            // active skins are preferred via the ordering.
            return Realm.Run(r => r.All<SkinInfo>()
                                   .OrderBy(s => s.DeletePending)
                                   .FirstOrDefault(s => s.Hash == hash)?.Detach());
        }

        /// <summary>
        /// Marks the skin with the given primary key as pending deletion.
        /// Protected skins cannot be deleted.
        /// </summary>
        /// <returns>Whether a skin was found, unprotected and not already deleted.</returns>
        public override bool Delete(Guid id) => Realm.Write(r =>
        {
            var item = r.Find<SkinInfo>(id);

            if (item == null || item.DeletePending || item.Protected)
                return false;

            item.DeletePending = true;
            return true;
        });

        private static void resolveFiles(Realm realm, SkinInfo skin)
        {
            foreach (RealmNamedFileUsage usage in skin.Files)
            {
                if (usage.File.IsManaged)
                    continue;

                RealmFile? existing = realm.Find<RealmFile>(usage.File.Hash);

                if (existing != null)
                    usage.File = existing;
            }
        }
    }
}
