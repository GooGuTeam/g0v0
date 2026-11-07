// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Utils;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The editable hitsound sample library backing the hitsounding sample gallery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library maps every <c>(set, bank, sample)</c> slot to an audio resource of the current beatmap set. Assigning a slot
    /// materialises the canonical resource (for example <c>normal-hitnormal2.wav</c> or <c>soft-hitclap.ogg</c>) by copying the
    /// bytes of the selected source resource, so normal beatmap playback and export resolve it without any hitsounding specific
    /// logic. The source resource is never moved or deleted; only competing canonical resources of the replaced slot are removed,
    /// as their extension may otherwise win the skin lookup.
    /// </para>
    /// <para>
    /// Sample sets, assignments and aliases are persisted as a <c>hitsounding-samples.json</c> resource inside the beatmap set.
    /// Beatmap sets which are not backed by the local database (for example the dummy beatmap used by tests) keep their library
    /// in memory only and never attempt file operations.
    /// </para>
    /// <para>
    /// All members are intended to be called from the update thread. The beatmap skin raises its change event from a database
    /// notification thread, so a library refresh is always deferred to the next update before any state is rebuilt.
    /// </para>
    /// </remarks>
    public partial class HitSoundingSampleLibrary : Component
    {
        /// <summary>
        /// The name of the beatmap resource used to persist the library.
        /// </summary>
        private const string persistence_filename = @"hitsounding-samples.json";

        /// <summary>
        /// The current persistence format version.
        /// </summary>
        private const int persistence_version = 1;

        /// <summary>
        /// The number of update frames a preview waits for a freshly imported sample to become resolvable by the beatmap skin.
        /// </summary>
        private const int preview_retry_count = 20;

        private static readonly string[] sample_names = [HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_CLAP];
        private static readonly string[] extension_priority = [@".wav", @".mp3", @".ogg"];

        /// <summary>
        /// Invoked on the update thread whenever the sets, files, assignments or aliases change.
        /// </summary>
        public event Action? Changed;

        private readonly List<int> sets = [];
        private readonly List<string> files = [];
        private readonly HashSet<string> audioFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AssignmentKey, Assignment> assignments = [];
        private readonly Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> emptySets = [];

        private EditorBeatmapSkin? beatmapSkin;
        private bool loaded;
        private bool refreshPending;

        private SampleChannel? previewChannel;
        private string? previewFilename;
        private int previewRetriesRemaining;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentWorkingBeatmap { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private Editor? editor { get; set; }

        /// <summary>
        /// All editable sample set indices, sorted ascending.
        /// </summary>
        /// <remarks>
        /// Always contains <c>1</c> (the beatmap's standard sample set), plus every set discovered from an existing canonical
        /// sample and every empty set created via <see cref="AddSet"/>. The user skin set (<c>0</c>) is not included because it
        /// cannot be stored in the beatmap.
        /// </remarks>
        public IReadOnlyList<int> Sets => sets;

        /// <summary>
        /// All audio resources of the current beatmap set which can be used as a sample source, sorted by filename.
        /// </summary>
        /// <remarks>
        /// The music track(s) of the beatmap set are excluded, and so are the canonical resources generated for an assignment
        /// (their source is listed instead). Canonical resources which already existed before the library was first read are
        /// listed and remain selectable.
        /// </remarks>
        public IReadOnlyList<string> Files => files;

        [BackgroundDependencyLoader]
        private void load()
        {
            beatmapSkin = beatmap.BeatmapSkin;

            if (beatmapSkin != null)
                beatmapSkin.BeatmapSkinChanged += onBeatmapSkinChanged;

            if (editor != null)
                editor.Mode.ValueChanged += onEditorModeChanged;

            loaded = true;
            refresh();
        }

        protected override void Update()
        {
            base.Update();

            if (previewChannel != null)
            {
                if (!isChannelPlaying(previewChannel))
                    clearPreviewChannel();
            }
            else if (previewFilename != null)
                tryStartPreview();

            if (!refreshPending)
                return;

            refreshPending = false;
            refresh();
        }

        /// <summary>
        /// Retrieves the alias displayed for <paramref name="filename"/>.
        /// </summary>
        /// <returns>The custom alias, or the filename stem when no alias has been set.</returns>
        public string GetAlias(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return string.Empty;

            return aliases.TryGetValue(filename, out string? alias) ? alias : Path.GetFileNameWithoutExtension(filename);
        }

        /// <summary>
        /// Retrieves the sample resource currently assigned to a slot.
        /// </summary>
        /// <param name="set">The sample set index, one or greater.</param>
        /// <param name="bank">The sample bank (<c>normal</c>, <c>soft</c> or <c>drum</c>).</param>
        /// <param name="sample">The sample name (<c>hitnormal</c>, <c>whistle</c>, <c>finish</c> or <c>clap</c>).</param>
        /// <returns>
        /// The source resource name for an assignment, the canonical resource name for a sample which already existed in the
        /// beatmap set, or <c>null</c> when the slot is missing. The user skin set (<c>0</c>) always returns <c>null</c>.
        /// </returns>
        public string? GetAssignment(int set, string bank, string sample)
        {
            if (!tryCreateKey(set, bank, sample, out AssignmentKey key))
                return null;

            return assignments.TryGetValue(key, out Assignment? assignment) ? assignment.Source : null;
        }

        /// <summary>
        /// Creates a new empty sample set, using the next available set index.
        /// </summary>
        public void AddSet()
        {
            if (!loaded)
                return;

            int next = sets.Count == 0 ? 2 : Math.Max(2, sets[^1] + 1);

            if (!emptySets.Add(next))
                return;

            persist();
            refresh();
        }

        /// <summary>
        /// Copies audio files from disk into the beatmap set so they can be assigned to sample slots.
        /// </summary>
        /// <param name="paths">Local audio file paths. Missing or unsupported files are skipped.</param>
        /// <remarks>
        /// Each imported file keeps its filename (sanitised for safe export, with a numeric suffix on collision). The import is
        /// synchronous: <see cref="Files"/> contains every imported file when this method returns.
        /// </remarks>
        public void ImportFiles(IEnumerable<string> paths)
        {
            if (!loaded)
                return;

            var beatmapSet = currentWorkingBeatmap.Value.BeatmapSetInfo;

            if (!isPersisted(beatmapSet))
                return;

            var existing = new HashSet<string>(beatmapSet.Files.Select(file => file.Filename), StringComparer.OrdinalIgnoreCase);
            bool imported = false;

            foreach (string path in paths)
            {
                if (!File.Exists(path))
                    continue;

                string? filename = getImportFilename(path, existing);

                if (filename == null)
                    continue;

                using (var stream = File.OpenRead(path))
                    beatmaps.AddFile(beatmapSet, stream, filename);

                existing.Add(filename);
                imported = true;
            }

            if (imported)
                refresh();
        }

        /// <summary>
        /// Sets or clears the alias displayed for an audio resource.
        /// </summary>
        /// <param name="filename">The audio resource name.</param>
        /// <param name="alias">The alias to display. An empty alias (or the default filename stem) clears the custom alias.</param>
        public void SetAlias(string filename, string alias)
        {
            if (!loaded || string.IsNullOrEmpty(filename) || !audioFileNames.Contains(filename))
                return;

            string trimmed = alias.Trim();

            if (trimmed.Length == 0 || string.Equals(trimmed, Path.GetFileNameWithoutExtension(filename), StringComparison.Ordinal))
            {
                if (aliases.Remove(filename))
                    persistAndRefresh();
            }
            else
            {
                if (aliases.TryGetValue(filename, out string? existing) && string.Equals(existing, trimmed, StringComparison.Ordinal))
                    return;

                aliases[filename] = trimmed;
                persistAndRefresh();
            }
        }

        /// <summary>
        /// Assigns an audio resource to a sample slot, replacing any previous assignment.
        /// </summary>
        /// <param name="set">The sample set index, one or greater.</param>
        /// <param name="bank">The sample bank (<c>normal</c>, <c>soft</c> or <c>drum</c>).</param>
        /// <param name="sample">The sample name (<c>hitnormal</c>, <c>whistle</c>, <c>finish</c> or <c>clap</c>).</param>
        /// <param name="filename">The source audio resource of the beatmap set. It is preserved untouched.</param>
        /// <remarks>
        /// The source bytes are copied to the canonical resource of the slot (for example <c>normal-hitnormal2.wav</c>), and any
        /// other audio extension of the same canonical slot is removed so sample lookup can never select a stale file.
        /// </remarks>
        public void Assign(int set, string bank, string sample, string filename)
        {
            AssignmentKey key = createKey(set, bank, sample);

            if (!loaded)
                return;

            if (!audioFileNames.Contains(filename))
                throw new ArgumentException($@"{filename} is not an audio resource of the current beatmap set.", nameof(filename));

            var beatmapSet = currentWorkingBeatmap.Value.BeatmapSetInfo;
            string target = getCanonicalFilename(key, Path.GetExtension(filename));

            if (isPersisted(beatmapSet))
            {
                if (!string.Equals(filename, target, StringComparison.OrdinalIgnoreCase))
                {
                    string? storagePath = beatmapSet.GetPathForFile(filename);

                    if (storagePath == null)
                        throw new InvalidOperationException($@"Could not locate {filename} in the beatmap set.");

                    using Stream? source = currentWorkingBeatmap.Value.GetStream(storagePath);

                    if (source == null)
                        throw new InvalidOperationException($@"Could not read {filename} from the beatmap set.");

                    beatmaps.AddFile(beatmapSet, source, target);
                }

                deleteSlotFiles(beatmapSet, key, filename, target);
            }

            assignments[key] = new Assignment(filename, target);
            persistAndRefresh();
        }

        /// <summary>
        /// Clears a sample slot, deleting its canonical resource from the beatmap set.
        /// </summary>
        /// <param name="set">The sample set index, one or greater.</param>
        /// <param name="bank">The sample bank (<c>normal</c>, <c>soft</c> or <c>drum</c>).</param>
        /// <param name="sample">The sample name (<c>hitnormal</c>, <c>whistle</c>, <c>finish</c> or <c>clap</c>).</param>
        public void RemoveAssignment(int set, string bank, string sample)
        {
            AssignmentKey key = createKey(set, bank, sample);

            if (!loaded)
                return;

            var beatmapSet = currentWorkingBeatmap.Value.BeatmapSetInfo;
            bool persisted = isPersisted(beatmapSet);

            if (persisted)
                deleteSlotFiles(beatmapSet, key, null, null);

            if (!assignments.Remove(key) && !persisted)
                return;

            persistAndRefresh();
        }

        /// <summary>
        /// Starts or stops the audition of an audio resource through the beatmap skin's sample channels.
        /// </summary>
        /// <param name="filename">The audio resource name to audition.</param>
        public void TogglePreview(string filename)
        {
            if (!loaded || string.IsNullOrEmpty(filename))
                return;

            if (IsPlaying(filename))
            {
                StopPreview();
                return;
            }

            StopPreview();

            previewFilename = filename;
            previewRetriesRemaining = preview_retry_count;

            tryStartPreview();
        }

        /// <summary>
        /// Whether <paramref name="filename"/> is currently being auditioned.
        /// </summary>
        /// <param name="filename">The audio resource name to query.</param>
        public bool IsPlaying(string filename)
        {
            if (previewChannel == null || !string.Equals(previewFilename, filename, StringComparison.OrdinalIgnoreCase))
                return false;

            return isChannelPlaying(previewChannel);
        }

        /// <summary>
        /// Stops any running audition.
        /// </summary>
        public void StopPreview() => clearPreviewChannel();

        private void tryStartPreview()
        {
            string? filename = previewFilename;

            if (!loaded || filename == null)
                return;

            ISample? sample = beatmap.BeatmapSkin?.Skin.GetSampleFromSkinFiles(filename, true);

            if (sample != null)
            {
                SampleChannel channel = sample.GetChannel();
                channel.ManualFree = true;
                channel.Play();

                previewChannel = channel;
                previewRetriesRemaining = 0;
                return;
            }

            if (!audioFileNames.Contains(filename) || previewRetriesRemaining <= 0)
            {
                previewFilename = null;
                previewRetriesRemaining = 0;
                return;
            }

            // A resource imported or materialised on this frame is not resolvable by the skin until its file store is recycled
            // from the database notification, which happens on a later frame.
            previewRetriesRemaining--;
        }

        private static bool isChannelPlaying(SampleChannel channel)
        {
            try
            {
                return channel.Playing;
            }
            catch (ObjectDisposedException)
            {
                // The beatmap skin recycled its sample store while the preview was running.
                return false;
            }
        }

        private void clearPreviewChannel()
        {
            SampleChannel? channel = previewChannel;
            previewChannel = null;
            previewFilename = null;

            if (channel == null)
                return;

            try
            {
                channel.Stop();
                channel.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // The beatmap skin recycled its sample store while the preview was running.
            }
        }

        private void persistAndRefresh()
        {
            persist();
            refresh();
        }

        private void refresh()
        {
            if (!loaded)
                return;

            var beatmapSet = currentWorkingBeatmap.Value.BeatmapSetInfo;
            var musicTracks = getMusicTracks(beatmapSet);

            audioFileNames.Clear();

            foreach (var file in beatmapSet.Files)
            {
                if (isAudioFile(file.Filename))
                    audioFileNames.Add(file.Filename);
            }

            PersistedLibrary? persisted = readPersistence(beatmapSet);

            if (persisted != null)
            {
                aliases.Clear();

                if (persisted.Aliases != null)
                {
                    foreach (var pair in persisted.Aliases)
                    {
                        if (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                            aliases[pair.Key] = pair.Value.Trim();
                    }
                }

                emptySets.Clear();

                if (persisted.Sets != null)
                {
                    foreach (int set in persisted.Sets)
                    {
                        if (set >= 2)
                            emptySets.Add(set);
                    }
                }
            }

            var candidates = new Dictionary<AssignmentKey, Assignment>();

            if (persisted != null)
            {
                if (persisted.Assignments != null)
                {
                    foreach (var entry in persisted.Assignments)
                    {
                        string? source = entry.Source;
                        string? target = entry.Target;

                        if (source == null || target == null)
                            continue;

                        if (!tryCreateKey(entry.Set, entry.Bank ?? string.Empty, entry.Sample ?? string.Empty, out AssignmentKey key))
                            continue;

                        candidates[key] = new Assignment(source, target);
                    }
                }
            }
            else
            {
                foreach (var pair in assignments)
                    candidates[pair.Key] = pair.Value;
            }

            assignments.Clear();

            foreach (var pair in candidates)
            {
                if (!audioFileNames.Contains(pair.Value.Source) || !audioFileNames.Contains(pair.Value.Target))
                    continue;

                assignments[pair.Key] = pair.Value;
            }

            foreach (var discovered in discoverAssignments(musicTracks))
                assignments.TryAdd(discovered.Key, discovered.Value);

            var generatedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assignment in assignments.Values)
            {
                if (assignment.IsGenerated)
                    generatedTargets.Add(assignment.Target);
            }

            files.Clear();
            files.AddRange(audioFileNames
                           .Where(file => !musicTracks.Contains(file.Replace('\\', '/')) && !generatedTargets.Contains(file))
                           .OrderBy(file => file, StringComparer.OrdinalIgnoreCase));

            var setIndices = new SortedSet<int> { 1 };

            foreach (var key in assignments.Keys)
                setIndices.Add(key.Set);

            foreach (int set in emptySets)
                setIndices.Add(set);

            sets.Clear();
            sets.AddRange(setIndices);

            Changed?.Invoke();
        }

        private Dictionary<AssignmentKey, Assignment> discoverAssignments(HashSet<string> musicTracks)
        {
            var discovered = new Dictionary<AssignmentKey, Assignment>();

            foreach (string file in audioFileNames)
            {
                if (musicTracks.Contains(file.Replace('\\', '/')))
                    continue;

                if (!string.IsNullOrEmpty(Path.GetDirectoryName(file)))
                    continue;

                if (!tryParseSlot(Path.GetFileNameWithoutExtension(file), out AssignmentKey key))
                    continue;

                if (discovered.TryGetValue(key, out Assignment? existing) && extensionRank(existing.Source) <= extensionRank(file))
                    continue;

                discovered[key] = new Assignment(file, file);
            }

            return discovered;
        }

        private HashSet<string> getMusicTracks(BeatmapSetInfo beatmapSet)
        {
            var tracks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            add(beatmap.BeatmapInfo.Metadata.AudioFile);

            foreach (var info in beatmapSet.Beatmaps)
                add(info.Metadata.AudioFile);

            return tracks;

            void add(string? filename)
            {
                if (!string.IsNullOrWhiteSpace(filename))
                    tracks.Add(filename.Replace('\\', '/'));
            }
        }

        private void deleteSlotFiles(BeatmapSetInfo beatmapSet, AssignmentKey key, string? keepSource, string? keepTarget)
        {
            string canonical = getCanonicalBase(key);

            foreach (string file in beatmapSet.Files.Select(usage => usage.Filename).ToArray())
            {
                if (keepSource != null && string.Equals(file, keepSource, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (keepTarget != null && string.Equals(file, keepTarget, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!isRootAudioFile(file))
                    continue;

                if (!string.Equals(Path.GetFileNameWithoutExtension(file), canonical, StringComparison.OrdinalIgnoreCase))
                    continue;

                var usage = beatmapSet.GetFile(file);

                if (usage != null)
                    beatmaps.DeleteFile(beatmapSet, usage);
            }
        }

        private bool isPersisted(BeatmapSetInfo beatmapSet)
        {
            if (beatmapSet.ID == Guid.Empty)
                return false;

            return beatmaps.QueryBeatmapSet(set => set.ID == beatmapSet.ID) != null;
        }

        private void persist()
        {
            if (!loaded)
                return;

            var beatmapSet = currentWorkingBeatmap.Value.BeatmapSetInfo;

            if (!isPersisted(beatmapSet))
                return;

            var persistedAssignments = new List<PersistedAssignment>();

            var orderedAssignments = assignments.OrderBy(pair => pair.Key.Set)
                                                .ThenBy(pair => pair.Key.Bank, StringComparer.Ordinal)
                                                .ThenBy(pair => pair.Key.Sample, StringComparer.Ordinal);

            foreach (var pair in orderedAssignments)
            {
                persistedAssignments.Add(new PersistedAssignment
                {
                    Set = pair.Key.Set,
                    Bank = pair.Key.Bank,
                    Sample = pair.Key.Sample,
                    Source = pair.Value.Source,
                    Target = pair.Value.Target,
                });
            }

            var data = new PersistedLibrary
            {
                Version = persistence_version,
                Sets = emptySets.OrderBy(set => set).ToList(),
                Aliases = new Dictionary<string, string>(aliases, StringComparer.Ordinal),
                Assignments = persistedAssignments,
            };

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                beatmaps.AddFile(beatmapSet, stream, persistence_filename);
        }

        private PersistedLibrary? readPersistence(BeatmapSetInfo beatmapSet)
        {
            string? storagePath = beatmapSet.GetPathForFile(persistence_filename);

            if (storagePath == null)
                return null;

            try
            {
                using Stream? stream = currentWorkingBeatmap.Value.GetStream(storagePath);

                if (stream == null)
                    return null;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                var library = JsonConvert.DeserializeObject<PersistedLibrary>(reader.ReadToEnd());

                return library != null && library.Version <= persistence_version ? library : null;
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to read the hitsounding sample library");
                return null;
            }
        }

        private static string? getImportFilename(string path, HashSet<string> existing)
        {
            string extension = Path.GetExtension(path);

            if (!isAudioFile(path))
                return null;

            string stem = Path.GetFileNameWithoutExtension(path);

            foreach (char invalid in Path.GetInvalidFileNameChars())
                stem = stem.Replace(invalid, '-');

            stem = stem.Trim().TrimEnd('.');

            if (stem.Length == 0)
                stem = @"sample";

            if (stem.Length > 100)
                stem = stem[..100];

            extension = extension.ToLowerInvariant();

            string filename = stem + extension;

            for (int i = 1; existing.Contains(filename); i++)
                filename = $@"{stem}-{i}{extension}";

            return filename;
        }

        private static bool tryCreateKey(int set, string bank, string sample, out AssignmentKey key)
        {
            key = default;

            if (set < 1 || string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(sample))
                return false;

            string normalisedBank = bank.Trim().ToLowerInvariant();

            if (!HitSampleInfo.ALL_BANKS.Contains(normalisedBank, StringComparer.Ordinal))
                return false;

            string? normalisedSample = normaliseSample(sample);

            if (normalisedSample == null)
                return false;

            key = new AssignmentKey(set, normalisedBank, normalisedSample);
            return true;
        }

        private static AssignmentKey createKey(int set, string bank, string sample)
        {
            if (!tryCreateKey(set, bank, sample, out AssignmentKey key))
                throw new ArgumentException($@"Invalid sample slot ({set}, {bank}, {sample}).");

            return key;
        }

        private static string? normaliseSample(string sample)
        {
            switch (sample.Trim().ToLowerInvariant())
            {
                case @"normal":
                case HitSampleInfo.HIT_NORMAL:
                    return HitSampleInfo.HIT_NORMAL;

                case @"whistle":
                case HitSampleInfo.HIT_WHISTLE:
                    return HitSampleInfo.HIT_WHISTLE;

                case @"finish":
                case HitSampleInfo.HIT_FINISH:
                    return HitSampleInfo.HIT_FINISH;

                case @"clap":
                case HitSampleInfo.HIT_CLAP:
                    return HitSampleInfo.HIT_CLAP;

                default:
                    return null;
            }
        }

        private static bool tryParseSlot(string baseName, out AssignmentKey key)
        {
            key = default;

            foreach (string bank in HitSampleInfo.ALL_BANKS)
            {
                string prefix = $@"{bank}-";

                if (!baseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string remainder = baseName[prefix.Length..];

                foreach (string sample in sample_names)
                {
                    if (!remainder.StartsWith(sample, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string indexString = remainder[sample.Length..];
                    int set = 1;

                    if (indexString.Length > 0 && (!int.TryParse(indexString, NumberStyles.None, CultureInfo.InvariantCulture, out set) || set < 2))
                        return false;

                    key = new AssignmentKey(set, bank, sample);
                    return true;
                }
            }

            return false;
        }

        private static string getCanonicalBase(AssignmentKey key)
            => $@"{key.Bank}-{key.Sample}{(key.Set > 1 ? key.Set.ToString(CultureInfo.InvariantCulture) : string.Empty)}";

        private static string getCanonicalFilename(AssignmentKey key, string extension)
            => getCanonicalBase(key) + extension.ToLowerInvariant();

        private static int extensionRank(string filename)
        {
            string extension = Path.GetExtension(filename);

            for (int i = 0; i < extension_priority.Length; i++)
            {
                if (string.Equals(extension_priority[i], extension, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return extension_priority.Length;
        }

        private static bool isAudioFile(string filename)
            => SupportedExtensions.AUDIO_EXTENSIONS.Contains(Path.GetExtension(filename), StringComparer.OrdinalIgnoreCase);

        private static bool isRootAudioFile(string filename)
            => isAudioFile(filename) && string.IsNullOrEmpty(Path.GetDirectoryName(filename));

        private void onBeatmapSkinChanged()
        {
            if (!loaded)
                return;

            Schedule(() =>
            {
                if (loaded)
                    refreshPending = true;
            });
        }

        private void onEditorModeChanged(ValueChangedEvent<EditorScreenMode> mode)
        {
            if (mode.NewValue != EditorScreenMode.HitSounding)
                StopPreview();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                loaded = false;

                if (beatmapSkin != null)
                {
                    beatmapSkin.BeatmapSkinChanged -= onBeatmapSkinChanged;
                    beatmapSkin = null;
                }

                if (editor != null)
                {
                    editor.Mode.ValueChanged -= onEditorModeChanged;
                    editor = null;
                }

                StopPreview();
            }

            base.Dispose(isDisposing);
        }

        private readonly record struct AssignmentKey(int Set, string Bank, string Sample);

        private sealed class Assignment
        {
            public string Source { get; }
            public string Target { get; }

            public bool IsGenerated => !string.Equals(Source, Target, StringComparison.OrdinalIgnoreCase);

            public Assignment(string source, string target)
            {
                Source = source;
                Target = target;
            }
        }

        private sealed class PersistedLibrary
        {
            public int Version { get; set; }

            public List<int>? Sets { get; set; }

            public Dictionary<string, string>? Aliases { get; set; }

            public List<PersistedAssignment>? Assignments { get; set; }
        }

        private sealed class PersistedAssignment
        {
            public int Set { get; set; }

            public string? Bank { get; set; }

            public string? Sample { get; set; }

            public string? Source { get; set; }

            public string? Target { get; set; }
        }
    }
}
