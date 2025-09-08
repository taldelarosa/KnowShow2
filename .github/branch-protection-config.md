# Branch Protection Configuration
# This file documents the automated GitHub branch protection setup for this repository.
# Branch protection is now configured via YAML and applied using GitHub CLI.

## Automated Configuration (Recommended)

### YAML-Based Configuration
Branch protection is defined in `.github/repository-config.yml` and applied using:

```bash
# Validate configuration
./scripts/validate-config.sh

# Preview changes
./scripts/setup-branch-protection.sh --dry-run

# Apply all repository settings and branch protection
./scripts/setup-branch-protection.sh

# Apply only repository settings (skip branch protection)
./scripts/setup-branch-protection.sh --config-only
```

### Prerequisites
- GitHub CLI (`gh`) installed and authenticated
- `yq` installed for YAML parsing
- `jq` installed for JSON processing
- Repository admin access

### Installation
```bash
# Install prerequisites
sudo snap install gh
sudo snap install yq
sudo apt-get install jq

# Authenticate with GitHub
gh auth login

# Run setup
./scripts/setup-branch-protection.sh
```

## Current Configuration

The YAML configuration (`.github/repository-config.yml`) defines:

### General Protection Rules
- ✅ **Restrict pushes that create files**: Enabled
- ✅ **Require a pull request before merging**: Enabled
  - ✅ **Require approvals**: 1 required reviewer
  - ✅ **Dismiss stale reviews when new commits are pushed**: Enabled
  - ✅ **Require review from code owners**: Enabled (when CODEOWNERS file is present)
  - ✅ **Restrict reviews to users with write access**: Enabled
  - ✅ **Allow specified actors to bypass required pull requests**: Disabled

### Status Check Requirements
- ✅ **Require status checks to pass before merging**: Enabled
- ✅ **Require branches to be up to date before merging**: Enabled
- ✅ **Status checks that are required**:
  - `build-and-test`
  - `verify-setup`
  - `security-scan`
  - `lint-and-format`
  - `docs-check`

### Additional Restrictions
- ✅ **Restrict pushes that create files**: Enabled
- ✅ **Require signed commits**: Recommended (optional)
- ✅ **Require linear history**: Enabled (enforces squash or rebase merging)
- ✅ **Require deployments to succeed**: Disabled (not applicable)
- ✅ **Lock branch**: Disabled
- ✅ **Do not allow bypassing the above settings**: Enabled
- ✅ **Allow force pushes**: Disabled
- ✅ **Allow deletions**: Disabled

## Administrative Settings

### Repository Settings
- **Default branch**: `main`
- **Merge button options**:
  - ✅ **Allow merge commits**: Disabled
  - ✅ **Allow squash merging**: Enabled (recommended)
  - ✅ **Allow rebase merging**: Enabled
- **Automatically delete head branches**: Enabled

### Collaborator Permissions
- **Base permissions**: Read
- **Admin access**: Repository owner only
- **Write access**: Trusted contributors only
- **Maintain access**: Core team members

## Setup Instructions

1. Navigate to repository Settings → Branches
2. Click "Add rule" or edit existing rule for `main` branch
3. Configure all settings listed above
4. Save the branch protection rule

## Enforcement

These settings ensure:
- No direct pushes to main branch
- All changes go through pull request review
- All CI/CD checks must pass
- Code quality standards are maintained
- History remains clean and linear

## Bypassing Rules

Branch protection rules should NOT be bypassed except in emergency situations by repository administrators. Any bypass should be:
1. Documented with reasoning
2. Reviewed afterward
3. Followed by immediate process improvement

## Verification

To verify branch protection is working:
1. Try to push directly to main (should fail)
2. Create a feature branch and PR
3. Attempt to merge without approvals (should fail)
4. Verify all status checks are required

## Related Files
- `.github/workflows/ci.yml` - Defines the CI/CD pipeline
- `.github/pull_request_template.md` - PR template
- `.github/ISSUE_TEMPLATE/` - Issue templates
- `CODEOWNERS` - Code ownership (if created)
