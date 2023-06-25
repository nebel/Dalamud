using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Serilog;

namespace Dalamud.Game.Text.SeStringHandling.Payloads;

/// <summary>
/// An SeString Payload representing unhandled raw payload data.
/// Mainly useful for constructing unhandled hardcoded payloads, or forwarding any unknown
/// payloads without modification.
/// </summary>
public class RawPayload : Payload
{

    public bool isKnownType()
    {
        if (this.data.Length == 0)
        {
            return false;
        }

        if (this.subType == null)
        {
            return false;
        }

        if ((SeStringChunkType)this.chunkType == SeStringChunkType.Interactable
            && (EmbeddedInfoType)this.subType == EmbeddedInfoType.LinkTerminator)
        {
            return true;
        }

        return false;
    }

    public string InspectRaw()
    {
        string s;
        if (this.subType != null)
        {
            s = $"Payload: RAW[{(int)this.chunkType}/{(int)this.subType}] (0x{(int)this.chunkType:X}/0x{(int)this.subType:X})";
        }
        else
        {
            s = $"Payload: RAW[{(int)this.chunkType}] (0x{(int)this.chunkType:X})";
        }

        return s + $" (len={this.chunkLen}) \n" +
                    $"  {(this.data.Length == 0 ? "<EMPTY>" : BitConverter.ToString(this.data).Replace("-", " "))}" +
                    $" => {System.Text.Encoding.UTF8.GetString(this.data)}";
    }

    [JsonProperty]
    private byte chunkType;

    private uint chunkLen;

    [JsonProperty]
    internal byte[] data;

    private readonly byte? subType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RawPayload"/> class.
    /// </summary>
    /// <param name="data">The payload data.</param>
    public RawPayload(byte[] data)
    {
        // this payload is 'special' in that we require the entire chunk to be passed in
        // and not just the data after the header
        // This sets data to hold the chunk data fter the header, excluding the END_BYTE
        this.chunkType = data[1];
        this.data = data.Skip(3).Take(data.Length - 4).ToArray();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RawPayload"/> class.
    /// </summary>
    /// <param name="chunkType">The chunk type.</param>
    [JsonConstructor]
    internal RawPayload(byte chunkType, byte? subType, uint chunkLen)
    {
        this.chunkType = chunkType;
        this.subType = subType;
        this.chunkLen = chunkLen;
    }

    /// <summary>
    /// Gets a fixed Payload representing a common link-termination sequence, found in many payload chains.
    /// </summary>
    public static RawPayload LinkTerminator => new(new byte[] { 0x02, 0x27, 0x07, 0xCF, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03 });

    /// <inheritdoc/>
    public override PayloadType Type => PayloadType.Unknown;

    /// <summary>
    /// Gets the entire payload byte sequence for this payload.
    /// The returned data is a clone and modifications will not be persisted.
    /// </summary>
    [JsonIgnore]
    public byte[] Data
    {
        // this is a bit different from the underlying data
        // We need to store just the chunk data for decode to behave nicely, but when reading data out
        // it makes more sense to get the entire payload
        get
        {
            // for now don't allow modifying the contents
            // because we don't really have a way to track Dirty
            return (byte[])this.Encode().Clone();
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (obj is RawPayload rp)
        {
            if (rp.Data.Length != this.Data.Length) return false;
            return !this.Data.Where((t, i) => rp.Data[i] != t).Any();
        }

        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.Type, this.chunkType, this.data);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Type} - Data: {BitConverter.ToString(this.Data).Replace("-", " ")}";
    }

    /// <inheritdoc/>
    protected override byte[] EncodeImpl()
    {
        var chunkLen = this.data.Length + 1;

        var bytes = new List<byte>()
        {
            START_BYTE,
            this.chunkType,
            (byte)chunkLen,
        };
        bytes.AddRange(this.data);

        bytes.Add(END_BYTE);

        return bytes.ToArray();
    }

    /// <inheritdoc/>
    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        this.data = reader.ReadBytes((int)(endOfStream - reader.BaseStream.Position + 1));
    }
}
