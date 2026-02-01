# NetLift - Agent Guide

> Instrukcje dla agentów AI pracujących nad projektem NetLift

---

## Podstawowe zasady

### 1. System zadań oparty na plikach

```
.plan/
├── backlog/          # Zadania do zrobienia (podzielone na sprinty)
├── in-progress/      # Aktualnie realizowane
├── done/             # Zakończone
├── blocked/          # Zablokowane
└── messages/         # Komunikacja między agentami
```

### 2. Cykl życia zadania

```
backlog/sprint-XX/task.md
         │
         ▼ [Agent claims task]
    in-progress/task.md
         │
         ├──▶ [Completed] ──▶ done/sprint-XX/task.md
         │
         └──▶ [Blocked] ──▶ blocked/task.md
```

---

## Jak pracować z zadaniami

### Krok 1: Wybierz zadanie

```bash
# Sprawdź dostępne zadania w aktualnym sprincie
ls .plan/backlog/sprint-01/

# Sprawdź czy zadanie nie ma dependencies
# W pliku zadania sekcja "Depends on"
```

**WAŻNE:**
- Wybieraj zadania BEZ dependencies lub z dependencies już w `done/`
- Jedno zadanie na raz
- Zadania z niższym numerem mają wyższy priorytet

### Krok 2: Claim zadania

1. Przeczytaj plik zadania dokładnie
2. Przenieś plik do `in-progress/`:

```bash
mv .plan/backlog/sprint-01/001-task.md .plan/in-progress/
```

3. Dodaj swój identyfikator w sekcji Meta:

```markdown
## Meta
- **Agent:** claude-agent-1
- **Started:** 2025-01-31 10:00
```

### Krok 3: Wykonaj zadanie

1. Implementuj zgodnie z Acceptance Criteria
2. Aktualizuj Progress Log:

```markdown
## Progress Log
- [2025-01-31 10:00] Started work on solution structure
- [2025-01-31 10:30] Created src/ directory structure
- [2025-01-31 11:00] Added NetLift.sln with all projects
```

3. Commituj zmiany z prefixem zadania:

```bash
git commit -m "[TASK-001] Create solution structure"
```

### Krok 4: Zakończ zadanie

1. Upewnij się, że wszystkie Acceptance Criteria są spełnione
2. Oznacz checkboxy jako complete:

```markdown
## Acceptance Criteria
- [x] Solution file created
- [x] All projects added
- [x] Builds successfully
```

3. Dodaj finalny wpis w Progress Log:

```markdown
- [2025-01-31 12:00] COMPLETED - All criteria met
```

4. Przenieś do `done/`:

```bash
mv .plan/in-progress/001-task.md .plan/done/sprint-01/
```

---

## Obsługa blokad

Jeśli zadanie jest zablokowane:

1. Przenieś do `blocked/`:

```bash
mv .plan/in-progress/005-task.md .plan/blocked/
```

2. Dodaj sekcję Blocked By:

```markdown
## Blocked By
- **Reason:** Waiting for TASK-003 to define interfaces
- **Since:** 2025-01-31 14:00
- **Unblocks when:** TASK-003 is in done/
```

3. Wybierz inne zadanie do pracy

---

## Komunikacja między agentami

Używaj plików w `.plan/messages/`:

```markdown
# .plan/messages/2025-01-31-agent1-question.md

## From: agent-1
## To: agent-2 (or "all")
## Subject: Question about ITransformer interface

Message content here...

## Response
[agent-2 adds response here]
```

---

## Konwencje kodowania

### Nazewnictwo plików

```
src/NetLift.{Module}/{Category}/{ClassName}.cs
```

### Nazewnictwo commitów

```
[TASK-XXX] Short description

- Detail 1
- Detail 2
```

### Dokumentacja w kodzie

```csharp
/// <summary>
/// Brief description.
/// </summary>
/// <remarks>
/// Additional details if needed.
/// </remarks>
```

---

## Checklist przed zakończeniem zadania

- [ ] Kod kompiluje się bez błędów
- [ ] Testy przechodzą (jeśli dotyczy)
- [ ] Acceptance Criteria spełnione
- [ ] Progress Log zaktualizowany
- [ ] Commit z prawidłowym prefixem
- [ ] Plik przeniesiony do done/

---

## Priorytety i estymaty

### Priorytety

| Priority | Znaczenie |
|----------|-----------|
| **P0** | Blocker - bez tego nic dalej nie ruszy |
| **P1** | Core functionality - kluczowe dla MVP |
| **P2** | Nice to have - może poczekać |

### Estymaty

| Size | Czas | Opis |
|------|------|------|
| **S** | < 2h | Mała zmiana, dobrze zdefiniowana |
| **M** | 2-4h | Średnia złożoność |
| **L** | 4-8h | Duże zadanie, jeden dzień |
| **XL** | > 8h | Za duże - podziel na mniejsze! |

---

## Paralelizacja zadań

Zadania mogą być robione równolegle TYLKO jeśli:
1. Nie mają wspólnych dependencies
2. Nie modyfikują tych samych plików
3. Nie zależą od siebie nawzajem

### Przykłady równoległych zadań w Sprint 1:

```
PARALLEL GROUP A:
- 001-create-solution-structure
- 002-setup-roslyn-packages
- 003-setup-spectre-console-cli

PARALLEL GROUP B (po A):
- 004-implement-solution-parser
- 005-implement-project-parser
- 006-implement-packages-config-parser
```

---

## Rozwiązywanie problemów

### Nie wiem jak zaimplementować

1. Sprawdź ARCHITECTURE.md
2. Sprawdź DECISIONS.md
3. Stwórz spike task (research)
4. Zapytaj przez messages/

### Napotkałem edge case

1. Jeśli prosty → zaimplementuj
2. Jeśli złożony → dodaj TODO i kontynuuj
3. Jeśli blokujący → oznacz zadanie jako blocked

### Testy nie przechodzą

1. NIE commituj broken code
2. Napraw lub oznacz test jako skip z komentarzem
3. Dodaj wpis w Progress Log

---

## Ważne pliki do znajomości

| Plik | Zawartość |
|------|-----------|
| `.plan/MASTER.md` | Overview projektu, status |
| `.plan/ARCHITECTURE.md` | Architektura, struktura |
| `.plan/DECISIONS.md` | Decyzje architektoniczne |
| `rules/*.yaml` | Reguły transformacji |

---

## Quick Reference

```bash
# Sprawdź status
ls .plan/in-progress/
ls .plan/blocked/

# Claim zadania
mv .plan/backlog/sprint-01/XXX.md .plan/in-progress/

# Zakończ zadanie
mv .plan/in-progress/XXX.md .plan/done/sprint-01/

# Zablokuj zadanie
mv .plan/in-progress/XXX.md .plan/blocked/
```

---

*Last updated: 2025-01-31*
