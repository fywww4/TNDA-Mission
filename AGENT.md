# TNDA Mission：專案背景與實作交接

更新日期：2026-10-04（Asia/Taipei）。本文件記錄使用者與隊長討論的三個遊戲方案、目前分工與實作進度，供 Claude 或其他代理接續參考。

## 目前工作狀態

使用者已指定最新分工：**Codex 已完成 GAME1，現在接續製作 GAME3；Claude 負責 GAME2。** 各自只操作負責的場景與玩法；共用程式修改前應重新讀取內容，保留另一位代理已寫入的變更。

- **GAME1 已完成可玩原型**：原有 `Assets/Scenes/GAME1.unity` 已儲存，保留相機並新增 `GAME1 Prototype` 階層，所有 UI、角色、Boss、物件池參照均已繫結。
- GAME1 已配置四層平台、三座梯子、兩發玩家子彈、四個 Boss 投擲物、320×320 Pixel Perfect Camera、uGUI／TMP 介面與四種程式產生的電子音效。活動遊戲實體的上限為八個。
- 使用者已協助匯入 TMP Essentials；Boss 原圖保存在 `Assets/Prototype/Game1/Art/Boss.png`，透過 `BossPalette.mat` 去除白背景並套用四色配色。
- GAME1 的 CLI 遊玩檢查已確認：鍵盤投幣開始、跳躍、三座梯子抵達各層、射擊擊殺、Boss 重生與難度增加、清空舊彈體、死亡凍結、十秒續關倒數、保留進度並原地滿血復活、逾時結算、返回標題。
- 三層攀爬落點為 `(-3, -1.44)`、`(3, 1.01)`、`(-3, 3.06)`；該次遊玩檢查同時活動實體最高六個，上限由固定物件池限制為八個。這是流程檢查結果，尚未代表長時間遊玩平衡已定案。
- GAME1 已執行 CLI 重新編譯並確認完成；遊玩檢查後 Console 的即時 `groundTruth` 為編譯未失敗、零錯誤、零警告。
- 標題與遊玩預覽在 `Docs/Game1/title.png`、`Docs/Game1/gameplay.png`；CLI 操作紀錄用的 C# 片段位於 `tmp/Game1-playthrough.cs`、`tmp/Game1-previews.cs`，不會作為 Unity 遊戲腳本編譯。
- GAME2 的後續實作與驗證由 Claude 負責，狀態請由 Claude 更新；Codex 沒有修改其場景與專用玩法檔案。共用程式仍保持分工開始時的內容。
- **GAME2 已完成可玩原型（Claude，2026-10-04）**：`Assets/Scenes/GAME2.unity` 保留相機並新增 `GAME2 Prototype` 階層，已加入 Build Settings。詳見下方「GAME2：四車淘汰競速」的實作狀態。
- **錄製音效與開局倒數（Claude 依使用者要求，套用 GAME1 與 GAME2）**：
  - 音效檔在 `Assets/Prototype/Audio/`（`Countdown`、`Hit`、`Shooting`、`Speed`，皆為 mono、DecompressOnLoad）。
  - 共用 `ArcadeGame.cs` 新增（全部為選用，預設不啟用，GAME3 行為不變）：`ArcadePhase.Countdown`（加在列舉最後）、`countdownSeconds`（0 = 不倒數）、`countdownLabel`、`countdownClip`、`hitClip`、`CountdownLeft`、`PlayClip()`、`PlayHit()`、每幀呼叫的 `AfterTick()`。倒數期間世界已重置但凍結，`Elapsed` 從 GO 才開始計時；續關 `ContinueRun` 不會倒數。
  - `Countdown.mp3` 在 0／1／2／3 秒各有一聲嗶，對應畫面上的 3→2→1→GO!（`countdownSeconds = 3`）。
  - GAME1（`BossClimbGame.cs`）：新增 `shootClip`；射擊播 Shooting，玩家受擊與 Boss 中彈播 Hit。
  - GAME2：Bolt 命中與碰撞播 Hit；`engineClip`（Speed）取中段做循環引擎聲，依玩家車速調整音高與音量，只給真人車。
  - 為符合 3～5 種音效：GAME1 用射擊／受擊／跳躍合成音／過關合成音／倒數；GAME2 用倒數／引擎／受擊／道具合成音／淘汰結算合成音。
  - 場景接線：`Assets/Prototype/Editor/ArcadeSoundSetup.cs`；已透過 `ArcadeSoundSetup.PatchScene()` 套用到兩個場景並存檔（未重建場景）。`Game1SceneBuilder`／`Game2SceneBuilder` 各加一行 `ArcadeSoundSetup.Apply(game)`，日後重建也會帶上。
  - 驗證：兩個場景都在 Play Mode 測過倒數→GO→開始、倒數中不能射擊、射擊／受擊播放正確音效、GAME2 引擎循環與規則模擬結果不變。Console 無錯誤。截圖在 `Assets/Screenshots/Game1-countdown.png`、`Game2-countdown.png`。
  - **GAME3 也已套用（Claude 依使用者要求）**：`DemolitionGame.cs` 新增 `engineClip`（Speed）。真人車有循環引擎聲，衝撞加速時播一次 Speed；車對車撞擊依力道播 Hit，撞牆播較小聲的 Hit；破壞物與結算仍用合成音；同樣有 3-2-1-GO 倒數。GAME3 場景已用 `ArcadeSoundSetup.PatchScene()` 修補並存檔（未重建）。`Game3SceneBuilder` 加了一行 `ArcadeSoundSetup.Apply(game)`。
  - 循環引擎聲的程式抽成共用的 `Assets/Prototype/Runtime/ArcadeEngineAudio.cs`，GAME2 與 GAME3 共用。
  - 觀察（未修改，留給 GAME3 負責者判斷）：玩家完全不操作時，車會以 45% 速度自行前進並反覆撞下方牆，每次扣 0.035 耐久，約 4 秒就全毀結束。關閉倒數時結果相同，所以不是倒數造成的。
- **GAME3 已完成可玩原型（Codex，2026-10-04）**：`Assets/Scenes/GAME3.unity` 已配置、儲存並加入 Build Settings。四車與四個可破壞物，支援單人／雙人，預設九十秒。詳細數值與檢查結果見下方 GAME3 段落。
- 開始分工前，三個場景都只有 `Main Camera`。這段初始狀態已被後續實作更新，接手時應重新查詢 Editor 與場景，而非沿用初始快照。

## 使用者操作偏好

- 優先透過 Unity CLI 操作 Unity Editor。
- 使用者不熟悉 CMD 與 PowerShell；需要執行指令時，由代理直接執行，不要求使用者自行輸入。
- 完成 Unity 腳本、場景、資產或設定的修改後，觸發重新編譯，等待編譯與網域重新載入結束，檢查 Unity Console，修正本次修改造成的錯誤後再編譯、檢查。
- 專案內的根目錄 `AGENTS.md` 保留這些操作規範。
- Unity Editor 可連線時，使用 CLI 與 Unity Editor API 建立或修改場景、Prefab、材料與資產；不要手動編輯 `.unity`、`.prefab`、`.asset` 的 YAML。

## Unity CLI 與環境

- 專案根目錄：`C:\Users\a0900\Desktop\TNDA Mission`。
- 專案目前版本：Unity `6000.6.4f1`，以 `ProjectSettings/ProjectVersion.txt` 為準；早期對話中的 `6000.0.75f1` 已不是目前版本。
- Unity CLI 執行檔：`C:\Users\a0900\AppData\Local\Unity\bin\unity.exe`。
- 本次確認的 CLI 版本：`1.0.0-beta.12`。
- `unity` 不在目前代理 Shell 的 PATH，但以上完整路徑可直接執行，毋須重裝。
- 專案已有 `com.unity.pipeline` `0.8.0-exp.1`。
- 本次開始時 CLI 回報唯一 Editor 為 `ready`，對應本專案。這是先前讀取結果，接手時應重新讀取狀態。
- 渲染管線已透過 Editor API 確認為 Built-in；已安裝 Built-in 的 `UnityEngine.U2D.PixelPerfectCamera`。
- 已有 Input System、uGUI／TextMeshPro 套件；TMP Essentials 已匯入，GAME1 使用 LiberationSans SDF 字型。

PowerShell 呼叫範例（供代理直接執行）：

```powershell
& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' status --format json

& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' list --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json

& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command editor_status --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json

& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command recompile --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json

& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command recompile_status --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json

& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command console_status --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json

& 'C:\Users\a0900\AppData\Local\Unity\bin\unity.exe' command console --level error --tail 50 --caller plugin --skill unity-cli --project-path 'C:\Users\a0900\Desktop\TNDA Mission' --format json
```

- 每次 `unity command` 呼叫帶上 `--caller plugin --skill <實際使用的技能>`；操作由 Unity CLI 技能產生時使用 `unity-cli`。
- `recompile` 是非同步操作；查詢 `recompile_status`，直到 `completed` 或 `up_to_date`，再確認 Editor 沒有編譯或網域重新載入中的狀態。
- `console_status` 的 `groundTruth.compilationFailed` 與 `groundTruth.consoleErrors` 是檢查的重要依據；只看最近輸出沒有訊息不足以證明沒有錯誤。
- 命令名稱與參數以 `unity list` 的即時查詢結果為準。可用命令已確認包含 `eval`、`eval_file`、`open_scene`、`get_scene_hierarchy`、`save_scene`、`editor_play`、`editor_stop`、`screenshot`。
- 目前的命令目錄快取在 `tmp/unity-catalog.json`；`tmp/unity-commands.json` 是標籤摘要。快取不能取代重新確認連線。
- `eval_file` 可執行代理寫在磁碟上的 C# 片段，以 Unity Editor API 修改場景；程式碼片段可放在 `tmp/`，避免被當作專案腳本編譯。
- 同一個 Editor 同時由兩位代理操作，執行前先確認目前場景／Play Mode。完成後保留另一位代理的場景狀態；可在 Edit Mode 附加載入自己負責的場景，操作完只關閉自己開啟的場景。
- CLI `screenshot` 在這個套件版本以 `Camera.Render()` 擷取，不包含 Screen Space Overlay 的 uGUI。實際 Game View 含 UI 的截圖可在 Play Mode 透過 `ScreenCapture.CaptureScreenshot()` 於下一幀取得；`tmp/Game1-previews.cs` 可在 Edit Mode 暫時切換 Canvas 模式並隔離擷取圖層，最後還原、儲存 GAME1，保留其他場景。
- 專案預設 `Application.runInBackground` 為 false，Editor 未聚焦時可能停止遊戲幀。CLI 遊玩檢查時可暫時設定 true；這不是已儲存的專案設定變更。
- CLI `simulate_key` 會立刻呼叫 `InputSystem.Update()`，目前版本可能使 `wasPressedThisFrame` 無法在下一個遊戲 Update 被讀取。已透過暫時的虛擬 Keyboard 與 `InputSystem.QueueStateEvent` 排入下一幀，確認 GAME1 的鍵盤投幣可開始遊戲；不要把 CLI 注入時序問題當成實際鍵盤輸入壞掉。
- 先前 TMP 匯入等待期間曾出現兩筆 Pipeline 主執行緒逾時錯誤；TMP 匯入後連線已恢復。CLI 的歷史擷取緩衝區仍可能包含這些舊紀錄，判讀時應同時檢查時間、`groundTruth.sampledUtc`／`ageMs` 與最新 Console 計數。

## 文件與素材來源

主要規格：使用者提供的 `C:\Users\a0900\Downloads\W4.pdf`，共五頁，已完成文字擷取與全部頁面視覺閱讀。

原先課程簡報連結：<https://claude.ai/artifact/M4ttJnWihF2dehomdng4SE>，此前已透過瀏覽器讀取。

附圖順序與 PDF 提案順序不同，場景對應如下：

| 場景 | PDF 提案 | 玩法 | 流程圖原檔 |
| --- | --- | --- | --- |
| `GAME1` | A | 平台跳躍、攀爬與單鍵射擊 Boss | `C:\Users\a0900\Downloads\mermaid-diagram-2026-10-04-161102.png` |
| `GAME2` | B | 俯視四車淘汰競速 | `C:\Users\a0900\Downloads\mermaid-diagram-2026-10-04-155551.png` |
| `GAME3` | C | 報廢場破壞王 | `C:\Users\a0900\Downloads\mermaid-diagram-2026-10-04-163638.png` |

Boss 圖：`C:\Users\a0900\AppData\Local\Temp\codex-clipboard-90560725-ab9a-4219-9156-282b392d207e.png`。512×512、RGB、白色背景。使用者要求所有 Boss 重複使用這一隻。來源位於暫存目錄，恢復實作時先確認檔案仍存在，再複製到專案保留。

車輛圖：

- 藍色：`C:\Users\a0900\Downloads\77c2b9d3-de78-4e76-8acf-b16ffa180fa8.png`（1024×1536）。
- 黃色：`C:\Users\a0900\Downloads\078344fd-ba5c-48be-b853-dc9a95305721.png`（1284×1225）。
- 白色：`C:\Users\a0900\Downloads\3af16932-ddc6-470a-91e2-f090bcdd1ae2.png`（1284×1225）。

三張車輛圖均為 RGBA，角落已透明；不要僅因預覽呈現黑色就認定背景完全不透明。

## 共通製作規格與原型範圍

- 在使用者準備的 `GAME1`、`GAME2`、`GAME3` 製作，不另建替代場景。
- 以最低製作成本呈現完整、可玩的原型；平台、梯子、賽道、路障、彈體等採 Unity 內建 2D Square／Circle 等 Sprite。
- Boss 與車輛使用提供的圖片；重用素材，毋須新製美術。
- 單畫面，不捲動；邏輯解析度 320×320。
- 最多四種顏色、三至五種電子聲音、同時活動物件最多八至十個。靜態背景裝飾與活動遊戲實體應分別計數。
- 方向搖桿加一顆操作按鈕；各案包含遊玩、勝利或失敗、重玩的完整流程。
- 設計目標：陌生玩家 30 秒內理解；Game Over 後有再玩一次的動機。
- 目前程式規劃共用黑、白、青、黃配色，以及四種程式產生的電子音效；視覺與音效尚未驗證。
- Built-in 管線的 Pixel Perfect Camera 使用 `UnityEngine.U2D.PixelPerfectCamera`，勿套用 URP 同名元件的 API。預定 PPU 32、參考解析度 320×320、Point Filter、關閉 Mipmap／壓縮／抗鋸齒。

## GAME1：平台攀爬與 Boss 射擊

文件規格：

- 單人。初始化生命、擊殺計數與第一隻 Boss。
- WASD：A／D 左右移動；W 跳躍或向上爬梯；S 向下爬梯。單一按鈕發射子彈。
- 玩家在平台與梯子間走位，閃避 Boss 的攻擊／投擲物，抓空檔射擊。
- 玩家受擊扣血，血量歸零後凍結遊戲並顯示十秒投幣倒數。
- 十秒內再投幣：原地滿血復活，給短暫無敵，保留當前 Boss 已扣血量與擊殺進度。
- 沒有投幣：結算最終擊殺數，返回待機標題。
- Boss 死亡：擊殺數加一、清空投擲物；下一隻 Boss 在頂層重生。
- 下一波提高攻擊頻率、移動速度或投擲物軌跡難度，持續循環。

已完成：`BossClimbGame.cs` 與 `Assets/Prototype/Editor/Game1SceneBuilder.cs`。四層平台、三座梯子、兩發子彈池、四個障礙物池、三格玩家生命、十秒續關與兩秒復活無敵。這些數值是原型預設，非文件硬性規定。每擊殺一隻，下一隻生命增加兩格，Boss 移動與投擲速度提高、攻擊間隔縮短；使用同一張 Boss 圖。

建置程式已執行。**不要再次直接呼叫 `Build()`**：它會偵測既有 `GAME1 Prototype` 並拒絕重建，避免覆蓋已完成場景。後續調整請透過 CLI 使用 Editor API 修改現有物件並儲存。

## GAME2：四車淘汰競速

文件規格：

- 一人或兩人；總共四台車。單人搭配三台 AI，雙人搭配兩台 AI。
- 單画面俯視賽道，方向搖桿控制車輛，單一按鈕使用道具。
- 分數等於沿賽道中線推進的有效前進距離；原地打轉與倒車不能刷分。
- 撞牆、碰撞、道具攻擊不直接扣分，以減速或旋轉硬直影響前進距離。
- 第 60 秒淘汰最低分的一台；第 90 秒再淘汰最低分的一台；第 120 秒在最後兩台中決定冠軍。
- 前次淘汰後，存活車輛保留累積分數。
- 第 120～140 秒停止賽道判定，展示冠軍與結算；展示結束後退回待機流程。
- 兩個固定道具箱生成點；同屏生效道具／彈體最多兩個。

目前草稿：`EliminationRaceGame.cs`，橢圓賽道座標、AI 跟隨、路線距離高水位計分、兩個道具箱、Nitro／Bolt、兩個彈體池與三段淘汰。Nitro／Bolt 是文件未指定道具內容時採用的原型選擇。

實作狀態（Claude）：

- 場景建置：`Assets/Prototype/Editor/Game2SceneBuilder.cs`，以 CLI `eval` 呼叫 `TNDA.Prototype.Editor.Game2SceneBuilder.Build(true)` 重建（`true` 會取代既有的 `GAME2 Prototype`）。只開啟／儲存 GAME2，不影響其他場景。
- 車輛圖：原圖保存在 `Assets/Prototype/Game2/Art/Source/`；建置時去背、裁切並縮成 28px 高、四色（黑／白／青／黃）的 `CarBlue.png`、`CarYellow.png`、`CarWhite.png`，32 PPU、Point、無壓縮。車 0 藍、車 1 黃、車 2／3 白；車名標籤以 P1 青、P2 黃、CPU 白區分。
- 配置：雙層橢圓牆、中線虛線、起跑線；四台車（含世界空間 TMP 名牌）、兩個固定道具箱（上方黃色 = Nitro、左側青色 = Bolt）、兩發 Bolt 物件池、場中淘汰倒數看板、uGUI 標題／結算面板（1P／2P 按鈕）。活動實體上限 8（4 車 + 2 箱 + 2 彈）。`presentationSeconds = 20`。
- 對草稿的修正：車輛間僅在實際迎面碰撞時減速硬直（原本持續重疊會每幀硬直，造成全場卡死）；擦牆只減速、正面撞牆才硬直；AI 會避開正前方車輛；被 Bolt 命中旋轉兩圈後恢復原方向；`trackHalfWidth = 0.62`。
- 已驗證（`tmp/game2/simtest.cs` 無頭模擬 + Play Mode 截圖）：60.0／90.0／120.0 秒淘汰、120 秒結算顯示冠軍、展示 20 秒後回標題；分數不會下降（碰撞／命中不扣分）；原地繞圈 10 秒僅得約 50 分；倒車後須追回最高點才再加分；雙人為 P1、P2 + 兩台 AI；同時 Bolt 不超過兩個。Console 無錯誤。截圖：`Assets/Screenshots/Game2-title.png`、`Game2-race.png`、`Game2-results.png`（後者為跳至 119.5 秒時的淘汰畫面）。
- 未完成／待調整：實際鍵盤與手把手感尚未由真人遊玩確認；車速、AI 強度、道具效果為原型預設值；Editor 未聚焦時 Play Mode 不會更新（專案的 Run In Background 為關），測試時以執行期 `Application.runInBackground = true` 處理，未改專案設定。

## GAME3：報廢場破壞王

文件規格：

- 一人或兩人；封閉的俯視場地，方向搖桿控制車輛，單一按鈕加速衝撞。
- 破壞路障、围欄、箱子等得到基礎分；撞敵車依速度與角度給倍率，側撞／追撞可加分；撞毀敵車得到大額加分。
- 場景中的敵車會威脅玩家；受撞扣除車輛耐久度。
- 單人耐久歸零立即 Game Over。
- 雙人一台全毀後該玩家退場，另一台繼續；兩台皆毀才提前結束。
- 倒數結束且至少一台玩家車存活，按累積分數結算生存結果與排行。

### 已完成的 GAME3 原型

- `DemolitionGame.cs` 與 `Assets/Prototype/Editor/Game3SceneBuilder.cs` 已完成；場景保留使用者的相機，新增 `GAME3 Prototype`，所有車輛、破壞物、TMP、uGUI 與 Input System UI 參照均已繫結。
- 單人是一位玩家加三台敵車；雙人是兩位玩家加兩台敵車。四車與四破壞物限制同時活動的遊戲實體為八個。耐久條、名稱與衝撞尾焰屬於車輛的視覺元件。
- 固定單畫面 320×320、PPU 32、Point Filter，使用 Unity 內建 Square／Circle 製作邊界、箱子、油桶、圍欄與路障。車圖是使用者原圖在 GAME2 已轉換的像素版本，複製到 `Assets/Prototype/Game3/Art/` 獨立保存；沒有修改 GAME2 素材與共用程式。
- GAME3 自有 `CarPalette.shader`／`CarPalette.mat` 保留黑色車窗與輪胎，依玩家／敵車換成青、黃、白，搭配黑背景組成四色配色。
- 箱子 100 分、油桶 75 分、圍欄 150 分、路障 200 分；行車速度達 2.3 才能破壞，低速撞上會反彈／硬直。破壞物八秒後且附近沒有車輛時重新生成。
- 衝撞獲得速度積分；側撞倍率 2、追撞倍率 1.5、其他方向倍率 1。撞毀車輛額外加 500 分；玩家撞毀敵車更新畫面的 WRECKS 計數。敵車六秒後重新生成，重生耐久為 75%。
- 單鍵加速衝撞持續 0.8 秒，玩家冷卻 2.5 秒。介面顯示個別玩家耐久、分數、剩餘時間、擊毀數與衝撞冷卻；車頂也有耐久條。
- 單人死亡立即結算；雙人一台全毀時保留另一人的遊戲，兩台全毀才提前結束。時間到且玩家仍存活時顯示 SURVIVED，依玩家分數排序，列出合計與最佳分數。結算凍結行車判定，六秒後返回標題，也可投幣或 R 立即重玩。
- 文件沒有指定局長，原型預設九十秒，可從 `roundSeconds` 調整。敵車、破壞物、衝撞冷卻與結算展示的秒數均為原型選擇，尚未完成長時間遊玩平衡調整。

已完成的檢查：

- Play Mode 啟動，鍵盤 `2` 投幣後畫面顯示兩位玩家；已檢查實際 Game View。
- 單人三敵車、雙人兩敵車、衝撞啟動、箱子破壞與 100 分獎勵、敵車擊毀加分與重新生成。
- 同速度下的一般／側撞／追撞分別為 91／182／136 分；提高速度後的一般撞擊為 150 分。
- 單人死亡結束、雙人一人退場後繼續、雙人全毀結束、倒數結束生存結算、結算停止行車、回到標題。倒數流程檢查使用暫時縮短的局長，檢查後還原九十秒與原有最佳分數紀錄。
- 已透過 CLI 重新編譯並檢查 Console；最後即時檢查為編譯未失敗、零錯誤、零警告。過程中曾有 CLI「Editor is already in edit mode」操作警告，屬於另一邊剛好結束 Play Mode 後的重複停止命令，歷史擷取緩衝區仍可能保留該紀錄。
- 實際手把操作與長時間遊玩平衡尚未檢查。

圖片：`Docs/Game3/title.png`、`Docs/Game3/gameplay.png`、`Docs/Game3/gameover.png`。檢查與編輯期擷取片段在 `tmp/Game3-inspection.cs`。建置程式已執行，會拒絕重複建立 `GAME3 Prototype`；後續請使用 CLI 修改現有場景物件。

## 已寫入程式的組織

| 路徑 | 用途與待完成項目 |
| --- | --- |
| `Assets/Prototype/Runtime/ArcadeGame.cs` | 共用待機／遊玩／續關／結算流程、Input System 鍵盤與手把、音效、UI 更新、場景切換。GAME1 與 GAME3 的 UI 與相機欄位已繫結。 |
| `Assets/Prototype/Runtime/ArcadeVehicle.cs` | 共用車輛位置、轉向、速度、耐久、道具與分數資料。Sprite 與名稱標籤仍需配置。 |
| `Assets/Prototype/Runtime/ArcadeProjectile.cs` | 可序列化的彈體池資料，並非 MonoBehaviour；需要配置對應 SpriteRenderer。 |
| `Assets/Prototype/Runtime/BossClimbGame.cs` | GAME1 已完成的原型玩法。 |
| `Assets/Prototype/Editor/Game1SceneBuilder.cs` | 已執行的 GAME1 場景建置程式；保留其他場景，使用 Unity 內建 Square／Circle 與 Boss 原圖配置物件。 |
| `Assets/Prototype/Runtime/EliminationRaceGame.cs` | GAME2 玩法草稿。 |
| `Assets/Prototype/Runtime/DemolitionGame.cs` | GAME3 已完成的原型玩法；`BreakableProp` 是此檔內的可序列化資料類別。 |
| `Assets/Prototype/Editor/Game3SceneBuilder.cs` | 已執行的 GAME3 場景建置程式，配置封閉場地、四車、四破壞物、車頂耐久條與介面。 |
| `Assets/Prototype/Game3/Art/CarPalette.shader` | GAME3 車輛的四色顯示；保留黑色車窗與輪胎，配合個別車輛顏色。 |
| `Assets/Prototype/Art/ArcadePalette.shader` | 圖片四色轉換與可選白背景去除。已在 GAME1 Boss 材料與畫面上驗證。 |

目前程式預定的操作：

- 1／2：開始單人／雙人；GAME1 固定單人。
- C、Space 或 Enter：投幣開始；GAME1 死亡倒數期間可續關。
- P1：WASD 加 Space；P2：方向鍵加右 Ctrl。
- GAME1 的 Space 為射擊；GAME2 為使用道具；GAME3 為加速衝撞。
- R：重新開始；Esc：待機標題；F1／F2／F3：切換三個遊戲場景。
- 車輛草稿目前以 W／S 前進與煞車／倒車、A／D 轉向；這是搖桿輸入的原型映射。
- 手把使用左類比／方向鍵與 South 按鈕，Start 作為投幣；GAME1 向上推左類比或方向鍵可跳躍／爬梯。鍵盤投幣路徑已確認，尚未使用實體手把檢查。

## 實作流程（只在各自負責的範圍內執行）

1. 重新讀取 CLI、Editor 與 Console 狀態，編譯現有草稿並處理錯誤。
2. 將提供的圖片保留到專案，設定 Sprite 匯入與點取樣；建立材料並確認背景、大小及四色效果。
3. 透過 Unity CLI 執行 Editor API 建置場景，保留使用者場景與相機，配置靜態幾何、角色、車輛、物件池、TMP 字型與 uGUI 介面。
4. 繫結所有序列化參照，配置必要的 EventSystem／Input System UI 模組，儲存自己負責的場景並加入 Build Settings。
5. 設定固定單畫面 320×320 顯示，檢查自己負責的遊戲可見範圍、字體與活動實體數量。
6. 明確重新編譯並檢查 Console，再進行各場景的輸入、流程及畫面驗證。

重要驗證案例：

- GAME1：跳躍／爬梯可達各層；射擊確實命中；死亡凍結；續關保留 Boss 血量及擊殺數；十秒逾時返回待機；新 Boss 清除舊投擲物。
- GAME2：單人／雙人均有四車；沿中線前進累積分數；倒車／繞圈不能刷分；碰撞不直接扣分；60／90／120 秒正確淘汰；120 秒後凍結競賽並展示 20 秒；彈體不超過兩個。
- GAME3：低速撞擊與高速側撞有不同收益；破壞物／撞毀敵車能得分；單人死亡立即結束；雙人一人死亡後另一人可繼續；時間到正確判定生存與得分。
- 共通：重新開始清除上一局狀態；場景切換可用；沒有漏繫結參照、編譯錯誤或執行期例外；檢查實際 Game View 截圖。

## 參考工具文件與暫存檔

- Unity CLI 技能：`C:\Users\a0900\.codex\plugins\cache\openai-curated-remote\unity\0.1.6-beta\skills\unity-cli\SKILL.md`。
- Pixel Perfect 技能：同套插件的 `skills\2d-pixel-perfect\SKILL.md`。
- UI 技能：同套插件的 `skills\ui\SKILL.md` 與 `skills\ui-ugui\SKILL.md`。uGUI 使用 TMP，避免跳出需使用者手動處理的 TMP Essentials 匯入視窗。
- PDF 閱讀用渲染圖：`tmp/pdfs/w4-page-1.png` 至 `w4-page-5.png`，屬於工作暫存。
- 以上快取與檔案路徑可能隨插件版本變動；找不到時依現有安裝內容重新定位。
