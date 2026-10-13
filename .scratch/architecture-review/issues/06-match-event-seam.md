# 06: 比賽規則回傳事件，並跑起第一個 EditMode 測試

**What to build:** 比賽規則模擬改為回傳事件（撞毀、淘汰、命中、撞碎破壞物、撿到道具等），由場景這一側的 adapter 轉成提示文字、音效與爆炸特效。本票先接上事件 seam 並轉移一條完整路徑（例如火箭命中：事件 → 爆炸特效、音效、提示），並建立第一個不需載入場景即可執行的 EditMode 測試，證明 seam 有兩個 adapter（場景與測試）。

**Blocked by:** 02, 03, 05

**Status:** done (architecture-review-01)

- [x] 規則模擬可在沒有場景、沒有 Canvas 的情況下推進一局並產生事件
- [x] 至少一條路徑（火箭命中）完全經由事件呈現，畫面與音效與修改前相同
- [x] 專案有測試組件，至少一個 EditMode 測試可由 Unity CLI 執行並通過
- [x] 重新編譯成功，Console 零錯誤、零警告

驗證日期：2026-10-13。`DemolitionMatch` 以 `MatchTuning` 與車輛、破壞物、木箱、火箭池資料推進一局，把發生的事記在 `Events`；`DemolitionGame` 的 `Present()` 依序把事件轉成畫面與音效，再 `ClearEvents()`。火箭命中路徑：`RocketHit` → 爆炸特效＋命中音效；`ShieldBlocked`／`VehicleWrecked`／`WreckBonus` → 原本的提示文字。其餘提示與音效暫以 `NoticeCue`／`ToneCue`／`HitCue` 轉送，留給票單 07。測試組件 `TNDA.Prototype.Tests.EditMode` 以 preview scene 建立四台裸車，不載入 GAME3、不需 Canvas 或相機；4 個測試以 `run_tests --mode editor` 通過。場景回歸 164 項斷言通過。
