• 这套功能建议不要直接做成“每个 autotask 自己写一串 if/else 点击”。更稳的做法是拆成 3 层：界面识别、通用导航状态机、任务策略。这样能复用现有截图/脚本能力，也能把暂停/继续做干净。

  现有代码里可以直接复用的基础已经够了：截图入口在 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/Services/Start/Capture/GameCaptureService.cs:1，关卡内状态识别在 /C:/Users/Administrator/source/repos/BetterBTD/
  BetterBTD/Services/Tasks/CaptureAnalysis/GameStageStateService.cs:1，脚本执行和暂停会话在 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/Core/ScriptExecution/ScriptTaskFlowExecutor.cs:1 和 /C:/Users/
  Administrator/source/repos/BetterBTD/BetterBTD/Core/ScriptExecution/ScriptExecutionSession.cs:1。缺的核心是“关卡外 UI 状态识别 + 自动任务运行时”。

  总体方案

  1. 新增一层 GameUiStateService，职责只有一件事：基于当前截图判断“游戏正处于哪个画面”。
  2. 新增一层 GameUiNavigator，输入是“当前界面 + 目标关卡”，输出是“下一步该点哪里/该等多久”。
  3. 新增一层 AutoTaskRunner，负责完整生命周期：进入地图、启动脚本、处理胜利/失败、决定下一轮做什么。

  这样你说的“状态机”会变成两级，而不是一个巨大的总状态机：

  - 界面状态机：当前屏幕是什么
  - 任务状态机：当前任务进展到哪一步

  这两层分开后，刷黑框 / 刷竞速 / 刷收集 的区别主要落在“任务状态机”和“脚本选择策略”里，不会污染底层导航。

  一、界面识别层
  建议新增这些模型：

  public enum GameUiStateId
  {
      Unknown,
      MainMenu,
      MapCategorySelect,
      MapGrid,
      DifficultySelect,
      ModeSelect,
      Loading,
      InLevel,
      Victory,
      Defeat,
      Reward,
      ConfirmDialog
  }

  public sealed class GameUiSnapshot
  {
      public required GameUiStateId State { get; init; }
      public double Confidence { get; init; }
      public GameStageStateSnapshot? StageState { get; init; } // 关卡内时复用现有结果
      public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>();
  }

  实现方式不要先上 OCR 大而全，先走“锚点颜色 + 小模板匹配 + 少量 OCR”的组合：

  - MainMenu：识别地图入口按钮
  - MapCategorySelect/MapGrid：识别地图页签/地图格子区域
  - DifficultySelect：识别简单/中等/困难卡片布局
  - ModeSelect：识别标准/放气/CHIMPS 等模式卡片
  - Loading：既不在关卡内，也没有菜单关键锚点，且刚刚执行过进入关卡动作
  - InLevel：直接复用 GameStageStateService.DetectIsInLevel
  - Victory/Defeat/Reward/ConfirmDialog：识别继续、主页、重试、领取等按钮

  建议做成一组 IGameUiRecognizer，由 GameUiStateService 顺序评估，按优先级返回第一个高置信结果。优先级应该是：

  ConfirmDialog > Victory/Defeat/Reward > InLevel > Loading > ModeSelect > DifficultySelect > MapGrid > MainMenu > Unknown

  原因很简单：阻塞弹窗和结算页如果不先识别，后面的导航点击会全部打偏。

  二、通用导航状态机
  不要把“点击后一定跳转成功”当成假设。每个动作都要是：

  - 有前置状态
  - 有期望后置状态
  - 可重试
  - 失败时可回退

  建议抽象成：

  public sealed class UiActionPlan
  {
      public required string Description { get; init; }
      public required Func<CancellationToken, Task> ExecuteAsync { get; init; }
      public required GameUiStateId[] ExpectedNextStates { get; init; }
      public int RetryLimit { get; init; } = 3;
      public int CooldownMs { get; init; } = 400;
  }

  GameUiNavigator 的职责是：给定 GameUiSnapshot + StageEntryTarget，返回下一步动作。

  StageEntryTarget 不要用 UI 文本字符串，必须用规范化 ID：

  public sealed class StageEntryTarget
  {
      public required GameMapType Map { get; init; }
      public required StageDifficulty Difficulty { get; init; }
      public required StageMode Mode { get; init; }
  }

  也就是说，“猴子草甸 / 简单 / 标准”在运行时应该是：

  - GameMapType.MonkeyMeadow
  - StageDifficulty.Easy
  - StageMode.Standard

  界面显示再做本地化映射。现在 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/ViewModels/AutoTasksPageViewModel.cs:1 里用的是本地化字符串作为选项值，这一层在真正实现前最好先改掉，否则后面识别、脚本筛选、配置持久
  化都会混进语言耦合。

  三、任务运行时层
  建议新增：

  - AutoTaskCoordinator
  - AutoTaskRunner
  - AutoTaskExecutionSession
  - IAutoTaskStrategy

  其中 AutoTaskCoordinator 负责全局互斥。这个点很重要：当前项目里脚本执行器本身就是单实例单任务运行，输入和截图服务也是共享资源，所以自动任务必须限制“同一时间只能有一个活动任务”。AutoTasks 页面虽然有多个按钮，但运行时必须
  由协调器拒绝并发启动。

  AutoTaskRunner 主循环建议是：

  1. 捕获一帧
  2. 识别 GameUiSnapshot
  3. 交给当前 IAutoTaskStrategy 决定下一步
  4. 如果下一步是导航动作，则执行点击/等待
  5. 如果下一步是“进入脚本执行”，调用 ScriptTaskFlowExecutor
  6. 如果下一步是“结算本轮并进入下一轮”，更新上下文继续循环

  核心相位建议定义为：

  public enum AutoTaskPhase
  {
      PreparingStage,
      NavigatingToStage,
      WaitingForLevelLoad,
      ExecutingScript,
      SettlingResult,
      AdvancingObjective,
      Completed,
      Failed
  }

  这里的关键不是相位名字，而是把“导航到关卡”和“关卡内脚本执行”明确分开。前者由 AutoTaskRunner 控制，后者完全复用已有 ScriptTaskFlowExecutor。

  四、暂停 / 继续
  这个需求你已经明确了，所以一开始就应该把运行时写成“可暂停会话”，不要后补。

  建议直接仿照 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/Core/ScriptExecution/ScriptExecutionSession.cs:1 的模式做一个 AutoTaskExecutionSession，同样提供：

  - RequestPause()
  - Resume()
  - ReachCheckpointAsync(...)
  - DelayAsync(...)

  暂停行为分两种：

  - 当前在菜单导航阶段：AutoTaskRunner 在每个安全检查点暂停
  - 当前在脚本执行阶段：AutoTaskRunner.RequestPause() 同时转发给 ScriptTaskFlowExecutor.RequestPause()

  继续也是同理转发。

  这样不会出现“外层任务显示暂停了，但关卡内脚本还在点”的错位状态。

  五、不同 autotask 的差异怎么承载
  建议把差异集中到 IAutoTaskStrategy：

  public interface IAutoTaskStrategy
  {
      string Key { get; }

      Task<AutoTaskDecision> DecideNextAsync(
          AutoTaskContext context,
          GameUiSnapshot snapshot,
          CancellationToken cancellationToken);
  }

  三类任务分别这样落：

  - BlackBorderAutoTaskStrategy
      - 目标是固定地图/难度/模式对应的黑框流程
      - 脚本筛选规则：Map + Difficulty + Mode + tag=black-border
      - 胜利后根据结果决定是否下一模式/下一难度/下一地图
  - RaceAutoTaskStrategy
      - 进入竞速活动页面的识别和点击逻辑单独扩展
      - 脚本筛选规则：tag=race，必要时再加地图/模式限制
      - 结算后通常是重开同一活动
  - CollectionAutoTaskStrategy
      - 进入收集活动页面、地图节点、奖励领取这部分是任务特有逻辑
      - 脚本筛选规则：tag=collection
      - 结算后要处理奖励页和下一轮选择

  这几类任务共享同一个导航器，但可以注册自己的“附加识别器”和“任务阶段逻辑”。这样活动页这类不稳定页面不会污染普通地图流程。

  六、脚本选择策略
  现有脚本元数据已经有 Map / Difficulty / Mode / Hero / Tags，定义在 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/Models/ScriptEditor/ScriptDocumentModels.cs:1，标签规范也已经有 /C:/Users/Administrator/source/
  repos/BetterBTD/BetterBTD/Models/ScriptEditor/ScriptTagCatalog.cs:1。所以建议新增 AutoTaskScriptResolver：

  - custom：直接用用户选定脚本
  - blackborder：按 Map + Difficulty + Mode + black-border
  - race：按 race
  - collection：按 collection

  如果匹配到多份脚本，第一版建议先定一个简单稳定规则：

  1. 元数据完全匹配优先
  2. 标签匹配数量多的优先
  3. 文件名排序兜底

  后面再扩展“优先级”字段，不要一开始就把脚本库规则做复杂。

  七、地图数据不要只靠枚举
  现有 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/Models/GameElements/MapDefinitions.cs:1 只有枚举，没有“地图属于哪个难度分组、在选择页如何定位”的结构化信息。建议补一份 GameMapCatalog，至少包括：

  - GameMapType
  - MapDifficultyTier
  - DisplayNameKey
  - TemplateAssetKey 或 ReferenceAnchor
  - 必要时的 GridIndex/PageIndex

  第一版可以混合使用：

  - 地图难度分组走 MapDifficultyTier
  - 个别地图点击位置先走固定格子坐标
  - 后续再替换成模板匹配定位地图卡片

  这样能先把“猴子草甸”这条主链路做通，不会被“一次性把所有地图模板都做完”拖死。

  八、建议的首个可交付切片
  第一期不要直接覆盖刷黑框/竞速/收集全流程。先做一个最小可运行版本：

  1. 只支持普通地图链路：MainMenu -> MapGrid -> DifficultySelect -> ModeSelect -> InLevel
  2. 只支持 MonkeyMeadow + Easy + Standard
  3. 只支持 custom 或 blackborder 中的“进入关卡后启动指定脚本”
  4. 只处理 Victory / Defeat / Home / Retry 这几个结算页
  5. 支持暂停/继续/停止
  6. 加日志，能看到“当前识别状态 / 下一动作 / 重试次数”

  这条链路打通后，再往里塞 race 和 collection 的页面识别器与策略。

  九、测试建议
  这部分必须跟着设计一起上，不然菜单识别会非常脆。

  - 识别测试：为每个菜单页存一张样例截图，测试 GameUiStateService 输出
  - 导航测试：Fake recognizer + Fake input，验证从 MainMenu 到 InLevel 的动作序列
  - 暂停测试：在导航等待和脚本执行两种阶段分别验证 Pause/Resume
  - 脚本联动测试：验证 AutoTaskRunner 在进入 InLevel 后只启动一次脚本执行器
  - 回退测试：点击失败或识别到 Unknown 时，不会乱点，而是等待或回主界面

  可以直接复用你们测试里已有的 fake runtime 思路，不需要另起一套风格。

  十、我建议的代码落点
  目录上建议新增：

  - BetterBTD/Models/AutoTasks/
  - BetterBTD/Services/Tasks/AutoTasks/Recognition/
  - BetterBTD/Services/Tasks/AutoTasks/Navigation/
  - BetterBTD/Services/Tasks/AutoTasks/Strategies/
  - BetterBTD/Services/Tasks/AutoTasks/Runtime/

  同时把当前 /C:/Users/Administrator/source/repos/BetterBTD/BetterBTD/Models/AutoTaskConfig.cs:1 拆成两部分更合适：

  - 持久化配置：真正要保存的 Map/Difficulty/Mode/Script/Interval
  - UI 运行态：IsRunning/RunningButtonText/CurrentStateText

  否则配置、状态、本地化文本会继续混在一个类里，后面会很难维护。

  如果你认这套方向，下一步我建议直接给你落一个更细的“类图 + 关键接口定义 + 第一阶段实现顺序”，然后我就可以开始把 AutoTasks 从空实现改成可运行骨架。