# Copilot Instructions

## 项目指南
- UI 设计偏好：WPF 界面采用现代深色主题，主字体使用 Segoe UI/系统无衬线，层级克制（主标题>分组标题>正文>次要信息），强调可读性优先与功能型工具风格，颜色使用克制并以浅色文字+少量强调色为主。
- 界面设计偏好：避免传统老式菜单控件，选项与任务采用选项卡结构；设置页减少层级、直接展示关键项；删除与当前页面职责无关的设置项；控件风格需现代化且统一，统一采用 Lepoco WPF UI（http://schemas.lepo.co/wpfui/2022/xaml）控件风格，并优先使用其现代控件构建页面。
- 任务设置和选项设置页面采用现代化可折叠卡片布局；分类使用选项卡而非传统菜单；标题文本左对齐、箭头右对齐；优先使用 WPF UI (http://schemas.lepo.co/wpfui/2022/xaml) 控件实现。
- 页面布局中不使用 ScrollViewer，使用 StackPanel 处理滚动行为（按团队约定）。
- 项目约定：自定义 UI 依赖（如双状态按钮、值转换器）统一放在 `Views/Controls` 与 `Views/Converters` 并在页面中直接使用；开始界面应使用 `Page` 而非 `UserControl`。
- 架构约定：使用 CommunityToolkit.MVVM 重写并维护 ViewModel；View（含 MainWindow 和各 Page）仅处理初始化，不承载事件业务逻辑；导航使用 `NavigationViewItem.TargetPageType` 自动导航，不使用 Click 事件、额外 Frame 或额外承载布局。