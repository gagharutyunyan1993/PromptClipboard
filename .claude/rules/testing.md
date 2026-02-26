---
description: Testing strategies and verification requirements
paths:
  - "tests/**"
  - "src/**"
---

# Testing & Verification

Code is a liability until it is proven to work. Verification is a fundamental part of the development lifecycle.

## 1. Automated Testing Strategy
- **Unit Tests**: Test logic in isolation. Every complex utility, algorithm, or business rule must have unit tests.
- **Integration Tests**: Test how different parts of the system work together (e.g., API to Database).
- **Regression Testing**: Always add a test case for every bug fixed to ensure it never returns.
- **Coverage**: Aim for high coverage of "critical paths" and complex logic, rather than just chasing a percentage.

## 2. Test Quality
- **Readable Tests**: Tests should serve as documentation. Use descriptive test names (e.g., `should_return_error_when_user_is_not_found`).
- **AAA Pattern**: Structure tests using Arrange, Act, Assert.
- **Isolation**: Tests must be independent. One test failure should not cause others to fail.
- **Mocking**: Use mocks and stubs for external dependencies (APIs, File System) to keep tests fast and deterministic.

## 3. Verification Process
- **Pre-Commit Checks**: Run tests and linting before submitting code.
- **Continuous Integration**: Ensure all tests pass in the CI environment.
- **Manual Verification**: For UI/UX changes, provide screenshots or recordings as part of the verification (use `walkthrough.md`).

## 4. Definition of Done
A task is NOT done until:
- The code implementation is complete.
- All relevant tests pass.
- The change is verified in its target environment (or locally as a proxy).
- A `walkthrough.md` artifact is created showing the results.