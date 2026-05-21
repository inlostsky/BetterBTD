# “我的脚本”设计说明

## 目标

“我的脚本”不是目录扫描器，而是一个由应用统一管理的轻量脚本资源库。

它负责两件事：

1. 管理脚本的导入、导出、删除和检索。
2. 为后续 `AutoTask` 提供稳定的脚本绑定入口。

当前约束：

- 不依赖用户手工维护目录结构。
- 不兼容旧字段和旧格式。
- 标签只用于说明脚本用途，不直接绑定任何 `AutoTask` 功能。
- 页面保持简洁，只保留关键操作。

## 存储模型

脚本库统一存放在：

- `%LocalAppData%/BetterBTD/MyScripts`

目录结构：

- `library.json`
  - 保存受管脚本清单
  - 保存脚本绑定关系
- `Assets/`
  - 保存实际脚本文件

用户通过“导入脚本”把外部文件复制到资源库，通过“导出脚本”把受管脚本导回外部。

这样可以把：

- 脚本文件本体
- 显示名称
- 来源文件名
- 绑定关系

统一收口到应用控制的资源清单中，避免用户直接改目录、误删文件，或者主观猜测目录结构后破坏脚本库。

## 数据结构

### 脚本文件

脚本文件本身仍然只保存脚本内容：

- `schema`
- `formatVersion`
- `metadata`
- `monkeyObjects`
- `instructions`

当前 `metadata` 只保留：

- `scriptVersion`
- `description`
- `map`
- `difficulty`
- `mode`
- `hero`
- `tags`

说明：

- 旧实现中的 `category` 和 `name` 已废弃。
- 当前系统不做任何旧字段兼容。

### 资源库记录

受管资源库额外保存：

- `ScriptId`
- `DisplayName`
- `SourceFileName`
- `StoredFileName`
- `Description`
- `Map`
- `Difficulty`
- `Mode`
- `Hero`
- `Tags`
- `ImportedAt`
- `UpdatedAt`

其中 `DisplayName` 属于资源库层，不属于脚本文件内容本体。

### 槽位定义

当前框架已经支持以下槽位类型：

- `Custom`
  - `custom/default`
- `Collection`
  - 3 组模式
  - 每组 13 个脚本槽位
  - 目前只是框架占位
- `BlackBorder`
  - 所有地图
  - 所有难度
  - 所有对应模式
- `Race`
  - `race/current`

### 槽位绑定

绑定关系单独保存在资源库中：

- `SlotId`
- `ScriptId`
- `UpdatedAt`

这样可以保证：

- 一个脚本可被多个槽位复用
- 删除脚本时可同步清理绑定
- `AutoTask` 可以通过稳定的 `SlotId` 找到脚本

## AutoTask 接入方式

当前默认脚本解析流程已经切到受管资源库：

1. 如果 `PreferredFilePath` 存在且文件有效，优先直接使用。
2. 如果指定了 `SlotId`，按资源库绑定解析。
3. 如果是 `BlackBorder / Custom / Race` 且没有显式 `SlotId`，按默认规则推导槽位。
4. 如果都失败，则视为未配置脚本。

当前已接入：

- `Custom`
  - 支持直接路径
  - 同时保留 `custom/default` 槽位
- `BlackBorder`
  - 使用 `Map + Difficulty + Mode` 生成稳定槽位 ID
- `Race`
  - 支持 `race/current`
- `Collection`
  - 仅完成槽位框架，后续再接入实际策略

## 页面结构

当前“我的脚本”页面只保留两块内容：

### 左侧：脚本搜索结果

支持：

- 导入脚本
- 导出当前脚本
- 删除当前脚本
- 刷新资源库
- 按关键字搜索
- 按具体地图、难度、模式筛选

列表展示：

- 名称
- 地图
- 难度
- 模式
- 标签
- 状态

说明：

- 不再展示任务槽位列表。
- 列表使用虚拟化，避免脚本数量变多后造成明显卡顿。

### 右侧：选中脚本属性

属性只读展示：

- 脚本名称
- 脚本描述
- 脚本英雄
- 脚本地图
- 脚本难度
- 脚本模式
- 脚本标签
- 脚本状态

同时提供当前唯一绑定动作：

- 黑框任务绑定

绑定规则：

- 只针对 `BlackBorder`
- 自动根据脚本的 `Map + Difficulty + Mode` 推导目标槽位
- 用户不需要手工选择槽位

## 当前实现文件

核心模型：

- `BetterBTD/Models/MyScripts/ManagedScriptLibraryModels.cs`

核心服务：

- `BetterBTD/Services/MyScripts/ManagedScriptLibraryService.cs`
- `BetterBTD/Services/MyScripts/ManagedScriptSlotCatalogService.cs`

AutoTask 对齐：

- `BetterBTD/Models/AutoTasks/AutoTaskExecutionModels.cs`
- `BetterBTD/Services/Tasks/AutoTasks/AutoTaskRuntimeAdapters.cs`
- `BetterBTD/Core/AutoTasks/Strategies/BlackBorderAutoTaskStrategy.cs`
- `BetterBTD/Core/AutoTasks/Strategies/CustomAutoTaskStrategy.cs`
- `BetterBTD/Core/AutoTasks/Strategies/RaceAutoTaskStrategy.cs`

页面：

- `BetterBTD/ViewModels/MyScriptsPageViewModel.cs`
- `BetterBTD/Views/Pages/MyScriptsPageView.xaml`

## 后续建议

下一阶段优先做两件事：

1. 脚本编辑器与受管资源库打通
   - 支持直接编辑资源库内脚本
   - 编辑后回写资源库元数据缓存
2. `Collection` 任务落地
   - 将 3 x 13 占位槽位替换成真实活动槽位
   - 让 `CollectionAutoTaskStrategy` 返回稳定的 `SlotId`
