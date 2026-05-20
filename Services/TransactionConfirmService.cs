using Qubic.Core;

namespace Qubic.Toolkit;

/// <summary>
/// Shared "preview before broadcast" gate. Each transaction-sending page calls
/// <see cref="ConfirmAsync"/> right before it signs; a single modal mounted in
/// MainLayout subscribes via <see cref="OnRequest"/> and resolves the TCS.
/// </summary>
public sealed class TransactionConfirmService
{
    private TaskCompletionSource<bool>? _current;

    /// <summary>
    /// Raised when a page requests confirmation. The modal handles it and
    /// later calls <see cref="Resolve"/> with the user's choice.
    /// </summary>
    public event Action<TransactionPreview>? OnRequest;

    /// <summary>
    /// True while a confirmation is in flight — used by the modal to disable
    /// the page underneath.
    /// </summary>
    public TransactionPreview? Current { get; private set; }

    public Task<bool> ConfirmAsync(TransactionPreview preview)
    {
        // If a previous request is still pending (shouldn't happen with proper
        // UI gating but be safe), cancel it.
        _current?.TrySetResult(false);

        Current = preview;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _current = tcs;
        OnRequest?.Invoke(preview);
        return tcs.Task;
    }

    public void Resolve(bool confirmed)
    {
        var tcs = _current;
        _current = null;
        Current = null;
        tcs?.TrySetResult(confirmed);
    }
}

/// <summary>
/// Snapshot of the transaction that will be signed and broadcast if the user
/// confirms. Built by each page's <c>Broadcast</c> helper from the same args
/// it would otherwise pass straight to <c>Seed.CreateAndSignTransaction</c>.
/// </summary>
public sealed record TransactionPreview(
    string Action,
    long Amount,
    string Source,
    string Destination,
    uint TargetTick,
    ushort InputType,
    int InputSize,
    int? ContractIndex = null);
