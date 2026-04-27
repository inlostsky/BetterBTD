---
name: global-feedback-and-tray
description: Global feedback, snackbar, and tray icon interaction patterns
---

# 技能 13: 全局反馈与托盘交互

目标：让应用在最小化和后台运行时仍可被可靠控制。

## 反馈通道

- 页面内提示
- 全局 Snackbar 提示
- 系统托盘菜单操作

## 设计原则

- 关键状态变化要有全局反馈。
- 托盘菜单保持高频操作最短路径。
- 前台窗口与托盘命令状态保持一致。

## 规则

- 退出、更新、显示主窗等命令统一入口。
- 托盘文案简洁，避免业务术语过重。

## 小规模代码用例

```xml
<ui:NavigationView>
	<ui:NavigationView.ContentOverlay>
		<ui:SnackbarPresenter x:Name="SnackbarPresenter" />
	</ui:NavigationView.ContentOverlay>
</ui:NavigationView>
```

```xml
<tray:NotifyIcon xmlns:tray="http://schemas.lepo.co/wpfui/2022/xaml/tray"
				 TooltipText="Demo App">
	<tray:NotifyIcon.Menu>
		<ContextMenu>
			<ui:MenuItem Header="显示主界面" />
			<ui:MenuItem Header="退出" />
		</ContextMenu>
	</tray:NotifyIcon.Menu>
</tray:NotifyIcon>
```
