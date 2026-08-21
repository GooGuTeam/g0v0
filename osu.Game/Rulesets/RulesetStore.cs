// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using osu.Framework;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets
{
    public abstract class RulesetStore : IDisposable, IRulesetStore
    {
        private const string ruleset_library_prefix = @"osu.Game.Rulesets";
        private const string config_filename = @"rulesets.json";

        protected readonly Dictionary<Assembly, Type> LoadedAssemblies = new Dictionary<Assembly, Type>();
        protected readonly HashSet<Assembly> UserRulesetAssemblies = new HashSet<Assembly>();
        protected readonly Storage? RulesetStorage;
        protected readonly RulesetManagementConfig Config = new RulesetManagementConfig();

        private readonly List<RulesetEvent> events = new List<RulesetEvent>();

        public BindableBool BlockUnseenRulesets => Config.BlockUnseenRulesets;

        /// <summary>
        /// Loaded rulesets of all states.
        /// </summary>
        public IEnumerable<RulesetInfo> AllRulesets => AvailableRulesets.Concat(DisabledRulesets).Concat(BrokenRulesets);

        /// <summary>
        /// All available rulesets.
        /// </summary>
        public abstract IEnumerable<RulesetInfo> AvailableRulesets { get; }

        /// <summary>
        /// Rulesets that are disabled.
        /// </summary>
        public virtual List<RulesetInfo> DisabledRulesets => Config.DisabledRulesets;

        /// <summary>
        /// Rulesets that threw exceptions on load or caused the game to crash.
        /// </summary>
        public virtual List<RulesetInfo> BrokenRulesets => Config.BrokenRulesets;

        /// <inheritdoc />
        /// <summary>
        /// A chronological list of ruleset loading events.
        /// </summary>
        public virtual IEnumerable<RulesetEvent> Events => events;

        public event Action<RulesetLoadEvent>? OnLoaded;

        public event Action<RulesetErrorEvent>? OnError;

        protected RulesetStore(Storage? storage = null)
        {
            // On android in release configuration assemblies are loaded from the apk directly into memory.
            // We cannot read assemblies from cwd, so should check loaded assemblies instead.
            loadFromAppDomain();

            // This null check prevents Android from attempting to load the rulesets from disk,
            // as the underlying path "AppContext.BaseDirectory", despite being non-nullable, it returns null on android.
            // See https://github.com/xamarin/xamarin-android/issues/3489.
            if (RuntimeInfo.StartupDirectory.IsNotNull())
                loadFromDisk();

            // the event handler contains code for resolving dependency on the game assembly for rulesets located outside the base game directory.
            // It needs to be attached to the assembly lookup event before the actual call to loadUserRulesets() else rulesets located out of the base game directory will fail
            // to load as unable to locate the game core assembly.
            AppDomain.CurrentDomain.AssemblyResolve += resolveRulesetDependencyAssembly;

            RulesetStorage = storage?.GetStorageForDirectory(@"rulesets");

            if (RulesetStorage == null)
                return;

            if (RulesetStorage.Exists(config_filename))
            {
                using var configStream = RulesetStorage.GetStream(config_filename);
                using var sr = new StreamReader(configStream);

                try
                {
                    Config = JsonConvert.DeserializeObject<RulesetManagementConfig>(sr.ReadToEnd()) ?? new RulesetManagementConfig();
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"An error occurred while deserializing the ruleset config.");
                }
            }
            else
            {
                Logger.Log(@"The ruleset configuration file doesn't exist, creating.");
            }

            loadUserRulesets(RulesetStorage);
        }

        /// <summary>
        /// Write the ruleset configuration into the external storage.
        /// </summary>
        public void SaveConfiguration()
        {
            if (RulesetStorage == null)
                return;

            try
            {
                using var configStream = RulesetStorage.GetStream(config_filename, FileAccess.Write, FileMode.Create);
                using var sw = new StreamWriter(configStream);
                sw.Write(JsonConvert.SerializeObject(Config, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to save ruleset configuration.");
            }
        }

        /// <summary>
        /// Enable or disable a ruleset in the store config, then persist.
        /// Enabling a ruleset also marks it as trusted (adds to <see cref="RulesetManagementConfig.KnownRulesets"/>).
        /// </summary>
        public void SetRulesetEnabled(RulesetInfo ruleset, bool enabled)
        {
            if (enabled)
            {
                Config.DisabledRulesets.RemoveAll(r => r.Equals(ruleset));

                if (!Config.KnownRulesets.Any(r => r.Equals(ruleset)))
                    Config.KnownRulesets.Add(ruleset.Clone());
            }
            else
            {
                if (!Config.DisabledRulesets.Any(r => r.Equals(ruleset)))
                    Config.DisabledRulesets.Add(ruleset.Clone());
            }

            SaveConfiguration();
        }

        /// <summary>
        /// Adds the ruleset to <see cref="DisabledRulesets"/> if not already present,
        /// or updates the existing entry with fresh metadata. Used during loading to
        /// ensure disabled/untrusted rulesets appear in <see cref="AllRulesets"/> for display.
        /// </summary>
        protected void AddOrUpdateDisabledRuleset(RulesetInfo ruleset)
        {
            var existing = Config.DisabledRulesets.FirstOrDefault(r => r.Equals(ruleset));

            if (existing != null)
            {
                existing.Name = ruleset.Name;
                existing.InstantiationInfo = ruleset.InstantiationInfo;
                existing.OnlineID = ruleset.OnlineID;
                existing.Available = ruleset.Available;
            }
            else
            {
                Config.DisabledRulesets.Add(ruleset.Clone());
            }
        }

        /// <summary>
        /// Retrieve a ruleset using a known ID.
        /// </summary>
        /// <param name="id">The ruleset's internal ID.</param>
        /// <returns>A ruleset, if available, else null.</returns>
        public RulesetInfo? GetRuleset(int id) => AvailableRulesets.FirstOrDefault(r => r.OnlineID == id);

        /// <summary>
        /// Retrieve a ruleset using a known short name.
        /// </summary>
        /// <param name="shortName">The ruleset's short name.</param>
        /// <returns>A ruleset, if available, else null.</returns>
        public RulesetInfo? GetRuleset(string shortName) => AvailableRulesets.FirstOrDefault(r => r.ShortName == shortName);

        protected Assembly? GetUnderlyingAssembly(RulesetInfo ruleset)
            => Events.OfType<RulesetLoadEvent>()
                     .FirstOrDefault(r => ruleset.Equals(r.RulesetInfo))?
                     .Assembly;

        public bool PresentRulesetExternally(RulesetInfo ruleset)
        {
            var sourceEvent = Events.OfType<RulesetLoadEvent>()
                                    .FirstOrDefault(r => ruleset.Equals(r.RulesetInfo));
            string? location = sourceEvent?.Location;
            if (location == null) return false;

            return RulesetStorage?.PresentFileExternally(location) ?? false;
        }

        /// <summary>
        /// Records a ruleset loading event and raises the corresponding event handler.
        /// </summary>
        /// <param name="e">The event to record.</param>
        protected void AddEvent(RulesetEvent e)
        {
            events.Add(e);

            switch (e)
            {
                case RulesetLoadEvent loadEvent:
                    Logger.Log($"[Ruleset] Loaded new ruleset from {loadEvent.Location}.");
                    OnLoaded?.Invoke(loadEvent);
                    break;

                case RulesetErrorEvent errorEvent:
                    string finalName = errorEvent.RulesetInfo?.Name
                                       ?? errorEvent.Assembly?.GetName().Name!.Split('.').Last()
                                       ?? @"<unknown>";

                    Logger.Log($"[Ruleset] An issue with ruleset \"{finalName}\" occurred.", level: LogLevel.Error);
                    if (errorEvent.Exception != null)
                        Logger.Log(errorEvent.Exception.ToString());

                    OnError?.Invoke(errorEvent);
                    break;
            }
        }

        /// <summary>
        /// Associates any recorded events for the specified assembly with the given <see cref="RulesetInfo"/>.
        /// </summary>
        /// <param name="assembly">The assembly whose events should be associated.</param>
        /// <param name="rulesetInfo">The loaded ruleset info to attach.</param>
        protected void SetRulesetInfo(Assembly assembly, RulesetInfo rulesetInfo)
        {
            foreach (RulesetEvent evt in events.Where(evt => evt.Assembly == assembly && evt.RulesetInfo == null))
            {
                evt.RulesetInfo = rulesetInfo;
                Logger.Log($@"Updating ruleset event entry: {evt.Location} <=> {rulesetInfo.Name}");
            }
        }

        private Assembly? resolveRulesetDependencyAssembly(object? sender, ResolveEventArgs args)
        {
            var asm = new AssemblyName(args.Name);

            // the requesting assembly may be located out of the executable's base directory, thus requiring manual resolving of its dependencies.
            // this attempts resolving the ruleset dependencies on game core and framework assemblies by returning assemblies with the same assembly name
            // already loaded in the AppDomain.
            var domainAssembly = AppDomain.CurrentDomain.GetAssemblies()
                                          // Given name is always going to be equally-or-more qualified than the assembly name.
                                          .Where(a =>
                                          {
                                              string? name = a.GetName().Name;
                                              if (name == null)
                                                  return false;

                                              return args.Name.Contains(name, StringComparison.Ordinal);
                                          }).MaxBy(a => a.GetName().Version);

            if (domainAssembly != null)
                return domainAssembly;

            return LoadedAssemblies.Keys.FirstOrDefault(a => a.FullName == asm.FullName);
        }

        private void loadFromAppDomain()
        {
            foreach (var ruleset in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? rulesetName = ruleset.GetName().Name;

                if (rulesetName == null)
                    continue;

                if (!rulesetName.StartsWith(ruleset_library_prefix, StringComparison.InvariantCultureIgnoreCase) || rulesetName.Contains(@"Tests"))
                    continue;

                addRuleset(ruleset, RulesetSource.Builtin, ruleset.Location);
            }
        }

        private void loadUserRulesets(Storage rulesetStorage)
        {
            var rulesets = rulesetStorage.GetFiles(@".", @$"{ruleset_library_prefix}.*.dll");

            foreach (string? ruleset in rulesets.Where(f => !f.Contains(@"Tests")))
            {
                var assembly = loadRulesetFromFile(rulesetStorage.GetFullPath(ruleset), RulesetSource.User);
                if (assembly != null)
                    UserRulesetAssemblies.Add(assembly);
            }
        }

        private void loadFromDisk()
        {
            try
            {
                string[] files = Directory.GetFiles(RuntimeInfo.StartupDirectory, @$"{ruleset_library_prefix}.*.dll");

                foreach (string file in files.Where(f => !Path.GetFileName(f).Contains("Tests")))
                    loadRulesetFromFile(file, RulesetSource.Builtin);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Could not load rulesets from directory {RuntimeInfo.StartupDirectory}");
            }
        }

        private Assembly? loadRulesetFromFile(string file, RulesetSource source)
        {
            string filename = Path.GetFileNameWithoutExtension(file);

            if (LoadedAssemblies.Values.Any(t => Path.GetFileNameWithoutExtension(t.Assembly.Location) == filename))
                return null;

            try
            {
                var assembly = Assembly.LoadFrom(file);
                addRuleset(assembly, source, file);
                return assembly;
            }
            catch (Exception e)
            {
                AddEvent(new RulesetErrorEvent(null, e, file));
            }

            return null;
        }

        private void addRuleset(Assembly assembly, RulesetSource source, string location)
        {
            if (LoadedAssemblies.ContainsKey(assembly))
                return;

            // the same assembly may be loaded twice in the same AppDomain (currently a thing in certain Rider versions https://youtrack.jetbrains.com/issue/RIDER-48799).
            // as a failsafe, also compare by FullName.
            if (LoadedAssemblies.Any(a => a.Key.FullName == assembly.FullName))
                return;

            try
            {
                LoadedAssemblies[assembly] = assembly.GetTypes().First(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset)));
                AddEvent(new RulesetLoadEvent(assembly, source, location));
            }
            catch (Exception e)
            {
                AddEvent(new RulesetErrorEvent(assembly, e, location));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolveRulesetDependencyAssembly;
        }

        public static void LogRulesetFailure(RulesetInfo ruleset, Exception e) => logRulesetFailure(ruleset.Name, e);

        private static void logRulesetFailure(string name, Exception exception)
        {
            Logger.Log($"An issue with ruleset \"{name}\" occurred. Please check for an update from the developer.", level: LogLevel.Error);
            Logger.Log(exception.ToString());
        }

        #region Implementation of IRulesetStore

        IRulesetInfo? IRulesetStore.GetRuleset(int id) => GetRuleset(id);
        IRulesetInfo? IRulesetStore.GetRuleset(string shortName) => GetRuleset(shortName);
        IEnumerable<IRulesetInfo> IRulesetStore.AvailableRulesets => AvailableRulesets;

        #endregion
    }

    /// <summary>
    /// Represents an event entry during ruleset setup.
    /// </summary>
    public class RulesetEvent
    {
        /// <summary>
        /// The assembly that produced this event, if any.
        /// </summary>
        public Assembly? Assembly { get; init; }

        /// <summary>
        /// The ruleset info associated with this event. Populated after a ruleset is successfully resolved.
        /// </summary>
        public RulesetInfo? RulesetInfo { get; set; }

        /// <summary>
        /// The file system location relevant to this event (e.g. the loaded DLL path).
        /// </summary>
        public string Location { get; set; } = string.Empty;
    }

    /// <inheritdoc />
    /// <summary>
    /// A new ruleset was loaded into the ruleset store.
    /// </summary>
    public class RulesetLoadEvent : RulesetEvent
    {
        /// <summary>
        /// The location of the ruleset (builtin or user provided).
        /// </summary>
        public RulesetSource Source { get; init; }

        public RulesetLoadEvent(Assembly assembly, RulesetSource source, string location)
        {
            Assembly = assembly;
            Source = source;
            Location = location;
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// An error was occured while loading the ruleset assembly.
    /// </summary>
    public class RulesetErrorEvent : RulesetEvent
    {
        public Exception? Exception { get; init; }

        public RulesetErrorEvent(Assembly? assembly, Exception? exception, string location)
        {
            Assembly = assembly;
            Exception = exception;
            Location = location;
        }
    }

    public enum RulesetSource
    {
        /// <summary>
        /// The ruleset is loaded from the installation directory or AppDomain.
        /// </summary>
        Builtin,

        /// <summary>
        /// The ruleset is loaded from the "rulesets" directory.
        /// </summary>
        User,
    }
}
