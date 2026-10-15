using Microsoft.IO;

namespace Plus.Communication.Flash;

public static class PlusMemoryStream
{
    // pixelrp: with bounded free pools. The default manager keeps every block
    // and buffer ever handed back to it, so one burst - a stall with many
    // packet handlers waiting at once, a run of big room loads - raised a
    // floor under the emulator's memory that it never came back down from.
    // Past these sizes a returned buffer is left to the GC instead. 32 MB is
    // 256 blocks: set well above what ordinary traffic should have in use at
    // once, so the pools still serve it and only a burst's surplus is shed.
    //
    // The 128 KB block size is kept on purpose: GameClient.OnReceived reads a
    // whole received frame out of the stream's first block, so a frame
    // larger than one block cannot be read - a smaller block would make that
    // happen to ordinary frames.
    public static readonly RecyclableMemoryStreamManager Manager = new(
        maximumSmallPoolFreeBytes: 32L * 1024 * 1024,
        maximumLargePoolFreeBytes: 32L * 1024 * 1024);

    public static RecyclableMemoryStream GetStream() => (Manager.GetStream() as RecyclableMemoryStream)!;
    public static RecyclableMemoryStream GetStream(ReadOnlySpan<byte> buffer) => (Manager.GetStream(buffer) as RecyclableMemoryStream)!;
}