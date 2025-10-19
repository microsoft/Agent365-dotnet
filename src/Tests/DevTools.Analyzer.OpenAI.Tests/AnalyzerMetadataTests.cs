using Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Constants;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.A365.DevTools.Analyzer.OpenAI.Tests;

/// <summary>
/// Tests for analyzer metadata and constants to ensure consistency and quality.
/// </summary>
public class AnalyzerMetadataTests
{
    #region Constants Validation Tests

    [Fact]
    public void DiagnosticIds_FollowCorrectPattern()
    {
        var diagnosticIds = new[]
        {
            AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess,
            AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess,
            AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess,
            AnalyzerConstants.DiagnosticIds.ChatClientProviderUsage,
            AnalyzerConstants.DiagnosticIds.FunctionProviderEnforcement,
            AnalyzerConstants.DiagnosticIds.ProviderRegistrationValidation,
            AnalyzerConstants.DiagnosticIds.HardcodedTenantWorkerPrevention,
            AnalyzerConstants.DiagnosticIds.CrossTenantDataAccessPrevention,
            AnalyzerConstants.DiagnosticIds.AgentConstructionValidation
        };

        foreach (var id in diagnosticIds)
        {
            Assert.StartsWith("A365OAI", id);
            Assert.Equal(11, id.Length); // A365OAI + 4 digits
            
            var numberPart = id.Substring(7);
            Assert.True(int.TryParse(numberPart, out var number));
            Assert.True(number >= 1 && number <= 9999);
        }
    }

    [Fact]
    public void DiagnosticIds_AreUnique()
    {
        var diagnosticIds = new[]
        {
            AnalyzerConstants.DiagnosticIds.ChatClientDirectAccess,
            AnalyzerConstants.DiagnosticIds.OpenAIClientDirectAccess,
            AnalyzerConstants.DiagnosticIds.TenantWorkerIdAccess,
            AnalyzerConstants.DiagnosticIds.ChatClientProviderUsage,
            AnalyzerConstants.DiagnosticIds.FunctionProviderEnforcement,
            AnalyzerConstants.DiagnosticIds.ProviderRegistrationValidation,
            AnalyzerConstants.DiagnosticIds.HardcodedTenantWorkerPrevention,
            AnalyzerConstants.DiagnosticIds.CrossTenantDataAccessPrevention,
            AnalyzerConstants.DiagnosticIds.AgentConstructionValidation
        };

        var uniqueIds = diagnosticIds.Distinct().ToArray();
        Assert.Equal(diagnosticIds.Length, uniqueIds.Length);
    }

    [Fact]
    public void Categories_AreWellDefined()
    {
        Assert.NotEmpty(AnalyzerConstants.Categories.Governance);
        Assert.NotEmpty(AnalyzerConstants.Categories.Usage);
        
        // Categories should be descriptive
        Assert.True(AnalyzerConstants.Categories.Governance.Length >= 5);
        Assert.True(AnalyzerConstants.Categories.Usage.Length >= 5);
    }

    [Fact]
    public void TypeNames_AreComplete()
    {
        var typeNames = new[]
        {
            AnalyzerConstants.TypeNames.ChatClient,
            AnalyzerConstants.TypeNames.OpenAIClient,
            AnalyzerConstants.TypeNames.IChatClientProvider,
            AnalyzerConstants.TypeNames.ChatClientProvider,
            AnalyzerConstants.TypeNames.IOpenAIFunctionProvider,
            AnalyzerConstants.TypeNames.OpenAIFunctionProvider,
            AnalyzerConstants.TypeNames.AgentApplication,
            AnalyzerConstants.TypeNames.HttpContext,
            AnalyzerConstants.TypeNames.TenantContextHelper,
            AnalyzerConstants.TypeNames.ChatTool,
            AnalyzerConstants.TypeNames.WebApplication,
            AnalyzerConstants.TypeNames.IServiceCollection,
            AnalyzerConstants.TypeNames.BackgroundService
        };

        foreach (var typeName in typeNames)
        {
            Assert.NotEmpty(typeName);
            Assert.True(typeName.Length >= 3); // Reasonable minimum length
        }
    }

    [Fact]
    public void MethodNames_AreComplete()
    {
        var methodNames = new[]
        {
            AnalyzerConstants.MethodNames.FindFirst,
            AnalyzerConstants.MethodNames.GetRequiredService,
            AnalyzerConstants.MethodNames.AddSingleton,
            AnalyzerConstants.MethodNames.AddScoped,
            AnalyzerConstants.MethodNames.AddTransient,
            AnalyzerConstants.MethodNames.GetChatClient,
            AnalyzerConstants.MethodNames.GetAvailableTools,
            AnalyzerConstants.MethodNames.ExecuteFunctionAsync,
            AnalyzerConstants.MethodNames.GetTenantId,
            AnalyzerConstants.MethodNames.GetWorkerId,
            AnalyzerConstants.MethodNames.CreateFunctionTool,
            AnalyzerConstants.MethodNames.MapPost,
            AnalyzerConstants.MethodNames.MapGet,
            AnalyzerConstants.MethodNames.MapPut,
            AnalyzerConstants.MethodNames.MapDelete,
            AnalyzerConstants.MethodNames.Build
        };

        foreach (var methodName in methodNames)
        {
            Assert.NotEmpty(methodName);
            Assert.True(methodName.Length >= 3); // Reasonable minimum length
        }
    }

    #endregion

    #region Configuration Quality Tests

    [Fact]
    public void HelpLinkBase_IsValidUrl()
    {
        Assert.NotEmpty(AnalyzerConstants.HelpLinkBase);
        Assert.True(Uri.TryCreate(AnalyzerConstants.HelpLinkBase, UriKind.Absolute, out var uri));
        Assert.True(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    [Fact]
    public void DefaultSeverity_IsAppropriate()
    {
        // Governance analyzers should have meaningful severity
        Assert.True(AnalyzerConstants.DefaultSeverity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ||
                   AnalyzerConstants.DefaultSeverity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void GuidanceSuffix_IsHelpful()
    {
        Assert.NotEmpty(AnalyzerConstants.GuidanceSuffix);
        Assert.True(AnalyzerConstants.GuidanceSuffix.Length >= 10); // Should be meaningful
        Assert.Contains("help", AnalyzerConstants.GuidanceSuffix.ToLower());
    }

    #endregion

    #region Tenant/Worker ID Configuration Tests

    [Fact]
    public void TenantWorkerIds_ClaimNames_AreComplete()
    {
        var claimNames = AnalyzerConstants.TenantWorkerIds.ClaimNames;
        
        Assert.Contains("tenant_id", claimNames);
        Assert.Contains("worker_id", claimNames);
        Assert.Equal(2, claimNames.Length);
    }

    [Fact]
    public void TenantWorkerIds_HeaderNames_AreComplete()
    {
        var headerNames = AnalyzerConstants.TenantWorkerIds.HeaderNames;
        
        Assert.Contains("X-Tenant-Id", headerNames);
        Assert.Contains("X-Worker-Id", headerNames);
        Assert.Equal(2, headerNames.Length);
    }

    [Fact]
    public void TenantWorkerIds_AllIdentifiers_IncludesBoth()
    {
        var allIds = AnalyzerConstants.TenantWorkerIds.AllIdentifiers;
        
        // Should include all claim names
        foreach (var claimName in AnalyzerConstants.TenantWorkerIds.ClaimNames)
        {
            Assert.Contains(claimName, allIds);
        }
        
        // Should include all header names
        foreach (var headerName in AnalyzerConstants.TenantWorkerIds.HeaderNames)
        {
            Assert.Contains(headerName, allIds);
        }
        
        // Should be exactly the sum of both collections
        Assert.Equal(
            AnalyzerConstants.TenantWorkerIds.ClaimNames.Length + 
            AnalyzerConstants.TenantWorkerIds.HeaderNames.Length,
            allIds.Length);
    }

    #endregion

    #region Namespace Configuration Tests

    [Fact]
    public void Namespaces_AreWellDefined()
    {
        var namespaces = new[]
        {
            AnalyzerConstants.Namespaces.OpenAIRuntime,
            AnalyzerConstants.Namespaces.OpenAI,
            AnalyzerConstants.Namespaces.OpenAIChat
        };

        foreach (var ns in namespaces)
        {
            Assert.NotEmpty(ns);
            Assert.True(ns.Length >= 5); // Reasonable minimum
            Assert.DoesNotContain(" ", ns); // No spaces in namespaces
        }
    }

    [Fact]
    public void MemberNames_AreComplete()
    {
        var memberNames = new[]
        {
            AnalyzerConstants.MemberNames.Headers,
            AnalyzerConstants.MemberNames.Items,
            AnalyzerConstants.MemberNames.Services,
            AnalyzerConstants.MemberNames.RequestServices,
            AnalyzerConstants.MemberNames.ChatClientField,
            AnalyzerConstants.MemberNames.OpenAIClientField,
            AnalyzerConstants.MemberNames.Value
        };

        foreach (var memberName in memberNames)
        {
            Assert.NotEmpty(memberName);
            Assert.True(memberName.Length >= 1);
        }
    }

    #endregion
}