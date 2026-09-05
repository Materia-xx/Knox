using System;

namespace Knox
{
    /// <summary>
    /// Thrown when a vault's secret list could not be fully enumerated after retries
    /// (typically sustained Key Vault throttling). Distinct from a plain access error
    /// so callers can tell "couldn't read this vault at all" from "read it but the
    /// list came back incomplete" and surface the difference to the user.
    /// </summary>
    public sealed class KnoxVaultLoadException : Exception
    {
        public KnoxVaultLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
