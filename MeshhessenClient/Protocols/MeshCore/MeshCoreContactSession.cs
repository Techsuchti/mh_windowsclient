namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// Holds the active direct-message conversation without coupling contact
/// selection or history state to the WPF window.
/// </summary>
public sealed class MeshCoreContactSession
{
    public MeshCoreContact? Contact { get; private set; }
    public string PeerKeyPrefix => Contact is null || Contact.PublicKey.Length == 0
        ? string.Empty
        : Convert.ToHexString(Contact.PublicKey.AsSpan(0, Math.Min(6, Contact.PublicKey.Length)));

    public void Select(MeshCoreContact contact) => Contact = contact;
    public void Clear() => Contact = null;
}
