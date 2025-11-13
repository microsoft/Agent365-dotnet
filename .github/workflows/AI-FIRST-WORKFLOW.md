# AI-First Workflow Documentation

## Overview

The **AI-First Workflow** is an automated SDK parity enforcement system that ensures feature consistency across all Agent365 SDKs. This workflow runs in the **Agent365-dotnet** repository (C# SDK). When a developer makes changes to the C# SDK, this workflow automatically creates tracking issues in the corresponding Python and Node.js SDK repositories, with GitHub Copilot agent assigned to implement them.

## 🎯 Purpose

To maintain feature parity across multiple SDK implementations by:
- Automatically detecting C# SDK changes in pull requests
- Creating parity issues in the Python (Agent365-python) and Node.js (Agent365-nodejs) repositories
- Assigning issues to GitHub Copilot agent for automated implementation
- Providing rich context to enable high-quality automated code generation
- Keeping teams informed through automated PR comments

## 🔄 Trigger Conditions

The workflow triggers on:
- **Event Type:** Pull request events (opened, synchronize, reopened, labeled)
- **Target Branches:**
  - `users/*/aiFirstExperiments`
  - `users/*/aiFirstExperiments/**`
  - `aiFirstExperiments`
  - `aiFirstExperiments/**`
- **Path Filters:** Only when C# SDK implementation files are modified:
  - `src/**/*.cs` (excluding test files)

**Important:** The workflow does NOT run on:
- Draft pull requests
- Pull requests authored by bots (except when re-triggered by label)
- Pull requests without the `codegen-experiment` label
- Changes to test files (`**/Tests/**`, `*.Tests.cs`)
- Changes to documentation, samples, or build files
- Changes outside the `src/` directory

**Concurrency Control:**
- The workflow uses per-PR concurrency grouping (`ai-first-{PR_NUMBER}`)
- If a new run starts for the same PR, the previous run is automatically cancelled
- This prevents race conditions and duplicate issue creation when multiple commits are pushed quickly

## 📋 Workflow Steps

### Step 0: Check Prerequisites

A dedicated job that validates whether the workflow should run. This job has three checks:

**Draft PR Check**
- Immediately exits if PR is in draft status
- Prevents unnecessary processing and issue creation during development
- Ensures workflow only runs on ready-for-review PRs
- Returns gracefully with success status (not a failure)

**Bot Author Check**
- Immediately exits if PR author is a bot (e.g., `dependabot[bot]`, GitHub Actions)
- **Exception:** Allows PRs from `copilot-swe-agent[bot]` to proceed
- Prevents infinite loops where bot PRs trigger more parity issues
- Returns gracefully with success status

**Label Check**
- Checks if PR has the `codegen-experiment` label
- Only PRs with this label will trigger the workflow
- Allows selective enablement of the AI-First workflow
- Returns gracefully with success status if label is missing

**Job Output:**
- `should_run`: Combines all three checks into a single boolean
- Main job only runs if all checks pass: `should_run == 'true'`

### Step 1: Initial Setup and Validation

**Checkout Repository**
- Uses `actions/checkout@v4` with full history (`fetch-depth: 0`)
- Enables git diff analysis across branches

### Step 2: Detect C# SDK Changes

**Change Detection:**
- Analyzes files changed in the PR using `git diff`
- Checks for C# implementation files under `src/**/*.cs`
- Excludes test files (`**/Tests/**`, `*.Tests.cs`)

**Output Variables:**
- Sets GitHub Actions outputs based on detected changes:
  - `has_changes`: `true` if C# SDK files were modified, `false` otherwise
  - `source_languages`: `csharp` if changes detected, empty otherwise
  - `target_languages`: `python,typescript` if changes detected, empty otherwise

**Repository Mapping:**
- Python issues created in: `microsoft/Agent365-python`
- TypeScript issues created in: `microsoft/Agent365-nodejs`

**Why Detection Still Matters:**
Even though the workflow triggers on path filters, it still validates that meaningful SDK changes exist. This prevents parity issues from being created for:
- PRs that only modify test files
- PRs with changes in non-SDK directories
- Edge cases where path filters might not catch everything

### Step 3: Create Parity Issues

**Multi-Repository Support:**

The workflow creates parity issues in separate repositories for each SDK platform. This is configured via environment variables at the workflow level:

```yaml
env:
  REPO_MAP_PYTHON: "microsoft/Agent365-python"
  REPO_MAP_TYPESCRIPT: "microsoft/Agent365-nodejs"
```

Since this is the C# repository (Agent365-dotnet), parity issues are always created in the Python and Node.js repositories.

**Repository Determination:**
For each target language, the workflow:
1. Maps language to repository using environment variables
2. Falls back to current repository if no specific repo configured
3. Logs target repository for visibility

**Issue References:**
- Same repository: `#123`
- Cross repository: `owner/repo#123`

**For Each Target Language:**

#### Issue Title Format
```
[SDK Parity] {SDK_NAME} for PR #{PR_NUMBER}
```

Examples:
- `[SDK Parity] .NET/C# for PR 485`
- `[SDK Parity] Node.js/TypeScript for PR 485`
- `[SDK Parity] Python for PR 485`

#### Issue Body Structure

**Parent Issue Reference (if detected):**
- If the source PR links to a parent issue (using `Closes #123` or `Closes owner/repo#456`), a reference is included
- Supports both same-repository and cross-repository parent issues
- Format: `This parity task is related to issue owner/repo#123`

**Source PR Information:**
- Original PR number, title, and URL
- PR author (GitHub username)
- Full PR description for context

**Parity Task Details:**
- Source SDK (what was changed)
- Target SDK (what needs updating)
- Clear action required statement

**Language-Specific Implementation Guidelines:**

Each SDK gets tailored guidance instructing the Copilot agent to:
- Review existing code patterns in the target SDK directory
- Follow the coding style and naming conventions already in use
- Check configuration files for coding standards (e.g., `.editorconfig`, `tsconfig.json`, `pyproject.toml`)
- Ensure consistency with existing SDK components
- Include appropriate tests following established patterns

**Related Information:**
- Workflow run link for traceability
- Trigger event, branch, and commit SHA
- Instructions for skipping parity (close with `wontfix` label)

#### Duplicate Prevention

Before creating an issue, the workflow checks in the **target repository**:

1. **Open Issues:** Searches for existing open issues with the same title
   - If found: Reuses the existing issue (adds to `EXISTING_ISSUES` list)
   
2. **Closed Issues with `wontfix`:** Searches for closed issues with the same title and `wontfix` label
   - If found: Skips creation (respects the decision that parity is not needed)

3. **No Existing Issues:** Creates a new issue (adds to `NEW_ISSUES` list)

**Issue Tracking:**
- `NEW_ISSUES`: Issues created in the current workflow run
- `EXISTING_ISSUES`: Issues that already existed before this run
- `PARITY_ISSUES`: All issues (new + existing combined)

This distinction is important for later steps that handle updates differently based on whether issues are new or pre-existing.

This prevents:
- Duplicate issue spam
- Re-creating issues that were intentionally closed
- Overwhelming the Copilot agent with redundant work

#### Assignment and Labels

**Assignee:** `copilot-swe-agent`
- GitHub Copilot agent that will implement the parity changes
- Must be a repository collaborator to be successfully assigned

**Labels:** `copilot`, `codegen-experiment`
- `copilot`: Tags the issue for Copilot-related automation
- `codegen-experiment`: Identifies this as part of the AI-First code generation experiment
- Helps with issue tracking, filtering, and metrics collection

#### Error Handling

The workflow provides detailed error messages for common issues:
- Token not configured
- No source language detected
- Multiple source languages detected
- Issue creation failures

### Step 4: Wait for Copilot PRs and Auto-Assignment

**Purpose:** Monitors created parity issues for Copilot-generated PRs and automatically assigns them to the original PR author

**Multi-Repository Support:**
The workflow monitors PRs across different repositories based on where issues were created. It:
- Parses issue references to extract repository and issue number (e.g., `owner/repo#123`)
- Searches for PRs in the correct repository
- Assigns users and posts comments in the target repository
- Requires PAT token to have permissions across all involved repositories

**Polling Strategy:**
- **Max Wait Time:** 5 minutes (300 seconds)
- **Check Interval:** 30 seconds (10 polling attempts)
- **What it monitors:** Each parity issue for linked PRs created by `copilot-swe-agent[bot]`

**How It Works:**

1. **Issue Monitoring:** For each created parity issue, searches for open PRs that:
   - Are authored by `copilot-swe-agent[bot]`
   - Reference the issue number in the PR body (e.g., "Closes #123")
   - Are in the same repository as the issue

2. **Auto-Assignment/Review Request:** When a Copilot PR is detected:
   
   **If Original PR is Human-Authored:**
   - Checks if the human author is already assigned (idempotent operation)
   - If not assigned: Assigns the PR to the original PR author
   - Posts a comment explaining the auto-assignment and requesting review
   
   **If Original PR is Copilot-Authored:**
   - Recognizes that assigning Copilot to review Copilot's work is not useful
   - Retrieves human assignees and reviewers from the source PR
   - Adds those humans as reviewers on the parity PR
   - Posts a comment explaining the reviewer chain
   - If no human reviewers found: Posts a comment requesting manual assignment
   
   **In All Cases:**
   - Marks the issue as processed (won't check again)

3. **Progress Tracking:** Logs detailed progress:
   - Which issues are being monitored (with repository information)
   - When PRs are detected
   - Assignment/reviewer request success/failure
   - Remaining unprocessed issues

**Timeout Behavior:**
- ✅ **All PRs found:** Workflow completes after assigning all PRs (may finish before 5 minutes)
- ⏱️ **Timeout:** After 5 minutes, lists unprocessed issues and continues
- **Non-Blocking:** Timeout does not fail the workflow
- **Fallback:** The separate `assign-copilot-prs.yml` workflow serves as a backup for any missed assignments

**Why This Matters:**
- **Automatic notification:** Original author or their reviewers get immediately assigned when Copilot creates the PR
- **Right reviewer:** People who know the feature best review the parity implementation
- **Prevents Copilot-to-Copilot loops:** Avoids assigning Copilot to review its own generated code
- **No manual work:** Eliminates need to manually find and assign Copilot PRs
- **Accountability chain:** Maintains traceability from feature → implementation → parity → review

**Comments Posted on Copilot PR:**

*When original PR is human-authored:*
```markdown
## 🤖 Auto-Assignment

This PR was automatically assigned to @username for review.

**Reason:** This is a parity implementation for issue #123, which was 
triggered by @username's original PR.

**Next Steps:**
- @username: Please review this implementation to ensure it matches your intent
- Validate the parity changes are correct and complete
- Approve and merge when satisfied
```

*When original PR is Copilot-authored (with human reviewers found):*
```markdown
## 🤖 Auto-Assignment

This PR is part of a parity chain that started with a Copilot-generated PR.

**Human reviewers from the original PR have been added:** @alice, @bob

**Reason:** This is a parity implementation for issue #123, which was 
triggered by Copilot's PR #456.

**Next Steps:**
- Reviewers: Please review this implementation to ensure it maintains parity
- Validate the parity changes are correct and complete
- Approve and merge when satisfied
```

*When original PR is Copilot-authored (no human reviewers found):*
```markdown
## 🤖 Auto-Assignment

This PR is part of a parity chain that started with a Copilot-generated PR (#456).

**Note:** No human reviewers were found on the source PR, so this PR was not 
automatically assigned.

**Action Required:**
- Please manually assign reviewers who can validate this parity implementation
- Ensure the changes maintain consistency with the source PR
```

### Step 5: Notify Existing Copilot PRs About New Changes

**Purpose:** When new commits are pushed to the original PR, notify any existing Copilot PRs that may need updates

**Condition:** 
- Only runs on `synchronize` event (new commits pushed)
- Only runs if `has_changes == true` (C# SDK changes detected)
- Only processes `EXISTING_ISSUES` (not newly created issues)
- Skips if all issues were just created in the current run

**Why This Matters:**
When a developer pushes additional commits to their PR after parity issues were created:
- Copilot may have already started or completed work on parity PRs
- Those parity PRs need to incorporate the new changes
- Manual notification would be tedious and error-prone

**How It Works:**

1. **Issue Filtering:** 
   - Parses `EXISTING_ISSUES` variable (issues that existed before this workflow run)
   - Skips `NEW_ISSUES` (no PRs can exist yet for brand new issues)
   - Extracts repository and issue number from each reference

2. **PR Discovery:**
   - Searches each target repository for open PRs by `copilot-swe-agent[bot]`
   - Filters PRs that reference the specific issue number
   - Validates PR exists before attempting notification

3. **Smart Notification:**
   - **First notification:** Posts detailed update with @copilot mention, links to new commits, actionable instructions
   - **Subsequent notifications:** Posts brief follow-up comment (avoids spam)
   - Checks for existing "🔄 Original PR Updated" comment to determine which type to post

4. **Cross-Repository Support:**
   - Handles PRs in different repositories from the source PR
   - Uses full repository qualifiers in messages (e.g., `microsoft/Agent365#123`)

**First Notification Comment:**
```markdown
## 🔄 Original PR Updated

@copilot The original PR microsoft/Agent365#123 has been updated with new commits.

**Action Required:**
Please review the updated PR and ensure this parity implementation includes all relevant changes:

1. 📖 **Review the latest changes**: [link to commits]
2. 🔍 **Check for new features or fixes**: Look for additions that need to be ported
3. ✏️ **Update this PR if needed**: Add any missing functionality to maintain parity
4. ✅ **Verify completeness**: Ensure all changes from the original PR are reflected here

**Original PR Details:**
- **PR:** microsoft/Agent365#123
- **Author:** @username
- **Latest commit:** `abc1234`
- **View changes:** [link to files]

**Note:** This is an automated notification triggered by new commits. 
If the changes are not relevant to this parity task, you can ignore this message.
```

**Follow-up Notification Comment:**
```markdown
## 🔄 Additional Changes Detected

New commits have been pushed to the original PR #123.

**Action Required:**
- Review the latest changes: [link]
- Update this PR if necessary to maintain parity
```

**Benefits:**
- Keeps Copilot agents informed of source PR changes
- Reduces risk of parity PRs becoming stale
- Provides actionable instructions for updating
- Avoids notification spam with deduplication logic

### Step 6: Update Parent Issue with Task List

**Condition:** Only runs if a parent issue was detected and parity issues were created

**Cross-Repository Parent Issue Support:**
The workflow supports linking to parent issues in any repository. It automatically detects:
- **Same-repo references:** `Closes #123`, `Fixes #456`, `Resolves #789`
- **Cross-repo references:** `Closes microsoft/Agent365#123`, `Fixes owner/repo#456`

The workflow will post the task list comment to the correct repository, regardless of where the parent issue lives.

**Task List Creation:**
- Detects if the triggering PR links to an issue using GitHub's standard linking keywords
- Parses both same-repo (`#123`) and cross-repo (`owner/repo#123`) formats
- Posts a comment on that parent issue with checkboxes for each parity issue
- Enables tracking of parity implementation progress across repositories

**Example PR Body References:**
```markdown
Closes microsoft/planning-repo#123
Fixes #456
Resolves owner/feature-tracker#789
```

**Comment Format:**
```markdown
## 🔄 SDK Parity Tracking

The following parity issues have been created to maintain SDK consistency:

- [ ] microsoft/python-agent-sdk#10
- [ ] microsoft/Agent365#485
- [ ] #486

*Updated by [AI-First Workflow](...)*
```

**Note:** Parity issue references may include repository prefixes if they were created in different repositories (based on `REPO_MAP_*` configuration).

**Benefits:**
- Centralizes parity tracking on the original feature request
- Provides visibility into cross-SDK implementation status
- Enables automated tracking and reporting

### Step 7: Post Comment on PR

**Always Runs:** This step runs regardless of whether issues were created

**PR Number Detection:**
- For `pull_request` events: Uses event context directly
- For `push` events: Searches for PR with matching head branch
- If no PR found on push: Gracefully exits (direct branch push)

**Comment Scenarios:**

The workflow posts one of four comment types depending on the situation:

#### When Parity Issues Are Created (New and/or Existing)
- Lists the C# SDK as the source and target SDKs needing updates (Python and Node.js)
- Distinguishes between:
  - **New parity issues:** Just created in this workflow run
  - **Existing open issues:** Already existed before this run
  - **Both:** Mixed situation with new and existing
- Provides links to all parity issues (new + existing)
- Explains the Copilot agent will work on them
- Notes that human review will be required
- Includes instructions for skipping parity (close issue with `wontfix` label)

**Example (Mixed New and Existing):**
```markdown
## 🤖 SDK Parity Automation

### Summary
This PR modifies the **C#** SDK. To maintain feature parity across all SDKs, 
new parity issues have been created, and existing open issues were found, for the 
following target SDKs:

- Python
- Node.js/TypeScript

### What happens next?
1. ✅ **Parity issues ready**: GitHub Copilot agent (copilot-swe-agent) is assigned to these issues
2. 🤖 **Copilot will work on them**: The agent will analyze your changes and generate corresponding PRs
3. 👀 **Review required**: Once Copilot creates the PRs, they will need human review before merging

### Parity Issues
- #485
- #486

### Need to skip parity?
If parity is not needed for a particular SDK, close the corresponding issue with the `wontfix` label.
```

#### When No SDK Changes Detected
- This can occur when the PR only modifies test files or other excluded patterns
- The workflow triggers (due to path filters), but detection finds no SDK implementation changes
- Workflow runs successfully but skips parity issue creation
- A comment is posted explaining that no SDK implementation changes were detected

## 🔐 Authentication and Permissions

### Required Secret
**Name:** `PAT_TOKEN_CODEGEN_EXPERIMENT`
- **Type:** Personal Access Token (PAT)
- **Purpose:** Authenticates with GitHub API for operations requiring elevated permissions
- **Required Scopes:**
  - `repo` (full repository access)
  - `write:org` (if assigning to organization members)

**Used For:**
- Creating issues with bot assignees (`copilot-swe-agent[bot]`)
- Adding reviewers/assignees to PRs (requires PAT for bot/user operations)
- @mentioning users (e.g., `@copilot`) to ensure notifications are sent
- Querying PR data for reviewer extraction

**Multi-Repository Support:**
The workflow operates across three repositories:
- **Agent365-dotnet** (C# SDK - current repository)
- **Agent365-python** (Python SDK - target repository)
- **Agent365-nodejs** (Node.js SDK - target repository)

The PAT must have:
- Access to all three repositories
- Collaborator status in Agent365-python and Agent365-nodejs
- Sufficient permissions to create issues, edit PRs, and post comments in target repositories

### Default Token Usage
**Token:** `GITHUB_TOKEN` (automatically provided by GitHub Actions)
- **Purpose:** Used for posting PR comments to appear as `github-actions[bot]`
- **Required Permissions:** Already configured in workflow

**Used For:**
- Posting assignment comments on Copilot-generated PRs
- Posting update notifications (when not @mentioning)
- Posting summary comments on source PRs

**Benefits:**
- Comments appear from `github-actions[bot]` instead of the PAT owner
- More professional and consistent appearance
- Separates automation identity from individual user accounts

### Token Strategy Summary

| Operation | Token Used | Appears As | Reason |
|-----------|-----------|------------|---------|
| Creating issues with assignment | `PAT_TOKEN` | PAT owner | Required to assign bots |
| Adding reviewers/assignees to PRs | `PAT_TOKEN` | PAT owner | Required for user operations |
| @mentioning @copilot | `PAT_TOKEN` | PAT owner | Ensures notification delivery |
| Assignment comments on PRs | `GITHUB_TOKEN` | `github-actions[bot]` | Professional appearance |
| Update notifications | `GITHUB_TOKEN` | `github-actions[bot]` | Professional appearance |
| Source PR summary comments | `GITHUB_TOKEN` | `github-actions[bot]` | Professional appearance |

### Workflow Permissions
```yaml
permissions:
  issues: write         # Create and manage issues
  contents: write       # Read repository contents
  pull-requests: write  # Post comments on PRs
  actions: read         # Access workflow run information
```

**Note:** The `pull-requests: write` permission allows `GITHUB_TOKEN` to post comments on PRs, which is why most comments now appear from the bot.

### Cross-Repository Considerations
When SDK implementations live in separate repositories:
- Copilot bot (`copilot-swe-agent[bot]`) must have access to all repositories
- Users being assigned must be collaborators in the target repositories
- Network connectivity between repositories must be available
- GitHub CLI commands use `--repo owner/name` parameter for cross-repo operations

**Parent Issue Linking:**
When parent issues live in different repositories:
- PAT token must have permissions to post comments in the parent issue repository
- GitHub's standard linking keywords work across repositories: `Closes owner/repo#123`
- Task list will be posted to the correct repository automatically
- Parity issues may reference multiple repositories in the task list

## 📊 Example Scenario

### Developer Action
A developer opens a PR in **Agent365-dotnet** that adds a new authentication method in the C# SDK:

**Changed Files:**
- `src/Runtime/Core/AuthenticationService.cs` (new file)
- `src/Runtime/Core/IAuthenticationProvider.cs` (modified)

### Workflow Execution

**Step 1: Detection**
- ✅ Detects C# SDK changes in `src/**/*.cs`
- ✅ Sets source language: `csharp`
- ✅ Sets target languages: `python, typescript`

**Step 2: Issue Creation**

**Issue microsoft/Agent365-python#485:** `[SDK Parity] Python for PR 123`
- Created in Agent365-python repository
- Body includes full PR context from Agent365-dotnet
- Python-specific implementation guidelines
- Assigned to `copilot-swe-agent`
- Labeled with `copilot`, `codegen-experiment`

**Issue microsoft/Agent365-nodejs#486:** `[SDK Parity] Node.js/TypeScript for PR 123`
- Created in Agent365-nodejs repository
- Body includes full PR context from Agent365-dotnet
- TypeScript-specific implementation guidelines
- Assigned to `copilot-swe-agent`
- Labeled with `copilot`, `codegen-experiment`

**Step 3: Monitoring and Auto-Assignment**
Workflow polls every 30 seconds, monitoring both cross-repo issues:
- ⏱️ 0s: Issues created in target repos, monitoring starts
- ⏱️ 30s: Checking... no PRs yet
- ⏱️ 60s: Checking... Copilot creates PR microsoft/Agent365-python#500 for issue #485
  - ✅ Detects PR #500 references issue #485
  - 📌 Assigns PR #500 to original author
  - 💬 Posts assignment comment on PR #500
- ⏱️ 90s: Checking... Copilot creates PR microsoft/Agent365-nodejs#501 for issue #486
  - ✅ Detects PR #501 references issue #486
  - 📌 Assigns PR #501 to original author
  - 💬 Posts assignment comment on PR #501
- ✅ All issues processed, monitoring complete

**Step 4: Parent Issue Update**
Posts task list comment on original feature request (if linked)

Example: If PR body contains `Closes #100`, the workflow posts a task list to issue #100:
```markdown
## 🔄 SDK Parity Tracking

The following parity issues have been created to maintain SDK consistency:

- [ ] microsoft/Agent365-python#485
- [ ] microsoft/Agent365-nodejs#486

*Updated by [AI-First Workflow](...)*
```

**Cross-Repository Example:** If PR body contains `Closes microsoft/planning#200`, the task list is posted to `microsoft/planning#200` instead.

**Step 5: PR Comment**
Posts comment on PR #123 in Agent365-dotnet with:
- Summary: "This PR modifies the **C#** SDK"
- Target SDKs list: Python, Node.js/TypeScript
- Created issues links: microsoft/Agent365-python#485, microsoft/Agent365-nodejs#486
- Next steps explanation

### Expected Outcome

1. **Issues Created and Monitored:**
   - Two parity issues created in external repos (Agent365-python#485, Agent365-nodejs#486)
   - Workflow begins polling for Copilot-generated PRs in those repositories
   - Original PR author receives notification of parity tracking

2. **Copilot Implements Parity:**
   - Copilot reads issue Agent365-python#485, analyzes C# PR changes
   - Implements corresponding changes in Python SDK
   - Creates PR microsoft/Agent365-python#500 linking to issue #485
   - Repeats for TypeScript SDK (creates PR microsoft/Agent365-nodejs#501 for issue #486)

3. **Automatic Assignment:**
   - Workflow detects PRs #500 and #501 in their respective repos within polling period
   - Automatically assigns both PRs to the original author
   - Posts explanatory comments on both PRs
   - Original author receives GitHub notifications for assignment

4. **Human Review:**
   - Original author reviews Copilot-generated PRs in Agent365-python and Agent365-nodejs
   - Validates implementation correctness and intent
   - Approves and merges when satisfied

5. **Result:**
   - All three SDKs (Python, .NET, TypeScript) have the new authentication method
   - Feature parity maintained across all SDKs ✨
   - Complete automation from PR → issues → Copilot implementation → assignment → review

### Copilot-to-Copilot Scenario

**Special Case:** When the original PR is also created by Copilot

**Developer Action:**
A human reviewer (@alice) is assigned to review Copilot's PR #200 that adds a new feature in the Python SDK. Another developer (@bob) is requested as a reviewer.

**Workflow Execution:**

**Step 1-3:** Same as above - issues #510 and #511 created for .NET and TypeScript

**Step 4: Smart Assignment**
When Copilot creates parity PRs #520 and #521:
- ✅ Workflow detects original PR #200 is authored by Copilot (Type: Bot, Login: Copilot)
- 🔍 Fetches assignees and reviewers from source PR #200
  - Finds assignee: @alice
  - Finds reviewer: @bob
- 👥 Adds @alice and @bob as reviewers on PR #520 (instead of assigning to Copilot)
- 👥 Adds @alice and @bob as reviewers on PR #521
- 💬 Posts comment explaining the reviewer chain:
  ```markdown
  ## 🤖 Auto-Assignment
  
  This PR is part of a parity chain that started with a Copilot-generated PR.
  
  **Human reviewers from the original PR have been added:** @alice, @bob
  
  **Reason:** This is a parity implementation for issue #510, which was 
  triggered by Copilot's PR #200.
  ```

**Why This Matters:**
- ❌ **Avoids:** Assigning Copilot to review Copilot's own work (not useful)
- ✅ **Ensures:** Human oversight continues throughout the parity chain
- ✅ **Maintains:** Same human reviewers across all SDK implementations
- ✅ **Scales:** Copilot-generated parity stays within human review workflow

**Result:**
- Copilot generates implementations for all SDKs
- Same humans (@alice, @bob) review all implementations
- Consistent human oversight across entire parity chain

## 🔄 Related Workflow: Auto-Assignment (Fallback)

### Workflow: `assign-copilot-prs.yml`

**Purpose:** Serves as a backup mechanism to assign Copilot-generated PRs if the main workflow times out or misses them

**When It Runs:**
- Triggers on any PR opened event
- Only processes PRs created by `copilot-swe-agent[bot]`

**How It Works:**
1. **Parse:** Extracts linked issue number from PR body
2. **Fetch:** Reads the issue body to find "PR Author: @username"
3. **Assign:** Assigns the PR to the original author
4. **Notify:** Posts comment explaining the auto-assignment

**Relationship to Main Workflow:**
- **Primary:** Main ai-first.yml workflow handles assignment during polling (within 5 minutes)
- **Fallback:** This workflow catches any PRs created after the main workflow times out
- **Redundancy:** If main workflow successfully assigns a PR, this workflow simply confirms the assignment

**Benefits:**
- Ensures no Copilot PRs are left unassigned
- Provides redundancy if main workflow has issues
- Event-driven (no polling overhead)
- Simple and reliable

**Requirements:**
- PR must link to an issue (e.g., "Closes #123")
- Issue must contain "PR Author: @username" in the body (created by ai-first.yml)
- Author must be a repository collaborator to be assigned

## 🛠️ Configuration

### Multi-Repository Setup

The workflow supports creating parity issues across different repositories. Configure this at the workflow level using environment variables:

```yaml
env:
  REPO_MAP_PYTHON: ""        # Empty = use current repo (microsoft/Agent365)
  REPO_MAP_TYPESCRIPT: ""    # Empty = use current repo
  REPO_MAP_CSHARP: ""        # Empty = use current repo
```

**Configuration Examples:**

**Scenario 1: All SDKs in One Repository (Default)**
```yaml
env:
  REPO_MAP_PYTHON: ""
  REPO_MAP_TYPESCRIPT: ""
  REPO_MAP_CSHARP: ""
```
Result: All issues created in `microsoft/Agent365`

**Scenario 2: Separate Python Repository**
```yaml
env:
  REPO_MAP_PYTHON: "microsoft/python-agent-sdk"
  REPO_MAP_TYPESCRIPT: ""
  REPO_MAP_CSHARP: ""
```
Result: 
- Python issues → `microsoft/python-agent-sdk`
- TypeScript/C# issues → `microsoft/Agent365` (current repo)

**Scenario 3: All SDKs in Separate Repositories**
```yaml
env:
  REPO_MAP_PYTHON: "microsoft/python-agent-sdk"
  REPO_MAP_TYPESCRIPT: "microsoft/typescript-agent-sdk"
  REPO_MAP_CSHARP: "microsoft/dotnet-agent-sdk"
```
Result: Each SDK has its own repository with isolated issues and PRs

**Benefits of Multi-Repository Support:**
- **Flexibility:** SDKs can evolve independently
- **Scalability:** Large SDK implementations don't clutter the main repository
- **Team autonomy:** Each SDK team manages their own repository
- **Consistent automation:** Parity tracking works seamlessly across boundaries

**Requirements for Cross-Repository Usage:**
- PAT token with permissions for all repositories
- Copilot bot access to all repositories
- Users must be collaborators in repositories where they'll be assigned
- Repository names must be in `owner/repo` format (no trailing slashes or `.git`)

### Monitored SDK Paths
Edit the `paths` filter in `.github/workflows/ai-first.yml` to add or modify monitored file patterns.

### Language Detection Patterns
Update `SDK_PATH_PATTERNS` dictionary in `.github/scripts/language_detector.py` to configure which file patterns map to which SDK languages.

### Assignee and Labels
- **Default Assignee:** `copilot-swe-agent` (must be a repository collaborator)
- **Default Labels:** `copilot`, `codegen-experiment`
- Both assignee and labels can be modified in the workflow file's `LABELS` and `ASSIGNEE` variables

## ⚙️ Customization

### Adding a New SDK
1. Add file path pattern to workflow `paths` filter
2. Update `SDK_PATH_PATTERNS` in `language_detector.py`
3. Add language case mapping in workflow's issue creation step

### Changing Target Branches
Update the `branches` list in the workflow trigger configuration.

### Modifying Issue Template
Edit the `ISSUE_BODY` variable in the workflow file to customize issue structure and content.

## 🔍 Troubleshooting

### Workflow Not Triggering
- Ensure PR is not in draft status (drafts are skipped by design)
- Verify PR has the `codegen-experiment` label
- Verify PR targets a configured branch
- Check that changed files match the monitored path patterns
- Review the "Check Prerequisites" job output for skip reasons

### "Multiple Source Languages Detected" Error
This is intentional - split your PR into separate PRs, one per SDK, to enforce clean and focused changes.

### Issues Created Without Assignee
The `copilot-swe-agent` must be added as a repository collaborator. Check repository Settings → Collaborators.

### "GH_TOKEN is not set" Error
Configure the `PAT_TOKEN_CODEGEN_EXPERIMENT` secret in repository Settings → Secrets and variables → Actions.

### Duplicate Issues
Verify issue titles haven't changed format. The workflow searches for existing issues by title pattern.

### Comment Not Posted
- Verify token has `pull-requests: write` permission
- Check workflow logs for specific API error messages

### Copilot PRs Not Assigned to Anyone

**Scenario:** Original PR was created by Copilot, and no human reviewers are found.

**Explanation:** 
- When the source PR is Copilot-authored, the workflow avoids assigning Copilot to review its own work
- Instead, it looks for human assignees/reviewers on the source PR to carry forward
- If no humans are found, the parity PR is left unassigned with a notification comment

**Resolution:**
- Manually assign appropriate reviewers to the parity PR
- Or: Ensure Copilot-generated source PRs always have human reviewers assigned
- The workflow comment will indicate this situation and request manual assignment

### Cross-Repository Issues

**Issues not created in target repository:**
- Check PAT token has permissions for the target repository
- Verify repository name format: `owner/repo` (no trailing slashes or .git)
- Ensure repository exists and is accessible
- Validate `REPO_MAP_*` environment variables are correctly set

**Parent issue task list not posted:**
- Verify PAT token has permissions for the parent issue repository
- Check that PR body uses correct linking syntax: `Closes #123` or `Closes owner/repo#456`
- Ensure parent issue repository is accessible
- Review workflow logs for "Updating Parent Issue" step output
- Confirm issue number is valid in the target repository

**PR monitoring fails across repositories:**
- Verify Copilot bot has access to target repositories
- Check that issue references are formatted correctly (owner/repo#123)
- Ensure PR search works in target repository
- Review API rate limits if many repositories involved

**Assignment fails in target repository:**
- Confirm user is a collaborator in target repository
- Verify PAT token has PR write permissions for target repo
- Check that user accepts repository invitations if newly added
- Validate Copilot bot has proper permissions

**Notification step fails:**
- Check that `synchronize` event is properly triggering
- Verify `EXISTING_ISSUES` variable contains valid repository references
- Ensure PRs exist before notification attempts
- Review API error messages in workflow logs

## 📈 Monitoring

### Recommended Metrics
- Issue creation rate per SDK PR
- Copilot success rate (issues resolved vs. created)
- Time from source PR to all parity PRs merged
- False positives (issues closed with `wontfix`)

### Maintenance Tasks
- Review monitored paths as SDK structure evolves
- Update implementation guidelines to reflect current best practices
- Monitor workflow run times and optimize if needed
- Verify Copilot agent assignment is working correctly

## 📚 Related Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [GitHub CLI Documentation](https://cli.github.com/manual/)
- [Language Detector Script](.github/scripts/language_detector.py)

---

**Last Updated:** October 27, 2025  
**Maintained By:** Agent365 Team
