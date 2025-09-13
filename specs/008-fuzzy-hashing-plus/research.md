# Research: Fuzzy Hashing Plus Configuration System

## Research Tasks

### 1. CTPH (Context-triggered piecewise hashing) Library Selection

**Decision**: Use ssdeep-dotnet library (C# port of ssdeep)

**Rationale**: 
- ssdeep is the industry standard implementation of CTPH
- Native C# port available as NuGet package
- Well-maintained with good performance characteristics
- Produces similarity scores (0-100) suitable for threshold comparison
- Compatible with .NET 8.0

**Alternatives considered**:
- tlsh-dotnet: Too specialized for malware detection
- Custom CTPH implementation: Too complex and error-prone
- Calling ssdeep binary: Cross-platform compatibility issues

### 2. JSON Configuration Hot-Reloading Pattern

**Decision**: FileSystemWatcher with debouncing + per-file validation

**Rationale**:
- FileSystemWatcher detects changes immediately
- Debouncing prevents excessive reloads during editing
- Per-file validation ensures config integrity
- Fallback to previous config on validation errors
- Minimal performance impact (config cached between files)

**Alternatives considered**:
- Polling every N seconds: Too slow for "per-file" requirement
- No hot-reload: Violates requirement FR-008
- IOptionsMonitor: Overkill for single config file

### 3. Configuration Validation Strategy

**Decision**: JSON Schema validation with fluent validation fallback

**Rationale**:
- JSON Schema provides structural validation
- FluentValidation handles business rules (threshold ranges)
- Clear error messages for troubleshooting
- Type-safe deserialization with JsonSerializer

**Alternatives considered**:
- Manual validation: Error-prone and unmaintainable
- Attributes only: Limited validation capabilities
- Third-party validators: Unnecessary dependency

### 4. Backward Compatibility Approach

**Decision**: Config versioning with automatic migration

**Rationale**:
- Version field in config identifies format
- Migration functions transform old to new format
- Default values preserve existing behavior
- Gradual migration path for users

**Alternatives considered**:
- Breaking changes: Violates FR-010 requirement
- Parallel configs: Too complex for users
- No versioning: Cannot evolve config format

### 5. Performance Optimization for Per-File Reload

**Decision**: Lazy config reloading with change detection

**Rationale**:
- Check file modification time before parsing
- Only reload if config actually changed
- Cache parsed config in memory
- Target <10ms for config operations

**Alternatives considered**:
- Always reload: Too slow for large file batches
- Never reload during processing: Violates FR-008
- Background reload: Complex synchronization issues

## Technical Integration Points

### Existing Codebase Integration
- Replace existing hash comparisons in identification workflow
- Extend current configuration structure (preserve existing fields)
- Maintain existing CLI interface while adding new config commands
- Keep current logging patterns with structured output

### Dependencies to Add
```xml
<PackageReference Include="ssdeep-dotnet" Version="2.0.0" />
<PackageReference Include="FluentValidation" Version="11.8.0" />
<PackageReference Include="System.IO.Abstractions" Version="19.2.69" />
```

### Configuration Schema Evolution
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "version": "2.0",
  "matchConfidenceThreshold": 0.8,
  "renameConfidenceThreshold": 0.85,
  "fuzzyHashThreshold": 75,  // NEW: CTPH similarity threshold
  "hashingAlgorithm": "ctph", // NEW: algorithm selector
  "filenamePatterns": { /* existing */ },
  "filenameTemplate": "/* existing */"
}
```