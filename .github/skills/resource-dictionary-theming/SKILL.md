---
name: resource-dictionary-theming
description: Resource dictionary theming and reusable style organization
---

# 技能 11: 资源字典与主题化

目标：通过资源系统实现统一主题与低成本改版。

## 核心策略

- 统一使用 DynamicResource 访问主题色。
- 样式写入 ResourceDictionary，页面按 Key 复用。
- 在局部字典中覆写默认控件样式。

## 范围划分

- 全局主题资源: 色彩、字体、圆角。
- 模块样式资源: 列表、树、抽屉、代码编辑器。

## 规则

- 禁止页面内散落硬编码颜色。
- 样式 Key 命名要可读、可检索。
- 新控件先接入主题再接入业务。

## 演进建议

- 为常用尺寸和间距建立 token。
- 定期合并重复样式，防止样式碎片化。

## 小规模代码用例

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
					xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
					xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
	<ResourceDictionary.MergedDictionaries>
		<ui:ControlsDictionary />
	</ResourceDictionary.MergedDictionaries>

	<Style x:Key="SettingsHeaderText" TargetType="ui:TextBlock">
		<Setter Property="FontTypography" Value="BodyStrong" />
		<Setter Property="Foreground" Value="{DynamicResource TextFillColorPrimaryBrush}" />
	</Style>
</ResourceDictionary>
```
