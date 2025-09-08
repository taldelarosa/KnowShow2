# Branch Protection Configuration
# This file documents the required GitHub branch protection settings for this repository.
# These settings must be configured manually in the GitHub repository settings.

## Main Branch Protection Rules

Configure the following settings for the `main` branch in GitHub repository settings:

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
