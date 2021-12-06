using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Octokit;

#nullable enable

namespace OpenTabletDriver.Desktop.Updater
{
    public abstract class Updater : IUpdater
    {
        private int updateSentinel = 1;
        private readonly GitHubClient github = new GitHubClient(new ProductHeaderValue("OpenTabletDriver"));
        private Release? latestRelease;

        protected readonly Version CurrentVersion;
        protected static readonly Version AssemblyVersion = new Version(typeof(IUpdater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
        protected string BinaryDirectory;
        protected string AppDataDirectory;
        protected string RollbackDirectory;
        protected string DownloadDirectory = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());

        protected Updater(Version? currentVersion, string binaryDir, string appDataDir, string rollbackDir)
        {
            CurrentVersion = currentVersion ?? AssemblyVersion;
            BinaryDirectory = binaryDir;
            RollbackDirectory = rollbackDir;
            AppDataDirectory = appDataDir;

            if (!Directory.Exists(RollbackDirectory))
                Directory.CreateDirectory(RollbackDirectory);
            if (!Directory.Exists(DownloadDirectory))
                Directory.CreateDirectory(DownloadDirectory);
        }

        public Task<bool> CheckForUpdates() => CheckForUpdates(true);

        public async Task<bool> CheckForUpdates(bool forced)
        {
            if (updateSentinel == 0)
                return false;

            if (forced || latestRelease == null)
                latestRelease = await github.Repository.Release.GetLatest("OpenTabletDriver", "OpenTabletDriver");

            var latestVersion = new Version(latestRelease.TagName[1..]); // remove `v` from `vW.X.Y.Z
            return latestVersion > CurrentVersion;
        }

        public async Task<Release?> GetRelease()
        {
            if (latestRelease == null)
            {
                await CheckForUpdates();
            }

            return latestRelease;
        }

        public async Task InstallUpdate()
        {
            // Skip if update is already installed, or in the process of installing
            if (Interlocked.CompareExchange(ref updateSentinel, 0, 1) == 1)
            {
                if (await CheckForUpdates(false))
                {
                    SetupRollback();
                    await Install(latestRelease!);
                }
            }
        }

        protected abstract Task Install(Release release);

        private void SetupRollback()
        {
            var versionRollbackDir = Path.Join(RollbackDirectory, CurrentVersion + "-old");

            ExclusiveFileOp(BinaryDirectory, RollbackDirectory, versionRollbackDir, "bin",
                static (source, target) => Move(source, target));
            ExclusiveFileOp(AppDataDirectory, RollbackDirectory, versionRollbackDir, "appdata",
                static (source, target) => Copy(source, target));
        }

        // Avoid moving/copying the rollback directory if under source directory
        private static void ExclusiveFileOp(string source, string rollbackDir, string versionRollbackDir, string target,
            Action<string, string> fileOp)
        {
            var rollbackTarget = Path.Join(versionRollbackDir, target);

            var childEntries = Directory
                .EnumerateFileSystemEntries(source)
                .Except(new[] { rollbackDir, versionRollbackDir });

            if (!Directory.Exists(rollbackTarget))
                Directory.CreateDirectory(rollbackTarget);

            foreach (var childEntry in childEntries)
            {
                fileOp(childEntry, Path.Join(rollbackTarget, Path.GetFileName(childEntry)));
            }
        }

        protected static void Move(string source, string target)
        {
            if (File.Exists(source))
            {
                var sourceFile = new FileInfo(source);
                sourceFile.MoveTo(target);
                return;
            }

            var sourceDir = new DirectoryInfo(source);
            var targetDir = new DirectoryInfo(target);
            if (targetDir.Exists)
            {
                foreach (var childEntry in sourceDir.EnumerateFileSystemInfos())
                {
                    if (childEntry is FileInfo file)
                    {
                        file.MoveTo(Path.Join(target, file.Name));
                    }
                    else if (childEntry is DirectoryInfo directory)
                    {
                        directory.MoveTo(Path.Join(target, directory.Name));
                    }
                }
            }
            else
            {
                Directory.Move(source, target);
            }
        }

        protected static void Copy(string source, string target)
        {
            if (File.Exists(source))
            {
                var sourceFile = new FileInfo(source);
                sourceFile.CopyTo(target);
                return;
            }

            if (!Directory.Exists(target))
                Directory.CreateDirectory(target);

            var sourceDir = new DirectoryInfo(source);
            foreach (var fileInfo in sourceDir.EnumerateFileSystemInfos())
            {
                if (fileInfo is FileInfo file)
                {
                    file.CopyTo(Path.Join(target, file.Name));
                }
                else if (fileInfo is DirectoryInfo directory)
                {
                    Copy(directory.FullName, Path.Join(target, directory.Name));
                }
            }
        }
    }
}