# Nguồn và phạm vi tái sử dụng

Repository nguồn: https://github.com/LaoDai060204/Project, nền MDPro3 (Yu-Gi-Oh), commit `241edd04`.

`ButtonHover.cs` được chuyển từ `Assets/Scripts/MDPro3/UI/New UI/ButtonHover.cs`, thay namespace, giữ hành vi pointer enter/exit. Mã gốc có giấy phép GPL-3.0 tại `LICENSE` ở gốc repo. Phần mới trong thư mục này được cung cấp theo cùng giấy phép; bản sao đặt tại `BattleSpiritsSaga/LICENSE`.

Không dùng OCG/YGOSharp/Windbot làm engine Saga. Không nhập scene Boot của MDPro3 hoặc yêu cầu bundle MasterDuel. Giao diện Saga được dựng riêng bằng uGUI; không phải bản chuyển toàn bộ giao diện MasterDuel.

Engine, catalog và scene được phát triển tiếp từ bản BattleSpiritsSaga Alpha 0.1 đã cung cấp trong phiên làm việc trước. Ảnh thẻ và dữ liệu 20 Spirit được lấy từ tài nguyên BSS_DeckBuilder do người dùng cung cấp. Artwork/tên Battle Spirits Saga thuộc chủ sở hữu tương ứng; giấy phép mã nguồn không cấp quyền đối với artwork/thương hiệu. Đây là dự án fan-development, không liên kết chính thức với Bandai.

Tham chiếu bộ luật dùng cho baseline: https://battlespirits-saga.com/pdf/bss_comprehensive_rules.pdf (v1.7 khi baseline được đối chiếu). Chỉ tập con Spirit không hiệu ứng được triển khai; không tuyên bố bao phủ toàn bộ tài liệu.
