---
description: Performance considerations for orchestration operations
paths:
  - "src/**"
---

# Performance & Concurrency

Orchestrators often handle heavy IO and workflow management. The system must remain responsive under load.

## 1. Asynchronous Programming
- **Embrace Promises**: Avoid synchronous blocking operations at all costs (e.g., `fs.readFileSync`), especially on the main thread.
- **Concurrent Execution**: Use `Promise.all` or `Promise.allSettled` when executing multiple independent asynchronous tasks, rather than awaiting them sequentially in a `for` loop.

## 2. Resource Management
- **Memory Leaks**: Be vigilant about clearing timeouts, intervals, event listeners, and closing loose database/socket connections.
- **Pagination**: Never return unbounded lists from the database or external APIs. Implement robust pagination (cursor or offset-based).
- **Connection Pools**: Configure proper connection pooling for database clients to avoid resource exhaustion under load.

## 3. Algorithmic Efficiency
- **Big O Audits**: For critical data manipulation (sorting, filtering large arrays), evaluate the time and space complexity. Avoid O(n^2) where a Map lookup O(1) can be used.
- **Caching**: Implement caching strategies (e.g., Redis, in-memory LRU) for data that is frequently read but rarely modifications. Remember to implement cache invalidation.
