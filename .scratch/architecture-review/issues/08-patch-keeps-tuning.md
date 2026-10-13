# 08: 場景修補不再覆寫 Inspector 調整值

**What to build:** 重新執行 GAME3 場景修補（IntegrateExisting）只負責建立與接線物件，不再把淘汰時間、車速、撞毀扣分與獎勵、防護罩時間、道具重生時間等調整值改回寫死的數字。使用者在 Inspector 調過的數值在重跑修補後保留。

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] 在 Inspector 修改任一調整值後重跑修補，數值保持不變
- [ ] 全新建置的場景仍取得正確的預設值
- [ ] 重跑修補不會多建道具、火箭或介面物件
- [ ] 重新編譯成功，Console 零錯誤、零警告
