#define UNHOOKER_DBG

using System;

using Dalamud.Logging.Internal;
using Dalamud.Memory;

namespace Dalamud.Hooking.Internal;

/// <summary>
/// A class which stores a copy of the bytes at a location which will be hooked in the future, such that those bytes can
/// be restored later to "unhook" the function.
/// </summary>
public class Unhooker
{
    private static readonly ModuleLog Log = new("UH");

    private readonly IntPtr address;
#if UNHOOKER_DBG
    private readonly string caller;
#endif
    private byte[] originalBytes;
    private bool trimmed;

#if UNHOOKER_DBG
    /// <summary>
    /// Initializes a new instance of the <see cref="Unhooker"/> class. Upon creation, the Unhooker stores a copy of
    /// the bytes stored at the provided address, and can be used to restore these bytes when the hook should be
    /// removed. As such this class should be instantiated before the function is actually hooked.
    /// </summary>
    /// <param name="address">The address which will be hooked.</param>
    /// <param name="sourceFilePath">The source path of the caller.</param>
    public Unhooker(IntPtr address, string sourceFilePath = "")
    {
        this.caller = sourceFilePath;
        this.address = address;
        MemoryHelper.ReadRaw(address, 0x32, out this.originalBytes);
    }
#else
    /// <summary>
    /// Initializes a new instance of the <see cref="Unhooker"/> class. Upon creation, the Unhooker stores a copy of
    /// the bytes stored at the provided address, and can be used to restore these bytes when the hook should be
    /// removed. As such this class should be instantiated before the function is actually hooked.
    /// </summary>
    /// <param name="address">The address which will be hooked.</param>
    public Unhooker(IntPtr address)
    {
        this.address = address;
        MemoryHelper.ReadRaw(address, 0x32, out this.originalBytes);
    }
#endif

    /// <summary>
    /// When called after a hook is created, checks the pre-hook original bytes and post-hook modified bytes, trimming
    /// the original bytes stored and removing unmodified bytes from the end of the byte sequence. Assuming no
    /// concurrent actions modified the same address space, this should result in storing only the minimum bytes
    /// required to unhook the function.
    /// </summary>
    public void TrimAfterHook()
    {
        if (this.trimmed)
        {
            return;
        }

#if UNHOOKER_DBG
        var naiveLength = this.GetNaiveHookLength();
        var fullLength = this.GetFullHookLength();
        var status = fullLength == naiveLength ? "HOOK-OK" : "HOOK-MISMATCH";
        Log.Verbose(
            $"Trimmed 0x{this.address.ToInt64():X}: naiveLength={naiveLength} fullLength={fullLength} <{this.caller}> ({status})");

        if (status == "HOOK-MISMATCH")
        {
            MemoryHelper.ReadRaw(this.address, this.originalBytes.Length, out var newBytes);
            Log.Verbose("Mismatch analysis:");
            Log.Verbose($"      Original: {Convert.ToHexString(this.originalBytes)}");
            Log.Verbose($"        Hooked: {Convert.ToHexString(newBytes)}");
            Log.Verbose($"  Naive Unhook: {Convert.ToHexString(this.originalBytes)[..(naiveLength * 2)]}");
            Log.Verbose($"   Full Unhook: {Convert.ToHexString(this.originalBytes)[..(fullLength * 2)]}");
        }
#endif

        this.originalBytes = this.originalBytes[..this.GetFullHookLength()];
        this.trimmed = true;
    }

    /// <summary>
    /// Attempts to unhook the function by replacing the hooked bytes with the original bytes. If
    /// <see cref="TrimAfterHook"/> was called, the trimmed original bytes stored at that time will be used for
    /// unhooking. Otherwise, a naive algorithm which only restores bytes until the first unchanged byte will be used in
    /// order to avoid overwriting adjacent data.
    /// </summary>
    public void Unhook()
    {
        var len = this.trimmed ? this.originalBytes.Length : this.GetNaiveHookLength();
        if (len > 0)
        {
            Log.Verbose($"Reverting hook at 0x{this.address.ToInt64():X} ({len} bytes, trimmed={this.trimmed})");
            MemoryHelper.ChangePermission(this.address, len, MemoryProtection.ExecuteReadWrite, out var oldPermissions);
            MemoryHelper.WriteRaw(this.address, this.originalBytes[..len]);
            MemoryHelper.ChangePermission(this.address, len, oldPermissions);
        }
    }

    private unsafe int GetNaiveHookLength()
    {
        var current = (byte*)this.address;
        for (var i = 0; i < this.originalBytes.Length; i++)
        {
            if (current[i] == this.originalBytes[i])
            {
                return i;
            }
        }

        return 0;
    }

    private unsafe int GetFullHookLength()
    {
        var current = (byte*)this.address;
        for (var i = this.originalBytes.Length - 1; i >= 0; i--)
        {
            if (current[i] != this.originalBytes[i])
            {
                return i + 1;
            }
        }

        return 0;
    }
}
