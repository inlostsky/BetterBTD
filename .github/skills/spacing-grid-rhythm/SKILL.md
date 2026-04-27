---
name: spacing-grid-rhythm
description: Spacing tokens, grid rhythm, and layout measurement rules
---

# 技能 05: 间距与栅格节奏（页面与卡片联合规范）

目标：建立可量化的视觉节奏，让页面、卡片、控件在扩展后仍保持统一。

关联文档：

- 页面架构: `03-page-information-architecture.md`
- 卡片规范: `04-card-design-system.md`

## 1. 间距 Token 建议值

建议建立以下 token（可放入样式资源）：

1. 页面级
- `PageMargin = 42,16,42,12`

2. 卡片级
- `CardMarginBottom = 12`
- `CardInnerPadding = 16`

3. 文本级
- `TitleToDescription = 0~4`
- `PageTitleToFirstGroup = 8~12`

4. 操作区
- `ActionRightInset = 24`（开关/按钮）
- `InputRightInset = 36`（输入框/下拉）

5. 组级
- `SectionGap = 12~16`

## 2. 栅格模型规范

### 2.1 卡片头部栅格（标准）

推荐 2 行 2 列：

- 列 1: `*`（标题 + 描述）
- 列 2: `Auto`（操作控件）
- 行 1: 标题
- 行 2: 描述

适用场景：开关、按钮、下拉、输入框。

### 2.2 混合操作栅格

推荐 2 行 3 列：

- 列 1: 文本区
- 列 2: 开关
- 列 3: 辅助按钮

适用场景：开关 + 重置、开关 + 更多。

### 2.3 展开内容栅格

- `CardExpander` 内容区每段独立 `Grid Margin="16"`
- 分段使用 `Separator Margin="-18,0"` 保持边缘对齐

## 3. 控件尺寸与对齐建议

统一建议（可按场景微调）：

- `ToggleSwitch`: 放在 `Grid.RowSpan="2"` 垂直居中
- `ComboBox`: `Width=180~220`
- `TextBox`: `MinWidth>=90`
- 右侧按钮高度建议与同卡输入控件视觉一致（通常 `32~34`）

对齐规则：

- 同一页面同类控件宽度一致。
- 右侧操作列必须使用统一右边距策略（24/36）。
- 描述文字与标题左边缘对齐，不做额外缩进。

## 4. 页面到卡片的节奏链路

页面节奏应遵循以下链路：

- 页面外边距 -> 分组标题 -> 卡片序列 -> 卡片内部栅格 -> 控件右侧留白

任何一个环节失衡，都会产生视觉噪音。

## 5. 常见布局问题与修复

1. 问题：只用 `StackPanel` 叠所有内容，导致复杂布局错位。
- 修复：卡片内部改用 `Grid`，明确列宽。

2. 问题：不同卡片右侧控件离边距离不同。
- 修复：统一 `ActionRightInset` 与 `InputRightInset`。

3. 问题：下拉框和输入框宽度随机，页面“锯齿感”明显。
- 修复：同类控件固定宽度区间。

4. 问题：描述文字行距和标题间距不一致。
- 修复：统一 `TitleToDescription` 范围。

## 6. 评审清单（间距与栅格）

- 页面是否统一使用 `PageMargin`。
- 卡片是否统一 `Margin="0,0,0,12"`。
- 卡片内部是否统一 `Grid Margin="16"`。
- 右侧控件是否按 24/36 规则留白。
- 同类控件宽度是否一致。
- 展开卡片分段是否使用统一分隔策略。

## 7. 小规模代码用例（标准栅格模板）

```xml
<StackPanel Margin="42,16,42,12">

	<!-- 组标题 -->
	<ui:TextBlock Margin="0,0,0,8" FontTypography="BodyStrong" Text="常规设置" />

	<!-- 标准卡片: 2行2列 -->
	<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon Settings24}">
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

				<ui:TextBlock Grid.Row="0" Grid.Column="0" FontTypography="Body" Text="日志级别" />
				<ui:TextBlock Grid.Row="1" Grid.Column="0"
							  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
							  Text="Debug 模式会输出更多细节" />

				<ComboBox Grid.Row="0" Grid.RowSpan="2" Grid.Column="1"
						  Width="180"
						  Margin="0,0,36,0" />
			</Grid>
		</ui:CardControl.Header>
	</ui:CardControl>

	<!-- 混合操作卡片: 2行3列 -->
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
							  Text="开启后在界面显示状态信息" />

				<ui:ToggleSwitch Grid.Row="0" Grid.RowSpan="2" Grid.Column="1" Margin="0,0,12,0" />
				<ui:Button Grid.Row="0" Grid.RowSpan="2" Grid.Column="2" Margin="0,0,24,0" Content="重置位置" />
			</Grid>
		</ui:CardControl.Header>
	</ui:CardControl>

</StackPanel>
```
