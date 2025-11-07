# How to Release Agent365 .NET SDK

This guide explains how to create and publish releases for the Agent365 .NET SDK packages.

## 📋 Table of Contents

- [Overview](#overview)
- [Repository Structure](#repository-structure)
- [Version Management](#version-management)
- [Development Workflow (Preview Packages)](#development-workflow-preview-packages)
- [Release Workflow (Stable Packages)](#release-workflow-stable-packages)
- [Publishing Packages](#publishing-packages)
- [Troubleshooting](#troubleshooting)

---

## Overview

The Agent365 .NET SDK uses:
- **Build Repository**: `Microsoft.Agents.A365.Builds` (Azure DevOps) - Contains pipeline definitions
- **Source Repository**: `Agent365-dotnet` (GitHub) - Contains actual source code
- **Versioning**: Nerdbank.GitVersioning (nbgv) with Git tags
- **Package Feed**: `OneCRM/Agent365` (Azure Artifacts - Internal)
- **Public Feed**: NuGet.org (For stable releases)

> **Important**: All build pipelines are configured with `trigger: none`, meaning they **must be triggered manually**. Pushing code or tags to GitHub will NOT automatically start a build.

---

## Repository Structure

```
Microsoft.Agents.A365.Builds (Azure DevOps)
├── Agents-dotnet/
│   └── .pipelines/
│       ├── OneBranch.Official.yml        # Official build pipeline
│       ├── OneBranch.Nightly.yml         # Nightly build pipeline
│       ├── OneBranch.PullRequest.yml     # PR validation pipeline
│       ├── Releases/
│       │   ├── official.yaml             # Release pipeline (stable)
│       │   └── nightly.yaml              # Nightly release pipeline (preview)
│       └── templates/
│           └── build-sign-package.yaml   # Shared build template

Agent365-dotnet (GitHub)
├── src/                                  # Source code
├── version.json                          # Version configuration
└── HOW_TO_RELEASE.md                    # This file
```

---

## Version Management

### How Versioning Works

1. **Nerdbank.GitVersioning (nbgv)** calculates version from:
   - `version.json` configuration
   - Git commit history
   - Git tags
   - Current branch name

2. **Version Format**:
   - Stable: `1.0.0`
   - Preview: `1.0.0-preview.123`
   - Beta: `1.0.0-beta.1`

3. **Branch-based Versioning**:
   - `main` branch → Stable versions (no suffix)
   - Other branches → Preview versions (with `-preview` suffix)

### version.json Configuration

The `version.json` file in the repo root controls versioning:

```json
{
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/tags/v\\d+\\.\\d+\\.\\d+$"      // ONLY version tags are stable
  ]
}
```

**What this means:**
- **Git tags** matching `v1.0.0` → **Stable releases** (no suffix): `1.0.0`
- **Main branch** (no tag) → **Preview releases**: `1.0.0-preview.123`
- **Any other branch** → **Preview releases**: `1.0.0-preview.456`

### Visual Workflow

```
┌─────────────────────────────────────────────────────────────┐
│                      Main Branch                             │
│                                                              │
│  Commit A ──► Commit B ──► Commit C ──► Tag v1.0.0          │
│  (preview)    (preview)    (preview)      (STABLE)          │
│  1.0.0-       1.0.0-       1.0.0-         1.0.0            │
│  preview.1    preview.2    preview.3                         │
│                                                              │
│  ◄────── Daily Development ────────────►  ◄─ Release ─►     │
│         (Preview Packages)                (Stable Package)   │
└─────────────────────────────────────────────────────────────┘
```

**Key Point**: You work on `main` branch for everything. Only add a tag when you want a stable release.

---

## Development Workflow (Preview Packages)

### Purpose
Create preview packages for testing and development from the main branch.

### Steps

#### 1. Work on Main Branch

```bash
# Clone the repository (if not already done)
git clone https://github.com/microsoft/Agent365-dotnet.git
cd Agent365-dotnet

# Make sure you're on main branch
git checkout main
git pull origin main
```

#### 2. Make Your Changes

```bash
# Make code changes
# ... edit files ...

# Commit changes
git add .
git commit -m "feat: add new feature"

# Push to GitHub
git push origin main
```

#### 3. Trigger Build Pipeline Manually

Since the build pipelines have `trigger: none`, manually trigger a build:

1. Go to Azure DevOps: https://dev.azure.com/dynamicscrm/OneCRM/_build
2. Select pipeline: `Agents.A365-dotnet-Build-Official` (for official builds) or `Agents-dotnet-nightly` (for nightly builds)
3. Click **Run pipeline**
4. Configure run:
   - **Branch**: Select `main`
   - **Variables**: 
     - `ProjectBranch`: `main`
     - `debug`: `true` (optional, for detailed logs)
5. Click **Run**

> **Note**: Since you're building from `main` without a tag, nbgv will generate a **preview** version like `1.0.0-preview.123`

#### 4. View Build Results

1. Go to Azure DevOps: https://dev.azure.com/dynamicscrm/OneCRM/_build
2. Look for pipeline run with your commit message
3. Build creates packages with **preview** version like: `1.0.0-preview.123`
   - The number `123` is the height (number of commits) since the last version tag

#### 5. Packages Published Automatically

After successful build, the release pipeline automatically:
- Publishes to internal feed: `OneCRM/Agent365`
- Available at: https://dev.azure.com/dynamicscrm/OneCRM/_artifacts/feed/Agent365

### Expected Output

**Package names:**
```
Microsoft.Agents.A365.Notifications.1.0.0-preview.123.nupkg
Microsoft.Agents.A365.Observability.1.0.0-preview.123.nupkg
Microsoft.Agents.A365.Runtime.Common.1.0.0-preview.123.nupkg
... (and other packages)
```

**Where to find them:**
- Feed: https://dev.azure.com/dynamicscrm/OneCRM/_artifacts/feed/Agent365
- Filter by "Include prerelease" to see preview packages

---

## Release Workflow (Stable Packages)

### Purpose
Create stable production-ready packages for public consumption.

### Prerequisites

- All features merged to `main` branch
- Testing completed on preview packages
- Release notes updated in `CHANGELOG.md`
- Version number decided (e.g., `1.0.0`, `1.1.0`, `2.0.0`)

---

### Option A: Release from Main Branch (Recommended)

#### Step 1: Update Main Branch

```bash
# Switch to main branch
git checkout main
git pull origin main

# Ensure all changes are merged
git log --oneline -5
```

#### Step 2: Create and Push Git Tag

```bash
# Create an annotated tag
# Format: v{major}.{minor}.{patch}
git tag -a v1.0.0 -m "Release version 1.0.0"

# Verify tag was created
git tag -l "v1.0.0"
git show v1.0.0

# Push tag to GitHub
git push origin v1.0.0
```

> **Note**: Pushing a tag does NOT automatically trigger the build pipeline. You must manually trigger it in the next step.

#### Step 3: Trigger Official Build Manually

Manually trigger the official build pipeline:

1. Go to Azure DevOps: https://dev.azure.com/dynamicscrm/OneCRM/_build
2. Select pipeline: `Agents.A365-dotnet-Build-Official`
3. Click **Run pipeline**
4. Configure run:
   - **Branch/tag**: Select `refs/tags/v1.0.0` (or select `main` branch)
   - **Variables** (if needed):
     - `ProjectBranch`: `main` (or the branch where the tag points)
5. Click **Run**

#### Step 4: Monitor Build

1. Watch the build progress
2. Build will:
   - Checkout code from `main` branch
   - Detect version from tag: `v1.0.0`
   - Build and sign DLLs
   - Create NuGet packages with version: `1.0.0` (no preview suffix)
   - Sign NuGet packages
   - Run security scans

**Expected build time:** 10-20 minutes

#### Step 5: Verify Packages

After build succeeds, verify artifacts:

1. In Azure DevOps build results, go to **Artifacts** tab
2. Download artifact: `drop_build_Release`
3. Check `packages/` folder contains:
   ```
   Microsoft.Agents.A365.Notifications.1.0.0.nupkg
   Microsoft.Agents.A365.Observability.1.0.0.nupkg
   ...
   ```

---

### Option B: Release from Release Branch

#### Step 1: Create Release Branch

```bash
# Create release branch from main
git checkout main
git pull origin main
git checkout -b release/v1.0

# Push release branch
git push origin release/v1.0
```

#### Step 2: Tag the Release

```bash
# Create tag on release branch
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

#### Step 3: Follow Steps 3-5 from Option A

---

## Publishing Packages

### Automatic Publishing (Internal Feed)

After the build completes, the **Release Pipeline** automatically publishes packages.

#### Pipeline: `Agents.A365-dotnet-Release-Official`

**Stages:**

1. **PPE Stage (Internal Testing)**
   - Publishes to: `OneCRM/Agent365` feed
   - URL: https://dev.azure.com/dynamicscrm/OneCRM/_artifacts/feed/Agent365
   - Purpose: Internal testing and validation

2. **PROD Stage (Public Release)** _(Currently commented out)_
   - Publishes to: NuGet.org
   - Purpose: Public consumption

---

### Verify Published Packages

#### Check Internal Feed (OneCRM/Agent365)

1. Navigate to: https://dev.azure.com/dynamicscrm/OneCRM/_artifacts/feed/Agent365
2. Search for your package: `Microsoft.Agents.A365.Notifications`
3. Verify version appears: `1.0.0`
4. Check package metadata:
   - Version
   - Published date
   - Download count

#### Test Package Installation

```bash
# Add feed to NuGet sources (first time only)
dotnet nuget add source "https://pkgs.dev.azure.com/dynamicscrm/OneCRM/_packaging/Agent365/nuget/v3/index.json" \
  --name "Agent365"

# Install package
dotnet add package Microsoft.Agents.A365.Notifications --version 1.0.0
```

---

### Manual Publishing to NuGet.org (If Needed)

If the PROD stage is not enabled, manually publish:

#### Step 1: Get NuGet Packages

Download artifacts from successful build:
1. Go to build results
2. Click **Artifacts** tab
3. Download `drop_build_Release`
4. Extract `packages/` folder

#### Step 2: Publish to NuGet.org

```bash
# Get your NuGet API key from https://www.nuget.org/account/apikeys

# Publish each package
nuget push Microsoft.Agents.A365.Notifications.1.0.0.nupkg \
  -Source https://api.nuget.org/v3/index.json \
  -ApiKey YOUR_API_KEY

# Or use dotnet CLI
dotnet nuget push Microsoft.Agents.A365.Notifications.1.0.0.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY
```

#### Step 3: Verify on NuGet.org

1. Go to: https://www.nuget.org/packages/Microsoft.Agents.A365.Notifications
2. Verify new version appears
3. Wait 10-15 minutes for indexing

---

## Quick Reference Commands

### Check Version Locally

```bash
cd src
dotnet nbgv get-version
```

### Create Stable Release Tag

```bash
git checkout main
git pull origin main
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

### Create Preview Release Tag

```bash
git tag -a v1.1.0-preview.1 -m "Preview release 1.1.0"
git push origin v1.1.0-preview.1
```

### List All Tags

```bash
git tag -l
```

### Delete a Tag (If Mistake)

```bash
# Delete local tag
git tag -d v1.0.0

# Delete remote tag
git push origin --delete v1.0.0
```

---

## Troubleshooting

### Issue: Want to Enable Automatic Triggers?

**Problem:** Build pipelines require manual triggering every time

**Solution:** Update the pipeline YAML files to enable automatic triggers.

**For Official Builds (on main branch and tags):**

Edit `Microsoft.Agents.A365.Builds/Agents-dotnet/.pipelines/OneBranch.Official.yml`:

```yaml
# Change from:
trigger: none

# To:
trigger:
  branches:
    include:
    - main
  tags:
    include:
    - v*
```

**For Nightly Builds (on develop branch):**

Edit `Microsoft.Agents.A365.Builds/Agents-dotnet/.pipelines/OneBranch.Nightly.yml`:

```yaml
# Change from:
trigger: none

# To:
trigger:
  branches:
    include:
    - develop
    - feature/*
  tags:
    include:
    - v*-preview*
    - v*-beta*
```

**For PR Validation:**

Edit `Microsoft.Agents.A365.Builds/Agents-dotnet/.pipelines/OneBranch.PullRequest.yml`:

```yaml
# Change from:
trigger: none

# To:
pr:
  branches:
    include:
    - main
    - develop
```

After making these changes, pipelines will trigger automatically when:
- Code is pushed to specified branches
- Tags are pushed matching the patterns
- Pull requests are created/updated

---

### Issue: Wrong Version Number Generated

**Problem:** Package has unexpected version like `2025.11.6-preview`

**Solution:**
1. Check `version.json` exists in repo root
2. Verify Git tag format: `v1.0.0` (not `1.0.0`)
3. Ensure tag is pushed: `git push origin v1.0.0`
4. Check build logs for nbgv output

### Issue: Build Fails with "CodeSign.MissingSigningCert"

**Problem:** DLLs inside packages are not signed

**Solution:**
- This should be fixed in the pipeline
- Verify `build-sign-package.yaml` has correct signing paths
- Check signing task runs before packaging

### Issue: Packages Not Found in Feed

**Problem:** Release pipeline shows success but packages not in feed

**Solution:**
1. Check artifact name matches: `drop_build_Release`
2. Verify feed name: `OneCRM/Agent365` (project-scoped)
3. Check pipeline logs for NuGet push task
4. Verify build service has Contributor permissions on feed

### Issue: "No packages matched the search pattern"

**Problem:** Release pipeline can't find packages

**Solution:**
1. Check artifact structure in build logs
2. Verify packages are in `$(Pipeline.Workspace)/packages/`
3. Update path in release YAML if needed

### Issue: Cannot Push to NuGet.org

**Problem:** Authentication fails or package already exists

**Solution:**
1. Verify API key is valid: https://www.nuget.org/account/apikeys
2. Check package version doesn't already exist (cannot overwrite)
3. Increment version and create new tag

---

## Version Examples

| Scenario | Command/Action | Package Version | Notes |
|----------|----------------|-----------------|-------|
| Preview from main | `git push origin main` (no tag) | `1.0.0-preview.123` | For daily development |
| Preview from main | Build main branch manually | `1.0.0-preview.45` | Number = commit height |
| Beta release | `git tag v1.1.0-beta.1` + push | `1.1.0-beta.1` | Pre-release with tag |
| Release candidate | `git tag v1.2.0-rc.1` + push | `1.2.0-rc.1` | Pre-release with tag |
| Stable release | `git tag v1.0.0` + push | `1.0.0` | Production ready |
| Patch release | `git tag v1.0.1` + push | `1.0.1` | Bug fix release |
| Minor release | `git tag v1.1.0` + push | `1.1.0` | New features |
| Major release | `git tag v2.0.0` + push | `2.0.0` | Breaking changes |

---

## Additional Resources

- **Azure DevOps Organization**: https://dev.azure.com/dynamicscrm
- **Azure DevOps Project**: https://dev.azure.com/dynamicscrm/OneCRM
- **Build Pipelines**: https://dev.azure.com/dynamicscrm/OneCRM/_build
- **Artifact Feed**: https://dev.azure.com/dynamicscrm/OneCRM/_artifacts/feed/Agent365
- **GitHub Repository**: https://github.com/microsoft/Agent365-dotnet
- **Nerdbank.GitVersioning Docs**: https://github.com/dotnet/Nerdbank.GitVersioning

---

## Support

For questions or issues:
1. Check pipeline logs in Azure DevOps
2. Review this guide's Troubleshooting section
3. Contact the Agent365 team

---

*Last Updated: November 6, 2025*
