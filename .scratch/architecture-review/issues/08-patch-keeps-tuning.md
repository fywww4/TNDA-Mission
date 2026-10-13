# 08: 場景修補不再覆寫 Inspector 調整值

**What to build:** 重新執行 GAME3 場景修補（IntegrateExisting）只負責建立與接線物件，不再把淘汰時間、車速、撞毀扣分與獎勵、防護罩時間、道具重生時間等調整值改回寫死的數字。使用者在 Inspector 調過的數值在重跑修補後保留。

**Blocked by:** None (can start immediately)

**Status:** done (architecture-review-01, implementation 6ea6804)

- [x] 在 Inspector 修改任一調整值後重跑修補，數值保持不變
- [x] 全新建置的場景仍取得正確的預設值
- [x] 重跑修補不會多建道具、火箭或介面物件
- [x] 重新編譯成功，Console 零錯誤、零警告

驗證日期：2026-10-13（Asia/Taipei）。`tmp/g3patch.cs` 從公開的 `IntegrateExisting()`／`Build()` 入口驗證：12 個玩法及機台調整值（含倒數、結算 0 秒）、破壞物與道具生成點保持不變；既有場景兩次修補後仍為 160 個物件、2 個道具、2 枚火箭；新建預設值與新場景重複修補通過；原場景逐位元還原，執行期間無錯誤或警告。Unity 重新編譯及網域重新載入完成，Console 即時零錯誤、零警告。

程式碼審查：Standards 0 項；Spec 0 項。審查固定點為 `ba54a92`，保留票單 01 的先前修改。
