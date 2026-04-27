---
name: main-shell-layout
description: Main shell layout patterns for WPF UI pages and navigation
---

# 技能 02: 主页面壳层布局

目标：构建稳定的应用骨架，承接所有业务页面。

## 结构模式

- 顶部: TitleBar
- 左侧: NavigationView
- 中央: Page 内容区域
- 覆盖层: Snackbar/全局提示

## 设计原则

- 导航是信息架构，不承载业务逻辑。
- 内容页通过路由切换，不直接在壳层写业务控件。
- 全局提示放在 ContentOverlay，保证跨页面可见。

## 实施要点

- 导航项按用户任务流排序，不按技术模块排序。
- 一级菜单放常用入口，二级菜单放同域高级能力。
- Footer 保留设置等低频但必须存在的入口。

## 验收标准

- 新增页面不需要修改壳层布局结构。
- 切换页面后标题和状态反馈行为一致。

## 小规模代码用例

```xml
<Grid>
	<Grid.RowDefinitions>
		<RowDefinition Height="Auto" />
		<RowDefinition Height="*" />
	</Grid.RowDefinitions>

	<ui:TitleBar Grid.Row="0" Title="演示应用" />

	<ui:NavigationView Grid.Row="1"
					   OpenPaneLength="160"
					   IsBackButtonVisible="Collapsed">
		<ui:NavigationView.ContentOverlay>
			<ui:SnackbarPresenter x:Name="SnackbarPresenter" />
		</ui:NavigationView.ContentOverlay>
	</ui:NavigationView>
</Grid>
```
