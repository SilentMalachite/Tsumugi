# Tsumugi Phase 0 — Progress Ledger

Task 1: complete (commits a243d18..f5a20ef, review clean — spec ✅; Important coverlet-dedup + Minor redundant-props fixed)

Task 2+3: complete (commits f5a20ef..ab38df1, review clean — spec ✅, quality Approved)
  Minor/deferred for final-review triage:
   - ArchitectureTests checks DIRECT refs only (brief-mandated reflection approach; transitive not caught). Suggest XML-doc remark noting the limitation.
   - Office.Create has no null guards (deferred per phased approach; Nullable=enable covers typed callers).
   - Note: .editorconfig gained CA1707.severity=none scoped to [tests/**/*.cs] (idiomatic for xUnit underscore test names).

Task 4: complete (commits ab38df1..979c2d5, review clean — spec ✅, quality Approved)
  Minor/deferred for final-review triage:
   - RegisterOfficeUseCaseTests success test does not assert dto.Id round-trips (add dto.Id.Should().Be(repo.Added!.Id)).
   - Note: App.axaml.cs gained `using AvaloniaApplication = Avalonia.Application` alias to resolve CS0118 (Tsumugi.Application namespace vs Avalonia.Application base type).

Task 5: complete (commits 979c2d5..353d31c, review clean — spec ✅, quality Approved; Important fixes applied: .editorconfig Migrations-scope + repo null-guard + trailing newline)
  MUST capture in Task 9 docs:
   - GHSA-2m69-gcr7-jv3q (SQLitePCLRaw.lib.e_sqlite3 2.1.11, HIGH, no fixed version) suppressed via advisory-scoped NuGetAuditSuppress in Directory.Build.props. Record rationale in open-questions.md AND/or an ADR: no fixed version; offline desktop app, no network-exposed SQLite; remediation trigger = upstream patch.
  Minor/deferred for final-review triage:
   - SqliteFixture.NewContext() calls Database.Migrate() per call (idempotent; could move to ctor).
   - Round-trip tests don't exercise the 32-char OfficeNumber maxlen boundary.
