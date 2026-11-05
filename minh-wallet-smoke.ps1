# Kiểm tra nhanh API khách hàng/ví (S1-M-1) trên API ĐANG CHẠY, dùng JWT thật của backend CyberManagement.
# Chỉ dùng với database phát triển của riêng bạn (script tạo khách và nạp tiền thật vào DB đó).
#
# Chạy API trước (xem Backend/README.md), rồi từ thư mục gốc repo:
#   powershell -ExecutionPolicy Bypass -File .\minh-wallet-smoke.ps1 -StaffUsername <tên đăng nhập Admin>
# Mật khẩu được hỏi khi chạy, không ghi vào lịch sử lệnh hay file.
param(
    [Parameter(Mandatory)][string]$StaffUsername,
    [string]$BaseUrl = 'http://127.0.0.1:5088'
)

$secure = Read-Host -Prompt "Mật khẩu của $StaffUsername" -AsSecureString
$staffPassword = [System.Net.NetworkCredential]::new('', $secure).Password

$http = [System.Net.Http.HttpClient]::new()
$pass = 0; $fail = 0
# Mật khẩu khách chỉ dùng cho khách thử tạo trong lần chạy này; sinh ngẫu nhiên, không lưu ở đâu.
$customerPassword = 'Aa1!' + [guid]::NewGuid().ToString('N')

function Call($method, $path, $body = $null, $token = $null) {
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), "$BaseUrl$path")
    if ($token) { $req.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token) }
    if ($null -ne $body) {
        $json = if ($body -is [string]) { $body } else { $body | ConvertTo-Json -Compress }
        $req.Content = [System.Net.Http.StringContent]::new($json, [Text.Encoding]::UTF8, 'application/json')
    }
    try { $res = $http.SendAsync($req).GetAwaiter().GetResult() }
    catch { Write-Host "Không kết nối được API tại $BaseUrl. Hãy chạy API trước." -ForegroundColor Red; exit 2 }
    $text = $res.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $obj = $null
    if ($text) { try { $obj = $text | ConvertFrom-Json } catch { } }
    [pscustomobject]@{ Status = [int]$res.StatusCode; Body = $obj; Raw = $text }
}
function Check($name, $ok) {
    if ($ok) { $script:pass++; Write-Host "PASS  $name" -ForegroundColor Green }
    else { $script:fail++; Write-Host "FAIL  $name" -ForegroundColor Red }
}
function Phone { '09' + (Get-Random -Minimum 10000000 -Maximum 99999999) }

# ---- Đăng nhập nhân viên ----
$login = Call 'POST' '/api/auth/login' @{ username = $StaffUsername; password = $staffPassword }
$staffPassword = $null
if ($login.Status -ne 200) { Write-Host "Đăng nhập nhân viên thất bại (HTTP $($login.Status)). Kiểm tra tên đăng nhập/mật khẩu." -ForegroundColor Red; exit 2 }
$staff = $login.Body.accessToken
function Admin($m, $p, $b = $null) { Call $m $p $b $staff }
function CustomerLogin($phone) { Call 'POST' '/api/auth/customer/login' @{ phone = $phone; password = $customerPassword } }

# ---- Xác thực và phân quyền ----
Check 'Chưa đăng nhập -> 401' ((Call 'GET' '/api/admin/customers').Status -eq 401)
Check 'Chưa đăng nhập xem ví -> 401' ((Call 'GET' '/api/client/wallet').Status -eq 401)

# ---- Tạo khách ----
$phoneA = Phone; $phoneB = Phone
$a = Admin 'POST' '/api/admin/customers' @{ fullName = 'Khach A'; phone = $phoneA; email = $null; initialPassword = $customerPassword }
Check 'Tạo khách A -> 201' ($a.Status -eq 201)
Check 'Khách A: ví 0, Active, chưa có phiên' ($a.Body.walletBalance -eq 0 -and $a.Body.status -eq 'Active' -and $a.Body.hasActiveSession -eq $false)
$b = Admin 'POST' '/api/admin/customers' @{ fullName = 'Khach B'; phone = $phoneB; email = $null; initialPassword = $customerPassword }
Check 'Tạo khách B -> 201' ($b.Status -eq 201)
if ($a.Status -ne 201 -or $b.Status -ne 201) { Write-Host "Không tạo được khách, dừng. $($a.Raw)"; exit 1 }
$ca = $a.Body; $cb = $b.Body

Check 'Khách mới đăng nhập được bằng auth chung -> 200' ((CustomerLogin $phoneA).Status -eq 200)
Check 'Sai mật khẩu -> 401' ((Call 'POST' '/api/auth/customer/login' @{ phone = $phoneA; password = 'sai-mat-khau-123' }).Status -eq 401)
$tokenA = (CustomerLogin $phoneA).Body.accessToken
$tokenB = (CustomerLogin $phoneB).Body.accessToken
Check 'Token khách gọi API admin -> 403' ((Call 'GET' '/api/admin/customers' $null $tokenA).Status -eq 403)

Check 'Trùng số điện thoại -> 409' ((Admin 'POST' '/api/admin/customers' @{ fullName = 'X'; phone = $phoneA; initialPassword = $customerPassword }).Status -eq 409)
Check 'Số điện thoại sai -> 400' ((Admin 'POST' '/api/admin/customers' @{ fullName = 'X'; phone = '12345'; initialPassword = $customerPassword }).Status -eq 400)
Check 'Tên rỗng -> 400' ((Admin 'POST' '/api/admin/customers' @{ fullName = '   '; phone = (Phone); initialPassword = $customerPassword }).Status -eq 400)
Check 'Mật khẩu ngắn -> 400' ((Admin 'POST' '/api/admin/customers' @{ fullName = 'X'; phone = (Phone); initialPassword = '123' }).Status -eq 400)
Check 'Email sai -> 400' ((Admin 'POST' '/api/admin/customers' @{ fullName = 'X'; phone = (Phone); email = 'khong-phai-email'; initialPassword = $customerPassword }).Status -eq 400)

# ---- Đọc / sửa ----
Check 'GET chi tiết -> 200' ((Admin 'GET' "/api/admin/customers/$($ca.customerId)").Status -eq 200)
Check 'GET id không tồn tại -> 404' ((Admin 'GET' '/api/admin/customers/99999999').Status -eq 404)
$list = Admin 'GET' '/api/admin/customers'
Check 'Danh sách: ID giảm dần' ($list.Status -eq 200 -and @($list.Body).Count -ge 2 -and $list.Body[0].customerId -ge $list.Body[-1].customerId)
$u = Admin 'PUT' "/api/admin/customers/$($ca.customerId)" ('{"fullName":"Khach A moi","phone":"' + $phoneA + '","email":null,"walletBalance":999999}')
Check 'Sửa hồ sơ -> 200, bỏ qua walletBalance' ($u.Status -eq 200 -and $u.Body.fullName -eq 'Khach A moi' -and $u.Body.walletBalance -eq 0)
Check 'Sửa sang số của khách khác -> 409' ((Admin 'PUT' "/api/admin/customers/$($ca.customerId)" @{ fullName = 'A'; phone = $phoneB }).Status -eq 409)
Check 'Sửa khách không tồn tại -> 404' ((Admin 'PUT' '/api/admin/customers/99999999' @{ fullName = 'A'; phone = (Phone) }).Status -eq 404)

# ---- Khóa / mở khóa ----
Check 'Khóa thiếu lý do -> 400' ((Admin 'PUT' "/api/admin/customers/$($cb.customerId)/status" @{ status = 'Locked'; reason = '' }).Status -eq 400)
Check 'Trạng thái sai -> 400' ((Admin 'PUT' "/api/admin/customers/$($cb.customerId)/status" @{ status = 'Banned'; reason = 'x' }).Status -eq 400)
Check 'Thiếu status -> 400' ((Admin 'PUT' "/api/admin/customers/$($cb.customerId)/status" '{"reason":"x"}').Status -eq 400)
$lock = Admin 'PUT' "/api/admin/customers/$($cb.customerId)/status" @{ status = 'Locked'; reason = 'Kiem thu' }
Check 'Khóa khách -> 200 Locked' ($lock.Status -eq 200 -and $lock.Body.status -eq 'Locked')
Check 'Khóa lần nữa -> 409' ((Admin 'PUT' "/api/admin/customers/$($cb.customerId)/status" @{ status = 'Locked'; reason = 'x' }).Status -eq 409)
Check 'Khách đang khóa đăng nhập -> 403' ((CustomerLogin $phoneB).Status -eq 403)
Check 'Nạp cho khách đang khóa -> 409' ((Admin 'POST' "/api/admin/customers/$($cb.customerId)/wallet/top-ups" @{ amount = 50000; collectionMethod = 'Cash' }).Status -eq 409)
$unlock = Admin 'PUT' "/api/admin/customers/$($cb.customerId)/status" @{ status = 'Active'; reason = 'Mo khoa kiem thu' }
Check 'Mở khóa -> 200 Active' ($unlock.Status -eq 200 -and $unlock.Body.status -eq 'Active')
Check 'Khóa khách không tồn tại -> 404' ((Admin 'PUT' '/api/admin/customers/99999999/status' @{ status = 'Locked'; reason = 'x' }).Status -eq 404)

# ---- Nạp tại quầy: dữ liệu sai ----
$topUpPath = "/api/admin/customers/$($ca.customerId)/wallet/top-ups"
foreach ($amount in -1000, 0, 5000, 2001000, 15500) {
    Check "Nạp $amount -> 400" ((Admin 'POST' $topUpPath @{ amount = $amount; collectionMethod = 'Cash' }).Status -eq 400)
}
Check 'Phương thức thu sai -> 400' ((Admin 'POST' $topUpPath @{ amount = 100000; collectionMethod = 'Visa' }).Status -eq 400)
Check 'Thiếu phương thức thu -> 400' ((Admin 'POST' $topUpPath '{"amount":100000}').Status -eq 400)
Check 'Nạp cho khách không tồn tại -> 404' ((Admin 'POST' '/api/admin/customers/99999999/wallet/top-ups' @{ amount = 100000; collectionMethod = 'Cash' }).Status -eq 404)
Check 'Token khách không được nạp -> 403' ((Call 'POST' $topUpPath @{ amount = 100000; collectionMethod = 'Cash' } $tokenA).Status -eq 403)
Check 'Các lần nạp bị từ chối không đổi ví' ((Admin 'GET' "/api/admin/customers/$($ca.customerId)").Body.walletBalance -eq 0)

# ---- Nạp tại quầy: hợp lệ ----
$t = Admin 'POST' $topUpPath @{ amount = 100000; collectionMethod = 'Cash' }
Check 'Nạp 100.000 -> 201, loại TopUp, số dư sau 100.000' ($t.Status -eq 201 -and $t.Body.type -eq 'TopUp' -and $t.Body.amount -eq 100000 -and $t.Body.balanceAfter -eq 100000)
$wa = Call 'GET' '/api/client/wallet' $null $tokenA
Check 'Ví khách A = 100.000 và có đúng 1 giao dịch' ($wa.Status -eq 200 -and $wa.Body.walletBalance -eq 100000 -and @($wa.Body.transactions).Count -eq 1)
$wb = Call 'GET' '/api/client/wallet' $null $tokenB
Check 'Khách B không thấy tiền/giao dịch của A' ($wb.Status -eq 200 -and $wb.Body.walletBalance -eq 0 -and @($wb.Body.transactions).Count -eq 0)
Check 'Hồ sơ khách A đúng chủ' ((Call 'GET' '/api/client/profile' $null $tokenA).Body.customerId -eq $ca.customerId)
Check 'Nhân viên không dùng được /api/client/wallet -> 403' ((Call 'GET' '/api/client/wallet' $null $staff).Status -eq 403)
$tx = Admin 'GET' "/api/admin/customers/$($ca.customerId)/wallet-transactions"
Check 'Nhân viên xem lịch sử ví của khách' ($tx.Status -eq 200 -and @($tx.Body).Count -eq 1)
Check 'Danh sách khách cho thấy số dư mới' ((Admin 'GET' "/api/admin/customers/$($ca.customerId)").Body.walletBalance -eq 100000)

# ---- Giới hạn đã biết (KHÔNG tính là pass) ----
$cc = (Admin 'POST' '/api/admin/customers' @{ fullName = 'Khach C'; phone = (Phone); initialPassword = $customerPassword }).Body
$cPath = "/api/admin/customers/$($cc.customerId)/wallet/top-ups"
[void](Admin 'POST' $cPath @{ amount = 10000; collectionMethod = 'Cash' })
[void](Admin 'POST' $cPath @{ amount = 10000; collectionMethod = 'Cash' })
$balanceC = (Admin 'GET' "/api/admin/customers/$($cc.customerId)").Body.walletBalance
if ($balanceC -eq 20000) { Write-Host 'KNOWN GAP (không tính là pass): hai yêu cầu nạp giống nhau đều được ghi; chưa có khóa chống lặp trong schema (Backend/docs/002-wallet-ledger.md mục 4).' -ForegroundColor Yellow }

Write-Host ''
Write-Host 'Chưa kiểm tra bằng script này: nạp/trừ song song, rollback giữa giao dịch (đã có trong Backend\tests\CyberManagement.Wallet.Checks), khóa khách đang có phiên chơi (cần phần phiên của Phúc).'
Write-Host "KẾT QUẢ: $pass PASS, $fail FAIL" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 }
