namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.Transactions/System/Transactions/Oletx/DtcInterfaces.cs,64888bda16bd1769
    /// </summary>
    internal enum ShimNotificationType : int
    {
        None = 0,
        Phase0RequestNotify = 1,
        VoteRequestNotify = 2,
        PrepareRequestNotify = 3,
        CommitRequestNotify = 4,
        AbortRequestNotify = 5,
        CommittedNotify = 6,
        AbortedNotify = 7,
        InDoubtNotify = 8,
        EnlistmentTmDownNotify = 9,
        ResourceManagerTmDownNotify = 10
    }
}