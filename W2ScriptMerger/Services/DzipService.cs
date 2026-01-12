using System.IO;
using System.IO.Compression;
using System.Text;
using W2ScriptMerger.Models;
using W2ScriptMerger.Tools;

namespace W2ScriptMerger.Services;

/// <summary>
/// Low-level helper for reading and writing Witcher 2 <c>.dzip</c> archives.
/// The format is a container similar to ZIP but with its own header/table layout.
/// </summary>
public static class DzipService
{
    // Magic number stored at the beginning of every Witcher 2 DZIP archive ("DZIP" as ASCII).
    // Validating this prevents us from trying to parse non-DZIP data with the custom layout.
    private const uint DzipMagic = 0x50495A44;
    private const uint DzipVersion = 2;

    /// <summary>
    /// Core reader that walks the DZIP header and entry table from an open stream.
    /// </summary>
    private static List<DzipEntry> ReadDzip(Stream? stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var magic = reader.ReadUInt32();
        if (magic is not DzipMagic)
            throw new InvalidDataException($"Invalid DZIP magic: expected 0x{DzipMagic:X8}, got 0x{magic:X8}");

        var version = reader.ReadUInt32();
        if (version < 2)
            throw new InvalidDataException($"Unsupported DZIP version: {version}");

        var entryCount = reader.ReadUInt32();
        reader.ReadUInt32(); // unknown/padding
        var entryTableOffset = reader.ReadInt64();
        // part of the DZIP header (an 8-byte checksum recorded after writing the entry table), but CDPR’s tools never validate it when reading.
        // Witcher 2’s runtime ignores it too: only the header fields (count, offsets, etc.) are required to load a dzip. Because the hash is effectively informational/legacy,
        // the current reader just advances past it and does not use it anywhere else.
       _ = reader.ReadUInt64();

        var entries = new List<DzipEntry>();
        stream.Seek(entryTableOffset, SeekOrigin.Begin);

        for (var i = 0; i < entryCount; i++)
        {
            var nameLength = reader.ReadUInt16();
            var nameBytes = reader.ReadBytes(nameLength);
            var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

            var entry = new DzipEntry
            {
                Name = name,
                TimeStamp = DateTime.FromFileTime(reader.ReadInt64()),
                ExpectedUncompressedSize = reader.ReadInt64(),
                Offset = reader.ReadInt64(),
                CompressedSize = reader.ReadInt64()
            };
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Lists all entries in a DZIP archive without extracting.
    /// </summary>
    public static List<DzipEntry> ListEntries(string dzipPath)
    {
        using var stream = File.OpenRead(dzipPath);
        return ReadDzip(stream);
    }

    /// <summary>
    /// Extracts every entry in the archive to a specified absolute path, preserving timestamps.
    /// </summary>
    /// <param name="dzipPath">Path to the DZIP archive to extract</param>
    /// <param name="outputDirectoryBase">Absolute path to extract to</param>
    /// <param name="ctx">Cancellation token</param>
    /// <returns>Full path to the extracted directory</returns>
    public static Task<string> UnpackDzipToAsync(string dzipPath, string outputDirectoryBase, CancellationToken ctx = default)
        => Task.Run(() => UnpackDzipTo(dzipPath, outputDirectoryBase), ctx);

    /// <summary>
    /// Extracts every entry in the archive to a specified absolute path, preserving timestamps.
    /// </summary>
    /// <param name="dzipPath">Path to the DZIP archive to extract</param>
    /// <param name="outputDirectoryBase">Absolute path to extract to</param>
    /// <returns>Full path to the extracted directory</returns>
    public static string UnpackDzipTo(string dzipPath, string outputDirectoryBase)
    {
        using var stream = File.OpenRead(dzipPath);
        var entries = ReadDzip(stream);

        foreach (var entry in entries)
        {
            var outputPath = Path.Combine(outputDirectoryBase, entry.Name.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var data = UnpackDzipEntry(stream, entry);
            File.WriteAllBytes(outputPath, data);
            File.SetLastWriteTime(outputPath, entry.TimeStamp);
        }

        return outputDirectoryBase;
    }

    /// <summary>
    /// Reads the raw bytes of an entry, inflating LZF-compressed payloads when needed.
    /// DZIP uses block-based compression: data is split into 64KB blocks, each independently LZF compressed.
    /// At the start of the compressed data is a table of uint32 offsets for each block.
    /// </summary>
    private static byte[] UnpackDzipEntry(Stream? stream, DzipEntry entry)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Seek(entry.Offset, SeekOrigin.Begin);

        // Data is not compressed, return as-is
        if (entry.CompressedSize == entry.ExpectedUncompressedSize)
        {
            var rawData = new byte[entry.CompressedSize];
            stream.ReadExactly(rawData, 0, (int)entry.CompressedSize);
            return rawData;
        }

        // Block-based LZF decompression (64KB blocks)
        const int blockSize = 0x10000; // 64KB
        var blockCount = (int)((entry.ExpectedUncompressedSize + blockSize - 1) / blockSize);

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // Read block offset table (blockCount + 1 entries, each is uint32 relative to entry.Offset)
        var offsets = new long[blockCount + 1];
        for (var i = 0; i <= blockCount; i++)
        {
            offsets[i] = entry.Offset + reader.ReadUInt32();
        }
        // Last offset is the end of compressed data
        offsets[blockCount] = entry.Offset + entry.CompressedSize;

        var output = new byte[entry.ExpectedUncompressedSize];
        var outputPosition = 0;
        var remaining = entry.ExpectedUncompressedSize;
        var uncompressedBlock = new byte[blockSize];

        for (var i = 0; i < blockCount; i++)
        {
            var blockCompressedSize = (int)(offsets[i + 1] - offsets[i]);
            var compressedBlock = new byte[blockCompressedSize];

            stream.Seek(offsets[i], SeekOrigin.Begin);
            stream.ReadExactly(compressedBlock, 0, blockCompressedSize);

            var decompressedSize = Lzf.Decompress(compressedBlock, uncompressedBlock);

            var bytesToCopy = (int)Math.Min(remaining, decompressedSize);
            Array.Copy(uncompressedBlock, 0, output, outputPosition, bytesToCopy);
            outputPosition += bytesToCopy;
            remaining -= bytesToCopy;
        }

        return output;
    }

    /// <summary>
    /// Packs every file beneath <paramref name="sourceDirectory"/> into a new <c>.dzip</c>.
    /// Uses block-based LZF compression (64KB blocks) to match the original DZIP format.
    /// </summary>
    public static void PackDzip(string outputPath, string sourceDirectory)
    {
        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        var entries = new List<DzipEntry>();

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        // Write header placeholder
        stream.Seek(32, SeekOrigin.Begin);

        const int blockSize = 0x10000; // 64KB blocks

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
            var fileData = File.ReadAllBytes(file);
            var fileInfo = new FileInfo(file);

            var offset = stream.Position;
            var blockCount = (fileData.Length + blockSize - 1) / blockSize;

            // Reserve space for offset table
            var offsetTablePosition = stream.Position;
            for (var i = 0; i <= blockCount; i++)
                writer.Write(0u); // placeholder

            // Compress and write each block, recording offsets
            var blockOffsets = new List<uint>();
            for (var i = 0; i < blockCount; i++)
            {
                blockOffsets.Add((uint)(stream.Position - offset));

                var blockStart = i * blockSize;
                var blockLength = Math.Min(blockSize, fileData.Length - blockStart);
                var blockData = new byte[blockLength];
                Array.Copy(fileData, blockStart, blockData, 0, blockLength);

                var compressedBlock = Lzf.Compress(blockData);
                stream.Write(compressedBlock, 0, compressedBlock.Length);
            }

            var compressedSize = stream.Position - offset;

            // Go back and write the actual offsets
            var endPosition = stream.Position;
            stream.Seek(offsetTablePosition, SeekOrigin.Begin);
            foreach (var blockOffset in blockOffsets)
                writer.Write(blockOffset);
            stream.Seek(endPosition, SeekOrigin.Begin);

            entries.Add(new DzipEntry
            {
                Name = relativePath,
                TimeStamp = fileInfo.LastWriteTime,
                ExpectedUncompressedSize = fileData.Length,
                Offset = offset,
                CompressedSize = compressedSize
            });
        }

        var entryTableOffset = stream.Position;

        // Write entry table
        foreach (var entry in entries)
        {
            var nameBytes = Encoding.ASCII.GetBytes(entry.Name + '\0');
            writer.Write((ushort)nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write(entry.TimeStamp.ToFileTime());
            writer.Write(entry.ExpectedUncompressedSize);
            writer.Write(entry.Offset);
            writer.Write(entry.CompressedSize);
        }

        // Calculate hash
        var hash = CalculateEntriesHash(entries);

        // Write header
        stream.Seek(0, SeekOrigin.Begin);
        writer.Write(DzipMagic);
        writer.Write(DzipVersion);
        writer.Write(entries.Count);
        writer.Write(0x64626267u); // "gbbd"
        writer.Write(entryTableOffset);
        writer.Write(hash);
    }

    /// <summary>
    /// Reproduces the hash stored in Witcher 2 dzips; used when writing headers.
    /// </summary>
    private static ulong CalculateEntriesHash(List<DzipEntry> entries)
    {
        // Hash seed used by CDPR tooling (matches FNV-1 variant observed in shipped assets).
        var hash = 0x00000000FFFFFFFFUL;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Name))
            {
                foreach (var c in entry.Name)
                {
                    hash ^= (byte)c;                      // fold each path character
                    hash *= 0x00000100000001B3UL;         // multiply by FNV prime
                }
                hash ^= (ulong)entry.Name.Length;         // include name length to differentiate same chars/order
                hash *= 0x00000100000001B3UL;
            }

            hash ^= (ulong)entry.TimeStamp.ToFileTime();  // bake in last-write time
            hash *= 0x00000100000001B3UL;
            hash ^= (ulong)entry.ExpectedUncompressedSize;        // uncompressed payload size
            hash *= 0x00000100000001B3UL;
            hash ^= (ulong)entry.Offset;                  // file position inside the archive
            hash *= 0x00000100000001B3UL;
            hash ^= (ulong)entry.CompressedSize;          // compressed payload size
            hash *= 0x00000100000001B3UL;
        }
        return hash;
    }
}
