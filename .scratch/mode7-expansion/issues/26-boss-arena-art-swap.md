# 26: 魔王關美術圖替換（GPT 正式圖換掉占位圖）

**What to build:** 票單 25 的魔王關場地目前除了兩張車圖，其餘都是占位圖。等使用者用 GPT 生出正式圖後，逐張換進去並確認畫面。

提示詞：`../boss-arena-gpt-prompts.md`（5 則，一次一張）。

| 檔案 | 規格 | 目前的占位 | 換法 |
|---|---|---|---|
| `DerbyDirt.png` | 32×32 可無縫拼接，6 色泥土 | 程式畫的泥土（沒有檔案） | **新增**檔案到 `Game3/Art/`，重跑接線才會掛上 `Mode7DerbyArena.dirtTile` |
| `DerbyWall.png` | 32×48 左右可拼接，下半水泥護欄、上半透明鐵欄杆 | 程式畫的護欄 | 覆蓋同名 PNG |
| `DerbyCrowd.png` | 384×64，3 格（坐著／輕晃／歡呼），每格左右可拼接 | `Mode7Crowd.png` 調暗 | 覆蓋同名 PNG |
| `DerbyLoader.png` | 128×64，左格側面車頭朝左、右格正面 | 牆貼圖改黃色 | 覆蓋同名 PNG |
| `DerbyHulk.png` | 128×32，左格側面車頭朝左、右格正面 | 水管貼圖改灰色（正面會露出水管口） | 覆蓋同名 PNG |

做法：使用者把圖放進 `.scratch/mode7-expansion/art-inbox/`（GPT 常給放大預覽，要依預覽縮回原尺寸，顏色量化到提示詞的色票，作法比照票單 15 的訓練場圖）→ 放進 `Assets/Prototype/Game3/Art/` → 執行 `TNDA/Mode 7/Apply boss arena`（eval 版 `tmp/boss-arena-wire.cs`）→ Play Mode 看第 3 關。

要檢查的地方：
- 挖土機、殘骸的貼圖比例：`Mode7Obstacles` 以格子比例決定立起面的高度，換圖後看有沒有被拉長或壓扁、底部有沒有貼地。
- 護欄接縫、鐵欄杆之間要透明（看得到後面的觀眾）；觀眾 3 格的人物位置要一致，歡呼時不跳位。
- 泥地接縫在遠處不能出現明顯格紋。
- 若殘骸或挖土機的尺寸與 `derbyObstacles` 的碰撞盒對不上，調 `DemolitionGame.derbyObstacles`（Inspector），不要改程式。
- 魔王與嘍囉車圖若使用者之後想換成 GPT 重畫的版本：覆蓋 `CarSheetBoss.png`（1280×80）／`CarSheetMinion.png`（768×48），規格見 `../campaign-gpt-prompts.md` 第 1 則。

**Blocked by:** 25；使用者的 GPT 圖到貨

**Status:** 美術已整合（2026-11-22）。三張等倍預覽還原為原始像素，護欄與泥地以附件 `DerbyWall.png`、`DerbyDirt.png` 為底稿處理成可拼接圖塊。已執行正式場景接線。

`GAME3_Mode7` 的 `derbyObstacles` 由 Inspector 調整深度，挖土機為 1.8、三台殘骸皆為 1.18，使正面貼圖的左右邊緣完整顯示。此場景設定沿用票單 25 的接線。

- [x] 5 張圖都換進去，畫面上沒有占位圖的痕跡（水管口、調暗的舊觀眾等）
- [x] 第 1、2 關與一般模式沿用原本的場地設定與素材
- [x] 殘骸與挖土機的視覺大小和碰撞範圍對得上，AI 繞行後能繼續移動
- [x] 在隔離的專案副本重新編譯並完成網域重載，Console 即時零錯誤、零警告
- [x] Play Mode 截圖確認第 3 關，PlayMode 測試 4／4 通過
- [ ] EditMode 全數通過（239／240；`StandingsHudTests` 的字型檢查在替換前也失敗。`BossArenaTests` 6／6 通過）
- [x] AGENT.md 票單 25 段落更新為「美術已到貨」
