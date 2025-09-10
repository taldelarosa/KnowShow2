# GitHub Repository Configuration as Infrastructure as Code
# This Terraform configuration manages GitHub repository settings including branch protection
#
# Usage:
#   terraform init
#   terraform plan
#   terraform apply
#
# Prerequisites:
#   - Terraform installed
#   - GitHub provider configured with token
#   - Repository already exists

terraform {
  required_version = ">= 1.0"
  
  required_providers {
    github = {
      source  = "integrations/github"
      version = "~> 5.0"
    }
  }
}

# Configure the GitHub Provider
provider "github" {
  # Token can be set via GITHUB_TOKEN environment variable
  # or configured here (not recommended for security)
}

# Repository data source (assumes repository already exists)
data "github_repository" "repo" {
  full_name = "taldelarosa/KnowShow2"
}

# Configure repository settings
resource "github_repository" "repo" {
  name        = "KnowShow2"
  description = "Episode Identifier - PGS Subtitle Extraction for AV1 videos"
  
  # Repository settings
  visibility          = "public"
  has_issues          = true
  has_projects        = true
  has_wiki           = false
  has_downloads      = true
  auto_init          = false
  
  # Merge settings
  allow_merge_commit     = false
  allow_squash_merge     = true
  allow_rebase_merge     = true
  allow_auto_merge       = false
  delete_branch_on_merge = true
  
  # Security settings
  vulnerability_alerts                = true
  ignore_vulnerability_alerts_during_read = false
  
  # Branch settings
  default_branch = "main"
  
  # Template settings for issues and PRs
  has_discussions = false
  
  # Topics for discoverability
  topics = [
    "video-processing",
    "subtitle-extraction",
    "pgs",
    "av1",
    "episode-identification",
    "csharp",
    "dotnet",
    "cli-tool"
  ]
}

# Main branch protection rule
resource "github_branch_protection" "main" {
  repository_id = github_repository.repo.node_id
  pattern       = "main"
  
  # Require pull requests
  required_pull_request_reviews {
    required_approving_review_count      = 1
    dismiss_stale_reviews               = true
    restrict_dismissals                 = false
    require_code_owner_reviews          = true
    require_last_push_approval          = false
    pull_request_bypassers              = []
  }
  
  # Required status checks
  required_status_checks {
    strict   = true
    contexts = [
      "build-and-test",
      "verify-setup",
      "security-scan", 
      "lint-and-format",
      "docs-check"
    ]
  }
  
  # Additional restrictions
  enforce_admins                = true
  allows_deletions             = false
  allows_force_pushes          = false
  require_signed_commits       = false
  require_conversation_resolution = true
  
  # No specific push restrictions (anyone with write access can create PRs)
  push_restrictions = []
}

# Repository ruleset for additional protection (GitHub Enterprise feature)
# Uncomment if using GitHub Enterprise
/*
resource "github_repository_ruleset" "main" {
  name        = "main-branch-protection"
  repository  = github_repository.repo.name
  target      = "branch"
  enforcement = "active"
  
  conditions {
    ref_name {
      include = ["refs/heads/main"]
      exclude = []
    }
  }
  
  rules {
    # Prevent direct pushes
    creation                = true
    update                 = true
    deletion               = true
    required_linear_history = true
    
    # Pull request requirements
    pull_request {
      required_approving_review_count   = 1
      dismiss_stale_reviews_on_push    = true
      require_code_owner_review        = true
      require_last_push_approval       = false
      required_review_thread_resolution = true
    }
    
    # Status check requirements
    required_status_checks {
      required_check {
        context = "build-and-test"
      }
      required_check {
        context = "verify-setup"
      }
      required_check {
        context = "security-scan"
      }
      required_check {
        context = "lint-and-format"
      }
      required_check {
        context = "docs-check"
      }
      strict_required_status_checks_policy = true
    }
  }
}
*/

# Configure issue labels
resource "github_issue_label" "bug" {
  repository  = github_repository.repo.name
  name        = "bug"
  color       = "d73a4a"
  description = "Something isn't working"
}

resource "github_issue_label" "enhancement" {
  repository  = github_repository.repo.name
  name        = "enhancement"
  color       = "a2eeef"
  description = "New feature or request"
}

resource "github_issue_label" "needs_triage" {
  repository  = github_repository.repo.name
  name        = "needs-triage"
  color       = "fbca04"
  description = "Needs initial review and categorization"
}

resource "github_issue_label" "workflow_violation" {
  repository  = github_repository.repo.name
  name        = "workflow-violation"
  color       = "b60205"
  description = "Violates project workflow requirements"
}

resource "github_issue_label" "urgent" {
  repository  = github_repository.repo.name
  name        = "urgent"
  color       = "ff0000"
  description = "Requires immediate attention"
}

resource "github_issue_label" "needs_attention" {
  repository  = github_repository.repo.name
  name        = "needs-attention"
  color       = "ff9500"
  description = "Requires maintainer attention"
}

resource "github_issue_label" "constitution_compliant" {
  repository  = github_repository.repo.name
  name        = "constitution-compliant"
  color       = "0e8a16"
  description = "Follows project constitution principles"
}

# Repository secrets for CI/CD (if needed)
# Note: Sensitive values should be set outside of Terraform
/*
resource "github_actions_secret" "example_secret" {
  repository       = github_repository.repo.name
  secret_name     = "EXAMPLE_SECRET"
  plaintext_value = var.example_secret_value
}
*/

# Variables for customization
variable "enable_enterprise_features" {
  description = "Enable GitHub Enterprise features like repository rulesets"
  type        = bool
  default     = false
}

variable "required_status_checks" {
  description = "List of required status check contexts"
  type        = list(string)
  default = [
    "build-and-test",
    "verify-setup", 
    "security-scan",
    "lint-and-format",
    "docs-check"
  ]
}

variable "required_reviewers" {
  description = "Number of required approving reviews"
  type        = number
  default     = 1
}

# Outputs for verification
output "repository_url" {
  description = "URL of the configured repository"
  value       = github_repository.repo.html_url
}

output "branch_protection_url" {
  description = "URL to view branch protection settings"
  value       = "${github_repository.repo.html_url}/settings/branches"
}

output "protection_summary" {
  description = "Summary of branch protection configuration"
  value = {
    repository_name     = github_repository.repo.name
    protected_branch    = github_branch_protection.main.pattern
    required_reviews    = github_branch_protection.main.required_pull_request_reviews[0].required_approving_review_count
    status_checks       = github_branch_protection.main.required_status_checks[0].contexts
    enforce_admins      = github_branch_protection.main.enforce_admins
    allows_force_pushes = github_branch_protection.main.allows_force_pushes
    allows_deletions    = github_branch_protection.main.allows_deletions
  }
}
