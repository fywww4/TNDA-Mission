# 11: 挑戰「90 秒最高分」與花金幣解鎖

**What to build:** 新增挑戰「90 秒最高分」：8 台車、不淘汰，比 90 秒內的總分。此挑戰預設上鎖，在挑戰列表花 150 金幣解鎖（金幣不足時提示不足），解鎖狀態存檔。

**Blocked by:** 07, 10

**Status:** done（2026-11-10）

完成紀錄：開工基準為 `9a1e772`。已完成 8 台車與 90 秒規則，使用者確認的公開介面 TDD 已由紅燈轉為綠燈。獨立副本挑戰測試 26／26、完整測試 119／119、正式 Mode 7 場景流程 28 項、票單 12 更新後回歸 21 項全部通過。列表／結算截圖已檢視；重新編譯及網域載入完成，Console 零錯誤、零警告，已停止 Play Mode。雙軸 code-review：Standards 0 項、Spec 0 項（固定快照 `f472bb0`）。沒有待完成的本票規則；Windows／Web 發行檔未重建。

交接位置：規則為 `Assets/Prototype/Runtime/Challenges.cs` 的 `NinetySecondScore`；測試為 `NinetySecondChallengeTests`，解鎖／紀錄測試已參數化涵蓋本挑戰；流程腳本 `tmp/ticket11-play.cs`。詳細驗證與畫面在 `Docs/Game3/challenge-90s-verification.md`。主 Editor 正由另一票單使用（CLI 查詢逾時），主工作區其他修改保留；獨立驗證副本為 `C:\Users\a0900\AppData\Local\Temp\TNDA-ticket11-20261110`，採獨立產品存檔。

- [x] 金幣不足時無法解鎖並有提示；足夠時扣 150 金幣並解鎖，重開遊戲後仍解鎖（2026-11-10 隨票單 12 完成共用流程）
- [x] EditMode 測試：90 秒內沒有任何計時淘汰，90 秒時比賽結束並以分數排名
- [x] 最佳紀錄與金幣發放照 10 的規則
- [x] 重新編譯成功，Console 零錯誤、零警告（獨立驗證副本）
