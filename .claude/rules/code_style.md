---
description: Code style, naming, and quality standards
paths:
  - "src/**"
  - "frontend/src/**"
  - "tests/**"
---

# Code Style & Quality Standards

Code must be written for humans first, and machines second. Consistency is more important than personal preference.

## 1. Naming Conventions
- **Descriptive & Non-Ambiguous**: Variables and functions should explain *what* they are or *what* they do. Avoid abbreviations (e.g., `userRepository` instead of `uRepo`).
- **Boolean Names**: Use prefixes like `is`, `has`, `should`, or `can` (e.g., `isUserAuthenticated`).
- **Standard Casing**: 
    - `camelCase` for variables, functions, and file names (unless language standard dictates otherwise).
    - `PascalCase` for classes and components.
    - `UPPER_SNAKE_CASE` for constants.

## 2. Function & Method Design
- **Single Responsibility**: One function should do one thing well.
- **Short Functions**: Aim for functions to be visible on one screen without scrolling.
- **Limited Arguments**: If a function needs more than 3 arguments, consider using a configuration object.
- **Pure Functions**: Favor pure functions where possible to make testing and debugging easier.

## 3. Comments & Documentation
- **Self-Documenting Code**: Code should be clear enough that comments are rarely needed for *what* is happening.
- **"Why" over "What"**: Use comments to explain the reasoning behind complex logic or non-obvious workarounds.
- **JSDoc/Docstrings**: Document public APIs, interfaces, and complex utility functions.

## 4. Strict Typing & Data Structures
- **No `any` Policy**: Never use the `any` type. If a type is truly unknown, use `unknown` and implement proper type guards.
- **Interfaces over Types**: Use `interface` for defining object structures and API contracts. Reserve `type` for unions, intersections, or primitives.
- **Explicit Return Types**: Always specify the return type of functions, especially for public APIs and complex logic.
- **Strict Null Checks**: Always handle `null` and `undefined` explicitly. Use optional chaining (`?.`) and nullish coalescing (`??`) safely.

## 5. Error Handling
- **Avoid Silent Failures**: Always handle errors or re-throw them with added context.
- **Custom Errors**: Use typed errors to distinguish between different failure modes (e.g., `ValidationError`, `NetworkError`).
- **Meaningful Messages**: Error messages should be helpful for both developers and (if applicable) end-users.
