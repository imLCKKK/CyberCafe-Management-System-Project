namespace CyberManagement.API.Security;

// Tên policy khớp PermissionPolicyProvider ("permission:<tên quyền>") và RolePermissionCatalog.
public static class CustomerPolicies
{
    public const string View = "permission:customer.view";
    public const string Create = "permission:customer.create";
    public const string Update = "permission:customer.update";
    public const string StatusChange = "permission:customer.status.change";
    public const string WalletView = "permission:wallet.view";
    public const string WalletTopUp = "permission:wallet.topup";
}
