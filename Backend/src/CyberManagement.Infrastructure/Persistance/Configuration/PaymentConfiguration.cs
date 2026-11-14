using CyberManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberManagement.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Values", "[Amount] > 0 AND [Method] IN ('Wallet','Cash','ZaloPay') AND [Status] IN ('Pending','Succeeded','Failed','Expired') AND LEN(LTRIM(RTRIM([OperationKey]))) > 0");
            t.HasCheckConstraint("CK_Payments_Source", "([OrderId] IS NOT NULL AND [SessionChargeId] IS NULL) OR ([OrderId] IS NULL AND [SessionChargeId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Payments_Type", "([Type] = 'Payment' AND [RefundOfPaymentId] IS NULL AND [OrderDetailId] IS NULL) OR ([Type] = 'Refund' AND [RefundOfPaymentId] IS NOT NULL AND [RefundOfPaymentId] <> [PaymentId] AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [Status] <> 'Expired')");
            t.HasCheckConstraint("CK_Payments_Time", "([Status] = 'Succeeded' AND [PaymentTime] IS NOT NULL AND [ConfirmedAt] IS NOT NULL AND [PaymentTimeBasis] IS NOT NULL AND ([PaymentTimeBasis] = 'Source' OR ([PaymentTimeBasis] = 'Confirmation' AND [PaymentTime] = [ConfirmedAt]))) OR ([Status] <> 'Succeeded' AND [PaymentTime] IS NULL AND [ConfirmedAt] IS NULL AND [PaymentTimeBasis] IS NULL)");
            t.HasCheckConstraint("CK_Payments_Method", "([Method] <> 'Wallet' OR [Status] <> 'Succeeded' OR [WalletTransactionId] IS NOT NULL) AND ([Method] <> 'ZaloPay' OR [Type] <> 'Payment' OR ([MerchantTransactionId] IS NOT NULL AND LEN(LTRIM(RTRIM([MerchantTransactionId]))) > 0 AND ([Status] <> 'Succeeded' OR ([GatewayTransactionId] IS NOT NULL AND LEN(LTRIM(RTRIM([GatewayTransactionId]))) > 0))))");
        });
        builder.HasKey(x => x.PaymentId);
        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.Type).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(10).IsRequired();
        builder.Property(x => x.PaymentTimeBasis).HasMaxLength(12);
        builder.Property(x => x.OperationKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(x => x.MerchantTransactionId).HasMaxLength(40);
        builder.Property(x => x.GatewayTransactionId).HasMaxLength(50);
        builder.Property(x => x.Reason).HasMaxLength(250);
        builder.HasIndex(x => x.OperationKey).IsUnique();
        builder.HasIndex(x => x.WalletTransactionId).IsUnique().HasFilter("[WalletTransactionId] IS NOT NULL");
        builder.HasIndex(x => new { x.Method, x.MerchantTransactionId }).IsUnique().HasFilter("[MerchantTransactionId] IS NOT NULL AND [Type] = 'Payment'");
        builder.HasIndex(x => new { x.Method, x.GatewayTransactionId }).IsUnique().HasFilter("[GatewayTransactionId] IS NOT NULL AND [Type] = 'Payment'");
        builder.HasIndex(x => new { x.RefundOfPaymentId, x.Status });
        builder.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.SessionCharge).WithMany().HasForeignKey(x => x.SessionChargeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.OrderDetail).WithMany().HasForeignKey(x => x.OrderDetailId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.WalletTransaction).WithMany().HasForeignKey(x => x.WalletTransactionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RefundOfPayment).WithMany().HasForeignKey(x => x.RefundOfPaymentId).OnDelete(DeleteBehavior.NoAction);
    }
}
