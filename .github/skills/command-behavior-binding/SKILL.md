---
name: command-behavior-binding
description: Command binding and behavior-driven interaction patterns
---

# 技能 07: 命令绑定与行为触发

目标：用 MVVM 命令驱动 UI 交互，减少事件代码分散。

## 模式

- 控件状态通过 Binding 管理。
- 事件通过 Behavior 转发到 Command。
- 业务处理在 ViewModel，不在 XAML 代码后置。

## 典型场景

- SelectionChanged 触发联动更新。
- Toggle 开关触发刷新逻辑。
- Loaded 触发初始化流程。

## 规则

- 命令命名明确动作语义。
- 触发器只做转发，不写业务。
- UI 状态由单一数据源驱动。

## 好处

- 页面可测试性更好。
- 交互行为可复用。
- 复杂功能更易拆分。

## 小规模代码用例

```xml
<ComboBox ItemsSource="{Binding LanguageDict}" SelectedItem="{Binding SelectedLanguage, Mode=TwoWay}">
	<b:Interaction.Triggers>
		<b:EventTrigger EventName="SelectionChanged">
			<b:InvokeCommandAction Command="{Binding LanguageChangedCommand}"
								   CommandParameter="{Binding SelectedItem, RelativeSource={RelativeSource AncestorType={x:Type ComboBox}}}" />
		</b:EventTrigger>
	</b:Interaction.Triggers>
</ComboBox>
```

```csharp
public ICommand LanguageChangedCommand => new RelayCommand<object?>(param =>
{
	// 只在 ViewModel 处理业务逻辑
	SaveLanguage(param);
});
```
