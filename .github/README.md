# GitHub Configuration for KnowShow2

This directory contains GitHub-specific configuration files that enforce our development workflow and quality standards.

## Files Overview

### Workflow Files (`.github/workflows/`)

**`ci.yml`** - Main CI/CD pipeline

- Builds and tests the .NET application
- Runs unit, integration, and contract tests
- Performs security scanning with Trivy
- Validates code formatting and linting
- Checks documentation and markdown files
- Verifies the setup script works correctly

**`branch-protection.yml`** - Branch protection enforcement

- Detects direct pushes to main branch
- Creates issues when workflow is violated
- Checks if branch protection rules are properly configured
- Provides guidance on correct workflow

### Templates

**`pull_request_template.md`** - Pull request template

- Standardized PR format
- Constitution compliance checklist
- Testing requirements
- Breaking change documentation

**`ISSUE_TEMPLATE/bug_report.yml`** - Bug report template

- Structured bug reporting
- Environment information collection
- Reproduction steps
- System compatibility check

**`ISSUE_TEMPLATE/feature_request.yml`** - Feature request template

- Feature proposal format
- Constitution compliance considerations
- Complexity estimation
- Alternative solution evaluation

### Configuration Files

**`CODEOWNERS`** - Code ownership definitions

- Defines who reviews what parts of the codebase
- Ensures appropriate expertise for code reviews
- Automatically assigns reviewers to PRs

**`branch-protection-config.md`** - Branch protection guide

- Documents required GitHub branch protection settings
- Step-by-step setup instructions
- Verification procedures
- Best practices

**`mlc_config.json`** - Markdown link checker configuration

- Configures link checking in CI
- Handles local/development URLs
- Sets retry policies and timeouts

## Git Workflow Enforcement

### Required Workflow

1. **Feature branches**: All development in `###-feature-name` branches
2. **Pull requests**: Required for all merges to main
3. **Code review**: Minimum 1 approval required
4. **Status checks**: All CI checks must pass
5. **Linear history**: Enforced through branch protection

### Branch Protection Rules

The `branch-protection-config.md` file documents the exact settings needed in GitHub repository settings. Key protections include:

- ✅ No direct pushes to main
- ✅ Pull request reviews required
- ✅ Status checks must pass
- ✅ Branch must be up-to-date
- ✅ Force pushes disabled
- ✅ Branch deletions disabled

### Automated Enforcement

The `branch-protection.yml` workflow:

- Monitors for direct pushes to main
- Creates issues when violations occur
- Checks if protection rules are enabled
- Provides remediation guidance

## Setup Instructions

### 1. Configure Branch Protection

Follow the instructions in `branch-protection-config.md` to set up GitHub branch protection rules.

### 2. Enable Workflows

Workflows are automatically enabled when files are pushed to the repository.

### 3. Configure Repository Settings

- Set default branch to `main`
- Enable "Automatically delete head branches"
- Configure merge options (recommend squash merging only)

### 4. Set up Notifications

Configure GitHub notifications for:

- Pull request reviews
- CI/CD failures
- Security alerts
- Workflow violations

## Quality Gates

All pull requests must pass these checks:

### Build and Test

- ✅ .NET application builds successfully
- ✅ All unit tests pass
- ✅ All integration tests pass
- ✅ All contract tests pass

### Code Quality

- ✅ Code formatting standards met
- ✅ No linting violations
- ✅ Static analysis passes
- ✅ No new warnings introduced

### Security

- ✅ Trivy security scan passes
- ✅ No high/critical vulnerabilities
- ✅ Dependencies are up-to-date

### Documentation

- ✅ Markdown files are well-formatted
- ✅ No broken links
- ✅ Documentation updated for changes

### Setup Verification

- ✅ Setup script works correctly
- ✅ Prerequisites are properly documented

## Troubleshooting

### Workflow Failures

1. Check the Actions tab for detailed logs
2. Review the specific failing step
3. Ensure all prerequisites are met
4. Verify environment setup

### Branch Protection Issues

1. Verify rules are configured correctly
2. Check user permissions
3. Ensure status checks are defined
4. Review admin bypass settings

### CI/CD Problems

1. Check .NET version compatibility
2. Verify test dependencies
3. Review environment variables
4. Check artifact upload/download

## Customization

### Adding New Checks

1. Add workflow steps to `ci.yml`
2. Update branch protection requirements
3. Document in `branch-protection-config.md`
4. Test with a feature branch

### Modifying Templates

1. Update template files
2. Test with sample issues/PRs
3. Document changes
4. Communicate to team

### Security Configuration

1. Review Trivy scan settings
2. Update vulnerability policies
3. Configure secret scanning
4. Set up dependency alerts

## Related Documentation

- [Git Workflow Requirements](../specs/002-build-an-application/plan.md#git-workflow-requirements)
- [Setup Guide](../SETUP.md)
- [Project Constitution](../memory/constitution.md)
- [Main README](../README.md)
