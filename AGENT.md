# TNDA Mission：GAME3 整合版交接

更新日期：2026-10-13（Asia/Taipei）。目前只製作 GAME3；使用者的新要求優先於先前三案規格。

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
- 60 秒淘汰目前最低分的車；90 秒再淘汰最低分；120 秒從最後兩車淘汰低分者，決定冠軍。
- 計分使用破壞、衝撞與火箭命中分數，沒有沿賽道行駛距離分數。
- 同分時耐久較低者淘汰；耐久也相同時，車輛索引較高者淘汰。
- 計時淘汰是永久退場；存活車保留原分數。排行榜保留被淘汰者的順位與分數。
- 被計時淘汰的車不會直接消失：從當時位置朝遠離場地中心的方向「超級打滑」飛出，每秒轉 4 圈、初速 4、每秒加速 14，飛出 320×320 畫面後才隱藏（約 0.7 秒）；飛出過程不參與碰撞與火箭判定。數值為 `spinOutTurnsPerSecond`／`spinOutStartSpeed`／`spinOutAcceleration`（2026-10-12 使用者要求）。2026-10-13 起一般撞毀（耐久歸零，玩家與 AI 皆同）也用同樣的打滑飛出，飛出後隱藏，仍於撞毀 3 秒後在角落重生（重生時中止殘留的飛出）。示範模式重生的車維持 AI 駕駛。
- 真人都被淘汰時，進入觀看狀態，AI 比賽繼續，R 可重開。
- 最後冠軍與四車名次展示 20 秒，再返回標題；也可立即投幣或 R 重玩。

### 耐久、破壞與得分

- 車輛耐久 100%；不輸入油門時減速至停止，沒有自動前進。
- 撞牆只減速／短暫硬直，不扣耐久。
- 箱子 100 分、油桶 75 分；車速至少 2.1 才可直接撞碎，火箭也能破壞。破壞物 6 秒後且附近沒有車時重生。
- 車對車衝撞依速度計分；側撞 2 倍、追撞 1.5 倍。
- 碰碰車手感（2026-10-13 使用者要求，避免被撞後黏住卡死）：每次新接觸兩車互推一個會滑行並逐漸減速的位移（`contactBump` 1.4）；被衝撞的車再加上依撞擊速度增加的滑行（`ramBump` 2.4），車頭隨機偏 15～30°，硬直縮短為 0.12 秒；撞人的車會小幅反彈。滑行以 `bumpFriction` 4.5 衰減，撞牆時反向減弱。實測：側面被撞的靜止 P1 約 1 秒內滑開 1.17 單位、兩車分開。
- 撞毀一台車獎勵攻擊者 300 分；被撞毀者扣 250 分，最低扣至 0 分。
- 一般撞毀不是永久淘汰：玩家與 AI 均在 3 秒後滿耐久重生，保留扣分後的分數，清除道具，給 2 秒保護。
- 開局也有 2 秒保護。
- 上述重生、扣分、傷害與道具數值屬於原型預設，可在 DemolitionGame Inspector 調整。

### AI

- **難度調整（2026-10-12 使用者要求）**：開局 GO 後前 10 秒（`aiGraceSeconds`）為緩衝期：AI 只把其他 AI 當衝撞目標、會轉向避開玩家，且 AI 的衝撞、火箭、櫻桃都不會傷害玩家。
- 10 秒後玩家與 AI 一視同仁：AI 依「分數越高越優先、距離越近越優先」選目標（`leaderBias` 1.5，含少量隨機避免全部追同一台）；分數領先該 AI `leaderHuntMargin` 300 分以上的車（含玩家）會被主動追撞，不受原本輪替策略限制。追玩家時不再避讓；火箭、櫻桃可擊中玩家。
- 其餘時間仍輪替追逐破壞物、道具與對手；AI 對 AI 的衝撞、破壞得分、死亡扣分與重生使用同一套規則。持櫻桃的 AI 追殺 `PickRival` 選出的目標。
- 驗證：P1 領先 1500 分且不操作時，0～10 秒沒有 AI 以 P1 為目標、耐久 100%；10～25 秒 AI 轉而追撞 P1 並撞毀一次。電腦1 領先時 AI 集中攻擊電腦1，0 分的 P1 未被鎖定、耐久 100%。
- 玩家仍可主動撞擊 AI、射擊 AI 或另一位玩家。

### 道具與操作

- 固定兩個道具生成點，外觀為木箱（2026-10-11 使用者要求，增加隨機性）：撿到時隨機給道具，撿取後 8 秒且附近沒車時重生。一般情況防護罩／火箭各 50%；撿取者若為目前最後一名（同分也算），50% 得到櫻桃，其餘防護罩／火箭各半（`trailingCherryChance`）。木箱圖由 `Game3ItemArt` 依使用者木箱圖片的設計（木框＋直條木板）重畫成 16×16 像素，原圖保留在 `Art/Source/ItemCrateSource.png`；原照片縮到 16px 無法辨識。刻意不畫斜撐，避免與可破壞的 X 箱子混淆。
- 每車最多持有一個道具；道具鍵只用來使用道具，沒有道具時按下不會有動作。已依使用者要求（2026-10-11）移除加速衝撞，AI 也不再衝撞加速。
- 防護罩持續 6 秒，抵擋耐久傷害，車身顯示青色圓框。
- 火箭沿車頭方向直飛，命中車扣 38% 耐久並給攻擊者 120 分；撞毀仍加 300 分。可撞碎破壞物並取得其分數。
- **櫻桃（小精靈吃豆玩法）**：只有最後一名能從木箱開到。按道具鍵吃下後 8 秒（`cherryDuration`）：最高速 ×1.35（`cherrySpeedMultiplier`），車旁顯示櫻桃圖示，最後 2 秒閃爍。櫻桃車碰到任何車即撞毀對方（攻擊者得撞毀獎勵 300 分）；但若對方實際衝撞櫻桃車（接近速度 ≥1 且大於櫻桃車），或被火箭命中，櫻桃車立即撞毀。防護罩與重生保護仍可擋下。
- 櫻桃 AI：持有櫻桃的 AI 依 `PickRival` 追殺對手；其他 AI 在 2.2 單位內會逃離櫻桃車。前 10 秒不傷真人，之後可以撞毀玩家，與一般衝撞／火箭共用 `VehicleHarmPolicy`（2026-10-13 票單 04 使用者確認）。
- 櫻桃圖：使用者提供的圖片，去白背景後縮為 13×14 四色像素圖 `Art/Cherry.png`，來源 `Art/Source/CherrySource.png`。場景修補：`Game3ItemArt.PatchScene()`；`Game3SceneBuilder.IntegrateExisting` 也會呼叫 `Game3ItemArt.Apply`。
- 櫻桃期間整台車變黃色（2026-10-13 使用者要求）：`CarPalette.shader` 新增 `_Solid`／`_SolidColor`，以 MaterialPropertyBlock 個別設定，車身與白色部分都變黃、只保留黑色外框車窗；最後 2 秒與櫻桃圖示一起閃爍。不改 `body.color`，所以排行榜與車名顏色不變。玩家與 AI 吃櫻桃都適用。
- 火箭命中任何車輛（玩家或 AI，含被防護罩擋下）都在命中點顯示爆炸特效（2026-10-13 使用者要求）：使用者提供的爆炸圖去白背景後縮為 34×32 四色像素圖 `Art/Explosion.png`（橘紅尖刺與金色雲→黃、深橘陰影→黑、淺黃核心→白；來源 `Art/Source/ExplosionSource.png`）。0.45 秒內由 0.5 倍彈到 1.15 倍再回 1 倍，最後閃爍消失（不使用透明度以維持四色）。每枚火箭一個顯示物件（`Effects/Rocket explosion 0～1`），由 `Game3ItemArt.Apply` 建立。
- 已在 Play Mode 驗證：最後一名約 50% 開到櫻桃、領先者 0%；櫻桃速度提升；櫻桃車碰 AI 使其撞毀並 +300；P2 衝撞櫻桃 P1 使 P1 撞毀；火箭一發擊毀櫻桃車（一般車只扣 38%）；櫻桃 AI 在前 10 秒不傷真人，10 秒後可以；30 秒 AI 對戰無例外。
- 火箭最多同時兩發；池已滿時保留道具，稍後再按。
- 1／2、C、Space、Enter 或手把 Start：開局；左 Ctrl 可開單人，數字鍵盤 Enter 可開雙人。
- P1：左／右方向鍵轉向，上方向鍵為油門，左 Ctrl 使用道具。
- P2：A／D 轉向，W 為油門，數字鍵盤 Enter 使用道具；主鍵盤 Enter 不控制 P2。
- 按住油門會逐漸加速（`accelerationPerSecond` 1.4，約 2 秒到最高速 2.8）；放開後以 `coastDownPerSecond` 2.2 減速到停止；無倒車。兩個數值可在 Inspector 調整。此操作已由使用者確認（2026-10-11）。
- 手把：左類比／方向鍵左右轉向，往上為油門，South 按鈕使用道具。
- R 重開；Esc 回標題。原 F1／F2／F3 切換已移除。

### 標題畫面（街機待機示範，2026-10-12 使用者要求）

- 待機時四台車全由 AI 駕駛，背景自動播放一局示範（`DemolitionGame.TickIdle`）：第 20 秒淘汰最後一名以展示打滑飛出，第 30 秒重新開始（`demoEliminationSeconds`／`demoLoopSeconds`）。示範不計入正式比賽、不觸發結算。
- 待機期間靜音、不顯示提示文字（`ArcadeGame.Sound/PlayClip/Announce` 在 Attract 階段直接略過），隱藏排行榜、狀態、車名與原本的標題說明面板／1P2P 按鈕。
- 畫面只顯示：標題「廢車場大亂鬥」、「每位玩家 1 枚硬幣」、閃爍的「按 1 單人　按 2 雙人」，以及底部 P1／P2 操作提示（規則說明已依使用者要求簡化，只留操作）。文字定義在執行期文字表 `CabinetText`（`P1Controls`、`P2Controls`、`CoinRule`、`CoinPrompt`），介面由 `Game3Text.CreateAttractScreen` 建立（場景物件 `Attract screen`，每次修補會重建）。
- 開始方式不變：1／C／Space／Enter／左 Ctrl 單人，2／數字鍵 Enter 雙人；開始後恢復正常 HUD 與 3-2-1 倒數。

## 畫面、音效與素材

- 保持單畫面 320×320、PPU 32、Built-in Pixel Perfect Camera；Point Filter，無 Mipmap、無壓縮。
- 黑、白、青、黃四種顏色。靜態場景、道具、保護圓框、火箭使用 Unity 內建 Square／Circle；車圖使用使用者提供的圖片。
- 活動實體上限 10：四車、兩個破壞物、兩個道具、兩枚火箭。車頂名稱、耐久條、保護圓框是車的視覺元件（加速衝撞的尾焰已於 2026-10-13 隨遺留欄位清除移除）。
- 排行榜列出四車分數與 HP／持有道具／重生倒數／淘汰順位，右側顯示玩家按鈕狀態與時間，頂端顯示下次淘汰倒數。
- 車的像素圖與四色材料位於 `Assets/Prototype/Game3/Art/`。
- GAME2 的車輛原始圖片已先複製至 `Assets/Prototype/Game3/Art/Source/`，才移除舊案資料夾。
- 共用音效 `Assets/Prototype/Audio/` 保留。GAME3 使用倒數、引擎、碰撞，以及道具與結算合成音，共五類聲音（衝撞加速移除後，Speed 只作為循環引擎聲）。
- 介面已改為繁體中文（2026-10-11）。**所有畫面文字只定義一次（2026-10-13，ticket 09）**：`Assets/Prototype/Runtime/CabinetText.cs` 收錄標題、投幣、P1／P2 操作、按鈕、提示、廣播與排行榜／結算文字；需代入數值的是 `string.Format` 模板（`{0}`）。Editor 場景修補（`Game3Text.PatchScene()`，含車名標籤轉換）與執行期 HUD 都讀它；比賽中底部提示 P1 為 `P1Footer`（`P1Controls`＋「R重玩」），P2 為 `P2Controls`，與標題畫面相同。新增畫面文字請加在 `CabinetText`，不要在程式內另寫中文字串；電腦車名為「電腦1～3」，持有道具時車名後顯示 [盾]／[箭]。
- **輸出解析度規定為實際 320×320 像素（使用者 2026-10-12 確認）**。Editor Game 視窗使用內建的 320×320 固定解析度；曾暫時設為 960×960 屬錯誤，已移除該自訂尺寸。不可用放大視窗來改善字體。
- **字型（2026-10-12）**：在 320×320 下唯一能清楚的做法是像素字型 1:1 繪製。使用俐方體 11 號 Cubic 11（`Assets/Prototype/Fonts/Cubic_11.ttf`，OFL），TMP 字型 `Cubic_11 Pixel 11px.asset`：RASTER_HINTED 點陣、取樣 11px、Point 濾鏡、Bitmap shader、512 圖集；預先產生所有畫面字元（`ArcadeFontSetup.UsedCharacters`：ASCII、常用全形標點、`CabinetText.All`，再加上場景中的標題、說明、`startMessages` 與現有 TMP 文字），建置不清除。行高與基線取整數（行高 13、ascent 9），Canvas 開啟 pixelPerfect。之前糊掉的原因是 Cubic 被以 7～8.5px 的 SDF 繪製；Noto Sans TC（`Fonts/NotoSansTC/`）不再使用。
- 文字大小只用 11 或其整數倍（標題 22、倒數 44）。版面由 `Game3Text.PixelLayout` 設定（整數像素座標）：頂部標題＋淘汰倒數、四車排行榜（左）、玩家狀態（右），提示列在場地上框內，底部操作提示一行（雙人時每 3 秒切換 P1／P2）。修改文字長度時注意 11px 中文每字 11px 寬，說明面板一行最多約 24 字。
- `SampleScene` 與使用者另外放入 `Assets/Prototype/Art/` 的圖片保留；不在遊戲 Build Settings 中。

## 程式位置

| 檔案 | 用途 |
| --- | --- |
| `Assets/Prototype/Runtime/DemolitionGame.cs` | 整合版玩法；含 BreakableProp／ArenaPickup、AI、積分、淘汰、道具與重生。 |
| `Assets/Prototype/Runtime/ArcadeVehicle.cs` | 車輛資料、駕駛與生死週期 module。TakeDamage／Eliminate／Respawn 統一清理狀態；持有櫻桃、滑行、打滑飛出、名次，以及供 AI 察覺的碰撞／重生計數。VehicleLifecycleRules 傳入 Inspector 玩法值。Drive 只有油門加速與放開滑行，無倒車、無自動前進。 |
| `Assets/Prototype/Runtime/AiDriver.cs` | AI 駕駛 module（`car.Ai`）：選目標、輪替策略、閃避玩家、逃離櫻桃、道具時機；比對車輛的 `Collisions`／`Spawns` 與存活車數自行重新規劃。比賽主程式經 `IAiArena` 提供局面。 |
| `Assets/Prototype/Runtime/ArenaItems.cs` | 道具 module：`ArenaItem` 列舉、木箱抽選、使用效果、名稱／標籤／狀態文字；比賽主程式經 `IItemArena` 提供火箭與提示。 |
| `Assets/Prototype/Runtime/VehicleHarmPolicy.cs` | 衝撞、櫻桃、火箭與 AI 選目標共用的傷害資格規則；所有 AI 攻擊在開局前 10 秒不傷玩家，之後可傷玩家。 |
| `Assets/Prototype/Runtime/ArcadeProjectile.cs` | 固定彈體池資料，並非 MonoBehaviour。 |
| `Assets/Prototype/Runtime/ArcadeGame.cs` | 標題、倒數、輸入、音效、結算與重玩；已移除舊場景切換。 |
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
