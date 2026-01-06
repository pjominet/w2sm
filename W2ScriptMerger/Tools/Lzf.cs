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
    public static int Decompress(byte[] input, byte[] output)
    {
        var inputIndex = 0;
        var outputIndex = 0;

        var inputLength = input.Length;
        var outputLength = output.Length;

        while (inputIndex < inputLength)
        {
            uint control = input[inputIndex++];

            if (control < (1 << 5))
            {
                var length = (int)(control + 1);

                if (outputIndex + length > outputLength)
                {
                    throw new InvalidOperationException();
                }

                Array.Copy(input, inputIndex, output, outputIndex, length);
                inputIndex += length;
                outputIndex += length;
            }
            else
            {
                var length = (int)(control >> 5);
                var offset = (int)((control & 0x1F) << 8);

                if (length == 7)
                    length += input[inputIndex++];

                length += 2;

                offset |= input[inputIndex++];

                if (outputIndex + length > outputLength)
                    throw new InvalidOperationException();

                offset = outputIndex - 1 - offset;
                if (offset < 0)
                    throw new InvalidOperationException();

                var block = Math.Min(length, outputIndex - offset);
                Array.Copy(output, offset, output, outputIndex, block);
                outputIndex += block;
                offset += block;
                length -= block;

                while (length > 0)
                {
                    output[outputIndex++] = output[offset++];
                    length--;
                }
            }
        }

        return outputIndex;
    }

    /// <summary>
    /// Compresses data using LibLZF algorithm
    /// </summary>
    /// <param name="input">Reference to the data to compress</param>
    /// <param name="inputLength">Length of the data to compress</param>
    /// <param name="output">Reference to a buffer which will contain the compressed data</param>
    /// <param name="outputLength">Length of the compression buffer (should be bigger than the input buffer)</param>
    /// <returns>The size of the compressed archive in the output buffer</returns>
    public static int Compress(byte[] input, int inputLength, byte[] output, int outputLength)
    {
        const uint hlog = 14;
        const uint hsize = (1 << 14);
        const uint maxLit = (1 << 5);
        const uint maxOff = (1 << 13);
        const uint maxRef = ((1 << 8) + (1 << 3));

        var hashTable = new long[hsize];

        Array.Clear(hashTable, 0, (int)hsize);

        uint inputIndex = 0;
        uint outputIndex = 0;

        var hval = (uint)(((input[inputIndex]) << 8) | input[inputIndex + 1]);
        var lit = 0;

        for (;;)
        {
            if (inputIndex < inputLength - 2)
            {
                hval = (hval << 8) | input[inputIndex + 2];
                long hslot = ((hval ^ (hval << 5)) >> (int)(((3 * 8 - hlog)) - hval * 5) & (hsize - 1));
                var reference = hashTable[hslot];
                hashTable[hslot] = inputIndex;


                long off;
                if ((off = inputIndex - reference - 1) < maxOff
                    && inputIndex + 4 < inputLength
                    && reference > 0
                    && input[reference + 0] == input[inputIndex + 0]
                    && input[reference + 1] == input[inputIndex + 1]
                    && input[reference + 2] == input[inputIndex + 2]
                   )
                {
                    /* match found at *reference++ */
                    uint length = 2;
                    var maxLength = (uint)inputLength - inputIndex - length;
                    maxLength = maxLength > maxRef ? maxRef : maxLength;

                    if (outputIndex + lit + 1 + 3 >= outputLength)
                        return 0;

                    do
                    {
                        length++;
                    } while (length < maxLength && input[reference + length] == input[inputIndex + length]);

                    if (lit != 0)
                    {
                        output[outputIndex++] = (byte)(lit - 1);
                        lit = -lit;
                        do
                        {
                            output[outputIndex++] = input[inputIndex + lit];
                        } while ((++lit) != 0);
                    }

                    length -= 2;
                    inputIndex++;

                    if (length < 7)
                        output[outputIndex++] = (byte)((off >> 8) + (length << 5));
                    else
                    {
                        output[outputIndex++] = (byte)((off >> 8) + (7 << 5));
                        output[outputIndex++] = (byte)(length - 7);
                    }

                    output[outputIndex++] = (byte)off;

                    inputIndex += length - 1;
                    hval = (uint)(((input[inputIndex]) << 8) | input[inputIndex + 1]);

                    hval = (hval << 8) | input[inputIndex + 2];
                    hashTable[((hval ^ (hval << 5)) >> (int)(((3 * 8 - hlog)) - hval * 5) & (hsize - 1))] = inputIndex;
                    inputIndex++;

                    hval = (hval << 8) | input[inputIndex + 2];
                    hashTable[((hval ^ (hval << 5)) >> (int)(((3 * 8 - hlog)) - hval * 5) & (hsize - 1))] = inputIndex;
                    inputIndex++;
                    continue;
                }
            }
            else if (inputIndex == inputLength)
            {
                break;
            }

            /* one more literal byte we must copy */
            lit++;
            inputIndex++;

            if (lit != maxLit)
                continue;

            if (outputIndex + 1 + maxLit >= outputLength)
                return 0;

            output[outputIndex++] = (byte)(maxLit - 1);
            lit = -lit;
            do
            {
                output[outputIndex++] = input[inputIndex + lit];
            } while ((++lit) != 0);
        }

        if (lit == 0)
            return (int)outputIndex;

        if (outputIndex + lit + 1 >= outputLength)
            return 0;

        output[outputIndex++] = (byte)(lit - 1);
        lit = -lit;
        do
        {
            output[outputIndex++] = input[inputIndex + lit];
        } while ((++lit) != 0);

        return (int)outputIndex;
    }
}
