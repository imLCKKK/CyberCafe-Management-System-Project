namespace CyberManagement.Application.Security;

public sealed record RoleSeed(string Name, string Description, string[] Permissions);

public static class RolePermissionCatalog
{
    public static readonly IReadOnlyList<RoleSeed> Roles =
    [
        new("Admin", "Quản trị toàn hệ thống", [
            "employee.view", "employee.manage", "role.manage", "shift.view", "shift.manage", "audit.view",
            "customer.view", "customer.create", "customer.update", "customer.status.change",
            "wallet.view", "wallet.topup", "wallet.adjust", "wallet.refund",
            "computer.view", "computer.manage", "computer.status.update", "maintenance.view", "maintenance.manage",
            "session.view", "session.start", "session.pause", "session.end", "session.cancel",
            "product.view", "product.manage", "supplier.manage", "stockreceipt.view", "stockreceipt.create",
            "inventory.adjust", "order.view", "order.create", "order.status.update", "order.cancel",
            "invoice.view", "invoice.create", "invoice.void", "payment.create", "payment.refund",
            "promotion.view", "promotion.manage", "game.manage", "tournament.view", "tournament.manage",
            "tournament.register", "team.assign", "report.view"
        ]),
        new("Manager", "Quản lý vận hành", [
            "employee.view", "shift.view", "shift.manage", "audit.view",
            "customer.view", "customer.create", "customer.update", "customer.status.change",
            "wallet.view", "wallet.topup", "wallet.adjust", "wallet.refund",
            "computer.view", "computer.manage", "computer.status.update", "maintenance.view", "maintenance.manage",
            "session.view", "session.start", "session.pause", "session.end", "session.cancel",
            "product.view", "product.manage", "supplier.manage", "stockreceipt.view", "stockreceipt.create",
            "inventory.adjust", "order.view", "order.create", "order.status.update", "order.cancel",
            "invoice.view", "invoice.create", "invoice.void", "payment.create", "payment.refund",
            "promotion.view", "promotion.manage", "game.manage", "tournament.view", "tournament.manage",
            "tournament.register", "team.assign", "report.view"
        ]),
        new("Cashier", "Thu ngân", [
            "shift.view", "customer.view", "customer.create", "customer.update", "wallet.view", "wallet.topup",
            "computer.view", "session.view", "session.start", "session.pause", "session.end", "product.view",
            "order.view", "order.create", "order.status.update", "invoice.view", "invoice.create",
            "payment.create", "promotion.view", "tournament.view", "tournament.register"
        ]),
        new("Waiter", "Phục vụ", [
            "shift.view", "customer.view", "computer.view", "session.view", "product.view",
            "order.view", "order.create", "order.status.update"
        ]),
        new("Technician", "Kỹ thuật", [
            "shift.view", "computer.view", "computer.status.update", "maintenance.view", "maintenance.manage"
        ])
    ];

    public static IReadOnlyDictionary<string, string> PermissionDescriptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["employee.view"] = "Xem nhân viên",
            ["employee.manage"] = "Quản lý nhân viên",
            ["role.manage"] = "Quản lý vai trò và quyền",
            ["shift.view"] = "Xem ca làm việc",
            ["shift.manage"] = "Quản lý ca làm việc",
            ["audit.view"] = "Xem nhật ký kiểm toán",
            ["customer.view"] = "Xem khách hàng",
            ["customer.create"] = "Tạo khách hàng",
            ["customer.update"] = "Cập nhật khách hàng",
            ["customer.status.change"] = "Thay đổi trạng thái khách hàng",
            ["wallet.view"] = "Xem ví khách hàng",
            ["wallet.topup"] = "Nạp ví",
            ["wallet.adjust"] = "Điều chỉnh ví",
            ["wallet.refund"] = "Hoàn tiền ví",
            ["computer.view"] = "Xem máy tính",
            ["computer.manage"] = "Quản lý máy tính",
            ["computer.status.update"] = "Cập nhật trạng thái máy tính",
            ["maintenance.view"] = "Xem bảo trì",
            ["maintenance.manage"] = "Quản lý bảo trì",
            ["session.view"] = "Xem phiên chơi",
            ["session.start"] = "Mở phiên chơi",
            ["session.pause"] = "Tạm dừng phiên chơi",
            ["session.end"] = "Kết thúc phiên chơi",
            ["session.cancel"] = "Hủy phiên chơi",
            ["product.view"] = "Xem sản phẩm",
            ["product.manage"] = "Quản lý sản phẩm",
            ["supplier.manage"] = "Quản lý nhà cung cấp",
            ["stockreceipt.view"] = "Xem phiếu nhập kho",
            ["stockreceipt.create"] = "Tạo phiếu nhập kho",
            ["inventory.adjust"] = "Điều chỉnh tồn kho",
            ["order.view"] = "Xem đơn hàng",
            ["order.create"] = "Tạo đơn hàng",
            ["order.status.update"] = "Cập nhật trạng thái đơn hàng",
            ["order.cancel"] = "Hủy đơn hàng",
            ["invoice.view"] = "Xem hóa đơn",
            ["invoice.create"] = "Tạo hóa đơn",
            ["invoice.void"] = "Hủy hóa đơn",
            ["payment.create"] = "Tạo thanh toán",
            ["payment.refund"] = "Hoàn tiền thanh toán",
            ["promotion.view"] = "Xem khuyến mãi",
            ["promotion.manage"] = "Quản lý khuyến mãi",
            ["game.manage"] = "Quản lý trò chơi",
            ["tournament.view"] = "Xem giải đấu",
            ["tournament.manage"] = "Quản lý giải đấu",
            ["tournament.register"] = "Đăng ký giải đấu",
            ["team.assign"] = "Phân đội giải đấu",
            ["report.view"] = "Xem báo cáo"
        };
}
