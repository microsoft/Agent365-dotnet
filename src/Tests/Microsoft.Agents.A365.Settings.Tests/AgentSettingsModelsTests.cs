// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Settings.Models;
using System.Text.Json;

namespace Microsoft.Agents.A365.Settings.Tests;

/// <summary>
/// Unit tests for the Agent Settings models.
/// </summary>
[TestClass]
public class AgentSettingsModelsTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentSettingsModelsTests()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [TestMethod]
    public void AgentSettingProperty_DefaultValues_AreCorrect()
    {
        // Act
        var property = new AgentSettingProperty();

        // Assert
        property.Name.Should().BeEmpty();
        property.Value.Should().BeEmpty();
        property.Type.Should().Be("string");
        property.Required.Should().BeFalse();
        property.Description.Should().BeNull();
        property.DefaultValue.Should().BeNull();
    }

    [TestMethod]
    public void AgentSettingProperty_Serialization_RoundTrips()
    {
        // Arrange
        var property = new AgentSettingProperty
        {
            Name = "testSetting",
            Value = "testValue",
            Type = "boolean",
            Required = true,
            Description = "A test setting",
            DefaultValue = "false"
        };

        // Act
        var json = JsonSerializer.Serialize(property, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentSettingProperty>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("testSetting");
        deserialized.Value.Should().Be("testValue");
        deserialized.Type.Should().Be("boolean");
        deserialized.Required.Should().BeTrue();
        deserialized.Description.Should().Be("A test setting");
        deserialized.DefaultValue.Should().Be("false");
    }

    [TestMethod]
    public void AgentSettingsTemplate_DefaultValues_AreCorrect()
    {
        // Act
        var template = new AgentSettingsTemplate();

        // Assert
        template.Id.Should().BeEmpty();
        template.AgentType.Should().BeEmpty();
        template.Name.Should().BeEmpty();
        template.Description.Should().BeNull();
        template.Version.Should().Be("1.0");
        template.Properties.Should().NotBeNull();
        template.Properties.Should().BeEmpty();
    }

    [TestMethod]
    public void AgentSettingsTemplate_Serialization_RoundTrips()
    {
        // Arrange
        var template = new AgentSettingsTemplate
        {
            Id = "template-123",
            AgentType = "custom-agent",
            Name = "Custom Agent Template",
            Description = "Template for custom agents",
            Version = "2.0",
            Properties = new List<AgentSettingProperty>
            {
                new AgentSettingProperty { Name = "setting1", Value = "value1", Type = "string" },
                new AgentSettingProperty { Name = "setting2", Value = "true", Type = "boolean", Required = true }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(template, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentSettingsTemplate>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("template-123");
        deserialized.AgentType.Should().Be("custom-agent");
        deserialized.Name.Should().Be("Custom Agent Template");
        deserialized.Description.Should().Be("Template for custom agents");
        deserialized.Version.Should().Be("2.0");
        deserialized.Properties.Should().HaveCount(2);
    }

    [TestMethod]
    public void AgentSettings_DefaultValues_AreCorrect()
    {
        // Act
        var settings = new AgentSettings();

        // Assert
        settings.Id.Should().BeEmpty();
        settings.AgentInstanceId.Should().BeEmpty();
        settings.TemplateId.Should().BeNull();
        settings.AgentType.Should().BeEmpty();
        settings.Properties.Should().NotBeNull();
        settings.Properties.Should().BeEmpty();
        settings.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        settings.ModifiedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void AgentSettings_Serialization_RoundTrips()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var modifiedAt = new DateTimeOffset(2024, 1, 16, 14, 45, 0, TimeSpan.Zero);
        
        var settings = new AgentSettings
        {
            Id = "settings-456",
            AgentInstanceId = "instance-789",
            TemplateId = "template-123",
            AgentType = "custom-agent",
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
            Properties = new List<AgentSettingProperty>
            {
                new AgentSettingProperty { Name = "apiKey", Value = "secret", Type = "string", Required = true }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentSettings>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("settings-456");
        deserialized.AgentInstanceId.Should().Be("instance-789");
        deserialized.TemplateId.Should().Be("template-123");
        deserialized.AgentType.Should().Be("custom-agent");
        deserialized.CreatedAt.Should().Be(createdAt);
        deserialized.ModifiedAt.Should().Be(modifiedAt);
        deserialized.Properties.Should().HaveCount(1);
        deserialized.Properties[0].Name.Should().Be("apiKey");
    }

    [TestMethod]
    public void AgentSettingsTemplate_CanAddProperties()
    {
        // Arrange
        var template = new AgentSettingsTemplate
        {
            Id = "template-123",
            AgentType = "custom-agent"
        };

        // Act
        template.Properties.Add(new AgentSettingProperty
        {
            Name = "newSetting",
            Value = "newValue"
        });

        // Assert
        template.Properties.Should().HaveCount(1);
        template.Properties[0].Name.Should().Be("newSetting");
    }

    [TestMethod]
    public void AgentSettings_CanAddProperties()
    {
        // Arrange
        var settings = new AgentSettings
        {
            Id = "settings-123",
            AgentInstanceId = "instance-456"
        };

        // Act
        settings.Properties.Add(new AgentSettingProperty
        {
            Name = "configOption",
            Value = "configValue"
        });

        // Assert
        settings.Properties.Should().HaveCount(1);
        settings.Properties[0].Name.Should().Be("configOption");
    }
}
