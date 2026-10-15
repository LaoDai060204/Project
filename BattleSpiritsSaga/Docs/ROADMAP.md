# Lộ trình từ bản chơi thử tới game đầy đủ

1. Kiểm thử trực tiếp Unity 6000.6.3f1: Home, toàn bộ luồng deck, trận dài, modal payment/Trash, tỷ lệ màn hình, lưu và khởi động lại, Windows build. Sửa lỗi import/UI trước khi mở rộng.
2. Engine hiệu ứng: event/trigger queue, lựa chọn mục tiêu, thời hạn hiệu ứng, priority/cancel, giải quyết hiệu ứng tuần tự; test theo từng lá và tình huống tương tác.
3. Từng nhóm lá: Magic và Flash → Nexus → Burst → keyword/Spirit effects. Chỉ bật lá trong collection khi có bộ kiểm tra tương ứng.
4. AI hiểu hiệu ứng, chiến thuật deck, độ khó và mô phỏng đánh giá nước đi.
5. Tutorial, đồ họa bàn đấu, animation summon/attack/core, âm thanh, localization tiếng Việt, tối ưu UI và accessibility.
6. Multiplayer sau: authoritative server, kiểm tra command, ẩn dữ liệu đối thủ, reconnect, đồng bộ replay/version, lobby và matchmaking.

Tiêu chí hoàn thành: phạm vi card pool được chốt, toàn bộ lá trong phạm vi hoạt động đúng, test engine và UI pass, build thật chạy được, game flow không có ngõ cụt. Không coi 20 Spirit vanilla là game Saga hoàn chỉnh.
