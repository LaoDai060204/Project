# Battle Spirits Saga — Development 0.2

Dự án Unity độc lập trong repository MDPro3, dành cho chế độ Battle Spirits Saga đấu AI offline. **Đây là bản nền tảng chơi thử, chưa phải game hoàn chỉnh với toàn bộ lá bài và hiệu ứng.**

## Mở và chạy

1. Tải/clone nhánh `codex/battle-spirits-saga-foundation`.
2. Unity Hub → Add project from disk → chọn **thư mục `BattleSpiritsSaga`**, không chọn thư mục gốc `Project`.
3. Mở bằng **Unity 6000.6.3f1** và chờ Unity tải packages/import tài nguyên.
4. Mở `Assets/Saga/Scenes/SagaGame.unity`, nhấn **Play**.
5. Chọn **PLAY VS AI**, chọn lượt đi nếu thắng tung đồng xu, rồi Keep hoặc Mulligan.
6. Chọn lá trên tay → Choose summon payment → chọn core trả phí và core duy trì → SUMMON. Chọn Spirit trên sân để tăng/giảm core, tấn công hoặc chặn.

Để build Windows: File → Build Profiles → Windows, kiểm tra scene `SagaGame` đã bật, chọn Build. Project dùng Built-in Render Pipeline và uGUI, không cần bundle MasterDuel/OCG bên ngoài. Chưa kiểm thử import/Play Mode hoặc Windows build bằng Unity Editor trong môi trường phát triển này.

## Đã triển khai

- Home → quản lý deck → đấu AI → kết quả trận.
- 20 Spirit không có hiệu ứng, ảnh lá gốc, bốn màu; deck 50–60 lá, tối đa 4 lá cùng tên.
- Tạo, đổi tên, sao chép, xóa deck; tìm kiếm và lọc collection; deck nháp được lưu nhưng không được dùng đấu nếu không hợp lệ.
- Saga core/soul core, triệu hồi và giảm phí theo symbol, level/BP, lượt đi, mulligan, attack/block, Flash pass, life/deck-out và concede.
- AI ra quyết định từ bài của mình và thông tin công khai; không đọc bài ẩn của đối thủ.
- Xem Trash công khai, nhật ký trận, lịch sử 20 trận và replay từng hành động.
- Thống kê thắng/thua, tốc độ AI, âm thanh nút bấm; tự lưu JSON với bản sao lưu và phục hồi file lỗi.

Deck mặc định gồm 13 loại × 4 = 52 lá. Collection có 20 loại hỗ trợ; không thể thêm lá ngoài tập này vào trận. UI hiện dùng tiếng Anh.

## Chưa triển khai

Magic, Nexus, Burst, keyword, hiệu ứng Spirit, lựa chọn mục tiêu/trigger, đầy đủ card pool, luật giải đấu/banned list, đấu người online, account, progression, tutorial tương tác, hiệu ứng hình ảnh hoàn thiện. AI hiện là heuristic cơ bản. Không có lưu tiếp một trận đang chơi; replay dành cho trận đã kết thúc và cùng phiên bản engine.

## Kiểm tra tự động

Cài .NET 8 SDK rồi chạy từ thư mục này:

```sh
dotnet run --project Tests/EngineTests.csproj
```

Các bài kiểm tra bao gồm lượt đầu, mulligan, thanh toán nguyên tử, reduction khi hy sinh nguồn, soul core, BP/tie, refresh, hết deck, hết life, bộ bài sai, bảo toàn core/lá, 100 trận AI và replay xác định, lưu/khôi phục profile. Đây là kiểm tra engine C#; không thay thế kiểm thử Unity UI.

## Dữ liệu và cấu trúc

- `Assets/Saga/Scripts/SagaEngine.cs`: luật, command log và AI, không phụ thuộc Unity.
- `SagaProfile.cs`: deck validation, lịch sử và lưu dữ liệu.
- `SagaBattleView.cs` / `SagaFrontEnd.cs`: giao diện uGUI.
- `Assets/Resources/Saga/TrainingCatalog.asset`: whitelist lá được engine hỗ trợ.
- `Tests/TrainingData.json`: dữ liệu kiểm tra tương ứng catalog.
- `Docs/ROADMAP.md`: phần việc tiếp theo.

Profile nằm tại `Application.persistentDataPath/saga-profile.json`; vị trí thực tế tùy hệ điều hành. Khi dữ liệu lỗi không có backup hợp lệ, game giữ nguyên file và tắt lưu để tránh ghi đè.

Xem `Docs/PROVENANCE.md` về nguồn dự án và tài nguyên.
