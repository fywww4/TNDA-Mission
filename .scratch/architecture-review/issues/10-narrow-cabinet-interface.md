# 10: 縮窄街機框架的繼承 interface

**What to build:** 街機框架（標題 → 倒數 → 比賽 → 結算 → 待機示範、輸入、音效）原本為三款遊戲設計成抽象基底，現在只剩 GAME3。依 06／07 後的比賽模擬形狀，只保留 GAME3 實際需要的掛點（或改為組合），讓讀者少追一層繼承。

**Blocked by:** 07

**Status:** done (architecture-review-01)

- [x] 框架對外的掛點數量減少，沒有只為已刪除遊戲存在的虛擬成員
- [x] 音效與字型設定工具仍可套用到 GAME3
- [x] Play Mode：完整流程（待機示範、投幣、倒數、比賽、結算 20 秒返回標題、R 重玩、Esc 回標題）與修改前相同
- [x] 重新編譯成功，Console 零錯誤、零警告

驗證日期：2026-10-14。保留繼承、縮窄掛點（未改為組合：場景元件與序列化欄位不必搬移，Editor 工具照舊以 `ArcadeGame` 取用街機欄位）。`ArcadeGame` 給遊戲的掛點只剩三個抽象成員 `ResetMatch`、`Tick`、`RefreshHud`，加上 Unity 的 `OnDestroy`。移除：只為已刪除遊戲存在的 `SupportsTwoPlayers`（GAME3 恆為雙人）、`TickIdle`、`AfterTick`、可覆寫的 `Awake`；`Tick` 改為每幀在街機自身的階段處理後呼叫一次，並傳入該幀開始時的階段，因此倒數轉 GO、結算轉標題的那一幀與修改前一樣不會被當成新階段模擬。`Phase` 改為私有設定，`resultAge`／`noticeAge`／`ShowOverlay`／`PlayClip` 改為私有。`DemolitionGame` 的待機示範、比賽模擬與每幀的飛出／顯示／爆炸／引擎聲收在同一個 `Tick` 內。

`tmp/g3flow.cs`（edit mode，固定亂數）逐幀記錄完整流程：待機示範（20 秒淘汰、30 秒重來）、投幣、倒數、整場比賽到結算、結算 20 秒返回標題、雙人開局、R 重玩、比賽中與倒數中 Esc 回標題。11,701 幀的階段、時間、提示文字與顏色、倒數字、結算面板、HUD、排行榜與四車位置分數，修改前後逐位元相同（以 2d9d247 的程式與修改後各跑兩次，雜湊皆為 `3BFAD1AECA585FF5`；腳本開頭固定亂數並清除提示計時，避免先前 Editor 狀態滲入）。`tmp/g3tools.cs` 在記憶體中對 GAME3 套用 `ArcadeSoundSetup.Apply` 與 `ArcadeFontSetup.Apply`，結果與套用前相同（未存檔場景）。EditMode 測試 26 個通過；Play Mode `g3cues`／`g3text`／`g3lifecycle`／`g3wreck`／`g3cherry`／`g3rocket` 與 edit mode `g3attract` 全部通過；實際幀迴圈 Attract → Countdown → Playing 正常。重新編譯與網域重新載入完成，Console 零錯誤、零警告。

## Review

Standards：無硬性違規。已修正：`phase` 與 `Phase` 並列容易混淆，參數改名為 `startPhase`，並註明引擎聲依目前的 `Phase`（比賽結束那一幀即靜音）。判斷項保留：基底 `Advance` 與 `DemolitionGame.Tick` 各自依階段分支（為合併三個掛點的代價）；`Elapsed` 仍為 `protected set`，供待機示範自行計時（票單前既有）；`tmp/g3flow.cs` 再寫一次 `"WreckBest"` 鍵（驗證腳本）。

Spec：未發現缺漏、範圍外改動或行為差異；逐一比對倒數轉 GO、結算轉標題、比賽結束、Update 中的 R／Esc，與修改前相同。
