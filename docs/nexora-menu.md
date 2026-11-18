# Menu NEXORA CYBER

Cập nhật ngày 17/11/2026 theo danh sách và giá được người dùng chốt trong chat. Tài liệu phục vụ chuẩn bị ảnh và phối hợp tích hợp sản phẩm cho **S2-U-1 / SCRUM-34**; không phải xác nhận Task đã được nghiệm thu.

## Trạng thái và phân công

- Đã chốt: 49 món/đồ uống bán lẻ, 6 combo và 7 phần gọi thêm; giá trong các bảng dưới đây tính bằng VNĐ.
- Đã chốt: mì cay nằm trong nhóm Mì, chọn cấp độ từ 0 đến 7; phần trứng gọi thêm dùng tên **Trứng thêm**.
- Đã chốt: giá combo là bội số của 5.000đ, thấp hơn tổng giá lẻ các thành phần từ 5.000đ đến 10.000đ. Menu hiển thị tên, thành phần và giá combo; không hiển thị số tiền tiết kiệm.
- Đã chốt quy cách: cơm/mì một phần cho một người; cơm gà dùng một đùi gà, cơm sườn một miếng sườn, kèm dưa leo và đồ chua; mì xào/trộn có topping theo tên món. Mì cay một tô có rau, nấm và topping theo tên; thập cẩm gồm bò, tôm, mực và xúc xích.
- Đã chốt đồ uống: trà sữa/trà trái cây một ly 500ml, topping mặc định chỉ gồm thành phần ghi trong tên món; nước đóng chai dùng chai nhựa, bao bì và dung tích theo sản phẩm thực tế được chọn.
- Đã chốt: khẩu phần món trong combo bằng món bán lẻ, ảnh combo thể hiện đủ món và nước đi kèm. Ăn vặt đã chốt xúc xích chiên một cây, phô mai que ba que, hoành thánh chiên sáu cái.
- Đã chốt định lượng tám món ăn vặt còn lại: hai món khoai tây, gà popcorn và bánh gạo lắc phô mai mỗi phần 150g; cá viên/bò viên/gà viên mỗi phần 8 viên; mẹt thập cẩm 18 viên gồm cá, bò, gà mỗi loại 6 viên.
- Đã chốt phong cách ảnh: ảnh minh họa như chụp món thật, món sáng rõ trên nền tối; góc chụp và bát đĩa đồng nhất, không chèn chữ hoặc giá vào ảnh. Làm trước ba ảnh mẫu cơm gà xối mỡ, mì cay hải sản và trà đào cam sả để người dùng xem trước khi làm cả bộ.
- Điều chỉnh ảnh ăn vặt theo yêu cầu trực tiếp ngày 17/11/2026: ưu tiên bày món tự nhiên, đầy đặn và hấp dẫn, không ép ảnh phải đếm đúng 8 viên hoặc xếp thành các cụm đếm số. Định lượng bán vẫn ghi trong bảng menu; ảnh là minh họa cách trình bày. Có thể dùng chén sốt nhỏ làm phụ kiện trình bày, không tự thêm món mới vào thực đơn.
- Điều chỉnh combo theo ảnh tham khảo người dùng gửi ngày 17/11/2026: dùng bố cục poster quảng cáo với món/nước lớn, tên combo, thành phần và giá bán nổi bật. Người dùng xác nhận màu không bắt buộc kem–đỏ; giữ nền tối và màu cyan–tím NEXORA cho đồng bộ. Riêng poster combo được đặt chữ và giá đã chốt trong ảnh; không ghi số tiền tiết kiệm. Ảnh món lẻ tiếp tục không chèn chữ/giá. Nếu giá hoặc thành phần combo thay đổi, phải cập nhật poster tương ứng trước khi dùng lại.
- Đã duyệt ba ảnh mẫu: người dùng xác nhận ảnh đẹp và chấp nhận mẫu ngày 17/11/2026. Tổng cộng có ba lượt tạo ban đầu và hai lượt chỉnh cơm/mì; bộ cuối giữ ba ảnh. Đây là duyệt mẫu ảnh, chưa phải nghiệm thu toàn bộ SCRUM-34.
- Còn đối chiếu: sản phẩm chai/dung tích cụ thể và nghiệp vụ áp dụng món thêm. Theo yêu cầu tiếp tục ngày 17/11/2026, đã tạo `CyberManagement_Dev` trên `localhost`, áp 15 migration và nạp đủ 62 món đã chốt cùng ảnh. Món mới có tồn bằng 0, chưa tự tạo số tồn hoặc chứng từ nhập kho.
- Bộ ảnh đã tạo và xuất cho web: 62 ảnh được chọn, mỗi ảnh có bản JPEG 320px/640px. Có 49 ảnh món/đồ uống bán lẻ, 6 poster combo theo theme NEXORA và 7 ảnh món thêm; nhóm ăn vặt dùng cách bày tự nhiên theo điều chỉnh mới. Bộ ảnh đã gắn vào sản phẩm SQL Development; chưa phải quyết định nghiệm thu SCRUM-34.
- Người dùng xác nhận là **Khang, đồng thời là Trưởng nhóm**, nhận SCRUM-34 từ Phúc và trực tiếp thực hiện cả ảnh/component lẫn điểm nối sản phẩm. AI hỗ trợ thực hiện/tự kiểm tra; Khang giữ quyền duyệt cuối với vai trò Trưởng nhóm.
- Khang tiếp tục sở hữu danh mục/sản phẩm/menu, entity/API/form và logic vòng quay/checkout. SCRUM-34 chỉ bổ sung trường ảnh vào sản phẩm, không triển khai nghiệp vụ combo/món thêm/cấp cay hoặc checkout.
- Danh sách do người dùng và AI cùng chuẩn bị sau khi chưa xác định được DB menu chính. Có thể chuẩn bị ảnh từ danh sách đã chốt; khi tích hợp phải đối chiếu sản phẩm SQL thực tế, không mặc định ID demo là ID trong SQL và không tạo nguồn sản phẩm chạy song song với API.

## Nước ngọt đóng chai — 9 món

Giá cho một chai nhựa; bao bì và dung tích cụ thể theo sản phẩm thực tế được chọn, chưa chốt một dung tích chung cho cả chín loại.

| Món | Giá |
| --- | ---: |
| Coca-Cola | 15.000 |
| Coca-Cola không đường | 15.000 |
| Pepsi | 15.000 |
| Sprite | 15.000 |
| 7Up | 15.000 |
| Fanta cam | 15.000 |
| Fanta nho | 15.000 |
| Sting dâu | 15.000 |
| Twister cam | 15.000 |

## Trà sữa & trà trái cây — 11 món

Giá cho một ly 500ml. Topping mặc định chỉ gồm những thành phần ghi trong tên món; không tự thêm topping khác vào ảnh hoặc phần bán.

| Món | Giá |
| --- | ---: |
| Trà sữa truyền thống trân châu | 25.000 |
| Trà sữa Thái xanh | 25.000 |
| Trà sữa socola | 30.000 |
| Trà sữa khoai môn | 30.000 |
| Trà sữa ô long | 30.000 |
| Sữa tươi trân châu đường đen | 30.000 |
| Trà đào cam sả | 22.000 |
| Trà vải | 22.000 |
| Trà chanh dây | 22.000 |
| Trà chanh | 15.000 |
| Trà tắc mật ong | 20.000 |

## Cơm — 8 món

Một phần cho một người. Cơm gà dùng một đùi gà, cơm sườn dùng một miếng sườn; các phần cơm kèm dưa leo và đồ chua.

| Món | Giá |
| --- | ---: |
| Cơm gà xối mỡ | 38.000 |
| Cơm gà sốt teriyaki | 38.000 |
| Cơm gà sốt cay | 38.000 |
| Cơm bò xào tiêu đen | 42.000 |
| Cơm sườn nướng | 40.000 |
| Cơm chiên trứng | 25.000 |
| Cơm chiên dương châu | 32.000 |
| Cơm chiên hải sản | 38.000 |

## Mì — 10 món

Mì xào/trộn một phần cho một người, topping theo tên món. Mì cay một tô có rau, nấm và topping theo tên; mì cay thập cẩm gồm bò, tôm, mực và xúc xích. Các món mì cay có lựa chọn cấp 0–7; không tạo tám dòng sản phẩm riêng cho tám cấp độ.

| Món | Giá |
| --- | ---: |
| Mì xào bò | 32.000 |
| Mì xào hải sản | 38.000 |
| Mì xào trứng xúc xích | 28.000 |
| Mì trộn bò | 35.000 |
| Mì trộn gà cay | 32.000 |
| Mì trộn trứng xúc xích | 28.000 |
| Mì cay gà | 40.000 |
| Mì cay bò | 42.000 |
| Mì cay hải sản | 45.000 |
| Mì cay thập cẩm | 50.000 |

## Ăn vặt — 11 món

Giá cho một phần theo định lượng đã chốt bên dưới.

| Món | Khẩu phần | Giá |
| --- | --- | ---: |
| Khoai tây chiên | 150g | 22.000 |
| Khoai tây lắc phô mai | 150g | 25.000 |
| Xúc xích chiên | 1 cây | 12.000 |
| Cá viên chiên | 8 viên | 20.000 |
| Bò viên chiên | 8 viên | 25.000 |
| Gà viên chiên | 8 viên | 25.000 |
| Gà popcorn | 150g | 30.000 |
| Phô mai que | 3 que | 25.000 |
| Mẹt viên chiên thập cẩm | 18 viên: cá, bò, gà mỗi loại 6 viên | 45.000 |
| Hoành thánh chiên | 6 cái | 25.000 |
| Bánh gạo lắc phô mai | 150g | 25.000 |

## Combo — 6 lựa chọn

Thành phần và giá được chốt theo bảng dưới đây, thay các ví dụ giá trước đó trong chat. Khẩu phần từng món bằng món bán lẻ; ảnh thể hiện đủ món và nước đi kèm. Lựa chọn đổi nước hoặc thay món chưa được chốt.

| Combo | Thành phần | Giá |
| --- | --- | ---: |
| Tiếp Trận | Mì xào bò + Coca-Cola | 40.000 |
| No Bụng | Cơm gà xối mỡ + Pepsi | 45.000 |
| Bùng Vị | Mì cay bò + trà đào cam sả | 55.000 |
| Ăn Vặt Solo | Khoai tây chiên + xúc xích chiên + Sprite | 40.000 |
| Đồng Đội — 2 người | 2 mì xào bò + khoai tây chiên + 2 Coca-Cola | 110.000 |
| Gà Giòn Solo | Cơm gà xối mỡ + khoai tây chiên + Coca-Cola | 65.000 |

## Món thêm — 7 lựa chọn

| Món thêm | Đơn vị | Giá |
| --- | --- | ---: |
| Cơm thêm | 1 chén | 5.000 |
| Mì thêm | 1 vắt | 7.000 |
| Trứng thêm | 1 quả | 7.000 |
| Xúc xích thêm | 1 cây | 8.000 |
| Bò thêm | 1 phần | 15.000 |
| Gà thêm | 1 phần | 15.000 |
| Hải sản thêm | 1 phần | 20.000 |

## Chuẩn bị ảnh và tích hợp

1. Dùng quy cách/định lượng và phong cách ảnh đã chốt ở trên; chọn sản phẩm chai/dung tích thực tế trước khi làm ảnh nước đóng chai tương ứng.
2. Ba ảnh mẫu đã được người dùng duyệt ngày 17/11/2026; dùng cùng phong cách cho bộ ảnh tiếp theo.
3. Dùng ảnh đúng món/thành phần combo, giữ nhất quán crop và kích thước; ảnh món lẻ không gắn sẵn chữ/giá. Poster combo theo điều chỉnh mới có tên/thành phần/giá bán, không ghi số tiền tiết kiệm.
4. Khang đối chiếu danh sách với sản phẩm SQL và lưu đường dẫn ảnh đúng sản phẩm khi nạp menu thực tế.
5. Component dùng chung có mô tả ảnh, trạng thái thiếu/tải lỗi, bố cục ổn định và hỗ trợ mobile theo tiêu chí [SCRUM-34](../codex/SPRINT_TASKS.md#s2-u-1--add-food-images-menu-cards).

## Giao điểm frontend và sản phẩm

- Frontend đã chuẩn bị component `FoodArt` nhận `product.image_url`, dùng chung tại admin, Home/menu/giỏ, teaser/vòng quay và ảnh trong chi tiết kết quả gợi ý. Không suy ảnh từ ID demo hoặc tên món; thiếu/tải lỗi hiển thị ô thay thế và URL thay đổi có thể tải lại. Thay đổi vòng quay chỉ áp dụng hiển thị ảnh.
- Trường `Products.ImageUrl` nullable `nvarchar(500)` đã được bổ sung bằng migration `20261117130111_AddProductImageUrl`. API sản phẩm/menu trả `imageUrl` (`string` hoặc `null`); adapter chuyển sang `image_url` cho component dùng chung trong admin/client. Form quản lý sản phẩm có trường đường dẫn ảnh và thumbnail.
- POST/PUT nhận đường dẫn từ gốc frontend hoặc URL HTTP/HTTPS, tối đa 500 ký tự. PUT bỏ qua trường ảnh sẽ giữ nguyên ảnh cũ; gửi `null` hoặc chuỗi trống xóa ảnh. Request cũ sửa tên/giá không làm mất ảnh; thay ảnh không thay đổi tồn.
- Khang đối chiếu tên món với ID sản phẩm SQL thực tế và lưu đúng URL ảnh cho mỗi sản phẩm. Thay đổi combo/món thêm/cấp cay trong nghiệp vụ và logic vòng quay/checkout vẫn thuộc các Task module của Khang; chưa triển khai các nghiệp vụ đó ở đây.
- Đã có kiểm tra HTTP và browser dùng SQL Server thật trong DB thử ngẫu nhiên do harness tự tạo/dọn, bao gồm lưu/đọc/xóa ảnh, form admin, Home/menu/giỏ và quyền truy cập. Các kiểm tra ảnh hỏng/mobile/danh sách dài dùng fixture hình dạng API riêng. DB `localhost / CyberManagement_Dev` đã có 7 danh mục và 62 sản phẩm, giá/ảnh được đối chiếu đầy đủ qua API thật. Dữ liệu menu đang chạy vẫn lấy từ API, không tự thay bằng bộ menu tài liệu khi API lỗi.
- Nạp lại bằng [NexoraMenuSeed.sql](../Backend/database/NexoraMenuSeed.sql) chỉ thêm dòng còn thiếu, không ghi đè giá/ảnh/trạng thái/tồn đã thay đổi và không xóa dữ liệu khác. Hướng dẫn cấu hình/đăng nhập local và chạy kiểm tra seed tại [Backend README](../Backend/README.md#nạp-menu-nexora-vào-development). Combo/món thêm hiện là sản phẩm trong nhóm tương ứng; cấu thành combo, cấp cay và quy tắc món thêm chưa được triển khai ở đây.
- `scripts/export-menu-images.ps1` xuất bản JPEG 320px/640px từ ảnh đã tạo để web chọn kích thước tải phù hợp; chỉ đổi kích thước và mã hóa, không chỉnh lại cách bày món. PNG sản xuất được giữ trong `.tools/menu-image-originals/` cục bộ; ba PNG mẫu đã duyệt vẫn giữ nguyên. Bản JPEG trong `frontend/public/food/` là asset bàn giao; `.tools` không đi theo clone.
- Bảng URL ảnh bàn giao tại [nexora-menu-assets.json](nexora-menu-assets.json), gồm tên món, nhóm ảnh và hai kích thước; không chứa ID/giá/tồn và không được dùng thay API sản phẩm. Prompt và lịch sử đổi cách trình bày lưu tại [nexora-menu-image-jobs.json](nexora-menu-image-jobs.json). Khi nạp menu, dùng URL `imageUrl` trong bảng ảnh làm giá trị trường ảnh của đúng sản phẩm SQL.

## Ba ảnh mẫu — đã được người dùng duyệt

Đã tạo bằng công cụ image_gen tích hợp theo quy cách và phong cách đã chốt; người dùng đã duyệt ba ảnh mẫu ngày 17/11/2026. Đây là ảnh minh họa AI. Bộ ảnh hiện đã được mở rộng thành 62 ảnh chọn và gắn vào sản phẩm SQL Development. Riêng giai đoạn làm mẫu có năm lượt tạo/chỉnh: ba ảnh ban đầu, một bản chỉnh bỏ topping ngoài quy cách trên cơm và một bản chỉnh phần xúc tu thành khoanh mực. Ba ảnh mẫu gốc dưới đây được giữ nguyên; prompt tạo/chỉnh mẫu được lưu tại [nexora-menu-image-prompts.json](nexora-menu-image-prompts.json), prompt bộ ảnh mở rộng tại [nexora-menu-image-jobs.json](nexora-menu-image-jobs.json).

| Món | Ảnh mẫu |
| --- | --- |
| Cơm gà xối mỡ | [com-ga-xoi-mo-v1.png](../frontend/public/food/samples/com-ga-xoi-mo-v1.png) |
| Mì cay hải sản | [mi-cay-hai-san-v1.png](../frontend/public/food/samples/mi-cay-hai-san-v1.png) |
| Trà đào cam sả | [tra-dao-cam-sa-v1.png](../frontend/public/food/samples/tra-dao-cam-sa-v1.png) |
