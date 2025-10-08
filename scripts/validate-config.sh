#!/usr/bin/env bash
# Validate the repository configuration YAML file
# Usage: ./scripts/validate-config.sh

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration file path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/../.github/repository-config.yml"

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

# Check if yq is installed
check_yq() {
    if ! command -v yq >/dev/null 2>&1; then
        log_error "yq is not installed (required for YAML parsing)"
        log_info "Install it with: sudo snap install yq"
        exit 1
    fi
}

# Validate YAML syntax
validate_yaml_syntax() {
    log_info "Validating YAML syntax..."
    
    if ! yq '.' "$CONFIG_FILE" >/dev/null 2>&1; then
        log_error "Invalid YAML syntax in $CONFIG_FILE"
        return 1
    fi
    
    log_success "YAML syntax is valid"
}

# Validate required sections
validate_required_sections() {
    log_info "Validating required configuration sections..."
    
    local errors=0
    
    # Check repository section
    if [[ "$(yq '.repository' "$CONFIG_FILE")" == "null" ]]; then
        log_error "Missing 'repository' section"
        ((errors++))
    fi
    
    # Check branch protection section
    if [[ "$(yq '.branch_protection' "$CONFIG_FILE")" == "null" ]]; then
        log_error "Missing 'branch_protection' section"
        ((errors++))
    fi
    
    # Check main branch protection
    if [[ "$(yq '.branch_protection.main' "$CONFIG_FILE")" == "null" ]]; then
        log_error "Missing 'branch_protection.main' section"
        ((errors++))
    fi
    
    if [[ $errors -eq 0 ]]; then
        log_success "All required sections are present"
    else
        log_error "Found $errors missing required sections"
        return 1
    fi
}

# Validate branch protection configuration
validate_branch_protection() {
    log_info "Validating branch protection configuration..."
    
    local errors=0
    
    # Check required pull request reviews
    local required_reviews=$(yq '.branch_protection.main.required_pull_request_reviews.required_approving_review_count' "$CONFIG_FILE")
    if [[ "$required_reviews" == "null" ]]; then
        log_error "Missing required_approving_review_count"
        ((errors++))
    elif [[ $required_reviews -lt 1 ]]; then
        log_warning "required_approving_review_count is less than 1 (current: $required_reviews)"
    fi
    
    # Check required status checks
    local status_checks=$(yq '.branch_protection.main.required_status_checks.contexts' "$CONFIG_FILE")
    if [[ "$status_checks" == "null" ]]; then
        log_error "Missing required status check contexts"
        ((errors++))
    else
        local contexts_count=$(yq '.branch_protection.main.required_status_checks.contexts | length' "$CONFIG_FILE")
        if [[ $contexts_count -eq 0 ]]; then
            log_warning "No required status check contexts defined"
        else
            log_success "Found $contexts_count required status check contexts"
        fi
    fi
    
    # Check security settings
    local enforce_admins=$(yq '.branch_protection.main.enforce_admins' "$CONFIG_FILE")
    if [[ "$enforce_admins" != "true" ]]; then
        log_warning "enforce_admins is not set to true (current: $enforce_admins)"
    fi
    
    local allow_force_pushes=$(yq '.branch_protection.main.allow_force_pushes' "$CONFIG_FILE")
    if [[ "$allow_force_pushes" == "true" ]]; then
        log_warning "allow_force_pushes is enabled (security risk)"
    fi
    
    local allow_deletions=$(yq '.branch_protection.main.allow_deletions' "$CONFIG_FILE")
    if [[ "$allow_deletions" == "true" ]]; then
        log_warning "allow_deletions is enabled (security risk)"
    fi
    
    if [[ $errors -eq 0 ]]; then
        log_success "Branch protection configuration is valid"
    else
        log_error "Found $errors issues in branch protection configuration"
        return 1
    fi
}

# Validate repository settings
validate_repository_settings() {
    log_info "Validating repository settings..."
    
    local errors=0
    
    # Check merge settings
    local allow_merge_commit=$(yq '.repository.settings.allow_merge_commit' "$CONFIG_FILE")
    local allow_squash_merge=$(yq '.repository.settings.allow_squash_merge' "$CONFIG_FILE")
    local allow_rebase_merge=$(yq '.repository.settings.allow_rebase_merge' "$CONFIG_FILE")
    
    if [[ "$allow_merge_commit" == "true" && "$allow_squash_merge" == "true" ]]; then
        log_warning "Both merge commits and squash merging are enabled"
    fi
    
    if [[ "$allow_merge_commit" == "false" && "$allow_squash_merge" == "false" && "$allow_rebase_merge" == "false" ]]; then
        log_error "All merge options are disabled - contributors won't be able to merge PRs"
        ((errors++))
    fi
    
    if [[ $errors -eq 0 ]]; then
        log_success "Repository settings are valid"
    else
        log_error "Found $errors issues in repository settings"
        return 1
    fi
}

# Validate labels configuration
validate_labels() {
    log_info "Validating labels configuration..."
    
    local labels_count=$(yq '.labels | length' "$CONFIG_FILE")
    
    if [[ "$labels_count" == "0" || "$labels_count" == "null" ]]; then
        log_warning "No labels defined"
        return 0
    fi
    
    local errors=0
    
    for i in $(seq 0 $((labels_count - 1))); do
        local name=$(yq ".labels[$i].name" "$CONFIG_FILE")
        local color=$(yq ".labels[$i].color" "$CONFIG_FILE")
        local description=$(yq ".labels[$i].description" "$CONFIG_FILE")
        
        if [[ "$name" == "null" ]]; then
            log_error "Label at index $i is missing 'name'"
            ((errors++))
        fi
        
        if [[ "$color" == "null" ]]; then
            log_error "Label '$name' is missing 'color'"
            ((errors++))
        elif [[ ! "$color" =~ ^[0-9a-fA-F]{6}$ ]]; then
            log_error "Label '$name' has invalid color format: '$color' (should be 6-digit hex)"
            ((errors++))
        fi
        
        if [[ "$description" == "null" ]]; then
            log_warning "Label '$name' is missing description"
        fi
    done
    
    if [[ $errors -eq 0 ]]; then
        log_success "Found $labels_count valid labels"
    else
        log_error "Found $errors issues in labels configuration"
        return 1
    fi
}

# Show configuration summary
show_summary() {
    log_info "Configuration Summary:"
    echo
    
    # Repository info
    local repo_name=$(yq '.repository.name' "$CONFIG_FILE")
    local repo_desc=$(yq '.repository.description' "$CONFIG_FILE")
    echo "Repository: $repo_name"
    echo "Description: $repo_desc"
    echo
    
    # Branch protection summary
    local required_reviews=$(yq '.branch_protection.main.required_pull_request_reviews.required_approving_review_count' "$CONFIG_FILE")
    local contexts_count=$(yq '.branch_protection.main.required_status_checks.contexts | length' "$CONFIG_FILE")
    echo "Branch Protection:"
    echo "  - Required reviews: $required_reviews"
    echo "  - Required status checks: $contexts_count"
    echo "  - Status checks:"
    yq '.branch_protection.main.required_status_checks.contexts[]' "$CONFIG_FILE" | sed 's/^/    - /'
    echo
    
    # Labels summary
    local labels_count=$(yq '.labels | length' "$CONFIG_FILE")
    echo "Labels: $labels_count defined"
    
    # Topics summary
    local topics_count=$(yq '.topics | length' "$CONFIG_FILE")
    echo "Topics: $topics_count defined"
}

# Main execution
main() {
    echo "=============================================="
    echo "  Repository Configuration Validator"
    echo "=============================================="
    echo
    
    log_info "Validating configuration file: $CONFIG_FILE"
    echo
    
    # Check prerequisites
    check_yq
    
    # Check if config file exists
    if [[ ! -f "$CONFIG_FILE" ]]; then
        log_error "Configuration file not found: $CONFIG_FILE"
        exit 1
    fi
    
    # Run all validations
    local total_errors=0
    
    validate_yaml_syntax || ((total_errors++))
    echo
    
    validate_required_sections || ((total_errors++))
    echo
    
    validate_branch_protection || ((total_errors++))
    echo
    
    validate_repository_settings || ((total_errors++))
    echo
    
    validate_labels || ((total_errors++))
    echo
    
    show_summary
    echo
    
    # Final result
    echo "=============================================="
    if [[ $total_errors -eq 0 ]]; then
        log_success "✅ Configuration validation passed!"
        echo "Run './scripts/setup-branch-protection.sh --dry-run' to preview changes"
    else
        log_error "❌ Configuration validation failed with $total_errors error(s)"
        echo "Please fix the issues above before applying the configuration"
        exit 1
    fi
}

# Run main function
main "$@"
