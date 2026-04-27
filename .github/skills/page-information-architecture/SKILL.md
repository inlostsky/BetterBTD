---
name: page-information-architecture
description: Page information architecture and card composition rules
---

# 技能 03: 页面信息架构（页面与卡片联合规范）

目标：让页面从“可用”提升到“可扩展、可扫描、可复用”，并与卡片规范形成统一体系。

关联文档：

- 卡片规范: `04-card-design-system.md`
- 间距栅格: `05-spacing-grid-rhythm.md`

## 1. 页面层级模型

推荐使用四层结构：

1. 页面头部层（Header Layer）
- 页面标题
- 页面一句话说明
- 页面级动作（可选，如“导入/导出”）

2. 主任务层（Primary Task Layer）
- 高频、关键路径卡片
- 用户进入页面后的第一操作区

3. 扩展任务层（Secondary Task Layer）
- 低频但常用卡片
- 需要对主任务提供补充能力

4. 高级设置层（Advanced Layer）
- 使用 `CardExpander` 折叠
- 避免将进阶项直接堆在首屏

## 2. 页面骨架规范

统一页面容器：

- 页面主容器建议 `StackPanel Margin="42,16,42,12"`
- 页面标题下方可加 1 行说明，帮助用户建立上下文
- 卡片按组排布，每组之间保留固定节奏

推荐骨架：

- Header（标题 + 描述）
- Group A（核心开关与主动作）
- Group B（配置项）
- Group C（高级折叠项）

## 3. 分组与编排策略

### 3.1 分组原则

- 按“用户任务闭环”分组，不按数据模型字段分组。
- 每组只解决一类决策问题。
- 每张卡片只承担一个明确动作目标。

### 3.2 顺序原则

- 高优先级能力放上方。
- 高频操作靠前。
- 低频高级项折叠。
- 危险操作放在组尾并加明显说明。

### 3.3 文案原则

- 标题: 动词 + 对象（如“启用遮罩窗口”）。
- 描述: 条件 + 影响（如“重启后生效”）。
- 避免泛化描述（如“建议设置”）。

## 4. 页面与卡片联合约束

为避免页面漂移，需遵守以下联合规则：

1. 页面级规则
- 同页面卡片宽度与外边距一致。
- 同页面同类操作控件宽度尽量统一。

2. 卡片级规则
- 卡片必须具备: 标题 + 操作区。
- 建议具备: 描述文字（尤其开关和风险项）。

3. 状态级规则
- 即时生效/重启生效必须写明。
- 禁用态必须说明原因。

4. 交互级规则
- 互斥状态用双状态按钮或单一状态源控制。
- 复杂参数用展开卡片承载，不污染首屏。

## 5. 页面复杂度分级（便于拆分）

1. L1（简单页面）
- 卡片数量 <= 5
- 可使用单一 `StackPanel`

2. L2（中等页面）
- 卡片数量 6~12
- 建议按组加小标题

3. L3（复杂页面）
- 卡片数量 > 12 或存在混合流程
- 必须拆分为子 View（如 `View/Pages/View/*`）

## 6. 页面评审清单（快速打勾）

- 是否存在明确的页面标题与一句话说明。
- 是否按任务分组，而不是按字段堆叠。
- 是否将低频进阶项放入折叠卡片。
- 是否每张卡片只有单一动作目标。
- 是否存在“生效时机”说明（即时/重启）。
- 是否存在统一的间距节奏。

## 7. 小规模代码用例（页面骨架 + 卡片编排）

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
	  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
	  xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
	  xmlns:controls="clr-namespace:BetterGenshinImpact.View.Controls">
	<StackPanel Margin="42,16,42,12">

		<!-- Header Layer -->
		<ui:TextBlock Margin="0,0,0,8" FontTypography="BodyStrong" Text="采集设置" />
		<ui:TextBlock Margin="0,0,0,12"
					  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
					  Text="配置截图、识别与调度的基础行为" />

		<!-- Primary Task Layer -->
		<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon Play24}">
			<ui:CardControl.Header>
				<Grid>
					<Grid.RowDefinitions>
						<RowDefinition Height="Auto" />
						<RowDefinition Height="Auto" />
					</Grid.RowDefinitions>
					<Grid.ColumnDefinitions>
						<ColumnDefinition Width="*" />
						<ColumnDefinition Width="Auto" />
					</Grid.ColumnDefinitions>
					<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="任务调度" />
					<ui:TextBlock Grid.Row="1" Grid.Column="0"
								  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
								  Text="启动后将自动执行已启用流程" />
					<controls:TwoStateButton Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
											 Margin="0,0,24,0"
											 EnableContent="启动"
											 DisableContent="停止" />
				</Grid>
			</ui:CardControl.Header>
		</ui:CardControl>

		<!-- Secondary Task Layer -->
		<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon Camera24}">
			<ui:CardControl.Header>
				<Grid>
					<Grid.RowDefinitions>
						<RowDefinition Height="Auto" />
						<RowDefinition Height="Auto" />
					</Grid.RowDefinitions>
					<Grid.ColumnDefinitions>
						<ColumnDefinition Width="*" />
						<ColumnDefinition Width="Auto" />
					</Grid.ColumnDefinitions>
					<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="截图模式" />
					<ui:TextBlock Grid.Row="1" Grid.Column="0"
								  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
								  Text="推荐 BitBlt，问题更少" />
					<ComboBox Grid.Row="0" Grid.RowSpan="2" Grid.Column="1" Width="200" Margin="0,0,36,0" />
				</Grid>
			</ui:CardControl.Header>
		</ui:CardControl>

		<!-- Advanced Layer -->
		<ui:CardExpander Margin="0,0,0,12" ContentPadding="0" Icon="{ui:SymbolIcon ChevronDown24}">
			<ui:CardExpander.Header>
				<ui:TextBlock FontTypography="Body" Text="高级参数" />
			</ui:CardExpander.Header>
			<Grid Margin="16">
				<ui:TextBlock Text="仅在调试场景下调整" />
			</Grid>
		</ui:CardExpander>
	</StackPanel>
</Page>
```
