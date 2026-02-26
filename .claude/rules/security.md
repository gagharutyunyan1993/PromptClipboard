---
description: Security guidelines and secret management
paths:
  - "src/**"
  - "frontend/src/**"
---

# Security & Data Integrity

Security in an Orchestrator environment is critical as it acts as the central hub for data and logic.

## 1. Secret Management
- **Never Hardcode Secrets**: API keys, database passwords, and environment specific tokens must NEVER appear in the codebase.
- **Environment Variables**: Access secrets via environment variables (`.env`) or a dedicated Secret Manager (e.g., AWS Secrets Manager, HashiCorp Vault).
- **Masking Logs**: Ensure that sensitive data (PII, passwords, tokens) is stripped or masked before logging.

## 2. Input Validation
- **Trust No Input**: Always validate external data, whether it comes from a user request, webhooks, or third-party APIs.
- **Use Schema Validation**: Use libraries like Zod, Joi, or similar to enforce strict schemas for incoming DTOs.
- **Sanitize Strings**: Protect against SQL Injection and XSS by using parameterized queries and sanitizing HTML input.

## 3. Principle of Least Privilege
- **Minimal Scopes**: API tokens and IAM roles created for the application should only have the exact permissions required to function, nothing more.
- **Role-Based Access Control (RBAC)**: Ensure internal services and API endpoints enforce authorization.
