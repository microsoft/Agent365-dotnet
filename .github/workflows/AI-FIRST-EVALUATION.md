# AI-First Workflow Evaluation Framework

## Overview

This document outlines a comprehensive evaluation strategy for the AI-First Workflow, which automates SDK parity enforcement across Agent365 SDK implementations. This workflow runs in the **Agent365-dotnet** repository (C# SDK) and creates parity issues in the **Agent365-python** and **Agent365-nodejs** repositories.

## 🎯 Evaluation Goals

The evaluation framework measures:
- **Functional correctness**: Does the workflow detect and create issues correctly?
- **Performance**: Is it fast enough for developer workflows?
- **Quality**: Does Copilot generate good code?
- **Reliability**: Does it handle errors gracefully?
- **Developer experience**: Do developers find it useful and easy to use?

---

## 1. Functional Correctness Metrics

### A. Workflow Trigger Accuracy

**What to measure**: Does the workflow trigger correctly for C# SDK changes

**Test Cases**:
- ✅ Single C# file changes in `src/**/*.cs` - should trigger
- ✅ Multiple C# files in same PR - should trigger
- ✅ Mixed content (C# SDK + docs/tests) - should detect only SDK changes
- ❌ No C# SDK changes (only docs/samples) - should not create issues
- ❌ Only test file changes (`**/Tests/**`, `*.Tests.cs`) - should not create issues
- ✅ C# changes on correct branch patterns - should trigger

**Evaluation Method**:
```bash
# Create test PRs with different file patterns in Agent365-dotnet repo
- PR1: Only src/**/*.cs changes → Should trigger and create 2 parity issues (Python + Node.js)
- PR2: Multiple C# files in src/ → Should trigger and create 2 parity issues
- PR3: Mix of src/**/*.cs + README.md → Should trigger and create 2 parity issues (ignores README)
- PR4: Only README.md changes → Should not trigger (path filter)
- PR5: Only src/**/Tests/**/*.cs changes → Should trigger but has_changes=false, no issues created
- PR6: Mix of src/Runtime/Core/*.cs + src/Tests/**/*.cs → Should create 2 parity issues (ignores tests)
```

**Success Criteria**: 100% accuracy in detecting C# SDK changes and creating 2 parity issues (Python and TypeScript) only for non-test implementation files

---

### B. Issue Creation Correctness

**What to measure**: Parity issues created correctly

**Validation Checklist**:
- [ ] Correct number of issues created: 2 (Python and TypeScript)
- [ ] Issue titles follow format: `[SDK Parity] {SDK_NAME} for PR #{NUMBER}`
- [ ] Issues created in correct repositories:
  - Python issues in `microsoft/Agent365-python`
  - TypeScript issues in `microsoft/Agent365-nodejs`
- [ ] Issue bodies contain all required information:
  - Source PR number, title, and URL
  - PR author and description
  - Target SDK and implementation guidelines
  - Workflow run link
- [ ] Correct labels applied: `copilot`, `codegen-experiment`
- [ ] Assigned to `copilot-swe-agent`
- [ ] No duplicates created on subsequent runs
- [ ] Existing issues with `wontfix` label are not recreated

**Evaluation Method**:
```bash
# Query created issues via GitHub API in target repositories
gh api repos/microsoft/Agent365-python/issues \
  --jq '.[] | select(.title | contains("[SDK Parity]")) | {number, title, labels, assignees}'

gh api repos/microsoft/Agent365-nodejs/issues \
  --jq '.[] | select(.title | contains("[SDK Parity]")) | {number, title, labels, assignees}'
```

**Success Criteria**: 100% of issues meet all checklist items

---

### C. Cross-Repository Operations

**What to measure**: Multi-repo functionality works correctly across Agent365 repositories

**Repository Configuration**:
- **Source:** `microsoft/Agent365-dotnet` (C# SDK - where workflow runs)
- **Target 1:** `microsoft/Agent365-python` (Python SDK - receives parity issues)
- **Target 2:** `microsoft/Agent365-nodejs` (Node.js SDK - receives parity issues)

**Validation Points**:
- [ ] Python issues created in `microsoft/Agent365-python`
- [ ] TypeScript issues created in `microsoft/Agent365-nodejs`
- [ ] Issue references use proper cross-repo format: `microsoft/Agent365-python#123`
- [ ] PR monitoring searches correct target repositories
- [ ] User assignment works across repositories
- [ ] Parent issue linking works cross-repo (e.g., `Closes microsoft/planning-repo#123`)
- [ ] Notifications posted to correct repositories
- [ ] Update notifications reach Copilot PRs in external repos

**Evaluation Method**:
```yaml
# Production configuration (already set in workflow)
env:
  REPO_MAP_PYTHON: "microsoft/Agent365-python"
  REPO_MAP_TYPESCRIPT: "microsoft/Agent365-nodejs"
```

Test cross-repo operations by:
1. Creating PRs in Agent365-dotnet with C# changes
2. Verifying issues appear in Agent365-python and Agent365-nodejs
3. Checking that Copilot PRs are created in the correct repositories
4. Validating cross-repo references and notifications

**Success Criteria**: 100% of cross-repo operations complete successfully

---

### D. Parent Issue Linking

**What to measure**: Task list posted to correct parent issue

**Test Cases**:
- Same-repo parent: `Closes #123` → Task list posted to issue #123 in current repo
- Cross-repo parent: `Closes owner/repo#456` → Task list posted to issue #456 in owner/repo
- Multiple keywords: `Fixes #123` or `Resolves #789`
- No parent issue: No task list posted (graceful handling)

**Validation**:
- [ ] Parent issue detected correctly from PR body
- [ ] Task list posted to correct repository
- [ ] All parity issues listed with checkboxes
- [ ] Issue references include repo prefix when cross-repo

**Success Criteria**: 100% accuracy in parent issue detection and posting

---

### E. Assignment and Review Requests

**What to measure**: Correct assignment behavior based on PR author type

**Test Cases**:

**Case 1: Human-Authored Source PR**
- Source PR created by human (@alice)
- Parity PRs assigned to @alice
- Comment posted explaining auto-assignment

**Case 2: Copilot-Authored with Human Reviewers**
- Source PR created by copilot-swe-agent[bot]
- Source PR has assignees (@alice) and/or reviewers (@bob)
- Parity PRs: @alice and @bob added as reviewers (NOT assigned)
- Comment posted explaining reviewer chain

**Case 3: Copilot-Authored without Human Reviewers**
- Source PR created by copilot-swe-agent[bot]
- Source PR has no human assignees or reviewers
- Parity PRs: No assignment/reviewers added
- Comment posted requesting manual assignment

**Validation Points**:
- [ ] Correctly detects PR author type (Human vs Bot)
- [ ] Identifies Copilot specifically (not just any bot)
- [ ] Extracts human assignees from source PR
- [ ] Extracts requested reviewers from source PR
- [ ] Deduplicates reviewer list (assignees + reviewers)
- [ ] Uses `--add-reviewer` for Copilot chains (not `--add-assignee`)
- [ ] Uses `--add-assignee` for human-authored PRs
- [ ] Posts appropriate comment for each scenario
- [ ] Handles missing reviewer data gracefully

**Evaluation Method**:
```bash
# Test Case 1: Human-authored
gh pr view <parity-pr> --json assignees \
  --jq '.assignees[].login' # Should contain original author

# Test Case 2: Copilot with reviewers
gh pr view <parity-pr> --json reviewRequests \
  --jq '.reviewRequests[].login' # Should contain humans from source PR

# Test Case 3: Copilot without reviewers
gh pr view <parity-pr> --json assignees,reviewRequests \
  --jq '{assigned: .assignees, reviewers: .reviewRequests}' # Should be empty
gh pr view <parity-pr> --json comments \
  --jq '.comments[] | select(.body | contains("No human reviewers were found"))' # Should exist
```

**Success Criteria**: 100% correct behavior for all three cases

---

## 2. Workflow Performance Metrics

### A. Execution Time

**Baseline Targets**:
- Prerequisites check: < 10 seconds
- Set source/target languages: < 5 seconds (hardcoded values)
- Issue creation: < 60 seconds per issue (2 issues total)
- PR monitoring (polling): ≤ 5 minutes (max)
- Total workflow (excluding Copilot work): < 6 minutes

**Evaluation Method**:
```bash
# Extract timing from workflow runs
gh run view <run-id> --log | grep "completed at"

# Calculate duration between steps
grep "##\[group\]" workflow.log | awk '{print $1, $2}'
```

**Success Criteria**:
- 90% of runs complete within target times
- No workflow run exceeds 10 minutes

---

### B. Polling Efficiency

**What to measure**: Copilot PR detection timing

**Metrics to Track**:
- Average time to detect Copilot PR creation
- Number of polling iterations needed
- Timeout rate (PRs not found within 5 minutes)
- False detection rate (wrong PRs matched)

**Evaluation Method**:
Track in workflow logs:
```
Polling attempt 1/10... No PRs found yet
Polling attempt 2/10... No PRs found yet
Polling attempt 3/10... ✅ Found PR #789 for issue #123
```

**Analysis**:
```bash
# Calculate average detection time
grep "Polling attempt" workflow.log | grep "Found PR" | \
  awk '{print $3}' | awk -F'/' '{sum+=$1; count++} END {print sum/count}'
```

**Success Criteria**:
- 80% of Copilot PRs detected within 2 minutes
- < 20% timeout rate (allows time for complex changes)

---

### C. Concurrency Handling

**What to measure**: No race conditions or duplicate issues when multiple commits pushed rapidly

**Test Scenario**:
```bash
# Push multiple commits in quick succession
git commit -m "Change 1" && git push
sleep 2
git commit -m "Change 2" && git push
sleep 2
git commit -m "Change 3" && git push
```

**Expected Behavior**:
- First workflow run starts
- Second commit triggers new run → cancels first run
- Third commit triggers new run → cancels second run
- Only final run completes
- No duplicate issues created

**Success Criteria**: Zero duplicate issues across all test runs

---

## 3. Copilot Agent Quality Metrics

### A. PR Creation Success Rate

**What to measure**: Percentage of parity issues where Copilot successfully creates a PR and is assigned/reviewed appropriately

**Formula**: 
- **PR Creation Rate**: `(PRs created / Total issues) × 100`
- **Correct Assignment Rate**: `(PRs with correct assignment/reviewers / Total PRs created) × 100`

**Assignment Validation**:
- Human-authored source → Parity PR assigned to original author
- Copilot-authored source (with humans) → Human reviewers added to parity PR
- Copilot-authored source (no humans) → Fallback comment posted

**Evaluation Method**:
```bash
# Check which issues have linked PRs
gh issue list --label "codegen-experiment" --state open \
  --json number,title,linkedPullRequests | \
  jq '.[] | select(.linkedPullRequests | length > 0) | 
  {issue: .number, pr_count: (.linkedPullRequests | length)}'

# Validate assignment for human-authored chains
gh pr view <parity-pr> --json assignees \
  --jq '.assignees[] | select(.login == "<original-author>")'

# Validate reviewers for Copilot-authored chains
gh pr view <parity-pr> --json reviewRequests \
  --jq '.reviewRequests[].login' # Should match source PR humans
```

**Success Criteria**:
- PR Creation: > 90% within 30 minutes (stretch: > 95% within 1 hour)
- Assignment Accuracy: 100% (critical for proper review flow)
- Reviewer Propagation: 100% (must maintain human oversight chain)
- Fallback Comments: 100% (when no humans available)

---

### B. Implementation Quality

**What to measure**: How well Copilot implements parity changes

**Quality Dimensions**:
1. **Compilability**: Does the code build without errors?
2. **Completeness**: Are all features from source PR included?
3. **Style consistency**: Does it follow SDK coding conventions?
4. **Test coverage**: Are tests included (if source had tests)?
5. **Code structure**: Similar patterns to source implementation?

**Evaluation Rubric** (1-5 scale):

| Score | Criteria |
|-------|----------|
| 1 ❌ | Doesn't compile / has syntax errors |
| 2 ⚠️ | Compiles but missing key features (>50% incomplete) |
| 3 ✓ | Works but has style/structure issues, requires significant rework |
| 4 ✓✓ | Good implementation, minor tweaks needed (<10% changes) |
| 5 ✅ | Excellent - ready to merge as-is or minor formatting only |

**Evaluation Method**: Manual code review of random sample

**Sample Size**: Review 20 Copilot PRs (or 100% if fewer than 20)

**Success Criteria**:
- Average quality score: > 3.5/5
- No scores of 1 (all code compiles)
- At least 50% score 4 or 5

---

### C. Context Utilization

**What to measure**: Does Copilot effectively use the provided context?

**Validation Checklist**:
- [ ] PR references source PR number in description
- [ ] Code mimics patterns from source changes
- [ ] Uses same naming conventions as source
- [ ] Follows language-specific guidelines provided in issue
- [ ] Similar code structure to source SDK implementation
- [ ] Includes similar comments/documentation as source

**Evaluation Method**: Manual review of Copilot PRs

**Success Criteria**: 80% of checklist items satisfied across sample

---

### D. Iteration Count

**What to measure**: How many revision rounds needed before merge?

**Track**:
- Number of review comments per PR
- Number of commits after initial Copilot implementation
- Time from PR creation to approval
- Number of PRs requiring human code changes

**Success Criteria**:
- Average: < 2 revision rounds
- 60% of PRs approved on first review

---

## 4. Developer Experience Metrics

### A. Notification Timeliness

**What to measure**: How quickly developers are notified

**Metrics**:
- **T1**: Time from PR creation → workflow comment posted
- **T2**: Time from PR update (new commit) → existing Copilot PRs notified
- **T3**: Time from Copilot PR creation → assignment/reviewer request
- **T4**: Time to detect Copilot-authored source PR and fetch human reviewers

**Targets**:
- T1: < 2 minutes
- T2: < 2 minutes
- T3: < 30 seconds (within polling window)
- T4: < 5 seconds (inline with assignment step)

**Evaluation Method**:
```bash
# Compare timestamps from GitHub events
gh pr view <pr-num> --json createdAt,comments \
  --jq '{created: .createdAt, first_comment: .comments[0].createdAt}'

# For Copilot-authored PRs, check reviewer request timing
gh pr view <copilot-pr> --json createdAt,reviewRequests \
  --jq '{created: .createdAt, first_reviewer: .reviewRequests[0].requestedAt}'
```

**Success Criteria**: 90% of notifications meet target times

---

### B. Clarity and Actionability

**What to measure**: Are workflow messages helpful?

**Developer Survey** (1-5 scale, 5 = strongly agree):

1. Workflow comments were clear and informative
2. I understood what actions were required of me
3. Links provided were helpful for navigation
4. Error messages (if any) helped me fix issues
5. The workflow saved me time compared to manual coordination
6. I would recommend using this workflow
7. Overall satisfaction with the AI-First automation

**Sample Size**: All developers who interact with workflow (minimum 10)

**Success Criteria**:
- Average score: > 4.0/5 on all questions
- No question averages below 3.5/5
- > 80% would recommend (question 6)

---

### C. False Positive Rate

**What to measure**: Workflow triggering when it shouldn't

**Examples of False Positives**:
- Creating issues for documentation-only changes
- Creating issues when SDKs already have parity
- Duplicate notifications for same change
- Triggering on test file changes only
- Triggering on sample code changes

**Evaluation Method**:
```bash
# Review all workflow runs
gh run list --workflow=ai-first.yml --json conclusion,headBranch,event

# Manually review PRs where workflow ran
# Classify: true positive, false positive, or false negative
```

**Success Criteria**: < 5% false positive rate

---

### D. False Negative Rate

**What to measure**: Workflow NOT triggering when it should

**Examples**:
- SDK changes made but not detected
- Issues not created for target SDKs
- Copilot not assigned to created issues

**Evaluation Method**: Manual audit of PRs that modified SDK files

**Success Criteria**: < 2% false negative rate

---

## 5. End-to-End Success Metrics

### A. Full Pipeline Success Rate

**What to measure**: Percentage of PRs that complete the full cycle successfully

**Full Cycle Definition**:
1. ✅ Source PR created with SDK changes
2. ✅ Workflow detects changes correctly
3. ✅ Parity issues created for all target SDKs
4. ✅ Copilot creates PRs for all issues
5. ✅ PRs assigned/reviewed appropriately based on source PR author:
   - Human-authored: PR assigned to original author
   - Copilot-authored: Human reviewers added (from source PR)
   - Copilot-authored (no humans): Comment posted requesting manual assignment
6. ✅ PRs approved and merged
7. ✅ Parent issue updated with task list (if applicable)

**Formula**: `(Completed cycles / Total source PRs) × 100`

**Evaluation Method**: Track PRs from creation to parity PR merge

**Additional Metrics**:
- **Human Assignment Success Rate**: `(Human-authored PRs correctly assigned / Total human PRs) × 100`
- **Reviewer Propagation Success Rate**: `(Copilot PRs with correct reviewers / Total Copilot PRs with humans) × 100`
- **Fallback Comment Success Rate**: `(Copilot PRs with fallback comment / Total Copilot PRs without humans) × 100`

**Success Criteria**:
- Target: > 70% full completion rate
- Stretch goal: > 85%
- Assignment/Reviewer accuracy: 100% (critical path)
- Fallback comment posting: 100% (when no humans found)

---

### B. Time to Parity

**What to measure**: Total time from source PR creation to all parity PRs merged

**Time Breakdown**:
- **T1**: Source PR created → Workflow completes (detection + issue creation)
- **T2**: Issues created → All Copilot PRs created
- **T3**: Copilot PRs created → All PRs reviewed
- **T4**: PRs reviewed → All PRs merged
- **Total**: T1 + T2 + T3 + T4

**Targets by Change Complexity**:
- **Simple** (< 50 LOC changed): < 24 hours
- **Medium** (50-200 LOC changed): < 48 hours
- **Complex** (> 200 LOC changed): < 72 hours

**Evaluation Method**:
```bash
# Track timestamps for each stage
gh pr view <source-pr> --json createdAt,mergedAt
gh issue view <parity-issue> --json createdAt
gh pr view <copilot-pr> --json createdAt,mergedAt
```

**Success Criteria**:
- 80% of PRs meet target times for their complexity level
- Average time to parity: < 36 hours

---

### C. Code Quality Consistency

**What to measure**: Quality of Copilot-generated code vs human-written

**Comparison Metrics**:

| Metric | Human Code | Copilot Code | Target Ratio |
|--------|------------|--------------|--------------|
| Cyclomatic complexity | X | Y | Y/X < 1.2 |
| Lines of code | X | Y | Y/X < 1.3 |
| Test coverage % | X | Y | Y/X > 0.9 |
| Lint warnings | X | Y | Y/X < 1.5 |
| Code review iterations | X | Y | Y/X < 2.0 |

**Evaluation Method**:
- Run static analysis on both human and Copilot implementations
- Compare metrics for equivalent functionality
- Calculate ratios

**Success Criteria**: All ratios within target range

---

## 6. Reliability and Error Handling

### A. Workflow Failure Rate

**What to measure**: Percentage of workflow runs that fail

**Failure Categories**:
1. Authentication failures (PAT token issues)
2. API rate limit errors
3. Network timeouts
4. Script errors (language_detector.py)
5. GitHub API errors (issues, PRs, comments)
6. Permission errors (cross-repo operations)

**Evaluation Method**:
```bash
# Get workflow run statistics
gh run list --workflow=ai-first.yml --json conclusion,databaseId \
  | jq '[.[] | .conclusion] | group_by(.) | 
    map({conclusion: .[0], count: length})'
```

**Success Criteria**:
- Overall failure rate: < 2%
- No single category > 1% failure rate
- All failures have clear error messages

---

### B. Graceful Degradation

**What to measure**: Does workflow handle failures gracefully?

**Test Scenarios**:
1. **Copilot doesn't create PR within 5 minutes**
   - Expected: Workflow continues, logs unprocessed issues, doesn't fail
   
2. **Issue creation fails for one SDK**
   - Expected: Creates issues for other SDKs, logs error for failed one
   
3. **PR comment API fails**
   - Expected: Issues still created, error logged, workflow succeeds
   
4. **Token expires mid-workflow**
   - Expected: Clear error message, specific remediation steps
   
5. **Parent issue doesn't exist**
   - Expected: Workflow continues, skips task list update

**Success Criteria**: All scenarios handle gracefully without complete failure

---

### C. Recovery Mechanisms

**What to measure**: Can failed operations be retried?

**Validation**:
- [ ] Re-running workflow on same PR works correctly
- [ ] Doesn't create duplicate issues on retry
- [ ] Reuses existing open issues appropriately
- [ ] Handles partially completed workflows
- [ ] Clear indication of what failed and why

**Success Criteria**: 100% of retries succeed after fixing root cause

---

## 7. Evaluation Schedule

### Phase 1: Initial Validation (Week 1-2)

**Goal**: Verify basic functionality

**Activities**:
- Run 20 test PRs in Agent365-dotnet with C# changes
- Measure workflow trigger accuracy (Section 1A)
- Validate issue creation in target repositories (Section 1B)
- Test cross-repo operations (Section 1C)
- Test error conditions (Section 6B)

**Success Criteria**:
- 100% trigger accuracy (triggers on C# changes, creates 2 issues)
- 95% issue creation success in target repos
- 100% cross-repo operations successful
- All error scenarios handled gracefully

**Deliverable**: Test results report with pass/fail for each scenario

---

### Phase 2: Copilot Integration (Week 3-4)

**Goal**: Evaluate AI agent performance

**Activities**:
- Monitor 10-20 real PRs end-to-end
- Evaluate Copilot PR creation rate (Section 3A)
- Manual quality review of Copilot implementations (Section 3B)
- Measure time to parity (Section 5B)

**Success Criteria**:
- 80% Copilot PR creation rate
- Average quality score > 3.5/5
- Average time to parity < 48 hours

**Deliverable**: Quality assessment report with code review scores

---

### Phase 3: Cross-Repository Testing (Week 5)

**Goal**: Validate multi-repo functionality

**Activities**:
- Configure test repositories for each SDK
- Test all cross-repo scenarios (Section 1C)
- Validate parent issue linking across repos (Section 1D)
- Test notifications and assignments in external repos

**Success Criteria**:
- 100% cross-repo operations successful
- No issues with permissions or references

**Deliverable**: Cross-repo test matrix with results

---

### Phase 4: Developer Experience (Week 6-7)

**Goal**: Gather user feedback

**Activities**:
- Deploy to all developers with `codegen-experiment` label
- Distribute developer survey (Section 4B)
- Measure notification timing (Section 4A)
- Track false positive/negative rates (Section 4C, 4D)

**Success Criteria**:
- > 4.0/5 average satisfaction score
- < 5% false positive rate
- > 80% would recommend

**Deliverable**: Developer feedback report with survey results

---

### Phase 5: Production Readiness (Week 8)

**Goal**: Validate reliability for production use

**Activities**:
- Monitor all metrics over 2 weeks
- Calculate full pipeline success rate (Section 5A)
- Review error logs and failure patterns (Section 6A)
- Performance benchmarking (Section 2)

**Success Criteria**:
- < 2% workflow failure rate
- > 70% full pipeline completion
- All performance targets met

**Deliverable**: Production readiness report with go/no-go recommendation

---

## 8. Monitoring Dashboard

### Recommended Metrics Dashboard

Create a dashboard (GitHub Pages, internal tool, or Grafana) tracking:

```
┌─────────────────────────────────────────────────────────────┐
│                AI-First Workflow Metrics                     │
├─────────────────────────────────────────────────────────────┤
│ VOLUME METRICS                                              │
│ • Total PRs processed: 47                                   │
│ • Parity issues created: 94                                 │
│ • Copilot PRs generated: 82 (87.2%)                        │
│ • Merged parity PRs: 65 (79.3% of generated)               │
│                                                              │
│ TIMING METRICS                                              │
│ • Avg workflow execution: 3.2 min                           │
│ • Avg Copilot PR creation: 12.5 min                        │
│ • Avg time to parity: 18.5 hours                           │
│ • Avg review time: 4.2 hours                               │
│                                                              │
│ QUALITY METRICS                                             │
│ • Avg code quality score: 4.1/5                            │
│ • PRs requiring revisions: 42%                             │
│ • Avg iterations to merge: 1.6                             │
│ • Build success rate: 95%                                   │
│                                                              │
│ RELIABILITY METRICS                                         │
│ • Workflow failure rate: 1.2%                              │
│ • Detection accuracy: 98.9%                                │
│ • False positive rate: 3.1%                                │
│ • Full pipeline completion: 72.3%                          │
│                                                              │
│ DEVELOPER EXPERIENCE                                        │
│ • Avg satisfaction score: 4.3/5                            │
│ • Would recommend: 87%                                      │
│ • Time saved vs manual: ~6.5 hours/PR                      │
│                                                              │
│ TREND (Last 30 Days)                                        │
│ • Quality: ↑ +0.3                                          │
│ • Speed: ↑ -2.1 hours                                      │
│ • Reliability: ↑ -0.5% failures                            │
└─────────────────────────────────────────────────────────────┘
```

### Data Collection

**Automated Collection**:
```bash
# Create metrics collection script
# Collect workflow runs from C# repo
gh api repos/microsoft/Agent365-dotnet/actions/workflows/ai-first.yml/runs \
  --paginate | jq '...'

# Collect parity issues from target repos
gh api repos/microsoft/Agent365-python/issues \
  --paginate -f labels=codegen-experiment | jq '...'

gh api repos/microsoft/Agent365-nodejs/issues \
  --paginate -f labels=codegen-experiment | jq '...'
```

**Manual Collection**:
- Code quality reviews (weekly)
- Developer surveys (bi-weekly)
- Detailed failure analysis (as needed)

---

## 9. Key Success Questions

This evaluation framework aims to answer:

1. **Does it work correctly?**
   - ✅ Functional correctness metrics (Section 1)
   
2. **Is it fast enough?**
   - ✅ Performance metrics (Section 2)
   
3. **Is Copilot good enough?**
   - ✅ Quality metrics (Section 3)
   
4. **Do developers like it?**
   - ✅ Experience metrics (Section 4)
   
5. **Does it save time?**
   - ✅ Time to parity metrics (Section 5B)
   
6. **Is it reliable?**
   - ✅ Error rate and recovery metrics (Section 6)
   
7. **Should we use it in production?**
   - ✅ Overall assessment from all sections

---

## 10. Decision Criteria

### Go to Production If:
- [x] Detection accuracy > 95%
- [x] Workflow failure rate < 2%
- [x] Copilot PR creation rate > 80%
- [x] Average quality score > 3.5/5
- [x] Developer satisfaction > 4.0/5
- [x] False positive rate < 5%
- [x] Time to parity < 48 hours (medium complexity)

### Hold for Improvements If:
- Any metric below minimum threshold
- Significant developer complaints
- Critical reliability issues
- Cross-repo functionality incomplete

### Abandon If:
- Copilot quality consistently poor (< 2.5/5)
- Developer satisfaction < 3.0/5
- Workflow failure rate > 10%
- More overhead than manual process

---

## 11. Next Steps

Based on evaluation results:

1. **Metrics collection scripts**: Automate data gathering from GitHub API
2. **Dashboard setup**: Create real-time metrics visualization
3. **Survey distribution**: Collect developer feedback
4. **Test PR creation**: Build automated test scenario execution
5. **Regular review cadence**: Weekly metrics review during evaluation phases

---

## Appendix: Sample Test PRs

### Test PR 1: Simple Python Addition
```
Changed Files:
- python/libraries/core/agent.py (+15, -3)

Expected Outcome:
- Detects: Python
- Creates issues for: TypeScript, C#
```

### Test PR 2: TypeScript Refactor
```
Changed Files:
- nodejs/packages/agents-a365-runtime/src/engine.ts (+45, -30)
- nodejs/packages/agents-a365-runtime/src/types.ts (+10, -5)

Expected Outcome:
- Detects: TypeScript
- Creates issues for: Python, C#
```

### Test PR 3: Documentation Only (Should Not Trigger)
```
Changed Files:
- README.md (+20, -5)
- docs/getting-started.md (+100, -10)

Expected Outcome:
- No SDK changes detected
- No issues created
- Informational comment on PR
```

### Test PR 4: Mixed SDK (Should Fail)
```
Changed Files:
- python/libraries/core/agent.py (+15, -3)
- nodejs/packages/agents-a365-runtime/src/engine.ts (+20, -10)

Expected Outcome:
- Workflow fails validation
- Error message: "Multiple source SDKs detected"
- No issues created
```

### Test PR 5: Cross-Repo Parent Issue
```
PR Body:
"Closes microsoft/planning-repo#456"

Changed Files:
- python/libraries/core/feature.py (+50, -10)

Expected Outcome:
- Creates issues for TypeScript, C#
- Posts task list to microsoft/planning-repo#456
```

### Test PR 6: Copilot-to-Copilot Chain
```
PR Details:
- Number: #200
- Author: copilot-swe-agent[bot] (Type: Bot)
- Assignees: @alice
- Requested Reviewers: @bob
- PR Body: "Closes #123"

Changed Files:
- python/kairo/tooling/executor.py (+50, -10)

Expected Outcome:
- Detects: Python (Copilot-authored source)
- Creates issues for: TypeScript (#520), C# (#521)
- Parity PR Assignment Behavior:
  - Does NOT assign to copilot-swe-agent[bot]
  - Adds @alice as reviewer on PR #520
  - Adds @bob as reviewer on PR #520
  - Adds @alice as reviewer on PR #521
  - Adds @bob as reviewer on PR #521
- Posted Comments:
  - Should include: "This PR is part of a parity chain that started with a Copilot-generated PR"
  - Should include: "Human reviewers from the original PR have been added: @alice, @bob"
  - Should reference source PR: "#200"
  - Should reference parent issue: "#123"
```

### Test PR 7: Copilot Chain without Human Reviewers
```
PR Details:
- Number: #300
- Author: copilot-swe-agent[bot] (Type: Bot)
- Assignees: (none)
- Requested Reviewers: (none)
- PR Body: "Implements new feature"

Changed Files:
- nodejs/src/identity/manager.ts (+30, -5)

Expected Outcome:
- Detects: TypeScript (Copilot-authored source)
- Creates issues for: Python, C#
- Parity PR Assignment Behavior:
  - Does NOT assign to anyone
  - Does NOT add any reviewers
- Posted Comments:
  - Should include: "This PR is part of a parity chain that started with a Copilot-generated PR"
  - Should include: "No human reviewers were found on the source PR"
  - Should include: "Please manually assign reviewers"
  - Should reference source PR: "#300"
```

---

## Document Version

- **Version**: 1.1
- **Last Updated**: January 2025
- **Authors**: AI-First Workflow Team
- **Status**: Updated for Copilot-to-Copilot Assignment Enhancement

