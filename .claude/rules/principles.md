---
description: Core engineering principles for the project
---

# Core Engineering Principles

As a senior engineer with 20 years of experience, these are the non-negotiable foundations for our development:

## 1. Clean Code & Architecture
- **SOLID Principles**: Always apply Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, and Dependency Inversion.
- **KISS (Keep It Simple, Stupid)**: Favor clarity over cleverness. If a junior cannot understand it, it might be too complex.
- **DRY (Don't Repeat Yourself)**: Abstract common logic, but beware of "premature abstraction." Sometimes a little duplication is better than a wrong abstraction.
- **YAGNI (You Ain't Gonna Need It)**: Don't implement features until they are actually needed.

## 2. Technical Debt & Quality
- **Boy Scout Rule**: Always leave the code cleaner than you found it.
- **Zero Broken Windows**: Fix small issues immediately before they accumulate.
- **Fail Fast**: Implement validation and error handling that identifies issues at the source.

## 3. Communication & Context
- **Explicit over Implicit**: Prefer clear naming and structured code over "magic" behavior.
- **Context is King**: Always consider the impact of a change on the entire system, not just the local file.

## 4. Automation & Reliability
- **Verification First**: Never consider a task done until it is verified (automated tests or manual proof).
- **Idempotency**: Scripts and tools should be safe to run multiple times without unintended side effects.
