using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Tests;

public sealed class MeshCoreCompanionProtocolTests
{
    [Fact]
    public void BleCommandIsSentWithoutStreamWrapper()
    {
        var codec = new MeshCoreCompanionFrameCodec();
        var result = codec.EncodeCommand(new byte[] { 0x01, 0x01, 0x02 }, MeshCoreCompanionTransport.Ble);

        Assert.Equal(new byte[] { 0x01, 0x01, 0x02 }, result);
    }

    [Fact]
    public void StreamCommandUsesLittleEndianLengthAndAppMarker()
    {
        var codec = new MeshCoreCompanionFrameCodec();
        var result = codec.EncodeCommand(new byte[] { 0x01, 0x02, 0x03 }, MeshCoreCompanionTransport.Stream);

        Assert.Equal(new byte[] { 0x3C, 0x03, 0x00, 0x01, 0x02, 0x03 }, result);
    }

    [Fact]
    public void StreamParserHandlesSplitFrames()
    {
        var codec = new MeshCoreCompanionFrameCodec();
        var first = codec.Feed(new byte[] { 0x3E, 0x03 }, MeshCoreCompanionTransport.Stream);
        var second = codec.Feed(new byte[] { 0x00, 0x05, 0x06, 0x07 }, MeshCoreCompanionTransport.Stream);

        Assert.Empty(first);
        var payload = Assert.Single(second);
        Assert.Equal(new byte[] { 0x05, 0x06, 0x07 }, payload);
    }

    [Fact]
    public void StreamParserSkipsNoiseBeforeFrame()
    {
        var codec = new MeshCoreCompanionFrameCodec();
        var result = codec.Feed(new byte[] { 0x00, 0xFF, 0x3E, 0x01, 0x00, 0x05 }, MeshCoreCompanionTransport.Stream);

        var payload = Assert.Single(result);
        Assert.Equal(new byte[] { 0x05 }, payload);
    }
}
