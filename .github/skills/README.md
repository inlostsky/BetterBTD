# WPF UI 设计开发技能清单

本目录沉淀 View 层 UI 设计开发技能，重点覆盖：

- 目录分类架构逻辑
- 主页面布局
- 卡片设计
- 控件设计
- Lepoco WPF UI 使用方式

## SKILL.md Format

A `SKILL.md` file is a markdown document with optional YAML frontmatter:

```md
---
name: code-review
description: Specialized code review capabilities
---

# Code Review Guidelines

When reviewing code, always check for:

1. **Security vulnerabilities**—SQL injection, XSS, etc.
2. **Performance issues**—N+1 queries, memory leaks
3. **Code style**—Consistent formatting, naming conventions
4. **Test coverage**—Are critical paths tested?

Provide specific line-number references and suggested fixes.
```

Frontmatter fields:

- `name`—The skill's identifier. If omitted, the directory name is used.
- `description`—A short description of what the skill does.

The markdown body contains the instructions that are injected into the session context when the skill is loaded.

## Configuration Options

SessionConfig skill fields:

- Node.js: `skillDirectories`, `disabledSkills`
- Python: `skill_directories`, `disabled_skills`
- Go: `SkillDirectories`, `DisabledSkills`
- .NET: `SkillDirectories`, `DisabledSkills`
- Java: `skillDirectories`, `disabledSkills`

## Best Practices

- Organize by domain - Group related skills together, for example `skills/security/` or `skills/testing/`.
- Use frontmatter - Include `name` and `description` in YAML frontmatter for clarity.
- Document dependencies - Note any tools or MCP servers a skill requires.
- Test skills in isolation - Verify skills work before combining them.
- Use relative paths - Keep skills portable across environments.

## Reading Order

1. directory-architecture/SKILL.md
2. main-shell-layout/SKILL.md
3. page-information-architecture/SKILL.md
4. card-design-system/SKILL.md
5. spacing-grid-rhythm/SKILL.md
6. typography-semantic-color/SKILL.md
7. command-behavior-binding/SKILL.md
8. tree-and-table-config-ui/SKILL.md
9. dialog-structure-pattern/SKILL.md
10. custom-composite-controls/SKILL.md
11. resource-dictionary-theming/SKILL.md
12. window-titlebar-backdrop/SKILL.md
13. global-feedback-and-tray/SKILL.md
14. lepoco-wpfui-practice/SKILL.md

## Usage

- Reuse existing skill entries when developing new pages.
- Check the relevant skill before code review or UI changes.
- Add a new skill before introducing a new interaction pattern.
