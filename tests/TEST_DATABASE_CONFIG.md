# Test Database Configuration

This document explains how to use the centralized test database configuration system.

## Overview

The `TestDatabaseConfig` class provides centralized configuration for test database paths and service creation, improving maintainability and environment independence.

## Features

- **Environment Variable Support**: Override database path using `TEST_DATABASE_PATH` environment variable
- **Default Fallback**: Uses a default database location when no environment variable is set
- **Temporary Database Generation**: Create unique temporary databases for isolated testing
- **Service Factory Methods**: Pre-configured service creation for testing

## Usage

### Basic Usage

```csharp
using EpisodeIdentifier.Tests.Contract;

// Get the default test database path
var dbPath = TestDatabaseConfig.GetTestDatabasePath();

// Create a FuzzyHashService for testing
var fuzzyHashService = TestDatabaseConfig.CreateTestFuzzyHashService();
```

### Environment Variable Override

Set the `TEST_DATABASE_PATH` environment variable to use a custom database:

```bash
# Linux/Mac
export TEST_DATABASE_PATH="/path/to/custom/test.db"

# Windows
set TEST_DATABASE_PATH=C:\path\to\custom\test.db
```

### Temporary Database

For tests that need isolation:

```csharp
// Generate a unique temporary database path
var tempDbPath = TestDatabaseConfig.GetTempDatabasePath();
var service = TestDatabaseConfig.CreateTestFuzzyHashService(tempDbPath);

// Clean up after test
TestDatabaseConfig.CleanupTempDatabase(tempDbPath);
```

## Migration

The following test files have been updated to use the centralized configuration:

- `tests/integration/VttWorkflowTests.cs`
- `tests/integration/SrtWorkflowTests.cs`  
- `tests/integration/AssWorkflowTests.cs`
- `tests/integration/EndToEndIdentificationTests.cs`

## Benefits

1. **Environment Independence**: Tests work across different development environments
2. **Easy Configuration**: Single point to change database paths
3. **Test Isolation**: Support for temporary databases when needed
4. **Maintainability**: Centralized service creation reduces code duplication
