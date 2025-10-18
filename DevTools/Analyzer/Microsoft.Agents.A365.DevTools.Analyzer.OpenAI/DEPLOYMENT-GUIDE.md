# Agent365 OpenAI Governance Analyzer - Deployment Guide

## 🎯 Phased Rollout Strategy

To avoid overwhelming developers with too many errors at once, follow this **phased approach** when introducing the Agent365 OpenAI governance rules to existing codebases.

## 📋 Phase Overview

| Phase | Rules | Focus Area | Expected Impact |
|-------|-------|------------|----------------|
| **Phase 1** | A365OAI0001, A365OAI0002 | Core Security | 5-15 violations |
| **Phase 2** | + A365OAI0006, A365OAI0008 | Function Governance | 3-8 additional violations |
| **Phase 3** | + All remaining rules | Complete Governance | 2-10 additional violations |

## 🚀 Phase 1: Core Security Rules (Week 1-2)

### Rules Introduced
- **A365OAI0001**: ChatClient multitenant violations
- **A365OAI0002**: OpenAIClient multitenant violations

### Project Configuration
```xml
<PropertyGroup>
  <!-- Phase 1: Start with warnings to assess impact -->
  <WarningsAsErrors>A365OAI0001;A365OAI0002</WarningsAsErrors>
</PropertyGroup>
```

### Expected Violations
- Direct ChatClient instantiation without tenant context
- OpenAIClient usage without proper multitenant configuration
- Missing ITenantResolver dependencies

### Remediation Priority
1. **High**: Replace direct client instantiation with factory patterns
2. **Medium**: Add tenant context to existing OpenAI calls
3. **Low**: Update configuration to support multitenancy

---

## 🔧 Phase 2: Function & Registration Governance (Week 3-4)

### Rules Added
- **A365OAI0006**: Function tool governance
- **A365OAI0008**: Registration pattern enforcement

### Project Configuration
```xml
<PropertyGroup>
  <!-- Phase 2: Add function governance -->
  <WarningsAsErrors>A365OAI0001;A365OAI0002;A365OAI0006;A365OAI0008</WarningsAsErrors>
</PropertyGroup>
```

### Expected Violations
- Improper function tool registration
- Missing function metadata attributes
- Incorrect service registration patterns

---

## 🎯 Phase 3: Complete Governance (Week 5-6)

### Rules Added
- **A365OAI0004**: Advanced security patterns
- **A365OAI0005**: Configuration governance
- **A365OAI0009**: Advanced function patterns
- **A365OAI0010**: Service lifecycle management
- **A365OAI0011**: Agent construction patterns

### Project Configuration
```xml
<PropertyGroup>
  <!-- Phase 3: Full governance enforcement -->
  <WarningsAsErrors>A365OAI0001;A365OAI0002;A365OAI0004;A365OAI0005;A365OAI0006;A365OAI0008;A365OAI0009;A365OAI0010;A365OAI0011</WarningsAsErrors>
</PropertyGroup>
```

---

## 📊 Team Adoption Strategies

### 1. 🔍 **Assessment First Approach**
```xml
<!-- Start with warnings only to assess impact -->
<PropertyGroup>
  <WarningsNotAsErrors>A365OAI0001;A365OAI0002</WarningsNotAsErrors>
</PropertyGroup>
```

### 2. 🚫 **Selective Suppression**
```xml
<!-- Temporarily suppress specific rules for legacy code -->
<PropertyGroup>
  <NoWarn>A365OAI0001</NoWarn> <!-- Suppress only ChatClient violations -->
  <WarningsAsErrors>A365OAI0002;A365OAI0006</WarningsAsErrors>
</PropertyGroup>
```

### 3. 📁 **File-Level Suppressions**
```csharp
// For legacy files that need gradual migration
#pragma warning disable A365OAI0001 // ChatClient multitenancy
using var client = new ChatClient("gpt-4", apiKey);
#pragma warning restore A365OAI0001
```

---

## 🛠️ Integration with CI/CD

### GitHub Actions Example
```yaml
name: Governance Check
on: [pull_request]

jobs:
  governance-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      # Phase 1: Core rules only
      - name: Check Phase 1 Compliance
        run: |
          # Enable only Phase 1 rules
          sed -i 's/<NoWarn>.*<\/NoWarn>/<WarningsAsErrors>A365OAI0001;A365OAI0002<\/WarningsAsErrors>/' **/*.csproj
          dotnet build --configuration Release
```

### Azure DevOps Example
```yaml
stages:
- stage: GovernancePhase1
  displayName: 'Phase 1: Core Security'
  jobs:
  - job: CheckCoreRules
    steps:
    - task: DotNetCoreCLI@2
      displayName: 'Build with Phase 1 Rules'
      inputs:
        command: 'build'
        arguments: '/p:WarningsAsErrors=A365OAI0001;A365OAI0002'
```

---

## 📈 Success Metrics

### Week 1-2 (Phase 1)
- [ ] Zero A365OAI0001 violations
- [ ] Zero A365OAI0002 violations
- [ ] All direct OpenAI clients replaced with factories

### Week 3-4 (Phase 2)
- [ ] Zero function governance violations
- [ ] All functions properly registered
- [ ] Service patterns compliant

### Week 5-6 (Phase 3)
- [ ] Zero analyzer violations across all rules
- [ ] Full multitenant governance compliance
- [ ] CI/CD integration complete

---

## 🆘 Troubleshooting Common Issues

### Issue: Too Many Violations
**Solution**: Start with warnings instead of errors
```xml
<WarningsNotAsErrors>A365OAI0001;A365OAI0002</WarningsNotAsErrors>
```

### Issue: Legacy Code Conflicts
**Solution**: Use file-level suppressions temporarily
```csharp
#pragma warning disable A365OAI0001
// Legacy code here
#pragma warning restore A365OAI0001
```

### Issue: Third-Party Dependencies
**Solution**: Exclude external packages
```xml
<PropertyGroup>
  <WarningsAsErrors Condition="'$(MSBuildProjectName)' != 'ExternalLib'">A365OAI0001;A365OAI0002</WarningsAsErrors>
</PropertyGroup>
```

---

## 📞 Support

For questions about deployment or rule interpretation:
- 📧 **Email**: agent365-governance@microsoft.com
- 💬 **Teams**: Agent365 Governance Channel
- 📖 **Wiki**: [Agent365 Governance Rules](./README.md)

---

*This deployment guide ensures smooth adoption of Agent365 governance rules while maintaining development velocity and minimizing disruption to existing workflows.*