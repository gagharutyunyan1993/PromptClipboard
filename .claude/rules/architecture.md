---
description: Architectural standards and dependency management
paths:
  - "src/**"
---

# Architecture & Design

As an Orchestrator, this system must be robust, scalable, and easy to maintain. Architectural integrity is paramount.

## 1. Clean Architecture & DDD
- **Separation of Concerns**: Strictly separate Domain logic (business rules) from Infrastructure (DB, API clients, external services) and Application layers (controllers, use cases).
- **Dependency Rule**: Dependencies must always point *inward* toward the Domain layer. The Domain layer must have zero dependencies on external libraries or frameworks (where possible).
- **Ubiquitous Language**: Use domain terminology in your code. The code should read like the business requirements.

## 2. Dependency Management
- **Inversion of Control (IoC)**: High-level modules should not depend on low-level modules; both should depend on abstractions (interfaces).
- **Anti-Corruption Layers**: Never leak 3rd-party types or models into the core domain. Wrap external libraries (Axios, SDKs) in internal adapter/wrapper services. If the external library changes, only the adapter should need rewriting.

## 3. State Management
- **Stateless Services**: Services and Use Cases should be stateless. State should be managed in the database or dedicated state stores (e.g., Redis).
- **Immutable Data**: Prefer immutable data structures. When modifying an object or array, return a new instance rather than mutating the original.
