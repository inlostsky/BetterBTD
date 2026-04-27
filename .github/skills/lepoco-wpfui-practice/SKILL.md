---
name: lepoco-wpfui-practice
description: Practical usage patterns for Lepoco WPF UI in this project
---

# 技能 14: Lepoco WPF UI 实战用法

目标：系统化掌握 Lepoco WPF UI 在项目中的使用方式。

## 接入方式

- 引入 ui 命名空间作为核心组件入口。
- 按需引入 tray 和 vio 扩展命名空间。

## 组件选型

1. 导航壳: NavigationView
2. 页面分组: CardControl / CardExpander
3. 文本与图标: TextBlock / SymbolIcon / FontIcon
4. 配置输入: ToggleSwitch / ComboBox / TextBox / Button
5. 层级数据: TreeListView + GridViewColumn
6. 窗口骨架: FluentWindow + TitleBar

## 主题使用

- 通过 ThemeResource 和 DynamicResource 获取颜色与字体资源。
- 保证深浅主题切换时控件可读性稳定。

## 推荐流程

1. 先搭壳层和分组。
2. 再接入绑定与命令。
3. 最后做样式抽取和资源归并。

## 工程化建议

- 新需求先判断是否落在已有组件能力内。
- 若出现重复 UI 片段，优先沉淀为控件或样式技能。

## 小规模代码用例

```xml
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
	  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
	  xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
	  FontFamily="{StaticResource TextThemeFontFamily}"
	  Foreground="{DynamicResource TextFillColorPrimaryBrush}">
	<StackPanel Margin="42,16,42,12">
		<ui:CardControl Margin="0,0,0,12" Icon="{ui:SymbolIcon Settings24}">
			<ui:CardControl.Header>
				<ui:TextBlock Text="主题模式" />
			</ui:CardControl.Header>
			<ui:Button Content="切换主题" Appearance="Primary" />
		</ui:CardControl>
	</StackPanel>
</Page>
```
