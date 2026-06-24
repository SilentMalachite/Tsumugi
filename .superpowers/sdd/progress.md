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

Task 6: complete (commits 353d31c..981dc41, review clean — spec ✅, quality Approved; VACUUM INTO escaping verified injection-safe, EF1002 pragma narrowly scoped)
  Minor/deferred for final-review triage:
   - No automated test for BackupDatabaseUseCase blank-path ArgumentException guard (logic verified by reading). Add an async ThrowsAsync test with a fake IBackupService (project uses fakes, not Moq).

Task 7: complete (commits 981dc41..1908754, review clean — spec ✅, quality Approved; Important temp-file-leak fixed)
  CUMULATIVE CHECKPOINT after Task 7: dotnet build -c Release = 0 warnings; dotnet format --verify-no-changes = pass; dotnet test = 12/12 pass (Domain 3, Application 4, Infrastructure 3, App 2).
  Minor/deferred for final-review triage:
   - App ServiceProvider not disposed on shutdown (wire lifetime.ShutdownRequested -> dispose before Phase 1 ViewModel injection).
   - SQLite path uses ApplicationData (~/.config on macOS, not ~/Library/Application Support) — final policy to be recorded in ADR 0003 (Task 9).

Task 8: complete (commits 1908754..3b1c39b, review clean — spec ✅, quality Approved; offline test non-vacuous, ci.sh fail-fast verified, CI OK with 15/15)
  Minor/deferred for final-review triage:
   - OfflineComplianceTests + ArchitectureTests check DIRECT refs only — add a one-line comment noting transitive refs not checked.
   - App-layer offline compliance must be tracked in open-questions.md (Task 9).

Task 9: complete (commit 3b1c39b..ed0649a, docs-only — open-questions.md captures GHSA suppression + App-offline + direct-ref limitation; ADRs 0001/0002/0003 present, 0003 reflects implemented ApplicationData path)

ALL 9 TASKS COMPLETE. Branch base = a243d18, tip = ed0649a.
