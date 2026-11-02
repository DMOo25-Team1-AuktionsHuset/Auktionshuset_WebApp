---
paths:
  - "**/*.razor"
  - "**/*.razor.cs"
---

# Blazor changes
- Keep business rules in application services, not components.
- Handle asynchronous UI state and disposal of subscriptions correctly.
- Verify authorization in the backend; UI hiding is not authorization.
