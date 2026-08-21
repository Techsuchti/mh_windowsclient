namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// MeshCore Companion Protocol command, response and push codes.
/// Values follow the current official Companion Protocol specification.
/// </summary>
public static class MeshCoreProtocolConstants
{
    public const byte CmdAppStart = 0x01;
    public const byte CmdSendTextMessage = 0x02;
    public const byte CmdSendChannelTextMessage = 0x03;
    public const byte CmdGetContacts = 0x04;
    public const byte CmdGetDeviceTime = 0x05;
    public const byte CmdSetDeviceTime = 0x06;
    public const byte CmdSendSelfAdvert = 0x07;
    public const byte CmdSetAdvertName = 0x08;
    public const byte CmdAddUpdateContact = 0x09;
    public const byte CmdSyncNextMessage = 0x0A;
    public const byte CmdSetRadioParams = 0x0B;
    public const byte CmdSetRadioTxPower = 0x0C;
    public const byte CmdResetPath = 0x0D;
    public const byte CmdSetAdvertLatLon = 0x0E;
    public const byte CmdRemoveContact = 0x0F;
    public const byte CmdShareContact = 0x10;
    public const byte CmdGetChannel = 0x13;
    public const byte CmdSetChannel = 0x14;
    public const byte CmdDeviceQuery = 0x16;

    public const byte RespOk = 0x00;
    public const byte RespError = 0x01;
    public const byte RespContactsStart = 0x02;
    public const byte RespContact = 0x03;
    public const byte RespEndOfContacts = 0x04;
    public const byte RespSelfInfo = 0x05;
    public const byte RespMsgSent = 0x06;
    public const byte RespContactMessage = 0x07;
    public const byte RespChannelMessage = 0x08;
    public const byte RespCurrentTime = 0x09;
    public const byte RespNoMoreMessages = 0x0A;
    public const byte RespBatteryAndStorage = 0x0C;
    public const byte RespDeviceInfo = 0x0D;
    public const byte RespContactMessageV3 = 0x10;
    public const byte RespChannelMessageV3 = 0x11;
    public const byte RespChannelInfo = 0x12;
    public const byte RespChannelDataReceived = 0x1B;

    public const byte PushAdvert = 0x80;
    public const byte PushPathUpdated = 0x81;
    public const byte PushSendConfirmed = 0x82;
    public const byte PushMessageWaiting = 0x83;
    public const byte PushRawData = 0x84;
    public const byte PushLoginSuccess = 0x85;
    public const byte PushLoginFail = 0x86;
    public const byte PushStatusResponse = 0x87;
    public const byte PushTraceData = 0x89;
    public const byte PushNewAdvert = 0x8A;
    public const byte PushTelemetryResponse = 0x8B;
    public const byte PushBinaryResponse = 0x8C;
    public const byte PushControlData = 0x8E;

    public const byte SerialAppToRadio = 0x3C;
    public const byte SerialRadioToApp = 0x3E;
}
