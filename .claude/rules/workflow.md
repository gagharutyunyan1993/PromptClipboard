---
description: Git version control and PR standards
---

# Workflow & Version Control

To ensure a smooth delivery pipeline and historical context, strict git practices are required.

## 1. Conventional Commits
- **Format**: `<type>(<scope>): <subject>` (e.g., `feat(api): add user endpoint`).
- **Types**: Use standard prefixes:
  - `feat`: A new feature.
  - `fix`: A bug fix.
  - `docs`: Documentation only changes.
  - `style`: Changes that do not affect the meaning of the code (white-space, formatting, etc).
  - `refactor`: A code change that neither fixes a bug nor adds a feature.
  - `test`: Adding missing tests or correcting existing tests.
  - `chore`: Changes to the build process or auxiliary tools.

## 2. Branching Strategy
- **Feature Branches**: All new work must happen on dedicated branches (e.g., `feature/login-system`, `bugfix/issue-123`).
- **No Direct Master/Main Commits**: The primary branch must stay deployable at all times.

## 3. Pull Requests (PRs)
- **Descriptive Titles**: State clearly what the PR does.
- **Contextual Descriptions**: Link to relevant tickets/issues. Detail *why* the change was made, not just *what* changed.
- **Self-Review First**: Never open a PR without reviewing your own differences first.
- **Approvals**: Require at least one approving review from a peer before merging. Enable required status checks (lints/tests).
