using System.IO.Compression;
using System.Text;

namespace Gradient.CryptoAnalysis.Site;

/// <summary>
/// Keeps on disk what the server works out from the candles and is slow to work out again, so it is there after a
/// restart: a folder for each build of the rules, as what one build worked out another may not agree with. A build is told
/// by the CryptoAnalysis library itself, which is the same from one build to the next while its code is, so nothing has to
/// be remembered when the rules change. Each thing kept is named by what it was worked out from, with the fingerprint of
/// its candles (see <see cref="PriceFingerprint"/>) in the name, so it is not found again once they have changed.
/// </summary>
public sealed class StructureCache(string root)
{
    /// <summary>
    /// How long another build's folder is left once nothing has been written to it, before it is removed.
    /// </summary>
    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(7);

    /// <summary>
    /// The build of the rules: the CryptoAnalysis library's own id.
    /// </summary>
    public static string Build => typeof(Sawtooth).Assembly.ManifestModule.ModuleVersionId.ToString("N");

    /// <summary>
    /// This build's folder.
    /// </summary>
    public string Folder { get; } = Path.Combine(root, Build);

    /// <summary>
    /// A text by its name, a path in this build's folder: read from there when it is kept, else made and kept. A folder
    /// that cannot be read or written to keeps nothing, and the text is made each time.
    /// </summary>
    public string Text(string name, Func<string> make)
    {
        var path = Path.Combine(Folder, $"{name}.gz");
        try
        {
            if (File.Exists(path))
            {
                using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress), Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
        }

        var text = make();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Written beside the file and moved over it, so a reader never has half of one.
            var writing = $"{path}.{Guid.NewGuid():N}.tmp";
            using (var writer = new StreamWriter(new GZipStream(File.Create(writing), CompressionLevel.Fastest), new UTF8Encoding(false)))
                writer.Write(text);
            File.Move(writing, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return text;
    }

    /// <summary>
    /// Removes the folders of other builds that nothing has been written to for <see cref="KeptFor"/>.
    /// </summary>
    public void Prune()
    {
        try
        {
            if (!Directory.Exists(root))
                return;
            foreach (var other in Directory.GetDirectories(root).Where(x => !string.Equals(Path.GetFullPath(x), Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase)))
            {
                var written = Directory.EnumerateFileSystemEntries(other, "*", SearchOption.AllDirectories).Select(Directory.GetLastWriteTimeUtc)
                    .Append(Directory.GetLastWriteTimeUtc(other)).Max();
                if (DateTime.UtcNow - written > KeptFor)
                    Directory.Delete(other, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
