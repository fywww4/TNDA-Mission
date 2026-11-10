# 12: 挑戰「不被撞毀」

**What to build:** 新增挑戰「不被撞毀」：4 台車，3 台 AI 全部是追王，玩家撐過 120 秒不被撞毀即過關，被撞毀就失敗；最佳紀錄看過關時剩下的耐久。預設上鎖，花 150 金幣解鎖（沿用 11 的解鎖流程）。

**Blocked by:** 10

**Status:** done（2026-11-10）

- [x] EditMode 測試：玩家撞毀時挑戰立即失敗；撐過 120 秒判定過關並記錄剩餘耐久
- [x] 三台 AI 都是追王並會追玩家（前 10 秒不傷玩家的緩衝期照舊）
- [x] 解鎖、最佳紀錄、金幣發放照 10／11 的規則
- [x] 重新編譯成功，Console 零錯誤、零警告

完成紀錄：規則開關 `MatchTuning.wreckEndsRound`／`allAiHuntThePlayer`；成績為過關時剩餘耐久（%），失敗不算紀錄。11 的金幣解鎖流程尚未實作，已一併做好共用部分（`ChallengeRecords.TryUnlock`、`GoldWallet.TrySpend`、標題按 1 解鎖）。測試與驗證見 AGENT.md「Mode 7 票單 12」。
