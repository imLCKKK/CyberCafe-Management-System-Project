# 002 – Hợp đồng ví và sổ cái (S1-M-1 / SCRUM-5)

Áp dụng cho schema 35 bảng trên `dev`. Mọi module có đụng đến tiền khách (phiên chơi, đơn món, chứng từ, giải đấu, cổng thanh toán) gọi ví **chỉ qua `WalletLedger`**, không tự sửa `Customers.WalletBalance` hay tự thêm `WalletTransactions`.

Mã nguồn: `Application/Wallet/WalletLedger.cs`, `WalletTransactionTypes.cs`; khóa dòng và transaction ở `Infrastructure/Repositories/CustomerRepository.cs`.

## 1. Đầu vào và quy ước dấu

`WalletLedger.Record(customer, type, amount, recordedAtUtc, description?, idempotencyKey?)`

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
- `BalanceAfter` = số dư sau giao dịch; `Description` ghi nguồn (ví dụ `Phiên #12`, `Đơn #34`); khóa chống lặp nằm ở `IdempotencyKey` (mục 4).

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

## 4. Chống xử lý lặp

Khóa dòng chỉ chống **hai request chạy cùng lúc làm sai số dư**; nó không biết hai request là "cùng một nghiệp vụ". Phần chống lặp nằm ở cột `WalletTransactions.IdempotencyKey` (nullable, tối đa 100 ký tự) và chỉ mục duy nhất có lọc `UX_WalletTransactions_Customer_IdempotencyKey` trên `(CustomerId, IdempotencyKey)` khi khóa khác null (migration `WalletTransactionIdempotencyKey`).

**Nạp tại quầy (đã áp dụng):**

- `POST /api/admin/customers/{id}/wallet/top-ups` bắt buộc header `Idempotency-Key`; thiếu hoặc sai định dạng (8-100 ký tự gồm chữ, số, `.` `_` `:` `-`) là 400.
- Khóa do client sinh cho **một ý định** của nhân viên (một lần mở form nạp) và gửi lại đúng khóa đó khi retry. Đổi số tiền hoặc phương thức thì phải dùng khóa mới.
- Server khóa dòng khách rồi tìm giao dịch cùng `(CustomerId, IdempotencyKey)` **trước** khi ghi: nếu đã có và cùng số tiền, trả lại giao dịch cũ (200 + `Idempotency-Replayed: true`) và **không ghi ví thêm**; nếu đã có nhưng khác số tiền hoặc khác loại, trả 409. Chưa có thì ghi như thường (201). Vì dòng khách đang bị khóa, các request song song cùng khóa xếp hàng nên chỉ một request ghi.
- Kiểm tra replay đứng trước kiểm tra trạng thái khách: gửi lại một nạp đã thành công vẫn nhận kết quả cũ dù khách vừa bị khóa; khóa mới cho khách đang khóa vẫn bị chặn (409).
- Khóa chỉ có phạm vi trong từng khách; chỉ mục duy nhất là lớp bảo vệ cuối nếu có đường ghi nào bỏ qua kiểm tra trên.

**Các nghiệp vụ khác (phí phiên, đơn, giải đấu, ZaloPay):** `WalletLedger.Record(..., idempotencyKey)` đã nhận khóa tùy chọn. Module nào có thể bị gọi lặp (callback ZaloPay, trừ phí theo chu kỳ) phải tự đặt khóa ổn định theo nghiệp vụ, ví dụ `GamingCharge:<sessionId>:<chuKy>`, `OrderPayment:<orderId>`, `ZaloPay:<transTokenOrAppTransId>`, và dùng cùng mẫu "tìm trước khi ghi" ở trên (`ICustomerRepository.FindWalletTransactionByKeyAsync`). Không truyền khóa thì ví **không** chống lặp cho giao dịch đó.

## 5. Thời điểm cộng/trừ ví

| Nghiệp vụ | Thời điểm |
|---|---|
| Nạp tại quầy | Nhân viên xác nhận đã thu tiền (`Cash`/`BankTransfer`) rồi bấm nạp: **cộng ví ngay**, cùng transaction. Không có trạng thái "đã cộng nhưng chưa thu" |
| Nạp qua ZaloPay | Chỉ cộng khi server xác minh chữ ký callback/truy vấn trạng thái; redirect trình duyệt không phải bằng chứng (S1-M-2) |
| Phí phiên chơi | **ĐỀ XUẤT, chờ Phúc/Trường xác nhận:** trừ **đầu mỗi chu kỳ** (trả trước) bằng một `GamingCharge` có khóa `GamingCharge:<sessionId>:<số thứ tự chu kỳ>`; `InsufficientWalletBalanceException` (409) khi không đủ tiền thì Phúc kết thúc/khóa phiên; kết thúc sớm không hoàn phần chu kỳ đang chạy trừ khi Phúc/Trường chốt hoàn bằng `Adjustment`. Độ dài chu kỳ và công thức làm tròn do Phúc chốt trong contract phiên chơi |
| Đơn món | Trừ khi xác nhận đơn (Khang chốt); hủy/hoàn món là `OrderRefund` |
| Giải đấu | Trừ khi đăng ký thành công; hủy/hoàn là `TournamentRefund` |

Schema 35 bảng không có bảng yêu cầu nạp (TopUps), nên luồng "khách gửi yêu cầu nạp tiền mặt rồi chờ nhân viên duyệt" của bản demo **không còn**: khách đến quầy nhờ nhân viên nạp.

## 6. API đã có

Nhân viên (JWT `permission` claim): `GET/POST /api/admin/customers`, `GET/PUT /api/admin/customers/{id}`, `PUT .../{id}/status` (bắt buộc lý do, ghi `CustomerStatusHistory`), `GET .../{id}/wallet-transactions`, `POST .../{id}/wallet/top-ups` (bắt buộc header `Idempotency-Key`; 201 lần đầu, 200 + `Idempotency-Replayed: true` khi gửi lại).

Khách (`role=Customer`, id luôn lấy từ claim `customer_id`): `GET /api/client/profile`, `GET /api/client/wallet`.

## 7. Kiểm tra

`Backend/tests/CyberManagement.Wallet.Checks` chạy trên SQL Server thử riêng (xem `Backend/README.md`). Giao diện được kiểm tra bằng `frontend/tests/wallet-customers-api.mjs` với API giả có kiểm soát (không thay thế kiểm tra SQL).
