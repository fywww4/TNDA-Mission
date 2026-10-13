# 03: 道具 module

**What to build:** 防護罩、火箭、櫻桃三種道具的身分不再以整數 1／2／3 散落各處。木箱抽選（一般防護罩／火箭各半；最後一名 50% 櫻桃）、使用效果、顯示名稱、車名後標籤（[盾]／[箭]／[櫻]）、狀態欄文字集中在一個道具 module。新增或調整一種道具只需改這一處。

**Blocked by:** 02（櫻桃計時已屬於車輛生死週期）

**Status:** done (architecture-review-01)

- [x] 道具名稱、標籤、狀態文字與使用效果只在道具 module 中定義
- [x] 木箱欄位的說明正確描述「隨機木箱」，不再寫著過時的固定道具
- [x] 抽選機率可直接驗證：最後一名約 50% 櫻桃、領先者 0% 櫻桃
- [x] Play Mode：撿木箱、使用三種道具、火箭池滿時保留道具、提示文字與修改前相同
- [x] 重新編譯成功，Console 零錯誤、零警告

驗證日期：2026-10-13。`Assets/Prototype/Runtime/ArenaItems.cs` 是道具 module：`ArenaItem` 列舉（None／Shield／Rocket／Cherry，沿用場景已儲存的 0～3 代碼，場景檔不需改動）、木箱抽選 `ArenaItems.Roll(trailing, cherryChance, random)`、最後一名判斷 `IsTrailing`、使用效果 `Use`、名稱、車名標籤、排行榜與玩家狀態文字。比賽主程式透過 `IItemArena` 提供防護罩／櫻桃秒數、火箭發射與提示，不再以整數判斷道具。`ArenaPickup.item` 預設 `None` 表示隨機木箱，說明已更新；Editor 建置時的 1／2 只決定木箱前的暫用外觀。

`tmp/g3items.cs` 的 16 項斷言（先紅後綠）通過：固定亂數抽選、4000 次抽樣最後一名約 50% 櫻桃且領先者 0%、撿木箱、三種道具使用、火箭池滿時保留道具與五種提示文字與修改前相同。既有 `g3harm`／`g3lifecycle`／`g3wreck`／`g3spin`／`g3bump`／`g3cherry`／`g3rocket`／`g3text` 全部通過；`g3drive`／`g3fx`／`g3ai` 正常（AI 目標分布與修改前相同）。重新編譯與網域重新載入完成，Console 零錯誤、零警告。

## Standards

無硬性違規：畫面文字仍全部來自 `CabinetText`，未手動修改場景 YAML，Inspector 調整值保留。已修正：`ArenaItems.Pickup` 改名 `PickupMessage`；`IsTrailing` 改收 `IReadOnlyList`；超出範圍的道具代碼視為無道具（舊整數判斷的行為）。判斷項保留：AI 的道具時機（`AiDriver.WantsItem`）與 Editor 暫用外觀仍各自依道具分支；`ArenaItems.Status` 讀取車輛的櫻桃／護盾計時；`DemolitionGame` 的介面成員只是轉接；搬移過來的 AI 常數與「最近者」寫法維持原樣。

## Spec

行為與修改前相同（亂數抽取順序、音效、提示時長、火箭池滿保留、櫻桃清除道具）。部分達成的判斷：新增道具仍需改 `ArenaItem`、定義表、`Roll` 與 AI 的道具時機；票單 05 要求道具時機屬於 AI module，兩票在此取捨。護盾擋傷、櫻桃加速與撞毀、火箭飛行命中屬於車輛／碰撞規則，未移入道具 module。Editor 新建的木箱不再預設固定道具，與 `Game3ItemArt` 的既有結果相同。

以票單 04 提交 `fb4ef07` 為固定點，與票單 05 一起雙軸審查。
