// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Platform;
using Realms;

namespace osu.Game.Database
{
    /// <summary>Compatibility base for existing exporters. New exporters can use <see cref="ModelExporter{TModel}"/> directly.</summary>
    public abstract class LegacyExporter<TModel> : ModelExporter<TModel>
        where TModel : RealmObject, IHasNamedFiles, IHasGuidPrimaryKey
    {
        protected LegacyExporter(Storage storage)
            : base(storage)
        {
        }
    }
}
