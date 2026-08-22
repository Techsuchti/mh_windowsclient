using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>Connects the MeshCore companion contact stream to the UI-facing node registry.</summary>
public sealed class MeshCoreContactSyncService : IDisposable
{
    private readonly MeshCoreCompanionService _companion;
    private readonly MeshCoreNodeRegistry _registry;

    public MeshCoreContactSyncService(MeshCoreCompanionService companion, MeshCoreNodeRegistry registry)
    {
        _companion = companion ?? throw new ArgumentNullException(nameof(companion));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _companion.ContactsSynchronized += OnContactsSynchronized;
    }

    public int SynchronizeCurrentContacts() => _registry.UpsertRange(_companion.Contacts);

    private void OnContactsSynchronized(object? sender, IReadOnlyList<MeshCoreContact> contacts)
        => _registry.UpsertRange(contacts);

    public void Dispose() => _companion.ContactsSynchronized -= OnContactsSynchronized;
}
