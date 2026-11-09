# 05: 觀眾席與鐵絲網

**What to build:** 場地外圍圍一圈鐵絲網，網外是觀眾席。觀眾平常輕微上下跳動（2～3 格動畫），有車撞毀時一起更激烈地歡呼一下。純裝飾：碰撞仍是現有的場地牆，AI 與規則不變。在 Mode 7 追尾視角中，鐵絲網與觀眾席立在場邊，往遠處延伸。

美術：使用者正請 GPT 產生 `crowd.png`（3 格 128×64，可水平拼接）與 `fence.png`（32×48，可水平拼接）。圖還沒來之前先用占位圖。

**Blocked by:** None (can start immediately)

**Status:** completed (2026-11-09)

- [x] 場地四邊都有鐵絲網，網外有觀眾席，接縫沒有明顯斷開
- [x] 觀眾平常跳動；任何車撞毀時切到激烈歡呼約 1 秒
- [x] 鐵絲網的網眼透明，看得到後面的觀眾席
- [x] 碰撞、AI 行為與修改前相同（EditMode 測試全部通過）
- [x] 1P、2P、待機示範畫面都檢查過，幀率沒有明顯下降
- [x] 重新編譯成功，Console 零錯誤、零警告

實作與驗證紀錄：`Docs/Game3/mode7-audience-verification.md`。完整 EditMode 54 / 54；Standards 0 項、Spec 0 項。
