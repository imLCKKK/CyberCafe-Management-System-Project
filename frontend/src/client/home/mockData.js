// Demo records follow the supplied DBML. Never send password hashes to the client.
export const CURRENT_CUSTOMER_ID = 1;
export function createDemoData(now = new Date()) {
  const at = (minutes) => new Date(now.getTime() + minutes * 60000).toISOString();
  return {
    Customers: [{ CustomerId: 1, FullName: 'Nguyễn Minh Anh', Username: 'minhanh', Phone: '0901234567', Email: 'minhanh@example.com', WalletBalance: 153000, Status: 'Active', CreatedAt: at(-43200) }],
    ComputerRooms: [{ RoomId: 1, RoomName: 'Phòng thi đấu', Floor: 2, Description: 'Không gian thi đấu' }],
    ComputerTypes: [{ ComputerTypeId: 1, TypeName: 'Gaming', CPU: 'Intel Core i5-12400F', RAM: '16 GB', GPU: 'RTX 3060', PricePerHour: 12000, Description: 'Máy hiệu năng cao' }],
    Computers: [{ ComputerId: 12, RoomId: 1, ComputerTypeId: 1, ComputerName: 'Máy 12', Status: 'Occupied' }],
    GamingSessions: [{ SessionId: 1027, ComputerId: 12, CustomerId: 1, StartTime: at(-135), EndTime: null, LastChargedAt: at(-5), TotalCost: 26000, Status: 'Active' }],
    SessionCharges: [{ ChargeId: 1, SessionId: 1027, StartTime: at(-135), EndTime: at(-5), Amount: 26000 }],
    Categories: [{ CategoryId: 1, CategoryName: 'Món ăn', IsActive: true }, { CategoryId: 2, CategoryName: 'Đồ uống', IsActive: true }, { CategoryId: 3, CategoryName: 'Ăn vặt', IsActive: true }],
    Products: [
      { ProductId: 1, CategoryId: 1, ProductName: 'Mì xào bò', Price: 28000, Stock: 20, IsActive: true },
      { ProductId: 2, CategoryId: 1, ProductName: 'Cơm gà xối mỡ', Price: 35000, Stock: 12, IsActive: true },
      { ProductId: 3, CategoryId: 2, ProductName: 'Trà đào cam sả', Price: 15000, Stock: 30, IsActive: true },
      { ProductId: 4, CategoryId: 2, ProductName: 'Cà phê sữa đá', Price: 20000, Stock: 18, IsActive: true },
      { ProductId: 5, CategoryId: 3, ProductName: 'Khoai tây chiên', Price: 22000, Stock: 10, IsActive: true },
      { ProductId: 6, CategoryId: 2, ProductName: 'Nước tăng lực', Price: 18000, Stock: 0, IsActive: true },
    ],
    Orders: [{ OrderId: 1048, CustomerId: 1, SessionId: 1027, EmployeeId: null, InvoiceId: null, OrderTime: at(-10), Status: 'Preparing', TotalAmount: 43000 }],
    OrderDetails: [{ OrderDetailId: 1, OrderId: 1048, ProductId: 1, Quantity: 1, UnitPrice: 28000 }, { OrderDetailId: 2, OrderId: 1048, ProductId: 3, Quantity: 1, UnitPrice: 15000 }],
    OrderStatusHistory: [{ HistoryId: 1, OrderId: 1048, OldStatus: 'Pending', NewStatus: 'Preparing', ChangedBy: null, ChangedAt: at(-8) }],
    InventoryTransactions: [],
    WalletTransactions: [{ TransactionId: 1, CustomerId: 1, Amount: 150000, BalanceAfter: 153000, Type: 'Deposit', Description: 'Nạp tiền tại quầy', CreatedAt: at(-140) }],
    Invoices: [{ InvoiceId: 1026, CustomerId: 1, SessionId: null, EmployeeId: null, PromotionId: null, InvoiceDate: at(-1440), Subtotal: 52000, Discount: 0, TotalAmount: 52000, Status: 'Paid' }],
    InvoiceDetails: [{ InvoiceDetailId: 1, InvoiceId: 1026, ItemType: 'Session', Description: 'Thời gian sử dụng máy', Quantity: 1, UnitPrice: 24000, Amount: 24000 }, { InvoiceDetailId: 2, InvoiceId: 1026, ItemType: 'Product', Description: 'Mì xào bò', Quantity: 1, UnitPrice: 28000, Amount: 28000 }],
    Payments: [{ PaymentId: 1, InvoiceId: 1026, WalletTransactionId: null, Amount: 52000, Method: 'Cash', PaymentTime: at(-1440), Status: 'Completed' }],
    Games: [{ GameId: 1, GameName: 'VALORANT', TeamSize: 5, IsActive: true }],
    GameRanks: ['Iron', 'Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond', 'Ascendant', 'Immortal', 'Radiant'].map((RankName, i) => ({ RankId: i + 1, GameId: 1, RankName, RankOrder: i + 1 })),
    Tournaments: [{ TournamentId: 1, GameId: 1, CreatedBy: null, TournamentName: 'Đấu trường cuối tuần', RegistrationStart: at(-1440), RegistrationEnd: at(2880), StartTime: at(4320), EntryFee: 0, Status: 'Open' }],
    TournamentRegistrations: [], Teams: [], TeamMembers: [],
  };
}
