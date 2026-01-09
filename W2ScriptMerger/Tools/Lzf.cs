namespace W2ScriptMerger.Tools;

/// <summary>
/// LZF Compressor based on the work of Gibbet (https://github.com/gibbed/Gibbed.RED).
/// </summary>
internal static class Lzf
{
    /// <summary>
    /// Decompresses data using LibLZF algorithm
    /// </summary>
    /// <param name="input">Reference to the data to decompress</param>
    /// <param name="output">Reference to a buffer which will contain the decompressed data</param>
    /// <returns>The size of the decompressed archive in the output buffer</returns>
    internal static int Decompress(byte[] input, byte[] output)
    {
        var inputPosition = 0;
        var outputPosition = 0;

        var inputLength = input.Length;
        var outputLength = output.Length;

        while (inputPosition < inputLength)
        {
            uint controlByte = input[inputPosition++];

            if (controlByte < (1 << 5))
            {
                var copyLength = (int)(controlByte + 1);

                if (outputPosition + copyLength > outputLength)
                {
                    throw new InvalidOperationException();
                }

                Array.Copy(input, inputPosition, output, outputPosition, copyLength);
                inputPosition += copyLength;
                outputPosition += copyLength;
            }
            else
            {
                var matchLength = (int)(controlByte >> 5);
                var backOffset = (int)((controlByte & 0x1F) << 8);

                if (matchLength == 7)
                    matchLength += input[inputPosition++];

                matchLength += 2;

                backOffset |= input[inputPosition++];

                if (outputPosition + matchLength > outputLength)
                    throw new InvalidOperationException();

                backOffset = outputPosition - 1 - backOffset;
                if (backOffset < 0)
                    throw new InvalidOperationException();

                var blockSize = Math.Min(matchLength, outputPosition - backOffset);
                Array.Copy(output, backOffset, output, outputPosition, blockSize);
                outputPosition += blockSize;
                backOffset += blockSize;
                matchLength -= blockSize;

                while (matchLength > 0)
                {
                    output[outputPosition++] = output[backOffset++];
                    matchLength--;
                }
            }
        }

        return outputPosition;
    }

    /// <summary>
    /// Compresses data using LibLZF algorithm
    /// </summary>
    /// <param name="input">Reference to the data to compress</param>
    /// <returns>The compressed data as a byte array</returns>
    internal static byte[] Compress(byte[] input)
    {
        var inputLength = input.Length;
        // Allocate buffer with extra space for incompressible data
        var outputLength = inputLength + 64;
        var output = new byte[outputLength];

        var compressedSize = CompressInternal(input, inputLength, output, outputLength);
        return compressedSize == 0 ?
            // Compression failed, return uncompressed data
            input :
            // Return only the compressed portion
            output.Take(compressedSize).ToArray();
    }

    /// <summary>
    /// Compresses data using LibLZF algorithm (internal implementation)
    /// </summary>
    /// <param name="input">Reference to the data to compress</param>
    /// <param name="inputLength">Length of the data to compress</param>
    /// <param name="output">Reference to a buffer which will contain the compressed data</param>
    /// <param name="outputLength">Length of the compression buffer</param>
    /// <returns>The size of the compressed archive in the output buffer</returns>
    private static int CompressInternal(byte[] input, int inputLength, byte[] output, int outputLength)
    {
        const uint hashLog = 14;
        const uint hashSize = 1 << 14;
        const uint maxLiteral = 1 << 5;
        const uint maxOffset = 1 << 13;
        const uint maxReference = (1 << 8) + (1 << 3);

        var hashTable = new long[hashSize];

        Array.Clear(hashTable, 0, (int)hashSize);

        uint inputPosition = 0;
        uint outputPosition = 0;

        // ReSharper disable once UselessBinaryOperation
        var hashValue = (uint)((input[inputPosition] << 8) | input[inputPosition + 1]);
        var literalCount = 0;

        while (true)
        {
            if (inputPosition < inputLength - 2)
            {
                hashValue = (hashValue << 8) | input[inputPosition + 2];
                long hashSlot = (hashValue ^ (hashValue << 5)) >> (int)((3 * 8 - hashLog) - hashValue * 5) & (hashSize - 1);
                var reference = hashTable[hashSlot];
                hashTable[hashSlot] = inputPosition;


                long backOffset;
                if ((backOffset = inputPosition - reference - 1) < maxOffset
                    && inputPosition + 4 < inputLength
                    && reference > 0
                    && input[reference + 0] == input[inputPosition + 0]
                    && input[reference + 1] == input[inputPosition + 1]
                    && input[reference + 2] == input[inputPosition + 2])
                {
                    /* match found at *reference++ */
                    uint matchLength = 2;
                    var maxMatchLength = (uint)inputLength - inputPosition - matchLength;
                    maxMatchLength = maxMatchLength > maxReference ? maxReference : maxMatchLength;

                    if (outputPosition + literalCount + 1 + 3 >= outputLength)
                        return 0;

                    do
                    {
                        matchLength++;
                    } while (matchLength < maxMatchLength && input[reference + matchLength] == input[inputPosition + matchLength]);

                    if (literalCount != 0)
                    {
                        output[outputPosition++] = (byte)(literalCount - 1);
                        literalCount = -literalCount;
                        do
                        {
                            output[outputPosition++] = input[inputPosition + literalCount];
                        } while (++literalCount != 0);
                    }

                    matchLength -= 2;
                    inputPosition++;

                    if (matchLength < 7)
                        output[outputPosition++] = (byte)((backOffset >> 8) + (matchLength << 5));
                    else
                    {
                        output[outputPosition++] = (byte)((backOffset >> 8) + (7 << 5));
                        output[outputPosition++] = (byte)(matchLength - 7);
                    }

                    output[outputPosition++] = (byte)backOffset;

                    inputPosition += matchLength - 1;
                    hashValue = (uint)((input[inputPosition] << 8) | input[inputPosition + 1]);

                    hashValue = (hashValue << 8) | input[inputPosition + 2];
                    hashTable[(hashValue ^ (hashValue << 5)) >> (int)((3 * 8 - hashLog) - hashValue * 5) & (hashSize - 1)] = inputPosition;
                    inputPosition++;

                    hashValue = (hashValue << 8) | input[inputPosition + 2];
                    hashTable[(hashValue ^ (hashValue << 5)) >> (int)((3 * 8 - hashLog) - hashValue * 5) & (hashSize - 1)] = inputPosition;
                    inputPosition++;
                    continue;
                }
            }
            else if (inputPosition == inputLength)
                break;

            /* one more literal byte we must copy */
            literalCount++;
            inputPosition++;

            if (literalCount != maxLiteral)
                continue;

            if (outputPosition + 1 + maxLiteral >= outputLength)
                return 0;

            output[outputPosition++] = (byte)(maxLiteral - 1);
            literalCount = -literalCount;
            do
            {
                output[outputPosition++] = input[inputPosition + literalCount];
            } while (++literalCount != 0);
        }

        if (literalCount == 0)
            return (int)outputPosition;

        if (outputPosition + literalCount + 1 >= outputLength)
            return 0;

        output[outputPosition++] = (byte)(literalCount - 1);
        literalCount = -literalCount;
        do
        {
            output[outputPosition++] = input[inputPosition + literalCount];
        } while (++literalCount != 0);

        return (int)outputPosition;
    }
}
