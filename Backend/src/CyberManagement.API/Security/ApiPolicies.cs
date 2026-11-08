namespace CyberManagement.API.Security;

public static class ApiPolicies
{
    public const string RoleManage = "permission:role.manage";
    public const string ComputerView = "permission:computer.view";
    public const string ComputerStatusUpdate = "permission:computer.status.update";
    public const string CustomerView = "permission:customer.view";
    public const string CustomerCreate = "permission:customer.create";
    public const string WalletView = "permission:wallet.view";
}
