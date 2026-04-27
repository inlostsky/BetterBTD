---
name: window-titlebar-backdrop
description: Window title bar and backdrop styling patterns
---

# 技能 12: 窗口标题栏与背景材质

目标：统一桌面窗口观感并保障跨窗口一致性。

## 关键能力

- FluentWindow 作为窗口基类。
- ExtendsContentIntoTitleBar 实现统一头部风格。
- WindowBackdropType 管理材质效果。

## 规则

- 所有工具窗遵循统一标题栏策略。
- 需要固定尺寸的窗体设置最小尺寸约束。
- 不同窗口风格差异应来源于功能，不来源于随意样式。

## 常见陷阱

- 标题栏内容过多导致拖拽区域不足。
- 不同窗口圆角和边框不一致。

## 小规模代码用例

```xml
<ui:FluentWindow x:Class="Demo.View.Windows.ToolWindow"
				 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
				 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
				 xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
				 Width="800"
				 Height="600"
				 ExtendsContentIntoTitleBar="True"
				 WindowBackdropType="Mica"
				 WindowCornerPreference="Round"
				 WindowStartupLocation="CenterOwner">
	<Grid>
		<ui:TitleBar Title="工具窗口" />
	</Grid>
</ui:FluentWindow>
```
