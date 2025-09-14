#!/bin/bash
# Markdown linting script for local development
# Usage: ./scripts/lint-markdown.sh [--fix]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT"

# Check if markdownlint-cli is installed
if ! command -v markdownlint &> /dev/null; then
    echo "markdownlint-cli not found. Installing..."
    npm install -g markdownlint-cli
fi

if [[ "$1" == "--fix" ]]; then
    echo "Running markdownlint with auto-fix..."
    markdownlint --config .markdownlint.json '**/*.md' --fix
    echo "Auto-fix completed. Running final check..."
fi

echo "Running markdownlint check..."
if markdownlint --config .markdownlint.json '**/*.md'; then
    echo "✅ All markdown files are properly formatted"
else
    echo "❌ Markdown linting failed. Run with --fix to auto-correct issues:"
    echo "   ./scripts/lint-markdown.sh --fix"
    exit 1
fi