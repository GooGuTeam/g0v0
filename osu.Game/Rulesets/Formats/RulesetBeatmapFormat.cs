// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.IO.Archives;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Formats
{
    /// <summary>
    /// A ruleset-owned chart format, for rulesets whose beatmaps cannot be represented as legacy .osu content.
    /// </summary>
    /// <remarks>
    /// Native charts are stored in the beatmap file store as JSON envelopes (<c>{"g0v0RulesetFormat": shortName, "data": {...}}</c>)
    /// under an .osu filename, so that hashing, difficulty enumeration and lazer exports keep working unchanged.
    /// The game never interprets <c>data</c>; it is only produced by <see cref="Encode"/> and consumed by <see cref="Decode"/>.
    /// Formats are created by <see cref="Ruleset.CreateBeatmapFormat"/> and registered by the ruleset store.
    /// </remarks>
    public abstract class RulesetBeatmapFormat
    {
        public const string ENVELOPE_KEY = @"g0v0RulesetFormat";
        public const string DATA_KEY = @"data";

        private static readonly List<RulesetBeatmapFormat> formats = new List<RulesetBeatmapFormat>();

        public static IReadOnlyList<RulesetBeatmapFormat> Registered
        {
            get
            {
                lock (formats)
                    return formats.ToArray();
            }
        }

        public static void Register(RulesetBeatmapFormat format)
        {
            ArgumentNullException.ThrowIfNull(format);

            lock (formats)
            {
                formats.RemoveAll(f => f.RulesetShortName == format.RulesetShortName);
                formats.Add(format);
            }
        }

        public static RulesetBeatmapFormat? ForRuleset(string? shortName) => shortName == null ? null : Registered.FirstOrDefault(f => f.RulesetShortName == shortName);

        public static RulesetBeatmapFormat? ForRuleset(IRulesetInfo? ruleset) => ForRuleset(ruleset?.ShortName);

        /// <summary>
        /// The <see cref="IRulesetInfo.ShortName"/> of the ruleset that owns this format.
        /// </summary>
        public abstract string RulesetShortName { get; }

        /// <summary>
        /// Archive extensions owned by this format (e.g. ".pez"). They are always routed to it, and the source file
        /// is deleted after a successful import as for .osz.
        /// </summary>
        public virtual IEnumerable<string> HandledExtensions => Array.Empty<string>();

        /// <summary>
        /// Generic container extensions (e.g. ".zip") this format may import. Such archives are only routed to it when
        /// <see cref="CanImport"/> claims them, and are never deleted after import.
        /// </summary>
        public virtual IEnumerable<string> ProbedExtensions => Array.Empty<string>();

        /// <summary>
        /// Whether a probed archive holds charts of this format. Must be cheap and side-effect free: inspect file names,
        /// or read only what is needed to recognise a chart, without decoding it.
        /// </summary>
        public virtual bool CanImport(ArchiveReader archive) => false;

        /// <summary>
        /// Whether an archive at <paramref name="path"/> should be handed to this format, by extension and, for probed
        /// extensions, by content.
        /// </summary>
        public bool Claims(string path, ArchiveReader archive)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            if (HandledExtensions.Contains(extension))
                return true;

            return ProbedExtensions.Contains(extension) && CanImport(archive);
        }

        /// <summary>
        /// Reads every chart in a foreign archive, as the documents that <see cref="Decode"/> accepts.
        /// Asset references inside the documents must use the archive's own file paths.
        /// </summary>
        protected virtual IEnumerable<JObject> ReadArchive(ArchiveReader archive) => Enumerable.Empty<JObject>();

        /// <summary>
        /// Populates <paramref name="output"/> (metadata, control points and hit objects) from a native document.
        /// </summary>
        public abstract void Decode(JObject data, Beatmap output);

        /// <summary>
        /// Serialises a playable beatmap of this ruleset back into its native document.
        /// Must be deterministic, as the result is hashed and diffed for undo.
        /// </summary>
        public abstract JObject Encode(IBeatmap beatmap);

        /// <summary>
        /// Converts a foreign archive into one carrying stored .osu envelopes, or returns null when it holds no charts of this format.
        /// The returned reader owns <paramref name="archive"/>.
        /// </summary>
        public ArchiveReader? ConvertArchive(ArchiveReader archive)
        {
            string[] filenames = archive.Filenames.ToArray();

            // A regular beatmap bundle must retain its existing import behaviour.
            if (filenames.Any(f => f.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase)))
                return null;

            var generated = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in ReadArchive(archive))
            {
                string name = $@"{RulesetShortName}-{generated.Count}.osu";
                while (filenames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    name = @"_" + name;
                generated.Add(name, Encoding.UTF8.GetBytes(Wrap(document)));
            }

            return generated.Count == 0 ? null : new GeneratedFilesArchiveReader(archive, generated);
        }

        public string Wrap(JObject data) => new JObject
        {
            [ENVELOPE_KEY] = RulesetShortName,
            [DATA_KEY] = data
        }.ToString(Formatting.None);

        /// <summary>
        /// Decodes a stored envelope with its owning format. Returns false for documents that are not native envelopes.
        /// </summary>
        public static bool TryDecode(JObject document, Beatmap output)
        {
            if (document[ENVELOPE_KEY]?.Type != JTokenType.String)
                return false;

            string shortName = document.Value<string>(ENVELOPE_KEY)!;
            var format = ForRuleset(shortName) ?? throw new InvalidDataException($"No beatmap format is registered for ruleset \"{shortName}\".");

            format.Decode(document[DATA_KEY] as JObject ?? throw new InvalidDataException("Native beatmap envelope has no data."), output);
            return true;
        }

        /// <summary>
        /// Writes <paramref name="beatmap"/> as a stored envelope if its ruleset has a native format.
        /// </summary>
        /// <returns>Whether the beatmap was written; false means the legacy encoder should be used.</returns>
        public static bool TryEncode(IBeatmap beatmap, Stream output)
        {
            if (beatmap is EditorBeatmap editorBeatmap)
                beatmap = editorBeatmap.PlayableBeatmap;

            var format = ForRuleset(beatmap.BeatmapInfo.Ruleset);
            if (format == null)
                return false;

            byte[] bytes = Encoding.UTF8.GetBytes(format.Wrap(format.Encode(beatmap)));
            output.Write(bytes, 0, bytes.Length);
            return true;
        }

        /// <summary>
        /// Whether stored beatmap content is a native envelope (and so must not go through legacy decoding).
        /// Leaves the stream position unchanged.
        /// </summary>
        public static bool IsNativeContent(Stream content)
        {
            long position = content.Position;

            try
            {
                using var reader = new StreamReader(content, Encoding.UTF8, true, 1024, true);
                using var json = new JsonTextReader(reader);
                return json.Read() && json.TokenType == JsonToken.StartObject
                                   && json.Read() && json.TokenType == JsonToken.PropertyName && (string?)json.Value == ENVELOPE_KEY;
            }
            catch (JsonException)
            {
                return false;
            }
            finally
            {
                content.Position = position;
            }
        }
    }
}
