# NetLift CLI Usage

Quick reference for NetLift commands and options.

## Installation

```bash
# Build from source
dotnet build
dotnet pack

# Install as global tool
dotnet tool install --global --add-source ./nupkg NetLift.Cli
```

## Quick Start

```bash
# 1. Analyze your solution
netlift analyze MySolution.sln --html

# 2. Preview migration changes
netlift migrate MySolution.sln --dry-run

# 3. Run migration
netlift migrate MySolution.sln --interactive

# 4. Validate results
netlift validate MySolution.sln
```

## Commands

### analyze

Analyze a .NET Framework solution for migration readiness.

```bash
netlift analyze <SOLUTION> [OPTIONS]

# Examples
netlift analyze MySolution.sln
netlift analyze MySolution.sln --html --output ./reports
netlift analyze MySolution.sln --target net9.0 --verbose
netlift analyze MySolution.sln --json > analysis.json
```

**Options:**
- `-o|--output <PATH>` - Output directory for reports (default: `./netlift-report`)
- `-t|--target <TFM>` - Target framework: `net8.0`, `net9.0` (default: `net8.0`)
- `--html` - Generate HTML report in addition to JSON
- `--json` - Output JSON only, suppress console summary
- `-v|--verbose` - Show detailed project breakdown and issues

### migrate

Migrate a solution to .NET 8+.

```bash
netlift migrate <SOLUTION> [OPTIONS]

# Examples
netlift migrate MySolution.sln
netlift migrate MySolution.sln --target net9.0
netlift migrate MySolution.sln --dry-run --dry-run-output changes.txt
netlift migrate MySolution.sln --interactive --no-backup
```

**Options:**
- `-t|--target <TFM>` - Target framework: `net8.0`, `net9.0` (default: `net8.0`)
- `--dry-run` - Preview changes without applying them
- `--dry-run-output <PATH>` - Write dry-run report to file
- `-i|--interactive` - Prompt before migrating each project
- `--no-backup` - Skip backup creation (not recommended)
- `-v|--verbose` - Show detailed migration progress

**Confidence Levels:**
- **95-100%** - Auto-applied, no review needed
- **80-94%** - Auto-applied with INFO comment
- **60-79%** - Applied with TODO, review recommended
- **<60%** - Not auto-applied, manual task generated

### validate

Validate a migrated solution by building and running tests.

```bash
netlift validate <SOLUTION> [OPTIONS]

# Examples
netlift validate MySolution.sln
netlift validate MySolution.sln --strict --verbose
netlift validate MySolution.sln --format json > validation.json
netlift validate MySolution.sln --skip-tests --output ./reports
```

**Options:**
- `--strict` - Require high confidence score for success
- `-f|--format <FORMAT>` - Output format: `text`, `json`, `xml` (default: `text`)
- `-o|--output <PATH>` - Output directory for HTML report
- `--skip-tests` - Skip running tests, build only
- `-v|--verbose` - Show detailed errors and warnings

## Common Workflows

### Safe Migration

```bash
# 1. Analyze first
netlift analyze MySolution.sln --html

# 2. Preview all changes
netlift migrate MySolution.sln --dry-run --dry-run-output preview.txt

# 3. Migrate interactively
netlift migrate MySolution.sln --interactive

# 4. Validate results
netlift validate MySolution.sln --strict
```

### Automated CI/CD

```bash
# Analyze and fail if complexity too high
netlift analyze MySolution.sln --json | jq '.overallComplexity.score > 80'

# Migrate with validation
netlift migrate MySolution.sln --no-backup
netlift validate MySolution.sln --strict --skip-tests
```

## Exit Codes

- `0` - Success
- `1` - Validation/migration failed
- `2` - Invalid input or file not found
