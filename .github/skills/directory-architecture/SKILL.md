---
name: directory-architecture
description: View directory classification and architecture guidance
---

# 技能 01: 目录分类架构

目标：建立可扩展、可维护的 View 层结构。

## 架构分层

1. Shell 层
- 承载主导航、标题栏、托盘、全局提示。

2. Pages 层
- 承载业务功能页面。
- 按功能域拆分页面文件，避免单文件臃肿。

3. Windows 层
- 承载独立窗口、弹窗、工具窗。
- 与 Pages 区分生命周期和交互范围。

4. Controls 层
- 承载可复用复合控件与控件样式。
- 控件逻辑独立于具体业务页面。

5. Style 层
- 承载 ResourceDictionary 与样式覆写。

## 实践规则

- 页面不直接复制粘贴复杂交互，优先抽到 Controls。
- 页面不内联大量样式，优先抽到 Style 字典。
- Window 负责流程边界，Page 负责功能面板。
- 目录名与功能名一致，降低定位成本。

## 反模式

- 把所有 UI 都写进单一 Page。
- 在 Page 中堆叠大量重复样式。
- 混用 Window 与 Page 职责。

## 小规模代码用例

```xml
<!-- Shell 层: 主窗口壳 -->
<ui:FluentWindow x:Class="Demo.View.MainWindow"
				 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
				 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
				 xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
	<ui:NavigationView>
		<ui:NavigationView.MenuItems>
			<ui:NavigationViewItem Content="主页" />
		</ui:NavigationView.MenuItems>
	</ui:NavigationView>
</ui:FluentWindow>
```

```xml
<!-- Pages 层: 功能页 -->
<Page x:Class="Demo.View.Pages.SettingsPage"
	  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
	  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<StackPanel Margin="42,16,42,12" />
</Page>
```
