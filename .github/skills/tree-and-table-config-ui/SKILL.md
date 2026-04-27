---
name: tree-and-table-config-ui
description: Tree and table configuration UI patterns for hierarchical data
---

# 技能 08: 树表混合配置界面

目标：高密度配置场景下保持结构清晰。

## 适用场景

- 配置项有目录层级和叶子节点。
- 需要同时展示名称、类型、操作。

## 结构模式

- TreeListView 承载层级结构。
- GridViewColumn 承载多列信息。
- 自定义输入控件处理键位等特殊数据。

## 设计规则

- 第一列固定为名称/目录。
- 操作列宽度稳定，防止跳动。
- 目录节点与叶子节点区分交互能力。

## 体验优化

- 提供默认值、读取外部配置入口。
- 对不可编辑项明确禁用态。

## 小规模代码用例

```xml
<ui:TreeListView ItemsSource="{Binding SettingItems}" BorderThickness="0">
	<ui:TreeListView.Columns>
		<GridViewColumnCollection>
			<ui:GridViewColumn Width="280" Header="名称">
				<ui:GridViewColumn.CellTemplate>
					<DataTemplate>
						<ui:TreeRowExpander Content="{Binding Name}" />
					</DataTemplate>
				</ui:GridViewColumn.CellTemplate>
			</ui:GridViewColumn>
			<ui:GridViewColumn Width="120" Header="值">
				<ui:GridViewColumn.CellTemplate>
					<DataTemplate>
						<TextBox Text="{Binding Value, Mode=TwoWay}" />
					</DataTemplate>
				</ui:GridViewColumn.CellTemplate>
			</ui:GridViewColumn>
		</GridViewColumnCollection>
	</ui:TreeListView.Columns>
</ui:TreeListView>
```
