namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,125c49a0017a4172
    /// </summary>
    internal enum OletxPrepareVoteType : int
    {
        ReadOnly = 0,
        SinglePhase = 1,
        Prepared = 2,
        Failed = 3,
        InDoubt = 4
    }
}