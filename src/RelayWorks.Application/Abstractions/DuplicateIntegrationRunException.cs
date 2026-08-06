namespace RelayWorks.Application.Abstractions;

public sealed class DuplicateIntegrationRunException : Exception
{
    public DuplicateIntegrationRunException(Exception innerException)
        : base("The tenant already has a run with this idempotency key.", innerException) { }
}
