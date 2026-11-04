namespace CyberManagement.Application.Wallet;

// Giá trị lưu ở WalletTransactions.Type. Dấu tiền: cộng ví là số dương, trừ ví là số âm.
public static class WalletTransactionTypes
{
    public const string TopUp = "TopUp";                       // + nạp ví
    public const string OrderRefund = "OrderRefund";           // + hoàn tiền món/đơn
    public const string TournamentRefund = "TournamentRefund"; // + hoàn lệ phí giải
    public const string GamingCharge = "GamingCharge";         // - phí phiên chơi
    public const string OrderPayment = "OrderPayment";         // - thanh toán đơn
    public const string TournamentFee = "TournamentFee";       // - lệ phí giải
    public const string Adjustment = "Adjustment";             // +/- điều chỉnh có quyền wallet.adjust

    public static bool IsCredit(string type) => type is TopUp or OrderRefund or TournamentRefund;
    public static bool IsDebit(string type) => type is GamingCharge or OrderPayment or TournamentFee;
    public static bool IsKnown(string type) => IsCredit(type) || IsDebit(type) || type == Adjustment;
}
