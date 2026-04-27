---
name: card-design-system
description: Card design language, controls, and interaction composition
---

# 技能 04: 卡片设计系统

目标：用卡片统一设置型 UI 的交互表达。

## 1. 卡片类型与适用场景

1. CardControl
- 固定可见配置项。
- 场景: 开关、单选、下拉、输入框等高频设置。

2. CardExpander
- 可折叠的高级配置。
- 场景: 进阶参数、低频参数、说明性较强设置。

3. Card + 自定义 Header（轻量变体）
- 当页面不是典型设置页，而是列表/状态展示时使用。
- 场景: 状态卡片、任务卡片、简要统计卡片。

## 2. 卡片设计语言（视觉语法）

统一表达公式：

- 图标 + 标题文字 + 描述性文字 + 右侧操作控件

建议尺寸与间距（与当前项目风格对齐）：

- 卡片外边距: `Margin="0,0,0,12"`
- 卡片内容内边距: `Grid Margin="16"`
- 标题与说明垂直间距: `0~4`
- 右侧操作与卡片右边距: `24`（常规）或 `36`（输入类控件）
- 图标建议: `Symbol24`
- 图标与文字区建议间距: `8~12`

文本层级建议：

- 标题: `FontTypography="Body"` 或 `BodyStrong`
- 描述: `Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"`
- 风险提示: `SystemFillColorCautionBrush`

## 3. 标准结构模板

标准两行头部（推荐默认）：

- 第一行: 功能标题
- 第二行: 补充描述（条件、风险、影响范围）

右侧操作区：

- 开关类: `ui:ToggleSwitch`
- 触发类: `ui:Button`
- 选择类: `ComboBox`
- 自定义复合动作: `TwoStateButton` 或 `StackPanel` 组合按钮

## 4. 可组合交互技能（重点）

### 4.1 卡片 + 双状态按钮（启动/停止）

适用：互斥状态切换（如启停、开闭、连接/断开）。

实现要点：

- 右侧放置双状态按钮。
- 状态由单一绑定源控制，避免两个按钮分别绑定不同状态源。

```xml
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
						  Text="启动后将自动执行已启用任务" />

			<controls:TwoStateButton Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
									 Margin="0,0,24,0"
									 EnableContent="启动"
									 DisableContent="停止"
									 IsChecked="{Binding IsDispatcherEnabled, Mode=OneWay}"
									 EnableCommand="{Binding StartCommand}"
									 DisableCommand="{Binding StopCommand}" />
		</Grid>
	</ui:CardControl.Header>
</ui:CardControl>
```

### 4.2 卡片 + 开关（ToggleSwitch）

适用：布尔配置，强调“开/关”语义。

实现要点：

- 标题说明该功能。
- 描述明确是否需要重启或重新加载。

```xml
<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon Flash24}">
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

			<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="启用快速模式" />
			<ui:TextBlock Grid.Row="1" Grid.Column="0"
						  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
						  Text="关闭后将回退到稳定模式" />

			<ui:ToggleSwitch Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
							 Margin="0,0,24,0"
							 IsChecked="{Binding IsFastModeEnabled, Mode=TwoWay}" />
		</Grid>
	</ui:CardControl.Header>
</ui:CardControl>
```

### 4.3 卡片 + 下拉箭头展开（CardExpander）

适用：高级参数默认折叠，降低主界面噪音。

实现要点：

- Header 内放关键状态。
- 展开内容区放详细参数。
- 分段建议使用 `Separator` 和 `Grid Margin="16"`。

```xml
<ui:CardExpander Margin="0,0,0,12" ContentPadding="0" Icon="{ui:SymbolIcon ChevronDown24}">
	<ui:CardExpander.Header>
		<Grid>
			<Grid.RowDefinitions>
				<RowDefinition Height="Auto" />
				<RowDefinition Height="Auto" />
			</Grid.RowDefinitions>
			<ui:TextBlock Grid.Row="0" FontTypography="Body" Text="高级参数" />
			<ui:TextBlock Grid.Row="1"
						  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
						  Text="点击展开设置重试次数、延迟与阈值" />
		</Grid>
	</ui:CardExpander.Header>

	<StackPanel>
		<Separator Margin="-18,0" BorderThickness="0,1,0,0" />
		<Grid Margin="16">
			<Grid.ColumnDefinitions>
				<ColumnDefinition Width="*" />
				<ColumnDefinition Width="Auto" />
			</Grid.ColumnDefinitions>
			<ui:TextBlock Text="最大重试次数" />
			<ui:TextBox Grid.Column="1" MinWidth="90" Margin="0,0,36,0" Text="{Binding RetryCount, Mode=TwoWay}" />
		</Grid>
	</StackPanel>
</ui:CardExpander>
```

### 4.4 卡片 + 开关 + 辅助按钮（混合操作）

适用：布尔配置需要附加动作（例如“重置位置”“查看详情”）。

```xml
<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon WindowConsole20}">
	<ui:CardControl.Header>
		<Grid>
			<Grid.RowDefinitions>
				<RowDefinition Height="Auto" />
				<RowDefinition Height="Auto" />
			</Grid.RowDefinitions>
			<Grid.ColumnDefinitions>
				<ColumnDefinition Width="*" />
				<ColumnDefinition Width="Auto" />
				<ColumnDefinition Width="Auto" />
			</Grid.ColumnDefinitions>

			<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="显示浮层" />
			<ui:TextBlock Grid.Row="1" Grid.Column="0"
						  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
						  Text="开启后可在游戏界面显示状态条" />

			<ui:ToggleSwitch Grid.Row="0" Grid.RowSpan="2" Grid.Column="1" Margin="0,0,12,0"
							 IsChecked="{Binding ShowOverlay, Mode=TwoWay}" />
			<ui:Button Grid.Row="0" Grid.RowSpan="2" Grid.Column="2" Margin="0,0,24,0"
					   Content="重置位置" Command="{Binding ResetOverlayLayoutCommand}" />
		</Grid>
	</ui:CardControl.Header>
</ui:CardControl>
```

### 4.5 卡片 + 下拉选择（ComboBox）

适用：枚举型设置（语言、模式、设备）。

```xml
<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon Globe24}">
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

			<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="界面语言" />
			<ui:TextBlock Grid.Row="1" Grid.Column="0"
						  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
						  Text="选择后点击更新按钮应用" />

			<ComboBox Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
					  Width="200"
					  Margin="0,0,36,0"
					  ItemsSource="{Binding LanguageDict}"
					  SelectedItem="{Binding SelectedLanguage, Mode=TwoWay}" />
		</Grid>
	</ui:CardControl.Header>
</ui:CardControl>
```

## 5. 卡片参数规范（可作为评审标准）

1. 结构规范
- 必须有标题。
- 建议有描述（尤其是开关和风险操作）。
- 右侧操作区必须垂直居中。

2. 文案规范
- 标题: 动词 + 对象（如“启用遮罩窗口”）。
- 描述: 结果/影响/限制条件。
- 重启生效、仅特定模式生效等信息必须写在描述中。

3. 对齐规范
- 标题和描述左对齐。
- 同页面相同类型控件宽度一致。
- 右侧控件建议统一右边距（24 或 36）。

4. 图标规范
- 每张卡片图标语义明确，不为了装饰而加。
- 同一页面避免图标风格混用（线性/填充混杂）。

## 6. 交互与状态规范

- 修改即生效和重启生效必须明确标注。
- 危险操作配二次确认或恢复入口。
- 异步操作提供进行中状态，避免重复触发。
- 禁用态必须解释原因（如权限不足、前置条件未满足）。

## 7. 常见反模式

- 卡片里堆太多控件，造成一张卡片承担多个任务。
- 标题过抽象，用户看不出会发生什么。
- 用颜色代替说明文案，导致信息不可达。
- 间距不一致，视觉节奏断裂。

## 8. 小规模代码用例（最小可复用模板）

```xml
<ui:CardExpander Margin="0,0,0,12" ContentPadding="0" Icon="{ui:SymbolIcon Settings24}">
	<ui:CardExpander.Header>
		<Grid>
			<Grid.RowDefinitions>
				<RowDefinition Height="Auto" />
				<RowDefinition Height="Auto" />
			</Grid.RowDefinitions>
			<Grid.ColumnDefinitions>
				<ColumnDefinition Width="*" />
				<ColumnDefinition Width="Auto" />
			</Grid.ColumnDefinitions>
			<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="启用高级模式" />
			<ui:TextBlock Grid.Row="1" Grid.Column="0"
						  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
						  Text="开启后可配置更多参数" />
			<ui:ToggleSwitch Grid.Row="0" Grid.RowSpan="2" Grid.Column="1" Margin="0,0,24,0" />
		</Grid>
	</ui:CardExpander.Header>

	<StackPanel>
		<Separator Margin="-18,0" BorderThickness="0,1,0,0" />
		<Grid Margin="16">
			<Grid.ColumnDefinitions>
				<ColumnDefinition Width="*" />
				<ColumnDefinition Width="Auto" />
			</Grid.ColumnDefinitions>
			<ui:TextBlock Text="采样间隔（毫秒）" />
			<ui:TextBox Grid.Column="1" MinWidth="90" Margin="0,0,36,0" Text="{Binding SampleInterval, Mode=TwoWay}" />
		</Grid>
	</StackPanel>
</ui:CardExpander>
```
