# 07: 其餘提示文字與音效改由事件呈現

**What to build:** 把剩下由規則程式直接觸發的提示文字、音效與物件顯示隱藏全部改走 06 建立的事件 seam。待機示範模式的靜音與不顯示提示，由場景 adapter 統一處理，不必在每個呼叫點判斷。規則模擬不再直接碰觸任何畫面或音效物件。

**Blocked by:** 06

**Status:** done (architecture-review-01)

- [x] 規則模擬中沒有直接的提示文字、音效或顯示物件呼叫
- [x] 待機示範模式仍完全靜音、不顯示提示文字
- [x] EditMode 測試涵蓋撞毀扣分與獎勵、計時淘汰順序（同分比耐久、再比索引）、冠軍決定
- [x] Play Mode：所有中文提示、音效、爆炸特效與修改前相同
- [x] 重新編譯成功，Console 零錯誤、零警告

驗證日期：2026-10-13。新事件：`CarRammed`（含衝撞倍率、受傷未撞毀才顯示提示）、`CherryStrike`（緩衝期保護時 Wrecked 為 false，仍有撞擊聲）、`WallHit`（速度大於 1 才記錄）、`PropSmashed`（ByRocket 時只有音效）、`ItemPicked`、`ItemUsed`、`RocketsBusy`。`NoticeCue`／`ToneCue`／`HitCue` 與 `IItemArena.ItemFeedback` 已移除。`ArcadeVehicle` 不再設定車色、不再顯示隱藏自己、不再同步 transform；改提供 `Visible`，由 `DemolitionGame.SyncViews()`（每次呈現事件與每幀 `AfterTick`）同步。待機示範的靜音與不顯示提示由 `Present()` 統一判斷，`ArcadeGame.Announce／Sound／PlayClip` 不再各自檢查階段；爆炸特效與結算畫面仍照常呈現。

EditMode 測試 26 個（`run_tests --mode editor`）：測試車輛不建 sprite，規則若碰觸顯示物件會直接拋例外。Play Mode `tmp/g3cues.cs` 以靜音探測 AudioSource 檢查音效，17 項全部通過；`tmp/g3attract.cs` 6 項。固定亂數 AI 軌跡（`tmp/seeded/g3ai_s3`／`s7`）與修改前完全相同。

## Review

Standards：無硬性違規。判斷項保留：`DemolitionGame.AnnounceItemUsed` 仍依道具分支決定顏色與停留時間（顏色屬呈現，留在 adapter），因此 `ArenaItems.UsedMessage` 的秒數參數對火箭無意義；`Match` 取值時逐欄複製 Inspector 調整值（為保留序列化欄位與 Inspector 值，票單 06 的既定做法）；`tmp/g3cues.cs` 以中文字面值作為修改前後對照的預期值。已修正：`ArcadeVehicle` 錯置的註解移到 `Visible`。

Spec：未發現行為差異，事件順序與修改前相同，畫面上最後留下的提示一致。已補：以 `Tick` 推進到 60／90 秒驗證依時間淘汰，以及示範比賽不依時間淘汰。判斷項保留：`Present()` 成為待機靜音唯一的判斷點，基底 `ArcadeGame` 不再有保底檢查（目前待機時沒有其他音效來源）；基底 `Elapsed` 與 `Match.Elapsed` 兩個時鐘一起重設、只在 `Tick` 推進。範圍外的小改動：`ArcadeGame.sounds` 標為 `[NonSerialized]`（修正 edit mode 驗證時越界）。
