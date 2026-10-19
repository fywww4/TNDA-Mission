# TNDA Mission：GAME3 整合版交接

更新日期：2026-10-16（Asia/Taipei）。目前只製作 GAME3；使用者的新要求優先於先前三案規格。

## 最新需求與現況

使用者要求刪除 GAME1、GAME2，把 GAME2 的最後一名死亡／淘汰規則加入 GAME3；AI 不追撞玩家，會互撞、破壞得分，被撞毀扣分，加入防護罩與火箭。

- 已透過 Unity CLI／AssetDatabase 移除兩個舊場景、兩案專用玩法與建置程式、Game1／Game2 素材資料夾。
- Build Settings 現在只啟用 `Assets/Scenes/GAME3.unity`，為第一個且唯一的遊戲場景。
- GAME3 的既有相機、車圖、車輛、介面保留並直接修補；未整個重建場景。
- 舊分工（Codex GAME1／GAME3、Claude GAME2）已結束，後續工作集中在 GAME3。
- 原三案交接記錄封存於 `Docs/Archive/AGENT-three-games-2026-10-04.md`，供查閱歷史；目前製作規格以本文件為準。

## 整合版規則

### 積分淘汰

- 一人搭配三台 AI，或兩人搭配兩台 AI，共四車。
- 競賽時間 120 秒；開局先 3－2－1－GO，GO 才開始計時。
- 開局標語（2026-10-13 使用者要求）：「出發！」後於畫面中央依序顯示「活下來！」與「盡量取得最高分！」各 1.5 秒（倒數字的一半大小 22px；`startMessages`／`startMessageSeconds`）。使用者原文為「取的」，已改為「取得」。
- **45 秒淘汰目前最低分的車；80 秒再淘汰最低分**（2026-10-16 使用者要求，原為 60／90 秒；`firstElimination`／`secondElimination`）；120 秒從最後兩車淘汰低分者，決定冠軍。
- 計分使用破壞、衝撞與火箭命中分數，沒有沿賽道行駛距離分數。
- 同分時耐久較低者淘汰；耐久也相同時，車輛索引較高者淘汰。
- 計時淘汰是永久退場；存活車保留原分數。排行榜保留被淘汰者的順位與分數。
- 被計時淘汰的車不會直接消失：從當時位置朝遠離場地中心的方向「超級打滑」飛出，每秒轉 4 圈、初速 4、每秒加速 14，飛出 320×320 畫面後才隱藏（約 0.7 秒）；飛出過程不參與碰撞與火箭判定。數值為 `spinOutTurnsPerSecond`／`spinOutStartSpeed`／`spinOutAcceleration`（2026-10-12 使用者要求）。2026-10-13 起一般撞毀（耐久歸零，玩家與 AI 皆同）也用同樣的打滑飛出，飛出後隱藏，仍於撞毀 3 秒後在角落重生（重生時中止殘留的飛出）。示範模式重生的車維持 AI 駕駛。
- **投幣繼續（2026-10-16 使用者要求，參考老師「CONTINUE? 倒數」）**：真人在 45 秒（第四名）或 80 秒（第三名）被計時淘汰時，場地中央出現黃框黑底面板「P1 投幣繼續？」＋ 44px 倒數 10→1（每秒一聲，沿用既有電子音，不增加音效種類）＋提示「按 5 投幣，再按 左Ctrl」／「按 左Ctrl 繼續（已投 N 枚）」。比賽不暫停。P1 按左 Ctrl、P2 按數字鍵 Enter（各自的道具鍵），花 1 枚硬幣復活：分數拉到「目前存活車的最低分」（原本較高則保留），在離其他車最遠的角落重生、耐久滿、2 秒保護、沒有道具，排行榜移除淘汰名次（`DemolitionMatch.Revive`、`VehicleRevived` 事件）。次數不限。120 秒決賽輸掉（第二名）不給繼續，直接結算；待機示範不出現。數值：`continueSeconds` 10、`gameOverSeconds` 2；面板由 `Game3Text.CreateContinuePanel` 建立（場景物件 `Continue panel`）。
- 倒數結束沒投幣：雙人時另一位真人還在就繼續比（被淘汰者觀戰）；已經沒有真人時顯示「GAME OVER」2 秒，立刻回標題待機示範（取代原本「真人都被淘汰後觀看 AI 比到結束」）。這是遊戲唯一的一局 120 秒，沒有關卡。
- 驗證：`tmp/g3continue.cs`（Play Mode）17 項全部通過；EditMode 新增復活分數與「比賽結束後不能復活」測試，共 30 個通過。
- **勝利畫面（2026-10-19 使用者要求）**：冠軍是真人時，比賽停住，全螢幕顯示使用者提供的勝利圖（128×128 對齊四色後以 2 倍 256×256 置中，下方「最終得分 NNNNN」；依使用者要求不顯示「Player1 勝利！」標題）3 秒（`victorySeconds`），再進入結算畫面。Player2 獲勝時用把青色車改成黃色的版本（`Art/VictoryP2.png`；P1 為 `Art/Victory.png`，來源 `Art/Source/VictorySource.png`，由 `Game3ItemArt.VictorySprites` 產生）。電腦車獲勝則直接進結算。介面物件 `Victory screen` 由 `Game3Text.CreateVictoryScreen` 建立，放在畫布最上層蓋過排行榜等 HUD。不加音效。驗證 `tmp/g3victory.cs`：P1 勝利顯示圖、畫面停住、3 秒後進結算；P2 勝利用黃車圖；電腦勝利直接結算。
- 最後冠軍與四車名次展示 20 秒，再返回標題；也可投幣後按 1／2 立即再玩。

### 耐久、破壞與得分

- 車輛耐久 100%；不輸入油門時減速至停止，沒有自動前進。
- 撞牆只減速／短暫硬直，不扣耐久。
- 箱子 100 分、油桶 75 分；車速至少 2.1 才可直接撞碎，火箭也能破壞。破壞物 6 秒後且附近沒有車時重生。
- 障礙物（破壞物）位置隨機（2026-10-15 使用者要求）：每局開始與每次重生都在場內隨機挑空位（離車 1.2、離木箱 1、兩個破壞物互離 1.4 以上；40 次找不到就沿用原位置）。開關為 `randomPropSpots`（Inspector，預設開；EditMode 測試關閉以固定位置）。
- 車對車衝撞依速度計分；側撞 2 倍、追撞 1.5 倍。
- 碰碰車手感（2026-10-13 使用者要求，避免被撞後黏住卡死）：每次新接觸兩車互推一個會滑行並逐漸減速的位移（`contactBump` 1.4）；被衝撞的車再加上依撞擊速度增加的滑行（`ramBump` 2.4），車頭隨機偏 15～30°，硬直縮短為 0.12 秒；撞人的車會小幅反彈。滑行以 `bumpFriction` 4.5 衰減，撞牆時反向減弱。實測：側面被撞的靜止 P1 約 1 秒內滑開 1.17 單位、兩車分開。
- 撞毀一台車獎勵攻擊者 300 分；被撞毀者扣 250 分，最低扣至 0 分。
- 一般撞毀不是永久淘汰：玩家與 AI 均在 3 秒後滿耐久重生，保留扣分後的分數，清除道具，給 2 秒保護。
- 開局也有 2 秒保護。
- 上述重生、扣分、傷害與道具數值屬於原型預設，可在 DemolitionGame Inspector 調整。

### AI

- **難度調整（2026-10-12 使用者要求）**：開局 GO 後前 10 秒（`aiGraceSeconds`）為緩衝期：AI 只把其他 AI 當衝撞目標、會轉向避開玩家，且 AI 的衝撞、火箭、櫻桃都不會傷害玩家。
- **三種固定個性（2026-10-15 使用者與老師建議定案，取代原本「輪替策略＋領先 300 分追撞」）**：每台電腦車一種個性，個性只決定怎麼開車與用道具；傷害、得分、扣分、重生規則全部相同。車名取代「電腦1～3」（2026-10-16 起改為英文名：碰碰＝**Zhima**、追王＝**SAN**、迷糊＝**Panda**；玩家為 **Player1**／**Player2**，定義在 `CabinetText`；底部操作提示仍寫 P1／P2）。依座位固定：電腦座位 1＝**迷糊**、2＝**追王**、3＝**碰碰**（`AiDriver.Personality`）。單人三種都有；雙人只剩追王、碰碰；待機示範的第四台（P1 座位）是第二台碰碰。
  - **追王**（2026-10-16 依使用者回饋「太強、欺負玩家」放軟）：「追擊 6 秒、散開 4 秒」輪流（參考小精靈鬼魂的追擊／散開）。每輪追擊開始時 60% 追第一名、40% 追第二名（不算自己，同分追近的；`hunterLeaderChance`）。撞到目標就立刻結束這輪追擊進入散開，下一輪不追剛撞過的那台車。散開時去撞破壞物、撿木箱，不鎖定任何車。數值 `hunterChaseSeconds` 6、`hunterScatterSeconds` 4，可在 Inspector 調整。防護罩在耐久低於 40% 才用，火箭只在追的目標在正前方時發射。驗證 `tmp/g3hunter.cs`：P1 領先 1500 分時，100 秒內追王鎖定 P1 的時間約 13%（原本幾乎全程），散開約 32%，散開時從不鎖定車輛，最長追擊 6.0 秒，追擊在目標附近結束 8 次，其中 7 次下一輪換了目標（檢查以「結束時距離目標很近」推測撞擊，剩下 1 次可能是時間到剛好在附近）。
  - **碰碰**：撞最近的車，撞到後 2 秒內改追下一台最近的；沒有可撞的車時去撞破壞物或撿木箱。衝撞前（目標 1.5 單位內、在前方）開防護罩，前方有車就發射火箭。
  - **迷糊**：在場地隨機遊蕩，不刻意撞車；破壞物或木箱剛好在前方 1.8 單位內才順路過去。撿到道具後隨機 2～4 秒就用掉，不挑目標。最高速度是其他電腦車的 85%（`wandererTopSpeedScale`）。
  - **後段加壓**：電腦車最高速度為玩家的 87%（`aiTopSpeed`），第一次淘汰（45 秒）後提高到 95%（`aiLateTopSpeed`）；個性不變。10 秒緩衝期照舊。
  - **櫻桃**：任何個性吃到櫻桃後都追殺目標（分數高、距離近優先，`leaderBias` 1.5），8 秒後恢復原本個性。所有個性都會逃離 2.2 單位內的櫻桃車、在緩衝期避開玩家。
  - 原本的 `leaderHuntMargin` 已移除（追王本來就永遠追第一名）。
- 驗證（`tmp/g3personality.cs`、`g3personality2.cs`，Play Mode）：車名 1P＝P1／迷糊／追王／碰碰、2P＝P1／P2／追王／碰碰、示範＝碰碰／迷糊／追王／碰碰；緩衝期追王追最高分的電腦車、沒有 AI 鎖定 1500 分的 P1，之後追王追 P1；領先的追王追第二名；碰碰先追最近的車、撞後換車；迷糊 75 秒內從未鎖定車輛；最高速度迷糊／追王／碰碰 2.07／2.44／2.44，淘汰後追王 2.66；櫻桃迷糊會追殺；迷糊道具約 2～4 秒用掉；雙人 30 秒對戰無例外、緩衝期真人耐久 100%。舊的 `tmp/g3ai.cs`／`g3driver.cs` 測的是取代前的輪替策略，已於 2026-10-16 刪除。
- 玩家仍可主動撞擊 AI、射擊 AI 或另一位玩家。

- **脫困（2026-10-16 使用者回報追王卡在角落、AI 被障礙物卡住）**：`AiDriver` 每秒檢查一次，若車子 1 秒內移動不到 0.3 單位（打滑或硬直中不算），就朝場地中央（避開岩漿方向）全速開 0.8 秒再重新規劃。慢速撞到破壞物（速度未達 2.1、被彈回）時：若本來就要去撞它，先退到 1.9 單位外助跑、再衝刺撞碎（最多 1.6 秒）；若只是擋路，從往目的地的那一側繞過去。驗證 `tmp/g3stuck.cs`：車頭頂進右上角的追王 3 秒內開出 2.8～3.9 單位；從靜止慢慢頂到破壞物的迷糊約 3 秒內助跑撞碎；三場 1P 比賽中電腦車「2 秒內幾乎沒動」的時間約占存活時間 2.5%（多為與其他車推擠）。

### 道具與操作

- 固定兩個道具生成點，外觀為木箱（2026-10-11 使用者要求，增加隨機性）：撿到時隨機給道具，撿取後 8 秒且附近沒車時重生。一般情況防護罩／火箭／油各 1/3（2026-10-16 加入油）；撿取者若為目前最後一名（同分也算），50% 得到櫻桃，其餘三種平分（`trailingCherryChance`）。實測 6 場四台 AI 對戰：撿木箱 61 次、櫻桃 9 次（約 15%，每場約 1.5 顆）。木箱圖由 `Game3ItemArt` 依使用者木箱圖片的設計（木框＋直條木板）重畫成像素圖（2026-10-15 起改為白色、縮小為 12×12，原為黃色 16×16），原圖保留在 `Art/Source/ItemCrateSource.png`；原照片縮到 16px 無法辨識。刻意不畫斜撐，避免與可破壞的 X 箱子混淆。
- 每車最多持有一個道具；道具鍵只用來使用道具，沒有道具時按下不會有動作。已依使用者要求（2026-10-11）移除加速衝撞，AI 也不再衝撞加速。
- 防護罩持續 6 秒，抵擋耐久傷害，車身顯示青色圓框。
- 火箭沿車頭方向直飛，命中車扣 38% 耐久並給攻擊者 120 分；撞毀仍加 300 分。可撞碎破壞物並取得其分數。
- **櫻桃（小精靈吃豆玩法）**：只有最後一名能從木箱開到。按道具鍵吃下後 8 秒（`cherryDuration`）：最高速 ×1.35（`cherrySpeedMultiplier`），車身變成條紋（見下），最後 2 秒閃爍。櫻桃車碰到任何車即撞毀對方（攻擊者得撞毀獎勵 300 分）；但若對方實際衝撞櫻桃車（接近速度 ≥1 且大於櫻桃車），或被火箭命中，櫻桃車立即撞毀。防護罩與重生保護仍可擋下。
- 櫻桃 AI：持有櫻桃的 AI 依 `PickRival` 追殺對手；其他 AI 在 2.2 單位內會逃離櫻桃車。前 10 秒不傷真人，之後可以撞毀玩家，與一般衝撞／火箭共用 `VehicleHarmPolicy`（2026-10-13 票單 04 使用者確認）。
- 櫻桃圖：使用者提供的圖片，去白背景後縮為 13×14 四色像素圖 `Art/Cherry.png`，來源 `Art/Source/CherrySource.png`。場景修補：`Game3ItemArt.PatchScene()`；`Game3SceneBuilder.IntegrateExisting` 也會呼叫 `Game3ItemArt.Apply`。
- 櫻桃期間整台車變**青黃條紋**（2026-10-16 使用者要求，取代 10-13 的整台黃色：黃色與 P2 撞色，也取消車旁的櫻桃圖示）：`CarPalette.shader` 的 `_Solid` 模式把車身與白色部分畫成橫跨車身、每條 3 個像素寬的條紋，交替 `_SolidColor`／`_StripeColor`（`cherryStripeA` 青、`cherryStripeB` 黃），只保留黑色外框車窗；以 MaterialPropertyBlock 個別設定，最後 2 秒閃爍。不改 `body.color`，排行榜與車名顏色不變。玩家與 AI 吃櫻桃都適用。
- **持有道具顯示在車尾（2026-10-16 使用者要求，參考瑪利歐賽車）**：持有的道具以圖示跟在車子正後方（`itemTrail` 0.7 單位，圖示保持直立），取代車名後的 [盾]／[箭]／[櫻]。防護罩、火箭各為 12×12 四色像素圖 `Art/Shield.png`、`Art/Rocket.png`，持有櫻桃（還沒吃）顯示 `Art/Cherry.png`。火箭由使用者提供的像素圖轉換（紅色機首與尾翼、火焰→黃，淺藍機身→白，藍色窗→青，灰色噴嘴→黑；來源 `Art/Source/RocketSource.png`）；防護罩依使用者提供的圖重畫（尖角白框、青色盾面、白色驚嘆號），因原圖縮到 12px 時驚嘆號的點會消失，原圖保留在 `Art/Source/ShieldSource.png`。每車一個 `Held item` 物件，由 `Game3ItemArt.Apply` 建立（同時移除舊的 `Cherry power`）。排行榜與右上狀態仍以文字顯示道具名稱。驗證：`tmp/g3cherry.cs` 11 項（含持有櫻桃圖示、吃下後條紋且無圖示、閃爍、結束還原）通過。
- 火箭命中任何車輛（玩家或 AI，含被防護罩擋下）都在命中點顯示爆炸特效（2026-10-13 使用者要求）：使用者提供的爆炸圖去白背景後縮為 34×32 四色像素圖 `Art/Explosion.png`（橘紅尖刺與金色雲→黃、深橘陰影→黑、淺黃核心→白；來源 `Art/Source/ExplosionSource.png`）。0.45 秒內由 0.5 倍彈到 1.15 倍再回 1 倍，最後閃爍消失（不使用透明度以維持四色）。每枚火箭一個顯示物件（`Effects/Rocket explosion 0～1`），由 `Game3ItemArt.Apply` 建立。
- **加分文字（2026-10-15 使用者要求，回應老師「看 Intro 看不出在幹嘛」）**：每次得分或扣分（撞碎破壞物、衝撞、火箭命中 +120、撞毀獎勵 +300、被撞毀者 -250）都在那台車上方跳出數字：先以 22px「彈出」0.12 秒，再以 11px 往上飄 16px，最後閃爍消失，共 0.9 秒（`popupSeconds`），不用半透明以維持四色。顏色為該車顏色。每車同時最多一個，0.15 秒內的連續得分合併成一個數字（例如衝撞＋撞毀獎勵），之後的新得分取代舊的；靠近的兩個數字自動錯開一行。規則以 `ScoreChanged` 事件回報，場景物件 `Score pop-ups`（四個 TMP 文字）由 `Game3Text.CreateScorePopups` 建立。待機示範也顯示加分文字（仍靜音、不顯示提示列）。
- 頂端提示列不再顯示得分類提示（撞碎、撞擊、櫻桃一碰撞毀、撞毀獎勵、被撞毀），只留出發、淘汰、拿到／使用道具、火箭還在飛、防護罩擋下等規則提醒。驗證：`tmp/g3popups.cs`（Play Mode）與更新後的 `tmp/g3cues.cs` 17 項全部通過；EditMode 測試 28 個全部通過。
- 已在 Play Mode 驗證：最後一名約 50% 開到櫻桃、領先者 0%；櫻桃速度提升；櫻桃車碰 AI 使其撞毀並 +300；P2 衝撞櫻桃 P1 使 P1 撞毀；火箭一發擊毀櫻桃車（一般車只扣 38%）；櫻桃 AI 在前 10 秒不傷真人，10 秒後可以；30 秒 AI 對戰無例外。
- 火箭最多同時兩發；池已滿時保留道具，稍後再按。2026-10-16 起火箭與油漬共用這兩個名額（`ArcadeProjectile.slick`）。
- **油（2026-10-16 使用者要求，類似香蕉皮）**：按道具鍵把油漬丟在車子正後方 0.55 單位。任何車開上去（距離 0.4 內）原地轉一圈 0.8 秒、速度歸零、不能操作，不扣耐久（`oilSlipSeconds`）；丟油的車前 1 秒不會踩到自己的油。踩到後油漬消失，沒人踩 12 秒後消失（`oilLifeSeconds`）；有人踩到時丟油者 +50 分（`oilBonus`，跳加分文字）。追王、碰碰在後方 2 單位內有車時才丟，迷糊 2～4 秒內隨便丟；AI 不躲油。油漬圖為使用者提供的黃色潑灑像素圖縮成 19×12（`Art/OilSlick.png`，來源 `Art/Source/OilSlickSource.png`，webp 轉檔時透明背景變黑，已改白再轉）；車尾圖示依使用者的黑色油滴圖重畫為 10×12，黑底加白框與白色反光，否則在黑色地板上看不見（`Art/Oil.png`，來源 `OilSource.png`）。依使用者要求油不加音效（丟油時不發出道具音）。
- **岩漿（2026-10-16 使用者要求）**：80 秒（與第二次淘汰同時，`lavaSeconds`；待機示範 15 秒，`demoLavaSeconds`）在離車、木箱、破壞物與重生角落夠遠的隨機位置產生一塊 1×1 單位的岩漿（`lavaHalfSize` 0.5），之後永久存在、不擴大。為守住 10 個活動物件，岩漿佔用一個破壞物的名額：那個破壞物從此不再出現（`BreakableProp.molten`）。碰到岩漿直接撞毀：防護罩、重生保護都擋不住，扣 250 分、3 秒後重生，沒有人得撞毀獎勵；破壞物與重生點不會出現在岩漿上。一般 AI 會繞開前方的岩漿，吃了櫻桃的 AI 不躲（速度快、控制差，可以引它撞進去）。提示列顯示「岩漿出現！碰到就撞毀」（與淘汰同時出現時接在淘汰提示後面）。圖為使用者提供的岩漿圖轉成 32×32 四色（深紅橘→黑、橘與琥珀→黃、亮黃→白；`Art/Lava.png`，來源 `LavaSource.png`），每 1/3 秒翻轉一次像在冒泡，避免和靜止的黃色油漬混淆。依使用者要求岩漿不加音效。電腦櫻桃速度維持 1.35 倍（使用者決定不調整）。
- 驗證：EditMode 新增木箱三選一、岩漿準時出現並無視防護罩、示範提早出現、油漬打滑與 +50、火箭與油共用名額，共 35 個全部通過；Play Mode `tmp/g3lava.cs`：示範 15 秒出現岩漿、80 秒前沒有、80 秒出現並與淘汰提示同時顯示、用掉一個破壞物名額、30 秒內一般 AI 沒有撞進岩漿、持有油時車尾顯示油圖示、丟出後地板顯示油漬、下一台車打滑且不扣耐久、丟油者 +50。
- **投幣（2026-10-16 使用者要求）**：5（主鍵盤或數字鍵盤，同街機模擬器）或手把 Select 投 1 枚，每投一枚提示「已投 N 枚」，最多 9 枚（`ArcadeGame.Credits`，不存檔）。單人開局花 1 枚、雙人花 2 枚、投幣繼續花 1 枚。開局：1、左 Ctrl 或手把 Start 單人；2 或數字鍵 Enter 雙人；硬幣不夠時提示「請先按 5 投幣」。原本的 C／Space／Enter 投幣已移除。
- P1：左／右方向鍵轉向，上方向鍵為油門，左 Ctrl 使用道具。
- P2：A／D 轉向，W 為油門，數字鍵盤 Enter 使用道具；主鍵盤 Enter 不控制 P2。
- 按住油門會逐漸加速（`accelerationPerSecond` 1.4，約 2 秒到最高速 2.8）；放開後以 `coastDownPerSecond` 2.2 減速到停止；無倒車。兩個數值可在 Inspector 調整。此操作已由使用者確認（2026-10-11）。
- 手把：左類比／方向鍵左右轉向，往上為油門，South 按鈕使用道具。
- Esc 回標題。R 免費重玩已於 2026-10-16 依使用者要求移除（正式展示每局都要投幣）。原 F1／F2／F3 切換已移除。

### 標題畫面（街機待機示範，2026-10-12 使用者要求）

- 待機時四台車全由 AI 駕駛，背景自動播放一局示範（`DemolitionGame.TickIdle`）：第 20 秒淘汰最後一名以展示打滑飛出，第 30 秒重新開始（`demoEliminationSeconds`／`demoLoopSeconds`）。示範不計入正式比賽、不觸發結算。
- 待機期間靜音、不顯示提示文字（場景 adapter `DemolitionGame.Present()` 在 Attract 階段只呈現畫面事件與加分文字，統一略過提示與音效），隱藏排行榜、狀態、車名與原本的標題說明面板／1P2P 按鈕。
- 畫面只顯示：標題「廢車場大亂鬥」、「單人 1 枚　雙人 2 枚」、閃爍的投幣提示（0 枚「按 5 投幣」、1 枚「已投 1 枚　按 1 單人」、2 枚以上「已投 N 枚　按 1 單人　按 2 雙人」），以及底部 P1／P2 操作提示（規則說明已依使用者要求簡化，只留操作）。文字定義在執行期文字表 `CabinetText`（`P1Controls`、`P2Controls`、`CoinRule`、`CoinPrompt`），介面由 `Game3Text.CreateAttractScreen` 建立（場景物件 `Attract screen`，每次修補會重建）。
- 開始方式：投幣後按 1／左 Ctrl 單人，2／數字鍵 Enter 雙人；開始後恢復正常 HUD 與 3-2-1 倒數。

## 建置前清理（2026-10-16）

- 刪除未使用的素材（經 Unity 移到資源回收筒）：`Fonts/NotoSansTC/`（約 8.7MB）、舊的 SDF 字型 `Cubic_11 Latin SDF`／`Cubic_11 CJK SDF`（目前只用 `Cubic_11 Pixel 11px`）、`Audio/Shooting.mp3`、`Assets/Screenshots/` 中 GAME1／GAME2 與 10-02 的舊截圖（GAME3 截圖保留）、`Docs/Game1/`。
- 刪除程式中沒有用到的部分：`ArcadeVehicle.tagLabel`（車名早已改由畫面空間標籤顯示，四台車的欄位都是空的）與 `DemolitionGame.LivePlayers`。
- `tmp/`：刪除 GAME1／GAME2 腳本、`game2/`、已被取代的 `g3ai.cs`／`g3driver.cs`、一次性修補與取樣腳本、CLI 目錄 json 與所有截圖輸出；保留目前使用的 `g3*.cs` 回歸腳本、截圖工具與老師講義頁 `tmp/pdfs/`。
- 保留：`SampleScene`（使用者先前要求，不在 Build）、`Assets/Prototype/Art/ArcadePalette.shader`（未使用但位於使用者的 Art 資料夾，未經確認不刪）。
- Build Settings 只有 `Assets/Scenes/GAME3.unity`；EditMode 測試組件只在 Editor 編譯，不會進入建置。清理後重新編譯，Console 零錯誤、零警告；EditMode 35 個通過；`g3cherry`／`g3cues`／`g3continue`／`g3lava`／`g3stuck` 全部通過。

## 畫面、音效與素材

- 保持單畫面 320×320、PPU 32、Built-in Pixel Perfect Camera；Point Filter，無 Mipmap、無壓縮。
- 黑、白、青、黃四種顏色。道具木箱與破壞物（X 箱子、油桶）為白色（2026-10-15 使用者要求，由 `Game3ItemArt.Apply` 修補場景）。靜態場景、道具、保護圓框、火箭使用 Unity 內建 Square／Circle；車圖使用使用者提供的圖片。
- 活動實體上限 10：四車、兩個破壞物、兩個道具、兩個火箭／油漬共用名額；80 秒後岩漿取代一個破壞物，總數不變。車頂名稱、耐久條、保護圓框是車的視覺元件（加速衝撞的尾焰已於 2026-10-13 隨遺留欄位清除移除）。
- 排行榜列出四車分數與 HP／持有道具／重生倒數／淘汰順位，右側顯示玩家按鈕狀態與時間，頂端顯示下次淘汰倒數。
- 車的像素圖與四色材料位於 `Assets/Prototype/Game3/Art/`。
- GAME2 的車輛原始圖片已先複製至 `Assets/Prototype/Game3/Art/Source/`，才移除舊案資料夾。
- **總音量（2026-10-16 使用者要求，聲音太大）**：`DemolitionGame` Inspector 的 Volume 區有 `Master Volume` 滑桿（0～1，預設 0.5），一次控制全部聲音（倒數、碰撞、合成音、引擎循環），以 `AudioListener.volume` 套用，Play Mode 中拖動即時生效；Play Mode 中的修改離開後會還原，要保留請在停止狀態下調整。
- 共用音效 `Assets/Prototype/Audio/` 保留。GAME3 使用倒數、引擎、碰撞，以及道具與結算合成音，共五類聲音（衝撞加速移除後，Speed 只作為循環引擎聲）。
- 介面已改為繁體中文（2026-10-11）。**所有畫面文字只定義一次（2026-10-13，ticket 09）**：`Assets/Prototype/Runtime/CabinetText.cs` 收錄標題、投幣、P1／P2 操作、按鈕、提示、廣播與排行榜／結算文字；需代入數值的是 `string.Format` 模板（`{0}`）。Editor 場景修補（`Game3Text.PatchScene()`，含車名標籤轉換）與執行期 HUD 都讀它；比賽中底部提示 P1 為 `P1Controls`，P2 為 `P2Controls`，與標題畫面相同。新增畫面文字請加在 `CabinetText`，不要在程式內另寫中文字串；車名為英文（2026-10-16）：Player1、Player2、Zhima（碰碰）、SAN（追王）、Panda（迷糊）；排行榜車名欄寬 7 字元，持有的道具改以車尾圖示顯示。
- **輸出解析度規定為實際 320×320 像素（使用者 2026-10-12 確認）**。Editor Game 視窗使用內建的 320×320 固定解析度；曾暫時設為 960×960 屬錯誤，已移除該自訂尺寸。不可用放大視窗來改善字體。
- **字型（2026-10-12）**：在 320×320 下唯一能清楚的做法是像素字型 1:1 繪製。使用俐方體 11 號 Cubic 11（`Assets/Prototype/Fonts/Cubic_11.ttf`，OFL），TMP 字型 `Cubic_11 Pixel 11px.asset`：RASTER_HINTED 點陣、取樣 11px、Point 濾鏡、Bitmap shader、512 圖集；預先產生所有畫面字元（`ArcadeFontSetup.UsedCharacters`：ASCII、常用全形標點、`CabinetText.All`，再加上場景中的標題、說明、`startMessages` 與現有 TMP 文字），建置不清除。行高與基線取整數（行高 13、ascent 9），Canvas 開啟 pixelPerfect。之前糊掉的原因是 Cubic 被以 7～8.5px 的 SDF 繪製；Noto Sans TC（`Fonts/NotoSansTC/`）不再使用。
- 文字大小只用 11 或其整數倍（標題 22、倒數 44）。版面由 `Game3Text.PixelLayout` 設定（整數像素座標）：頂部標題＋淘汰倒數、四車排行榜（左）、玩家狀態（右），提示列在場地上框內，底部操作提示一行（雙人時每 3 秒切換 P1／P2）。修改文字長度時注意 11px 中文每字 11px 寬，說明面板一行最多約 24 字。
- `SampleScene` 與使用者另外放入 `Assets/Prototype/Art/` 的圖片保留；不在遊戲 Build Settings 中。

## 程式位置

| 檔案 | 用途 |
| --- | --- |
| `Assets/Prototype/Runtime/DemolitionMatch.cs` | 比賽規則模擬（不需場景物件）：駕駛、破壞物、木箱、碰撞、火箭、撞毀、計時淘汰與冠軍；`MatchTuning` 為規則數值，以 `IAiArena`／`IItemArena` 提供局面給 AI 駕駛與道具 module，發生的事記錄為 `Events`。 |
| `Assets/Prototype/Runtime/MatchEvents.cs` | 比賽事件：火箭命中、撞擊、櫻桃撞車、撞牆、撞碎破壞物、撿到／使用道具、火箭仍在飛、防護罩擋下、撞毀、撞毀獎勵、每次得分變化（`ScoreChanged`，供加分文字）、淘汰、比賽結束。規則只記錄事件，提示文字、顏色、停留時間與音效都由 `DemolitionGame.Cue` 決定。 |
| `Assets/Prototype/Runtime/DemolitionGame.cs` | GAME3 場景 adapter：BreakableProp／ArenaPickup、Inspector 調整值與輸入送進 `DemolitionMatch`，再把狀態與事件轉成車輛／破壞物／木箱／火箭顯示、爆炸特效、提示文字與音效。 |
| `Assets/Prototype/Tests/EditMode/` | EditMode 測試組件 `TNDA.Prototype.Tests.EditMode`（不載入 GAME3 場景），以 Unity CLI `run_tests --mode editor` 執行；執行期程式在 `TNDA.Prototype` 組件。 |
| `Assets/Prototype/Runtime/ArcadeVehicle.cs` | 車輛資料、駕駛與生死週期 module。TakeDamage／Eliminate／Respawn 統一清理狀態；持有櫻桃、滑行、打滑飛出、名次，以及供 AI 察覺的碰撞／重生計數。VehicleLifecycleRules 傳入 Inspector 玩法值。Drive 只有油門加速與放開滑行，無倒車、無自動前進。規則不碰顯示物件：`Visible`（行駛中或打滑飛出中）由場景 adapter 轉成物件顯示隱藏，車色也由 adapter 在開局設定。 |
| `Assets/Prototype/Runtime/AiDriver.cs` | AI 駕駛 module（`car.Ai`）：依座位固定的個性（`AiPersonality`：碰碰／追王／迷糊）決定目標與道具時機；共用閃避玩家、逃離櫻桃、櫻桃追殺；比對車輛的 `Collisions`／`Spawns` 與存活車數自行重新規劃。比賽主程式經 `IAiArena` 提供局面。 |
| `Assets/Prototype/Runtime/ArenaItems.cs` | 道具 module：`ArenaItem` 列舉、木箱抽選、使用效果、名稱／標籤／狀態文字；比賽主程式經 `IItemArena` 提供火箭與持續時間；撿到／使用道具的提示文字由 `PickupMessage`／`UsedMessage` 產生，經事件由場景 adapter 顯示。 |
| `Assets/Prototype/Runtime/VehicleHarmPolicy.cs` | 衝撞、櫻桃、火箭與 AI 選目標共用的傷害資格規則；所有 AI 攻擊在開局前 10 秒不傷玩家，之後可傷玩家。 |
| `Assets/Prototype/Runtime/ArcadeProjectile.cs` | 固定彈體池資料，並非 MonoBehaviour；顯示由場景 adapter 同步。 |
| `Assets/Prototype/Runtime/ArcadeGame.cs` | 街機框架：標題、倒數、輸入、音效、結算與重玩。給遊戲的掛點只有 `ResetMatch`、`Tick`（每幀一次，傳入該幀開始時的階段）、`RefreshHud`（票單 10）。 |
| `Assets/Prototype/Runtime/ArcadeEngineAudio.cs` | 真人車循環引擎聲。 |
| `Assets/Prototype/Runtime/CabinetText.cs` | 所有畫面文字的唯一來源（含格式模板）；場景修補、執行期 HUD 與字型字元清單都讀它。 |
| `Assets/Prototype/Editor/Game3SceneBuilder.cs` | Build 供空原型；IntegrateExisting 直接修補既有 GAME3，重複執行不會多建道具／介面。 |
| `Assets/Prototype/Editor/ArcadeSoundSetup.cs` | 接線倒數、碰撞與引擎聲，不再參照已刪除遊戲類別。 |
| `Assets/Prototype/Editor/ArcadeFontSetup.cs` | 建立 Cubic 11 的 11px 點陣 TMP 字型並套用全部 GAME3 文字（字級取整到 11 的倍數）；PatchScene 可單獨修補字型。 |
| `Assets/Prototype/Game3/Art/CarPalette.shader` | 保留黑色車窗／輪胎，依車輛角色顯示青、黃、白。 |

## Unity Editor 操作規範

- 優先 Unity CLI；代理直接執行 CMD／PowerShell 指令，不要求使用者輸入。
- 修改腳本、場景、資產或設定後，明確重新編譯，等待編譯及網域重新載入完成，再檢查 Console。修正此次修改造成的錯誤後重查。
- Editor 可連線時，以 CLI／Editor API 修改場景、Prefab、材料、資產；勿手動編輯 `.unity`、`.prefab`、`.asset` YAML。
- 共用程式與使用者檔案操作前先讀取，不覆蓋新改動。

環境：Unity 6000.6.4f1，Pipeline 0.8.0-exp.1，Unity CLI 1.0.0-beta.13（2026-10-12 以官方安裝腳本更新；`self-update` 在此環境回報成功但未實際替換）。專案與 CLI：

```powershell
# 以下由代理直接執行
& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command editor_status --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json
& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command recompile --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json
& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command recompile_status --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json
& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command console_status --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json
```

- 每個 command 帶 --caller plugin --skill unity-cli 及專案完整路徑。可用命令以 unity list 為準。
- recompile 非同步；等到 completed／up_to_date 且 failed=false，Editor 不再 compiling／domainReloadInProgress。
- Console 歷史緩衝可能保留舊錯誤；判斷最新 `groundTruth.sampledUtc`、`ageMs`、`compilationFailed`、`consoleErrors`。
- eval_file 的 C# 是函式本體片段，不支援 using namespace 宣告。可用完整型別名稱；片段放 tmp 避免作為遊戲腳本編譯。
- CLI screenshot 以 Camera.Render 擷取，不含 Overlay UI；實際 Game View 可用 Play Mode ScreenCapture.CaptureScreenshot，或暫時將 Canvas 改 ScreenSpaceCamera 後擷取並還原。
- Editor 未聚焦時幀可能停止，可在預覽期間暫時設 Application.runInBackground=true，停止時還原。
- **勿在測試中修改 `Time.timeScale` 後忘記還原**：2026-10-12 慢動作截圖測試讓 `ProjectSettings/TimeManager.asset` 的 `m_TimeScale` 停在 0.15，遊戲變成 15% 速度。已改回 1，且 `ArcadeGame.Awake` 開局強制設 `Time.timeScale = 1`。慢動作擷取改用暫停逐格（`enabled=false` 後手動 `Advance`）較安全。

## 檢查與待調整

- 腳本整合後已重新編譯成功，Console 即時為零錯誤、零警告。
- 2026-10-10 已加入 Cubic 11 字型與新版雙人一鍵油門操作；再次重新編譯成功。已檢查 Play Mode 標題畫面與說明文字範圍，Console 零錯誤、零警告；完成後停止 Play Mode。
- 已透過 Editor API 確認場景儲存、四車、兩破壞物、兩道具、兩枚火箭池，上限 10。
- 已檢查標題與實際 Play Mode 畫面，修正新增排行榜與原標題面板重疊。
- 雙人 Play Mode 預覽約 28 秒時：AI 分數為 1247／475，發生互撞、耐久下降與護盾格擋；兩位未操作真人仍保持 100% 耐久，沒有執行期 Console 錯誤。此為預覽觀察，不代表所有流程與平衡已全面測試。
- 最新字型標題畫面：`Docs/Game3/integrated-title.png`；`Docs/Game3/integrated-gameplay.png` 是整合規則完成時、字型變更前的遊玩紀錄。`tmp/integrated-title-preview.cs` 僅擷取畫面並檢查參照，沒有新增測試組件。
- 真人鍵盤／手把手感、AI 強度與長時間平衡仍待玩家實際遊玩調整。
- 2026-10-13 完成架構票單 08：`Game3SceneBuilder.IntegrateExisting()` 保留 Inspector 玩法調整值（含倒數、結算停留時間與生成點）。全新 `Build()` 以 `DemolitionGame` 欄位預設值建立玩法，僅於建立新元件時設定結算 20 秒、倒數 3 秒。道具接線沿用序列化參照，避免改名為木箱後重複建立。已有字元的字型不再重複新增，圖片內容相同時不再重寫已載入的 PNG。`tmp/g3patch.cs` 透過公開入口驗證調整值、新建預設值、兩次修補維持 2 道具／2 火箭／160 物件，並逐位元還原原場景；回歸執行未產生錯誤或警告。腳本重新編譯及網域重新載入完成，Console 零錯誤、零警告。
- 票單 08 的 `code-review` 雙軸審查完成：Standards 0 項、Spec 0 項；以票單 01 結案提交 `ba54a92` 為固定點，實作提交 `6ea6804`。
- 2026-10-13 完成架構票單 09：畫面文字集中於 `CabinetText`，字型字元清單改由文字表＋場景文字產生，不再掃描原始碼檔名。`Game3Text.PatchScene()` 已重跑並儲存；比賽中底部提示改用與標題畫面相同的全形空白（P1 約 230px，在 312px 框內）。`tmp/g3text.cs`（Play Mode）驗證底部提示與標題畫面一致、無缺字、所有文字為 11 的倍數；320×320 標題與比賽截圖清晰。`g3cherry`／`g3wreck`／`g3rocket` 回歸通過。重新編譯及網域重新載入完成，Console 零錯誤、零警告。
- 票單 09 的 `code-review` 雙軸審查：Standards 無硬性違規（判斷項：道具代碼 1/2/3 的條件式重複、`startMessages` 仍為可在 Inspector 覆寫的場景值，屬票單 08 保留調整值的既定設計）；Spec 判定完成。
- 2026-10-13 完成架構票單 02：撞毀、永久淘汰、重生、櫻桃與碰碰車滑行集中於 `ArcadeVehicle`，不再以比賽主程式的平行陣列保存每車狀態；AI 暫存也移至車輛，接觸冷卻依車對車關係保存。一般撞毀會清除道具、櫻桃、滑行與硬直／冷卻，飛出後隱藏，3 秒重生保留扣分後分數與 AI 身分，提供 2 秒保護；計時淘汰保留分數與名次並永久退場。致命火箭或衝撞不再把硬直寫回已撞毀的車。
- 票單 02 的公開介面驗證 `g3lifecycle`／`g3wreck`／`g3spin`／`g3bump`／`g3cherry`／`g3rocket` 共 66 項斷言通過，全部不使用反射；`g3ai` 改為讀取車輛目標。AI、油門、特效、開局標語與文字回歸可正常執行，實際 320×320 飛出及重生保護畫面已檢視。已重新編譯並確認網域重新載入完成，Console 即時為零錯誤、零警告，完成後停止 Play Mode。額外的場景修補回歸 `g3patch` 因場景有未儲存狀態而未執行，該狀態保留。
- 票單 02 的 `code-review` 雙軸審查完成：Standards 0 項、Spec 0 項；固定點為票單 09 提交 `9dd9646`。
- 2026-10-13 完成架構票單 04：衝撞、櫻桃、火箭與 AI 選目標改由 `VehicleHarmPolicy` 決定傷害資格；使用者確認所有 AI 攻擊於 10 秒後皆可傷害玩家。`tmp/g3harm.cs` 的 68 項斷言在抽取前後全部通過，加上既有生命週期／道具與文字回歸共 136 項全部通過；AI、油門、特效與開局文字檢查正常。P1 領先 1500 分時，前 10 秒無 AI 鎖定且耐久 100%，10～25 秒 AI 追撞。重新編譯與網域重新載入完成，Console 即時零錯誤、零警告；停止 Play Mode。
- 票單 04 的 `code-review` 雙軸審查完成：Standards 0 項、Spec 0 項；固定點為票單 02 提交 `7812c0e`。
- 2026-10-13 完成架構票單 03：防護罩、火箭、櫻桃集中於 `ArenaItems`（`ArenaItem` 列舉沿用 0～3 序列化代碼）。`tmp/g3items.cs` 16 項斷言通過，既有回歸全部通過；Console 零錯誤、零警告。
- 2026-10-13 完成架構票單 05：AI 駕駛收進 `AiDriver`，碰撞、場地與生死流程不再改 AI 思考計時。`tmp/g3driver.cs` 14 項斷言通過；固定亂數 `g3ai` 抽取前後相同；既有回歸全部通過，Console 零錯誤、零警告。
- 2026-10-13 完成架構票單 06：比賽規則移到 `DemolitionMatch`，回傳 `MatchEvents` 事件，`DemolitionGame` 只當場景 adapter。火箭命中完全經由事件呈現（爆炸特效、命中音效、防護罩／撞毀提示）。新增 `TNDA.Prototype` 執行期組件與 EditMode 測試組件，4 個 EditMode 測試（火箭命中、致命火箭的撞毀與獎勵順序、防護罩格擋、整局淘汰到冠軍）以 Unity CLI 執行全部通過；`g3lifecycle`／`g3wreck`／`g3spin`／`g3bump`／`g3cherry`／`g3rocket`／`g3harm`／`g3items`／`g3driver` 164 項斷言通過。重新編譯與網域重新載入完成，Console 零錯誤、零警告。
- 2026-10-13 完成架構票單 07：其餘提示文字、音效與車輛顯示隱藏全部改走事件，`DemolitionMatch`／`ArcadeVehicle`／`ArenaItems` 不再碰觸任何畫面或音效物件（EditMode 測試以沒有 sprite 的車輛跑完整局證明）。待機示範的靜音與不顯示提示集中在 `DemolitionGame.Present()`。`ArcadeGame.sounds` 標為 `[NonSerialized]`，避免 Editor 重新載入網域後變成空陣列而在 edit mode 驗證時越界。EditMode 測試 26 個全部通過（新增撞毀扣分與獎勵、扣分不低於 0、計時淘汰同分比耐久再比索引、60／90 秒依時間淘汰且示範不淘汰、冠軍與 Survived、各事件）。Play Mode `tmp/g3cues.cs` 17 項確認各路徑提示文字、顏色、停留時間與音效與修改前相同，待機示範 21 秒無音效、無提示；`tmp/g3attract.cs`（edit mode）6 項，停用 adapter 統一判斷時 2 項失敗。既有回歸 `g3lifecycle`（改讀 `car.Visible`）／`g3wreck`／`g3spin`／`g3bump`／`g3cherry`／`g3rocket`／`g3harm`／`g3items`／`g3driver`／`g3text` 全部通過，固定亂數 AI 軌跡與修改前完全相同。重新編譯與網域重新載入完成，Console 零錯誤、零警告。
- 2026-10-14 完成架構票單 10：`ArcadeGame` 的掛點縮為 `ResetMatch`／`Tick`／`RefreshHud`，移除 `SupportsTwoPlayers`、`TickIdle`、`AfterTick` 與可覆寫的 `Awake`。`tmp/g3flow.cs` 逐幀記錄完整流程（待機示範、投幣、倒數、比賽、結算 20 秒返回、雙人、R、Esc），修改前後 11,701 幀完全相同；`tmp/g3tools.cs` 確認音效與字型設定工具仍可套用。EditMode 26 個測試與既有 Play Mode 回歸通過，Console 零錯誤、零警告。
