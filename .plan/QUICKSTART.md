# NetLift - Quick Start Guide

> Szybki przewodnik jak pracować z projektem NetLift

---

## 🚀 Rozpoczęcie pracy

### 1. Sprawdź aktualny status

```bash
# Zobacz co jest w toku
ls .plan/in-progress/

# Zobacz backlog aktualnego sprintu
ls .plan/backlog/sprint-01/

# Zobacz ukończone
ls .plan/done/
```

### 2. Wybierz zadanie

Zadania wykonuj w kolejności numerycznej (001 → 002 → ...).
Sprawdź dependencies w pliku zadania przed startem.

### 3. Uruchom agenta

```
# Przykład polecenia dla Claude Code:
"Weź zadanie TASK-001 z .plan/backlog/sprint-01/001-create-solution-structure.md
i zaimplementuj je zgodnie z Acceptance Criteria"
```

---

## 📁 Struktura folderów

```
.plan/
├── MASTER.md           ← Overview projektu, status
├── ARCHITECTURE.md     ← Architektura systemu
├── DECISIONS.md        ← Log decyzji (ADR)
├── AGENT_GUIDE.md      ← Instrukcje dla agentów
├── QUICKSTART.md       ← Ten plik
│
├── backlog/            ← Zadania do zrobienia
│   ├── sprint-01/      ← 14 zadań (Foundation)
│   ├── sprint-02/      ← 10 zadań (Project Files)
│   ├── sprint-03/      ← 8 zadań (Configuration)
│   ├── sprint-04/      ← 10 zadań (MVC)
│   ├── sprint-05/      ← 8 zadań (EF6)
│   ├── sprint-06/      ← 10 zadań (WCF)
│   └── sprint-07/      ← 6 zadań (Validation)
│
├── in-progress/        ← Aktualnie realizowane
├── done/               ← Zakończone (per sprint)
├── blocked/            ← Zablokowane
├── messages/           ← Komunikacja między agentami
└── templates/          ← Szablony zadań
```

---

## 🎯 Sprint Overview

| Sprint | Focus | Zadania | Opis |
|--------|-------|---------|------|
| **1** | Foundation | 001-014 | CLI, parsery, analiza |
| **2** | Project Files | 015-024 | .csproj, packages.config |
| **3** | Configuration | 025-032 | web.config → appsettings |
| **4** | MVC | 033-042 | Controllers, routing |
| **5** | EF | 043-050 | EF6 → EF Core |
| **6** | WCF | 051-060 | WCF → gRPC/REST |
| **7** | Validation | 061-066 | Build, test, polish |

---

## 🤖 Uruchamianie wielu agentów

### Równoległe zadania (bez dependencies):

```
"Uruchom równolegle:
1. TASK-001 (create solution structure)
2. TASK-011 (create test fixture)
3. TASK-012 (setup xunit tests)

Użyj trzech agentów równolegle."
```

### Sekwencyjne zadania (z dependencies):

```
"Wykonaj po kolei:
1. Najpierw TASK-001
2. Potem TASK-002 i TASK-003 równolegle
3. Na końcu TASK-004"
```

---

## ✅ Checklist przed zakończeniem zadania

Agent powinien:
- [ ] Spełnić wszystkie Acceptance Criteria
- [ ] Zaktualizować Progress Log w pliku zadania
- [ ] Commitować z prefixem `[TASK-XXX]`
- [ ] Przenieść plik do `done/sprint-XX/`
- [ ] Sprawdzić czy nie odblokowało innych zadań

---

## 📊 Śledzenie postępu

### Szybki status:

```bash
# Ile zadań done vs backlog
echo "Done:" && ls .plan/done/*/ | wc -l
echo "In Progress:" && ls .plan/in-progress/ | wc -l
echo "Backlog:" && ls .plan/backlog/*/ | wc -l
```

### Update MASTER.md:

Po zakończeniu kilku zadań, zaktualizuj:
- Current Status section
- Sprint Overview status emojis
- Milestones checkboxes

---

## 🔥 Priorytetowe zadania na start

Zacznij od tych (można równolegle):

1. **TASK-001** - Create solution structure (BLOCKER)
2. **TASK-011** - Create test fixture mvc5-basic
3. **TASK-012** - Setup xUnit tests

Po TASK-001:
- **TASK-002** + **TASK-003** równolegle

---

## 💡 Tips

1. **Jeden agent = jedno zadanie** - nie mieszaj
2. **Czytaj dependencies** - nie zaczynaj zablokowanych
3. **Mniejsze commity** - łatwiejszy review
4. **Aktualizuj Progress Log** - śledzenie postępu
5. **Nie usuwaj plików** z backlog - przenoś do done

---

## 🆘 Problemy?

1. **Zadanie zablokowane** → przenieś do `blocked/`, dodaj info co blokuje
2. **Nie wiem jak** → sprawdź ARCHITECTURE.md, DECISIONS.md
3. **Potrzebuję decyzji** → stwórz wpis w DECISIONS.md jako "Proposed"
4. **Bug w poprzednim zadaniu** → stwórz nowe zadanie z fix

---

*Last updated: 2025-01-31*
