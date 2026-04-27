---
name: typography-semantic-color
description: Typography hierarchy and semantic color usage for UI text
---

# 技能 06: 排版层级与语义颜色

目标：通过字体层级和语义色提升可读性。

## 字体层级

- 主标题: 强调页面主题
- 卡片标题: 强调功能意图
- 辅助说明: 降低视觉权重

## 颜色语义

- Primary: 主信息
- Secondary: 次信息
- Tertiary: 说明与弱提醒
- Accent/Caution: 状态或风险提示

## 规则

- 用语义色，不写死颜色值。
- 保持标题和正文的层级稳定。
- 避免大量高饱和颜色同时出现。

## 可访问性

- 文字与背景保证足够对比度。
- 不只用颜色传达状态，必要时加图标或文案。

## 小规模代码用例

```xml
<StackPanel>
	<ui:TextBlock FontTypography="BodyStrong" Text="下载更新" />
	<ui:TextBlock Margin="0,4,0,0"
				  Foreground="{ui:ThemeResource TextFillColorTertiaryBrush}"
				  Text="下载完成后会自动校验完整性" />
	<ui:TextBlock Margin="0,8,0,0"
				  Foreground="{DynamicResource SystemFillColorCautionBrush}"
				  Text="网络波动时可能重试" />
</StackPanel>
```
