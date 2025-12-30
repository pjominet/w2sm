using System.IO;
using System.IO.Compression;
using System.Text;
using W2ScriptMerger.Models;

namespace W2ScriptMerger.Services;

public class DzipService
{
    private const uint DzipMagic = 0x50495A44; // "DZIP"
    private const uint DzipVersion = 2;

    public static List<DzipEntry> UnpackDzip(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return UnpackDzip(stream);
    }

    private static List<DzipEntry> UnpackDzip(Stream? stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        
        var magic = reader.ReadUInt32();
        if (magic != DzipMagic)
            throw new InvalidDataException($"Invalid DZIP magic: expected 0x{DzipMagic:X8}, got 0x{magic:X8}");

        var version = reader.ReadUInt32();
        if (version < 2)
            throw new InvalidDataException($"Unsupported DZIP version: {version}");

        var entryCount = reader.ReadUInt32();
        reader.ReadUInt32(); // unknown/padding
        var entryTableOffset = reader.ReadInt64();
        var entryTableHash = reader.ReadUInt64();

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
                UncompressedSize = reader.ReadInt64(),
                Offset = reader.ReadInt64(),
                CompressedSize = reader.ReadInt64()
            };
            entries.Add(entry);
        }

        return entries;
    }

    public static byte[] ExtractEntry(string dzipPath, DzipEntry entry)
    {
        using var stream = File.OpenRead(dzipPath);
        return ExtractEntry(stream, entry);
    }

    private static byte[] ExtractEntry(Stream? stream, DzipEntry entry)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Seek(entry.Offset, SeekOrigin.Begin);
        var compressedData = new byte[entry.CompressedSize];
        stream.ReadExactly(compressedData, 0, (int)entry.CompressedSize);

        if (entry.CompressedSize == entry.UncompressedSize)
            return compressedData;

        // Data is zlib compressed
        using var compressedStream = new MemoryStream(compressedData);
        // Skip zlib header (2 bytes)
        compressedStream.ReadByte();
        compressedStream.ReadByte();
        
        using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        deflateStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    public void ExtractAll(string dzipPath, string outputDirectory)
    {
        using var stream = File.OpenRead(dzipPath);
        var entries = UnpackDzip(stream);

        foreach (var entry in entries)
        {
            var outputPath = Path.Combine(outputDirectory, entry.Name.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var data = ExtractEntry(stream, entry);
            File.WriteAllBytes(outputPath, data);
            File.SetLastWriteTime(outputPath, entry.TimeStamp);
        }
    }

    public static void PackDzip(string outputPath, string sourceDirectory)
    {
        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        var entries = new List<DzipEntry>();

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        // Write header placeholder
        stream.Seek(32, SeekOrigin.Begin);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
            var fileData = File.ReadAllBytes(file);
            var fileInfo = new FileInfo(file);

            var offset = stream.Position;
            byte[] dataToWrite;
            long compressedSize;

            // Compress with zlib
            using (var compressedStream = new MemoryStream())
            {
                // Write zlib header
                compressedStream.WriteByte(0x78);
                compressedStream.WriteByte(0x9C);
                
                using (var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflateStream.Write(fileData, 0, fileData.Length);
                }
                
                dataToWrite = compressedStream.ToArray();
                compressedSize = dataToWrite.Length;
            }

            // If compression didn't help, store uncompressed
            if (compressedSize >= fileData.Length)
            {
                dataToWrite = fileData;
                compressedSize = fileData.Length;
            }

            stream.Write(dataToWrite, 0, dataToWrite.Length);

            entries.Add(new DzipEntry
            {
                Name = relativePath,
                TimeStamp = fileInfo.LastWriteTime,
                UncompressedSize = fileData.Length,
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
            writer.Write(entry.UncompressedSize);
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

    private static ulong CalculateEntriesHash(List<DzipEntry> entries)
    {
        var hash = 0x00000000FFFFFFFFUL;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Name))
            {
                foreach (var c in entry.Name)
                {
                    hash ^= (byte)c;
                    hash *= 0x00000100000001B3UL;
                }
                hash ^= (ulong)entry.Name.Length;
                hash *= 0x00000100000001B3UL;
            }

            hash ^= (ulong)entry.TimeStamp.ToFileTime();
            hash *= 0x00000100000001B3UL;
            hash ^= (ulong)entry.UncompressedSize;
            hash *= 0x00000100000001B3UL;
            hash ^= (ulong)entry.Offset;
            hash *= 0x00000100000001B3UL;
            hash ^= (ulong)entry.CompressedSize;
            hash *= 0x00000100000001B3UL;
        }
        return hash;
    }
}
