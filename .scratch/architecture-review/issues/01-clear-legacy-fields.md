# 01: 清除 GAME1／GAME2 遺留欄位與死階段

**What to build:** 移除舊三案留下、GAME3 已不使用的狀態與輸入，讓讀程式的人只看到 GAME3 真正會用到的東西：永遠不會進入的「Continue（續關）」階段、跳躍輸入、賽道進度欄位（progress／bestProgress／lastAngle）、已移除的加速衝撞（boost）與尾焰顯示、未使用的文字進度條、倒車油門與自動前進油門參數。遊玩行為完全不變。

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] 上述遺留欄位、階段與參數已移除；場景中的尾焰物件若只為加速衝撞存在，一併移除或停用
- [ ] 重新編譯成功，Console 零錯誤、零警告
- [ ] Play Mode：標題示範、3-2-1 倒數、單人與雙人比賽、結算與返回標題都與修改前相同
- [ ] tmp 中仍會使用的 GAME3 驗證腳本（g3*.cs）可正常執行；只屬於 GAME1 的腳本不必維護
