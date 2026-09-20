using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace murumsWiiModStudio
{
    internal sealed class StudioReleaseVersion : IComparable<StudioReleaseVersion>
    {
        internal readonly int Major, Minor, Patch, Stage, Number;
        internal bool IsPreview
        {
            get
            {
                return Stage < 3;
            }
        }

        private StudioReleaseVersion(int major, int minor, int patch, int stage, int number)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Stage = stage;
            Number = number;
        }

        internal static StudioReleaseVersion Parse(string text)
        {
            var match = Regex.Match(text ?? "", @"^v?(\d+)\.(\d+)\.(\d+)(?:-(alpha|beta|rc)(\d+))?$");
            int major, minor, patch, number = 0;
            if (!match.Success || !Int32.TryParse(match.Groups[1].Value, out major) || !Int32.TryParse(match.Groups[2].Value, out minor) || !Int32.TryParse(match.Groups[3].Value, out patch) || (match.Groups[5].Success && !Int32.TryParse(match.Groups[5].Value, out number)))
                return null;
            int stage = match.Groups[4].Value == "alpha" ? 0 : match.Groups[4].Value == "beta" ? 1 : match.Groups[4].Value == "rc" ? 2 : 3;
            return new StudioReleaseVersion(major, minor, patch, stage, number);
        }

        public int CompareTo(StudioReleaseVersion other)
        {
            if (other == null)
                return 1;
            int[] left =
            {
                Major,
                Minor,
                Patch,
                Stage,
                Number
            };
            int[] right =
            {
                other.Major,
                other.Minor,
                other.Patch,
                other.Stage,
                other.Number
            };
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return left[i].CompareTo(right[i]);
            return 0;
        }
    }

    internal sealed class StudioUpdateRelease
    {
        internal string Tag, Notes, DownloadUrl, Digest;
        internal long Size;
        internal StudioReleaseVersion Version;
    }

    internal static class StudioUpdate
    {
        internal const string Repository = "murums04/murums-Wii-Mod-Studio";
        internal const string ReleasesUrl = "https://github.com/" + Repository + "/releases";
        internal const long MaximumDownload = 768L * 1024 * 1024;
        internal static string InstallationRoot
        {
            get
            {
                return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            }
        }

        internal static bool CanInstall
        {
            get
            {
                return File.Exists(Path.Combine(InstallationRoot, "install-manifest.txt"));
            }
        }

        internal static StudioUpdateRelease Check()
        {
            using (var response = GetResponse("https://api.github.com/repos/" + Repository + "/releases?per_page=100"))
            using (var input = response.GetResponseStream())
            using (var reader = new StreamReader(input))
            {
                char[] buffer = new char[4096];
                var json = new System.Text.StringBuilder();
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    json.Append(buffer, 0, count);
                    if (json.Length > 4 * 1024 * 1024)
                        throw new InvalidDataException("Release response is too large.");
                }

                return SelectRelease(json.ToString(), StudioVersion.Current);
            }
        }

        internal static StudioUpdateRelease SelectRelease(string json, string currentVersion)
        {
            var current = StudioReleaseVersion.Parse(currentVersion);
            if (current == null)
                throw new InvalidDataException("Invalid installed version.");
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = 4 * 1024 * 1024
            };
            var releases = serializer.Deserialize<object[]>(json);
            if (releases == null)
                throw new InvalidDataException("Invalid release response.");
            StudioUpdateRelease best = null;
            StudioReleaseVersion newest = null;
            foreach (var item in releases)
            {
                var release = item as Dictionary<string, object>;
                if (release == null || Flag(release, "draft"))
                    continue;
                string tag = Text(release, "tag_name");
                var version = StudioReleaseVersion.Parse(tag);
                if (version == null || version.CompareTo(current) <= 0 || (!current.IsPreview && (version.IsPreview || Flag(release, "prerelease"))))
                    continue;
                if (newest == null || version.CompareTo(newest) > 0)
                    newest = version;
                object assetsValue;
                if (!release.TryGetValue("assets", out assetsValue))
                    continue;
                var assets = assetsValue as object[];
                if (assets == null)
                    continue;
                foreach (var asset in assets.OfType<Dictionary<string, object>>())
                {
                    string name = Text(asset, "name"), digest = Text(asset, "digest"), url = Text(asset, "browser_download_url");
                    long size;
                    if ((name != "murums.Wii.Mod.Studio.exe" && name != "murums Wii Mod Studio.exe") || Text(asset, "state") != "uploaded" || !ValidDownloadUrl(url) || !Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$") || !Int64.TryParse(Text(asset, "size"), out size) || size < 1 || size > MaximumDownload)
                        continue;
                    if (best == null || version.CompareTo(best.Version) > 0)
                        best = new StudioUpdateRelease
                        {
                            Tag = tag,
                            Notes = Text(release, "body"),
                            Version = version,
                            DownloadUrl = url,
                            Digest = digest.Substring(7),
                            Size = size
                        };
                }
            }

            if (newest != null && (best == null || newest.CompareTo(best.Version) > 0))
                throw new InvalidDataException(L.T("Eine neue Version wurde veröffentlicht, aber der geprüfte Windows-Download ist noch nicht verfügbar. Bitte später erneut prüfen.", "A new version was published, but its verified Windows download is not available yet. Please check again later."));
            return best;
        }

        static string Text(Dictionary<string, object> value, string key)
        {
            object result;
            return value.TryGetValue(key, out result) && result != null ? Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture) : "";
        }

        static bool Flag(Dictionary<string, object> value, string key)
        {
            return Text(value, key).Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ValidDownloadUrl(string address)
        {
            Uri uri;
            return Uri.TryCreate(address, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Host == "github.com" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal);
        }

        static HttpWebResponse GetResponse(string address)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2 on .NET Framework 4.x.
            for (int redirect = 0; redirect < 5; redirect++)
            {
                var request = (HttpWebRequest)WebRequest.Create(address);
                request.UserAgent = "murumsWiiModStudio/" + StudioVersion.Current;
                request.Accept = "application/vnd.github+json";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.AllowAutoRedirect = false;
                var response = (HttpWebResponse)request.GetResponse();
                if ((int)response.StatusCode < 300 || (int)response.StatusCode >= 400)
                    return response;
                string location = response.Headers["Location"];
                response.Dispose();
                Uri next;
                if (!Uri.TryCreate(new Uri(address), location, out next) || next.Scheme != "https" || !next.IsDefaultPort || next.UserInfo.Length != 0 || (next.Host != "github.com" && next.Host != "api.github.com" && next.Host != "release-assets.githubusercontent.com" && next.Host != "objects.githubusercontent.com"))
                    throw new InvalidDataException("Untrusted update redirect.");
                address = next.AbsoluteUri;
            }

            throw new InvalidDataException("Too many update redirects.");
        }

        internal static string Download(StudioUpdateRelease release, Action<int> progress, Func<bool> cancelled)
        {
            if (release == null || !ValidDownloadUrl(release.DownloadUrl))
                throw new InvalidDataException("Invalid update URL.");
            string directory = Path.Combine(Path.GetTempPath(), "murumsWiiModStudio-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "murums Wii Mod Studio.exe");
            try
            {
                using (var response = GetResponse(release.DownloadUrl))
                using (var source = response.GetResponseStream())
                using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    byte[] buffer = new byte[65536];
                    long total = 0;
                    int count;
                    while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancelled())
                            throw new OperationCanceledException();
                        total += count;
                        if (total > release.Size || total > MaximumDownload)
                            throw new InvalidDataException("Unexpected update size.");
                        target.Write(buffer, 0, count);
                        progress((int)(100 * total / release.Size));
                    }

                    if (total != release.Size)
                        throw new InvalidDataException("Incomplete download.");
                }

                Verify(path, release);
                return path;
            }
            catch
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                    Directory.Delete(directory);
                throw;
            }
        }

        internal static void Verify(string path, StudioUpdateRelease release)
        {
            using (var hash = SHA256.Create())
            using (var file = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "");
                if (!actual.Equals(release.Digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("Die Prüfsumme des Downloads stimmt nicht. Bitte erneut versuchen.", "The download checksum does not match. Please retry."));
            }
        }

        internal static void LaunchInstaller(string path, StudioUpdateRelease release)
        {
            Verify(path, release);
            var info = FileVersionInfo.GetVersionInfo(path);
            if (info.ProductName != "murums Wii Mod Studio" || StudioReleaseVersion.Parse(info.ProductVersion) == null || StudioReleaseVersion.Parse(info.ProductVersion).CompareTo(release.Version) != 0)
                throw new InvalidDataException("The downloaded installer has an unexpected version.");
            var start = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--update \"" + InstallationRoot + "\"",
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = false
            };
            Process.Start(start);
        }
    }
}
