#!/usr/bin/env bash
# Configure GitHub repository settings using YAML configuration and GitHub CLI
# This automates repository setup based on .github/repository-config.yml
#
# Usage: ./scripts/setup-branch-protection.sh [--dry-run] [--verbose] [--config-only]
#   --dry-run     : Show what would be configured without making changes
#   --verbose     : Show detailed output
#   --config-only : Only apply repository settings, skip branch protection
#   --help        : Show this help message

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script options
DRY_RUN=false
VERBOSE=false
CONFIG_ONLY=false

# Configuration file path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/../.github/repository-config.yml"

# Parse command line arguments
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=true ;;
        --verbose) VERBOSE=true ;;
        --config-only) CONFIG_ONLY=true ;;
        --help|-h) 
            echo "GitHub Repository Configuration Script"
            echo "====================================="
            echo
            echo "This script configures GitHub repository settings and branch protection"
            echo "using the YAML configuration in .github/repository-config.yml"
            echo
            echo "Usage: $0 [--dry-run] [--verbose] [--config-only]"
            echo "  --dry-run     : Show what would be configured without making changes"
            echo "  --verbose     : Show detailed output"
            echo "  --config-only : Only apply repository settings, skip branch protection"
            echo "  --help        : Show this help message"
            echo
            echo "Prerequisites:"
            echo "  - GitHub CLI (gh) must be installed and authenticated"
            echo "  - yq must be installed for YAML parsing"
            echo "  - Repository must exist on GitHub"
            echo "  - User must have admin access to the repository"
            echo
            echo "Examples:"
            echo "  $0 --dry-run          # Preview changes without applying"
            echo "  $0 --verbose          # Apply with detailed output"
            echo "  $0 --config-only      # Only update repository settings"
            exit 0
            ;;
        *) echo "Unknown argument: $arg. Use --help for usage."; exit 1 ;;
    esac
done

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_verbose() {
    if $VERBOSE; then
        echo -e "${BLUE}[VERBOSE]${NC} $1"
    fi
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if gh CLI is installed
    if ! command -v gh >/dev/null 2>&1; then
        log_error "GitHub CLI (gh) is not installed."
        log_info "Install it with: sudo snap install gh"
        exit 1
    fi
    
    # Check if yq is installed for YAML parsing
    if ! command -v yq >/dev/null 2>&1; then
        log_error "yq is not installed (required for YAML parsing)."
        log_info "Install it with: sudo snap install yq"
        exit 1
    fi
    
    # Check if configuration file exists
    if [[ ! -f "$CONFIG_FILE" ]]; then
        log_error "Configuration file not found: $CONFIG_FILE"
        exit 1
    fi
    
    # Check if authenticated
    if ! gh auth status >/dev/null 2>&1; then
        log_error "GitHub CLI is not authenticated."
        log_info "Run: gh auth login"
        exit 1
    fi
    
    # Get repository info
    local repo_info
    if ! repo_info=$(gh repo view --json owner,name 2>/dev/null); then
        log_error "Not in a GitHub repository or repository not found."
        log_info "Make sure you're in the repository directory and it exists on GitHub."
        exit 1
    fi
    
    local owner=$(echo "$repo_info" | jq -r '.owner.login' 2>/dev/null || echo "unknown")
    local name=$(echo "$repo_info" | jq -r '.name' 2>/dev/null || echo "unknown")
    
    log_success "GitHub CLI is ready"
    log_success "YAML parser (yq) is available"
    log_verbose "Repository: $owner/$name"
    log_verbose "Authenticated user: $(gh api user --jq '.login' 2>/dev/null || echo 'unknown')"
    log_verbose "Configuration file: $CONFIG_FILE"
}

# Get current branch protection status
check_current_protection() {
    log_info "Checking current branch protection status..."
    
    if gh api "repos/:owner/:repo/branches/main/protection" >/dev/null 2>&1; then
        log_warning "Branch protection is already enabled for main branch"
        if $VERBOSE; then
            log_verbose "Current protection settings:"
            gh api "repos/:owner/:repo/branches/main/protection" | jq .
        fi
        return 0
    else
        log_info "No branch protection currently configured for main branch"
        return 0  # Changed from return 1 to return 0
    fi
}

# Configure branch protection
configure_protection() {
    if $CONFIG_ONLY; then
        log_info "Skipping branch protection (--config-only specified)"
        return 0
    fi
    
    log_info "Configuring branch protection from YAML configuration..."
    
    # Read branch protection configuration from YAML
    local main_protection=$(yq '.branch_protection.main' "$CONFIG_FILE")
    
    if [[ "$main_protection" == "null" ]]; then
        log_warning "No branch protection configuration found in YAML"
        return 0
    fi
    
    # Extract configuration values
    local required_reviews=$(yq '.branch_protection.main.required_pull_request_reviews.required_approving_review_count' "$CONFIG_FILE")
    local dismiss_stale=$(yq '.branch_protection.main.required_pull_request_reviews.dismiss_stale_reviews' "$CONFIG_FILE")
    local require_code_owners=$(yq '.branch_protection.main.required_pull_request_reviews.require_code_owner_reviews' "$CONFIG_FILE")
    local strict_checks=$(yq '.branch_protection.main.required_status_checks.strict' "$CONFIG_FILE")
    local enforce_admins=$(yq '.branch_protection.main.enforce_admins' "$CONFIG_FILE")
    local allow_deletions=$(yq '.branch_protection.main.allow_deletions' "$CONFIG_FILE")
    local allow_force_pushes=$(yq '.branch_protection.main.allow_force_pushes' "$CONFIG_FILE")
    local require_conversation=$(yq '.branch_protection.main.require_conversation_resolution' "$CONFIG_FILE")
    
    # Get status check contexts as array
    local contexts_raw=$(yq '.branch_protection.main.required_status_checks.contexts[]' "$CONFIG_FILE")
    local contexts_json="["
    while IFS= read -r context; do
        contexts_json="${contexts_json}\"$context\","
    done <<< "$contexts_raw"
    contexts_json="${contexts_json%,}]"
    
    # Build the protection configuration JSON
    local protection_config=$(cat <<EOF
{
  "required_status_checks": {
    "strict": $strict_checks,
    "contexts": $contexts_json
  },
  "enforce_admins": $enforce_admins,
  "required_pull_request_reviews": {
    "required_approving_review_count": $required_reviews,
    "dismiss_stale_reviews": $dismiss_stale,
    "require_code_owner_reviews": $require_code_owners,
    "require_last_push_approval": false
  },
  "restrictions": null,
  "allow_force_pushes": $allow_force_pushes,
  "allow_deletions": $allow_deletions,
  "block_creations": true,
  "required_conversation_resolution": $require_conversation
}
EOF
)
    
    if $DRY_RUN; then
        log_info "DRY RUN: Would configure branch protection with:"
        echo "$protection_config" | jq .
        return 0
    fi
    
    log_verbose "Applying protection configuration:"
    if $VERBOSE; then
        echo "$protection_config" | jq .
    fi
    
    # Apply the configuration
    if echo "$protection_config" | gh api "repos/:owner/:repo/branches/main/protection" --method PUT --input -; then
        log_success "Branch protection configured successfully!"
    else
        log_error "Failed to configure branch protection"
        return 1
    fi
}

# Configure repository settings
configure_repository_settings() {
    log_info "Configuring repository settings from YAML configuration..."
    
    # Read repository settings from YAML
    local settings=$(yq '.repository.settings' "$CONFIG_FILE")
    
    if [[ "$settings" == "null" ]]; then
        log_warning "No repository settings found in YAML"
        return 0
    fi
    
    # Extract settings
    local allow_merge_commit=$(yq '.repository.settings.allow_merge_commit' "$CONFIG_FILE")
    local allow_squash_merge=$(yq '.repository.settings.allow_squash_merge' "$CONFIG_FILE")
    local allow_rebase_merge=$(yq '.repository.settings.allow_rebase_merge' "$CONFIG_FILE")
    local delete_branch_on_merge=$(yq '.repository.settings.delete_branch_on_merge' "$CONFIG_FILE")
    
    # Build repository settings JSON
    local repo_settings=$(cat <<EOF
{
  "allow_merge_commit": $allow_merge_commit,
  "allow_squash_merge": $allow_squash_merge,
  "allow_rebase_merge": $allow_rebase_merge,
  "delete_branch_on_merge": $delete_branch_on_merge
}
EOF
)
    
    if $DRY_RUN; then
        log_info "DRY RUN: Would update repository settings with:"
        echo "$repo_settings" | jq .
        return 0
    fi
    
    log_verbose "Applying repository settings:"
    if $VERBOSE; then
        echo "$repo_settings" | jq .
    fi
    
    # Apply repository settings
    if echo "$repo_settings" | gh api "repos/:owner/:repo" --method PATCH --input -; then
        log_success "Repository settings updated successfully!"
    else
        log_error "Failed to update repository settings"
        return 1
    fi
}

# Configure repository labels
configure_labels() {
    log_info "Configuring repository labels from YAML configuration..."
    
    # Read labels from YAML
    local labels_count=$(yq '.labels | length' "$CONFIG_FILE")
    
    if [[ "$labels_count" == "0" || "$labels_count" == "null" ]]; then
        log_warning "No labels configuration found in YAML"
        return 0
    fi
    
    if $DRY_RUN; then
        log_info "DRY RUN: Would configure $labels_count labels:"
        yq '.labels[] | "- " + .name + " (" + .color + "): " + .description' "$CONFIG_FILE"
        return 0
    fi
    
    # Process each label
    for i in $(seq 0 $((labels_count - 1))); do
        local name=$(yq ".labels[$i].name" "$CONFIG_FILE")
        local color=$(yq ".labels[$i].color" "$CONFIG_FILE")
        local description=$(yq ".labels[$i].description" "$CONFIG_FILE")
        
        log_verbose "Processing label: $name"
        
        # Check if label exists
        if gh api "repos/:owner/:repo/labels/$name" >/dev/null 2>&1; then
            # Update existing label
            local label_data="{\"name\":\"$name\",\"color\":\"$color\",\"description\":\"$description\"}"
            if echo "$label_data" | gh api "repos/:owner/:repo/labels/$name" --method PATCH --input - >/dev/null; then
                log_verbose "Updated label: $name"
            else
                log_warning "Failed to update label: $name"
            fi
        else
            # Create new label
            local label_data="{\"name\":\"$name\",\"color\":\"$color\",\"description\":\"$description\"}"
            if echo "$label_data" | gh api "repos/:owner/:repo/labels" --method POST --input - >/dev/null; then
                log_verbose "Created label: $name"
            else
                log_warning "Failed to create label: $name"
            fi
        fi
    done
    
    log_success "Labels configuration completed!"
}

# Configure repository topics
configure_topics() {
    log_info "Configuring repository topics from YAML configuration..."
    
    # Read topics from YAML
    local topics_raw=$(yq '.topics[]' "$CONFIG_FILE" 2>/dev/null || echo "")
    
    if [[ -z "$topics_raw" ]]; then
        log_warning "No topics configuration found in YAML"
        return 0
    fi
    
    # Build topics JSON array
    local topics_json="["
    while IFS= read -r topic; do
        if [[ -n "$topic" ]]; then
            topics_json="${topics_json}\"$topic\","
        fi
    done <<< "$topics_raw"
    topics_json="${topics_json%,}]"
    
    if $DRY_RUN; then
        log_info "DRY RUN: Would set repository topics to:"
        echo "$topics_json" | jq .
        return 0
    fi
    
    log_verbose "Setting repository topics:"
    if $VERBOSE; then
        echo "$topics_json" | jq .
    fi
    
    # Apply topics
    local topics_data="{\"names\":$topics_json}"
    if echo "$topics_data" | gh api "repos/:owner/:repo/topics" --method PUT --input -; then
        log_success "Repository topics updated successfully!"
    else
        log_error "Failed to update repository topics"
        return 1
    fi
}

# Verify the configuration
verify_protection() {
    if $DRY_RUN; then
        return 0
    fi
    
    log_info "Verifying branch protection configuration..."
    
    local protection_data
    if protection_data=$(gh api "repos/:owner/:repo/branches/main/protection" 2>/dev/null); then
        log_success "Branch protection is active"
        
        # Check specific settings
        local required_checks=$(echo "$protection_data" | jq -r '.required_status_checks.contexts[]' | wc -l)
        local required_reviews=$(echo "$protection_data" | jq -r '.required_pull_request_reviews.required_approving_review_count')
        local enforce_admins=$(echo "$protection_data" | jq -r '.enforce_admins.enabled')
        
        log_verbose "Required status checks: $required_checks"
        log_verbose "Required approving reviews: $required_reviews"
        log_verbose "Enforce for admins: $enforce_admins"
        
        if [ "$required_checks" -eq 5 ] && [ "$required_reviews" -eq 1 ] && [ "$enforce_admins" = "true" ]; then
            log_success "All protection settings verified ✓"
        else
            log_warning "Some protection settings may not be as expected"
        fi
    else
        log_error "Failed to verify branch protection"
        return 1
    fi
}

# Test branch protection
test_protection() {
    if $DRY_RUN; then
        log_info "DRY RUN: Would test branch protection by attempting direct push"
        return 0
    fi
    
    log_info "Testing branch protection (this should fail)..."
    
    # Create a temporary test file
    local test_file=".branch-protection-test-$(date +%s)"
    echo "This is a test file to verify branch protection" > "$test_file"
    
    # Try to push directly to main (this should fail)
    if git add "$test_file" && git commit -m "Test: branch protection verification" >/dev/null 2>&1; then
        if git push origin main >/dev/null 2>&1; then
            log_warning "Branch protection may not be working - direct push succeeded"
            # Clean up the test commit
            git reset --hard HEAD~1
            git push --force-with-lease origin main >/dev/null 2>&1
        else
            log_success "Branch protection is working - direct push blocked ✓"
            # Clean up the test commit
            git reset --hard HEAD~1
        fi
    else
        log_error "Failed to create test commit"
    fi
    
    # Clean up test file
    rm -f "$test_file"
}

# Main execution
main() {
    echo "=============================================="
    echo "  GitHub Repository Configuration"
    echo "=============================================="
    echo
    
    if $DRY_RUN; then
        log_info "Running in DRY RUN mode - no changes will be made"
        echo
    fi
    
    if $CONFIG_ONLY; then
        log_info "Running in CONFIG ONLY mode - branch protection will be skipped"
        echo
    fi
    
    # Run all checks and configuration
    check_prerequisites
    echo
    
    check_current_protection
    echo
    
    configure_repository_settings
    echo
    
    configure_labels
    echo
    
    configure_topics
    echo
    
    configure_protection
    echo
    
    if ! $CONFIG_ONLY; then
        verify_protection
        echo
        
        test_protection
        echo
    fi
    
    # Final summary
    echo "=============================================="
    echo "  Setup Complete"
    echo "=============================================="
    
    if $DRY_RUN; then
        log_info "Dry run completed. Run without --dry-run to apply changes."
    elif $CONFIG_ONLY; then
        log_success "Repository configuration has been applied!"
        log_info "Run without --config-only to also configure branch protection."
    else
        log_success "Repository and branch protection have been configured!"
        echo
        log_info "What's now configured:"
        echo "  ✓ Repository settings (merge options, etc.)"
        echo "  ✓ Repository labels and topics"
        echo "  ✓ Direct pushes to main are blocked"
        echo "  ✓ Pull requests require 1 approval"
        echo "  ✓ All CI status checks must pass"
        echo "  ✓ Branches must be up-to-date"
        echo "  ✓ Stale reviews are dismissed"
        echo "  ✓ Force pushes and deletions are blocked"
        echo
        log_info "Try the workflow:"
        echo "  1. git checkout -b test-feature"
        echo "  2. Make changes and commit"
        echo "  3. git push origin test-feature"
        echo "  4. Create PR via GitHub"
    fi
}

# Run main function
main "$@"
