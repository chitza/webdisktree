using System.Text.RegularExpressions;
using WebDiskTree.Core.Abstractions;

namespace WebDiskTree.Infrastructure.Security;

/// <summary>Reads /proc/self/mountinfo on every call, so newly attached drives show up without a restart.</summary>
public partial class LinuxMountTable : MountTableBase
{
    public override IReadOnlyList<MountInfo> GetMounts() => Parse(File.ReadAllText("/proc/self/mountinfo"));

    // Format (proc(5)): id parent major:minor root mount-point mount-options [optional fields...] - fstype source super-options
    public static IReadOnlyList<MountInfo> Parse(string text)
    {
        var mounts = new List<MountInfo>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(" - ", StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var fields = line[..separator].Split(' ');
            var after = line[(separator + 3)..].Split(' ');
            if (fields.Length < 6 || after.Length < 1)
            {
                continue;
            }

            var isReadOnly = fields[5].Split(',').Contains("ro");
            mounts.Add(new MountInfo(Unescape(fields[4]), after[0], isReadOnly));
        }

        return mounts;
    }

    private static string Unescape(string value) =>
        OctalEscape().Replace(value, m => ((char)Convert.ToInt32(m.Groups[1].Value, 8)).ToString());

    [GeneratedRegex(@"\\([0-7]{3})")]
    private static partial Regex OctalEscape();
}
