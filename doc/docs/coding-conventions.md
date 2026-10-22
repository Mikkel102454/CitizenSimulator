# Coding and naming conventions

Use these conventions for new and edited C# code. Editor settings live in the repository's `.editorconfig`.

- Use four spaces, Allman braces, and file-scoped namespaces. Omit braces for a single-line `if` body; retain them for multi-line bodies and loops. Keep any braces needed to preserve scope or `else` binding.
- Keep one statement and one local declaration per line. Separate logical steps with a blank line.
- Use `PascalCase` for types, methods, properties, and new enum members. Interfaces start with `I`.
- Use `camelCase` for parameters and local variables, and `_camelCase` for private fields.
- Use domain names such as `business`, `contract`, `recipe`, and `reservation`. Single-letter indices are acceptable for simple loops.
- Include units when an amount could be ambiguous: `laborMinutes`, `electricityKwh`, `waterLitres`, and `massKgPerUnit`.
- Name boolean queries with `Is`, `Has`, or `Can`; use `Try` for operations that report expected failure.
- Use explicit primitive types. Use `var` when the type is obvious from a constructor, cast, or the surrounding domain operation.
- Give methods block bodies. Simple calculated properties may use expression bodies.
- Aim for lines of roughly 120 characters. Wrap long parameter lists, queries, and conditions at logical boundaries. Put each member of a multiline object initializer on its own line.
- Prefer guard clauses. Give complicated calculations descriptive intermediate values, and extract helpers when their names explain a business operation.
- Explain business rules and reasons in comments. Preserve validation order, short-circuit evaluation, and transaction boundaries during readability changes.

Preserve existing public names and serialized fields during cleanup, including legacy spelling and enum names. Renaming a public contract requires a separate change that updates its consumers.
