namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,136a0ae205cc832d
    /// </summary>
    internal enum OletxTransactionOutcome : int
    {
        NotKnownYet = 0,
        Committed = 1,
        Aborted = 2
    }
}
