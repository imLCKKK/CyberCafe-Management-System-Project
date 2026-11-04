# 002 – Hợp đồng ví và sổ cái (S1-M-1 / SCRUM-5)

Áp dụng cho schema 35 bảng trên `dev`. Mọi module có đụng đến tiền khách (phiên chơi, đơn món, chứng từ, giải đấu, cổng thanh toán) gọi ví **chỉ qua `WalletLedger`**, không tự sửa `Customers.WalletBalance` hay tự thêm `WalletTransactions`.

Mã nguồn: `Application/Wallet/WalletLedger.cs`, `WalletTransactionTypes.cs`; khóa dòng và transaction ở `Infrastructure/Repositories/CustomerRepository.cs`.

## 1. Đầu vào và quy ước dấu

`WalletLedger.Record(customer, type, amount, recordedAtUtc, description?)`

| `Type` (lưu ở `WalletTransactions.Type`) | Dấu `amount` | Ai gọi (dự kiến) |
|---|---|---|
| `TopUp` | + | Nạp tại quầy (S1-M-1); cổng ZaloPay (S1-M-2) |
| `OrderRefund` | + | Đơn/món (Khang) |
| `TournamentRefund` | + | Giải đấu (Trường) |
| `GamingCharge` | − | Phiên chơi (Phúc) |
| `OrderPayment` | − | Đơn/món (Khang) |
| `TournamentFee` | − | Giải đấu (Trường) |
| `Adjustment` | + hoặc − | Điều chỉnh có quyền `wallet.adjust` |

- Tiền là số nguyên VND, khác 0. `Amount` và `BalanceAfter` lưu `decimal(12,2)`.
- Số tiền cộng ví luôn dương, trừ ví luôn âm (trừ `Adjustment`). Sai dấu hoặc sai loại: `ArgumentException` (HTTP 400).
- `BalanceAfter` = số dư sau giao dịch; `Description` ghi nguồn (ví dụ `Phiên #12`, `Đơn #34`) vì bảng chưa có cột khóa nguồn (xem mục 4).

## 2. Kết quả và lỗi

| Tình huống | Kết quả |
|---|---|
| Thành công | Đổi `customer.WalletBalance` và thêm một `WalletTransaction` vào `customer.WalletTransactions`; **chưa lưu DB** |
| Khách không `Active` (đang khóa) | `BusinessRuleException` → 409, **trừ** `OrderRefund` và `TournamentRefund`: hoàn tiền vẫn được ghi để luồng hủy đơn/hủy giải không bị kẹt |
| Số dư sau giao dịch < 0 | `InsufficientWalletBalanceException` → 409; không đổi số dư, không thêm ledger |
| Khách không tồn tại | `EntityNotFoundException` → 404 |

## 3. Transaction, khóa và xung đột

`Record` **không** gọi `SaveChanges`, **không** commit. Nơi gọi phải:

1. Mở transaction: `repository.ExecuteInTransactionAsync(...)`.
2. Tải khách bằng `FindTrackedForWalletUpdateAsync` (`UPDLOCK, ROWLOCK`): giữ khóa dòng tới khi commit, nên hai request đồng thời cùng một ví xử lý lần lượt, không mất cập nhật và không cùng vượt qua kiểm tra số dư.
3. Gọi `WalletLedger.Record`, thay đổi dữ liệu nghiệp vụ của mình (phiên, đơn, chứng từ...) rồi gọi **một** `SaveChangesAsync` để nghiệp vụ và ví lưu cùng nhau.
4. Commit. Có lỗi ở bất kỳ bước nào thì transaction rollback: số dư, ledger và dữ liệu nghiệp vụ cùng quay lại.

Xung đột: trùng khóa duy nhất, deadlock (1205) hoặc `DbUpdateConcurrencyException` được `CustomerRepository.SaveChangesAsync` đổi thành `BusinessRuleException` (409). Client được phép thử lại; **server không tự retry**.

## 4. Chống xử lý lặp (CHƯA ĐỦ — cần chốt với Trường)

Schema hiện tại không có khóa chống lặp cho ví. Trạng thái thật:

- Khóa dòng chỉ chống **hai request chạy cùng lúc làm sai số dư**. Nó **không** biết hai request là "cùng một nghiệp vụ".
- Một request gửi lại (bấm đúp, mạng chập chờn, callback ZaloPay gọi hai lần) sẽ bị ghi **hai lần**.

Đề xuất cần Trường/Trưởng nhóm duyệt (thêm migration mới, không sửa lịch sử đã áp):

- `WalletTransactions.ReferenceType varchar(30) null` và `ReferenceId int null` (ví dụ `GamingSession`/12, `Order`/34, `GatewayTransaction`/7).
- Chỉ mục duy nhất có lọc trên `(Type, ReferenceType, ReferenceId)` khi `ReferenceId IS NOT NULL`. Nghiệp vụ nào được ghi nhiều lần theo thiết kế (phí phiên theo từng chu kỳ) thì thêm `ChargeSequence`.

Khi có cột này, module gọi `Record` truyền khóa nguồn; request trùng bị DB từ chối (409) và rollback. **Tới lúc đó, mỗi module tự chịu trách nhiệm không gọi hai lần cho cùng một nghiệp vụ.**

## 5. Thời điểm cộng/trừ ví

| Nghiệp vụ | Thời điểm |
|---|---|
| Nạp tại quầy | Nhân viên xác nhận đã thu tiền (`Cash`/`BankTransfer`) rồi bấm nạp: **cộng ví ngay**, cùng transaction. Không có trạng thái "đã cộng nhưng chưa thu" |
| Nạp qua ZaloPay | Chỉ cộng khi server xác minh chữ ký callback/truy vấn trạng thái; redirect trình duyệt không phải bằng chứng (S1-M-2) |
| Phí phiên chơi | Phúc chốt chu kỳ trừ; mỗi lần trừ là một `GamingCharge` |
| Đơn món | Trừ khi xác nhận đơn (Khang chốt); hủy/hoàn món là `OrderRefund` |
| Giải đấu | Trừ khi đăng ký thành công; hủy/hoàn là `TournamentRefund` |

Schema 35 bảng không có bảng yêu cầu nạp (TopUps), nên luồng "khách gửi yêu cầu nạp tiền mặt rồi chờ nhân viên duyệt" của bản demo **không còn**: khách đến quầy nhờ nhân viên nạp.

## 6. API đã có

Nhân viên (JWT `permission` claim): `GET/POST /api/admin/customers`, `GET/PUT /api/admin/customers/{id}`, `PUT .../{id}/status` (bắt buộc lý do, ghi `CustomerStatusHistory`), `GET .../{id}/wallet-transactions`, `POST .../{id}/wallet/top-ups`.

Khách (`role=Customer`, id luôn lấy từ claim `customer_id`): `GET /api/client/profile`, `GET /api/client/wallet`.

## 7. Kiểm tra

`Backend/tests/CyberManagement.Wallet.Checks` chạy trên SQL Server thử riêng (xem `Backend/README.md`). Giao diện được kiểm tra bằng `frontend/tests/wallet-customers-api.mjs` với API giả có kiểm soát (không thay thế kiểm tra SQL).
