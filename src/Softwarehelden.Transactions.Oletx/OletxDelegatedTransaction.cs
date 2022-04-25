using System;
using System.Transactions;

namespace Softwarehelden.Transactions.Oletx
{
    /// <summary>
    /// Wrapper for the original promotable single phase notification of the .NET data provider.
    /// </summary>
    internal sealed class OletxDelegatedTransaction : IPromotableSinglePhaseNotification
    {
        private readonly IPromotableSinglePhaseNotification promotableSinglePhaseNotification;
        private readonly Transaction transaction;

        /// <summary>
        /// Creates a new PSP notification.
        /// </summary>
        public OletxDelegatedTransaction(Transaction transaction, IPromotableSinglePhaseNotification promotableSinglePhaseNotification)
        {
            this.transaction = transaction;
            this.promotableSinglePhaseNotification = promotableSinglePhaseNotification;
        }

        /// <inheritdoc/>
        void IPromotableSinglePhaseNotification.Initialize()
        {
            this.promotableSinglePhaseNotification.Initialize();
        }

        /// <inheritdoc/>
        byte[] ITransactionPromoter.Promote()
        {
            byte[] propagationToken = this.promotableSinglePhaseNotification.Promote();

            // The MSDTC transaction identifier is not set when using a custom promoter type
            this.transaction.SetDistributedTransactionIdentifier(this, this.GetDistributedTransactionIdentifier(propagationToken));

            return propagationToken;
        }

        /// <inheritdoc/>
        void IPromotableSinglePhaseNotification.Rollback(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            this.promotableSinglePhaseNotification.Rollback(singlePhaseEnlistment);
        }

        /// <inheritdoc/>
        void IPromotableSinglePhaseNotification.SinglePhaseCommit(SinglePhaseEnlistment singlePhaseEnlistment)
        {
            this.promotableSinglePhaseNotification.SinglePhaseCommit(singlePhaseEnlistment);
        }

        private Guid GetDistributedTransactionIdentifier(byte[] propagationToken)
        {
            byte[] result = new byte[16];

            // MSDTC propagation token structure: dwVersionMin (4 bytes) | dwVersionMax (4 bytes) |
            // guidTx (16 bytes) | ..

            // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-dtco/f5911ac2-7663-477b-bf76-8d4d01cc090c

            Array.Copy(propagationToken, 8, result, 0, result.Length);

            return new Guid(result);
        }
    }
}
