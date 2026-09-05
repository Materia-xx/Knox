#if WINDOWS
using Azure.Core;
using Azure.Identity;

namespace Knox;

internal sealed class PersistentInteractiveBrowserTokenCredential : TokenCredential, IDisposable
{
    private const string TokenCacheName = "Knox_KeyVault";
    private readonly string _clientId;
    private readonly string _tenantId;
    private readonly AuthenticationRecordStore _recordStore;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private InteractiveBrowserCredential? _credential;

    public PersistentInteractiveBrowserTokenCredential(
        string connectionId,
        string clientId,
        string tenantId,
        AuthenticationRecordStore? recordStore = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("Client ID is required.", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        _clientId = clientId;
        _tenantId = tenantId;
        _recordStore = recordStore ?? new AuthenticationRecordStore(connectionId);
    }

    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        EnsureInitialized(requestContext, cancellationToken);
        return _credential!.GetToken(requestContext, cancellationToken);
    }

    public override async ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(requestContext, cancellationToken).ConfigureAwait(false);
        return await _credential!
            .GetTokenAsync(requestContext, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        _initializationLock.Dispose();
    }

    private void EnsureInitialized(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        _initializationLock.Wait(cancellationToken);
        try
        {
            if (_credential is not null)
            {
                return;
            }

            var record = _recordStore.Load(_clientId, _tenantId);
            var credential = CreateCredential(record);
            if (record is null)
            {
                AuthLog.Write("No matching Windows authentication record; starting interactive authentication.");
                record = credential.Authenticate(requestContext, cancellationToken);
                _recordStore.Save(record);
            }
            else
            {
                AuthLog.Write("Using the saved Windows authentication record and persistent token cache.");
            }

            _credential = credential;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task EnsureInitializedAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_credential is not null)
            {
                return;
            }

            var record = _recordStore.Load(_clientId, _tenantId);
            var credential = CreateCredential(record);
            if (record is null)
            {
                AuthLog.Write("No matching Windows authentication record; starting interactive authentication.");
                record = await credential
                    .AuthenticateAsync(requestContext, cancellationToken)
                    .ConfigureAwait(false);
                _recordStore.Save(record);
            }
            else
            {
                AuthLog.Write("Using the saved Windows authentication record and persistent token cache.");
            }

            _credential = credential;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private InteractiveBrowserCredential CreateCredential(AuthenticationRecord? record) =>
        new(new InteractiveBrowserCredentialOptions
        {
            TenantId = _tenantId,
            ClientId = _clientId,
            RedirectUri = new Uri("http://localhost"),
            AuthenticationRecord = record,
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions
            {
                Name = TokenCacheName
            }
        });
}
#endif
