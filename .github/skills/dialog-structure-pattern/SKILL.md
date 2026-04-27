---
name: dialog-structure-pattern
description: Dialog structure and button layout patterns for WPF UI windows
---

# 技能 09: 对话框结构模式

目标：统一弹窗体验，降低用户操作成本。

## 三段式结构

1. 顶部: TitleBar
2. 中部: 内容区
3. 底部: 按钮区

## 按钮策略

- 主按钮: 关键确认
- 次按钮: 放弃或回退
- 危险操作: 语义明确、避免误触

## 内容策略

- 简洁问题描述 + 关键上下文。
- 长内容可滚动，按钮区固定。

## 行为策略

- 默认按钮与取消按钮行为明确。
- 尺寸和最小尺寸稳定，防止布局破坏。

## 小规模代码用例

```xml
<ui:FluentWindow Width="480" Height="240" MinWidth="400" MinHeight="200" ExtendsContentIntoTitleBar="True">
	<Grid>
		<Grid.RowDefinitions>
			<RowDefinition Height="Auto" />
			<RowDefinition Height="*" />
			<RowDefinition Height="Auto" />
		</Grid.RowDefinitions>

		<ui:TitleBar Grid.Row="0" Title="确认操作" ShowMaximize="False" ShowMinimize="False" />
		<ui:TextBlock Grid.Row="1" Margin="24,12,24,12" TextWrapping="Wrap" Text="是否应用当前配置？" />
		<StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center" Margin="24,0,24,24">
			<ui:Button Content="确定" Appearance="Primary" MinWidth="100" Margin="0,0,8,0" />
			<ui:Button Content="取消" Appearance="Secondary" MinWidth="100" />
		</StackPanel>
	</Grid>
</ui:FluentWindow>
```
