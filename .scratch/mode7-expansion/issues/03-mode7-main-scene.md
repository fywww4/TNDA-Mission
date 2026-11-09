# 03: Mode 7 換成正式主場景

**What to build:** 把 `GAME3_Mode7` 變成正式遊戲。站立的 sprite（車、破壞物、木箱、道具、皇冠、爆炸）依與各相機的距離決定前後遮擋，近的蓋住遠的；地面與場地地板不再依賴可能被建置剔除的內建 shader；Build Settings 改為只用 Mode 7 場景（俯視版 `GAME3` 保留在專案中，不再加功能）。Windows 與網頁版都能建置並實際遊玩。

**Blocked by:** 01

**Status:** completed（2026-11-09；程式 `a47160b`）

- [x] 車子開到木箱前後時，遮擋關係正確；2P 兩個半邊各自正確
- [x] Windows 建置版與網頁版的地面、場地地板正常顯示
- [x] Build Settings 第一個且唯一的場景是 Mode 7 場景
- [x] 建置版從待機示範、投幣、單人與雙人比賽到結算返回標題全程無錯誤
- [x] AGENT.md 的建置輸出段落更新為新的場景與畫布大小
- [x] 重新編譯成功，Console 零錯誤、零警告

驗證與限制：`Docs/Game3/mode7-main-scene-verification.md`。完整 EditMode 86／86、渲染整合 1／1、Windows／Web 測試建置各 21 項流程通過。正式輸出位於 `Downloads\TNDA Mode7\Windows` 與 `Downloads\TNDA Mode7\Web`；正式網頁另完成鍵盤投幣、單人／雙人與地面視覺檢查。Windows 原生擷取工具逾時、WebGL 存檔棄用提醒與測試最高分紀錄副作用均在驗證文件記錄。
