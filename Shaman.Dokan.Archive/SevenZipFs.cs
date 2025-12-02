using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DokanNet;
using FileAccess = DokanNet.FileAccess;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.Security.AccessControl;

namespace Shaman.Dokan
{
    public class SevenZipFs : IDokanOperations
    {
        private readonly IArchive archive;
        private readonly Dictionary<string, IArchiveEntry> files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> dirs = new(StringComparer.OrdinalIgnoreCase);

        public SevenZipFs(string archivePath)
        {
            archive = ArchiveFactory.Open(archivePath);
            BuildTree();
        }

        private void BuildTree()
        {
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var path = entry.Key.Replace('\\', '/');
                files[path] = entry;

                string dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
                if (!dirs.ContainsKey(dir))
                    dirs[dir] = new List<string>();

                dirs[dir].Add(Path.GetFileName(path));
            }

            // Add directory entries
            foreach (var entry in archive.Entries.Where(e => e.IsDirectory))
            {
                var dir = entry.Key.Replace('\\', '/').TrimEnd('/');
                if (!dirs.ContainsKey(dir))
                    dirs[dir] = new List<string>();
            }
        }

        private bool IsDirectory(string path) =>
            dirs.ContainsKey(path.TrimEnd('/'));

        private bool Exists(string path) =>
            files.ContainsKey(path);

        public NtStatus CreateFile(
            string fileName, FileAccess access, FileShare share,
            FileMode mode, FileOptions options, FileAttributes attributes,
            IDokanFileInfo info)
        {
            fileName = fileName.Trim('/');

            if (fileName == "")
            {
                info.IsDirectory = true;
                return NtStatus.Success;
            }

            if (IsDirectory(fileName))
            {
                info.IsDirectory = true;
                return NtStatus.Success;
            }

            if (Exists(fileName))
                return NtStatus.Success;

            return NtStatus.ObjectNameNotFound;
        }

        public void Cleanup(string fileName, IDokanFileInfo info) { }
        public void CloseFile(string fileName, IDokanFileInfo info) { }

        public NtStatus ReadFile(
            string fileName, byte[] buffer, out int bytesRead,
            long offset, IDokanFileInfo info)
        {
            bytesRead = 0;
            fileName = fileName.Trim('/');

            if (!Exists(fileName))
                return NtStatus.ObjectNameNotFound;

            using var ms = new MemoryStream();
            files[fileName].WriteTo(ms);

            if (offset >= ms.Length)
                return NtStatus.Success;

            ms.Position = offset;
            bytesRead = ms.Read(buffer, 0, buffer.Length);
            return NtStatus.Success;
        }

        public NtStatus WriteFile(
            string fileName, byte[] buffer, out int written,
            long offset, IDokanFileInfo info)
        {
            written = 0;
            return NtStatus.AccessDenied;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
            => NtStatus.Success;

        public NtStatus GetFileInformation(
            string fileName, out FileInformation fi, IDokanFileInfo info)
        {
            fileName = fileName.Trim('/');
            fi = new FileInformation();

            if (fileName == "")
            {
                fi.FileName = "";
                fi.Attributes = FileAttributes.Directory;
                return NtStatus.Success;
            }

            if (IsDirectory(fileName))
            {
                fi.FileName = fileName;
                fi.Attributes = FileAttributes.Directory;
                return NtStatus.Success;
            }

            if (Exists(fileName))
            {
                var e = files[fileName];
                fi.FileName = fileName;
                fi.Attributes = FileAttributes.Normal;
                fi.Length = e.Size;
                fi.LastWriteTime = DateTime.Now;
                fi.CreationTime = DateTime.Now;
                return NtStatus.Success;
            }

            return NtStatus.ObjectNameNotFound;
        }

        public NtStatus FindFiles(
            string fileName, out IList<FileInformation> list, IDokanFileInfo info)
        {
            fileName = fileName.Trim('/');
            list = new List<FileInformation>();

            if (!dirs.TryGetValue(fileName, out var children))
                return NtStatus.Success;

            foreach (var e in children)
            {
                string full = fileName == "" ? e : $"{fileName}/{e}";

                if (IsDirectory(full))
                {
                    list.Add(new FileInformation
                    {
                        FileName = e,
                        Attributes = FileAttributes.Directory
                    });
                }
                else if (Exists(full))
                {
                    list.Add(new FileInformation
                    {
                        FileName = e,
                        Attributes = FileAttributes.Normal,
                        Length = files[full].Size
                    });
                }
            }

            return NtStatus.Success;
        }

        public NtStatus FindFilesWithPattern(
            string fileName, string searchPattern,
            out IList<FileInformation> filesOut, IDokanFileInfo info)
        {
            FindFiles(fileName, out filesOut, info);
            return NtStatus.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attr, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus SetFileTime(
            string fileName, DateTime? creation, DateTime? access,
            DateTime? write, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
            => NtStatus.Success;

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
            => NtStatus.Success;

        public NtStatus GetDiskFreeSpace(out long free, out long total, out long totalFree, IDokanFileInfo info)
        {
            total = totalFree = free = 10L * 1024 * 1024 * 1024;
            return NtStatus.Success;
        }

        public NtStatus GetVolumeInformation(
            out string label, out FileSystemFeatures features,
            out string fs, out uint maxNameLength, IDokanFileInfo info)
        {
            label = "ArchiveFS";
            fs = "NTFS";
            maxNameLength = 255;
            features = FileSystemFeatures.ReadOnlyVolume |
                       FileSystemFeatures.UnicodeOnDisk |
                       FileSystemFeatures.CasePreservedNames;

            return NtStatus.Success;
        }

        public NtStatus GetFileSecurity(
            string fileName, out FileSystemSecurity security,
            AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileSecurity(
            string fileName, FileSystemSecurity security,
            AccessControlSections sections, IDokanFileInfo info)
            => NtStatus.AccessDenied;

        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
            => NtStatus.Success;

        public NtStatus Unmounted(IDokanFileInfo info)
            => NtStatus.Success;

        public NtStatus FindStreams(
            string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new List<FileInformation>();
            return NtStatus.NotImplemented;
        }
    }
}
