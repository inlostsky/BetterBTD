---
name: custom-composite-controls
description: Custom composite control design and reusable control packaging
---

# 技能 10: 复合控件封装

目标：把高复杂交互封装为可复用控件。

## 封装对象

- 级联选择器
- 热键输入框
- 键位绑定输入框
- 抽屉和浮层控件

## 设计方法

- 用模板控制外观。
- 用依赖属性暴露状态与数据。
- 用命名部件组织复杂交互。

## 边界规则

- 控件只解决通用交互，不耦合业务语义。
- 页面只消费控件公开 API。

## 收益

- 减少重复 XAML。
- 降低单页复杂度。
- 提升统一体验。

## 小规模代码用例

```xml
<UserControl x:Class="Demo.View.Controls.LevelSelector"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<Grid>
		<ToggleButton x:Name="MainToggle" Content="选择级别" />
		<Popup IsOpen="{Binding IsChecked, ElementName=MainToggle}" PlacementTarget="{Binding ElementName=MainToggle}">
			<Border Padding="8" BorderThickness="1" CornerRadius="8">
				<ListBox ItemsSource="{Binding Options}" />
			</Border>
		</Popup>
	</Grid>
</UserControl>
```

```csharp
public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
	nameof(Options),
	typeof(IEnumerable<string>),
	typeof(LevelSelector));

public IEnumerable<string>? Options
{
	get => (IEnumerable<string>?)GetValue(OptionsProperty);
	set => SetValue(OptionsProperty, value);
}
```
