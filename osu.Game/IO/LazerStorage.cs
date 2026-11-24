// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Database;
using Realms;
using Realms.Exceptions;

namespace osu.Game.IO
{
    /// <summary>
    /// Provides access to the user data of another g0v0! or lazer installation.
    /// </summary>
    /// <remarks>
    /// The target installation's database is always opened using this build's schema.
    /// If it was created by a different client (or a different schema version), a temporary copy is upgraded to match this build instead,
    /// so the user's files are never modified. In that case any data which this build does not know about is dropped from the copy.
    /// </remarks>
    public partial class LazerStorage : IDisposable
    {
        /// <summary>
        /// The directory (relative to the installation root) containing the global file store.
        /// </summary>
        public const string FILE_STORE_DIRECTORY = @"files";

        /// <summary>
        /// The root directory of the target installation.
        /// </summary>
        public readonly string InstallPath;

        public string DatabasePath => Path.Combine(InstallPath, OsuGameBase.CLIENT_DATABASE_FILENAME);

        public string FileStorePath => Path.Combine(InstallPath, FILE_STORE_DIRECTORY);

        public bool FileStoreAvailable => Directory.Exists(FileStorePath);

        public LazerStorage(string installPath)
        {
            InstallPath = installPath;
        }

        /// <summary>
        /// Whether the provided directory looks like the data directory of a g0v0!/lazer installation.
        /// </summary>
        public static bool IsUsableInstall(DirectoryInfo? directory)
            => directory != null && File.Exists(Path.Combine(directory.FullName, OsuGameBase.CLIENT_DATABASE_FILENAME));

        /// <summary>
        /// The absolute path of a file in the installation's file store.
        /// </summary>
        public string GetFilePath(INamedFileUsage usage) => GetFilePath(usage.File.Hash);

        /// <summary>
        /// The absolute path of a file in the installation's file store.
        /// </summary>
        public string GetFilePath(string hash) => Path.Combine(FileStorePath, getStoragePath(hash));

        /// <summary>
        /// Whether a file is actually present in the installation's file store.
        /// </summary>
        /// <remarks>
        /// The database can reference files which were pruned from the store, so the two have to be checked separately.
        /// </remarks>
        public bool HasFile(string hash) => File.Exists(GetFilePath(hash));

        private static string getStoragePath(string hash) => Path.Combine(hash.Remove(1), hash.Remove(2), hash);

        private readonly Lock configurationLock = new Lock();

        private RealmConfiguration? configuration;
        private string? temporaryDirectory;
        private bool disposed;

        /// <summary>
        /// Open a new realm context for the installation's database.
        /// </summary>
        /// <remarks>
        /// Realm contexts are thread-confined, so every consumer should open (and dispose) its own context.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The storage was already disposed.</exception>
        public Realm OpenRealm()
        {
            RealmConfiguration resolved;

            // Multiple consumers (the per-content counts, the readability check) may open the database at the same time.
            // Resolving the configuration can copy the whole database, so it may only ever happen once.
            lock (configurationLock)
            {
                // A late caller would otherwise create another temporary copy which nothing cleans up again.
                ObjectDisposedException.ThrowIf(disposed, this);

                resolved = configuration ??= buildConfiguration();
            }

            return Realm.GetInstance(resolved);
        }

        private RealmConfiguration buildConfiguration()
        {
            if (!File.Exists(DatabasePath))
                throw new FileNotFoundException(@"The selected installation does not contain a database.", DatabasePath);

            ulong schemaVersion = RealmAccess.SCHEMA_VERSION;

            // Best case: the installation is compatible with this build, and can be read in place without touching anything.
            if (canOpenRealm(DatabasePath, readOnly: true, ref schemaVersion))
            {
                Logger.Log($@"Database of the installation at {InstallPath} can be read in place.", LoggingTarget.Database);
                return createConfiguration(DatabasePath, readOnly: true, schemaVersion);
            }

            // Otherwise the schema is upgraded on a copy. A version increase is required for realm to accept
            // that properties/classes only known to the other client are dropped.
            ulong targetVersion = Math.Max(RealmAccess.SCHEMA_VERSION, schemaVersion + 1);

            temporaryDirectory = Path.Combine(Path.GetTempPath(), $@"g0v0-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);

            string temporaryDatabase = Path.Combine(temporaryDirectory, OsuGameBase.CLIENT_DATABASE_FILENAME);

            try
            {
                File.Copy(DatabasePath, temporaryDatabase, true);
            }
            catch (Exception e)
            {
                throw new IOException($@"Could not copy the database from {DatabasePath}.", e);
            }

            Logger.Log($@"Upgrading a copy of the database of installation at {InstallPath} (schema version {schemaVersion} -> {targetVersion}).", LoggingTarget.Database);

            if (!canOpenRealm(temporaryDatabase, readOnly: false, ref targetVersion))
                throw new InvalidOperationException(@"The database of the selected installation could not be read, as it was created by an incompatible version.");

            return createConfiguration(temporaryDatabase, readOnly: false, targetVersion);
        }

        private static RealmConfiguration createConfiguration(string databasePath, bool readOnly, ulong schemaVersion) => new RealmConfiguration(databasePath)
        {
            IsReadOnly = readOnly,
            SchemaVersion = schemaVersion,

            // Only ever used against a temporary copy which we are free to rewrite.
            // This allows properties or classes which this build does not know about to be dropped.
            MigrationCallback = readOnly ? null : (_, _) => { },
        };

        private static bool canOpenRealm(string databasePath, bool readOnly, ref ulong schemaVersion)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (Realm.GetInstance(createConfiguration(databasePath, readOnly, schemaVersion)))
                    {
                    }

                    return true;
                }
                catch (RealmException e)
                {
                    // The installation was created by a client with a different schema version.
                    // Realm only reports the version it found in the exception message, so retry once with that version.
                    if (attempt == 0 && tryGetExistingSchemaVersion(e, out ulong existingVersion))
                    {
                        schemaVersion = existingVersion;
                        continue;
                    }

                    Logger.Log($@"Could not open realm database at {databasePath}: {e.Message}", LoggingTarget.Database, LogLevel.Important);
                    return false;
                }
            }

            return false;
        }

        private static readonly Regex existing_schema_version_pattern = new Regex(@"last set version (\d+)", RegexOptions.Compiled);

        private static bool tryGetExistingSchemaVersion(RealmException exception, out ulong version)
        {
            version = 0;

            Match match = existing_schema_version_pattern.Match(exception.Message);
            return match.Success && ulong.TryParse(match.Groups[1].Value, out version);
        }

        public void Dispose()
        {
            string? directory;

            lock (configurationLock)
            {
                if (disposed)
                    return;

                disposed = true;
                configuration = null;
                directory = temporaryDirectory;
                temporaryDirectory = null;
            }

            if (directory == null)
                return;

            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception e)
            {
                // Realms which are still open keep the files locked on some platforms.
                Logger.Log($@"Could not clean up temporary migration directory {directory}: {e.Message}", LoggingTarget.Database);
            }
        }
    }
}
