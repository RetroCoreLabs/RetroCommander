//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.PropertyBags;

namespace RetroCommander.Core.Tests;

[TestFixture]
public class PropertyBagTests
{
    #region GetProperty Tests

    [Test]
    public void GetProperty_ReturnsValue_WhenPropertyExists()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", "TestValue");

        var result = bag.GetProperty<string>("TestKey");

        Assert.That(result, Is.EqualTo("TestValue"));
    }

    [Test]
    public void GetProperty_ReturnsDefault_WhenPropertyDoesNotExist()
    {
        var bag = new PropertyBag();

        var result = bag.GetProperty<string>("NonExistent", "DefaultValue");

        Assert.That(result, Is.EqualTo("DefaultValue"));
    }

    [Test]
    public void GetProperty_ReturnsNull_WhenPropertyDoesNotExistAndNoDefault()
    {
        var bag = new PropertyBag();

        var result = bag.GetProperty<string>("NonExistent");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetProperty_ReturnsDefault_WhenTypeDoesNotMatch()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", 42); // Set as int

        var result = bag.GetProperty<string>("TestKey", "DefaultValue"); // Try to get as string

        Assert.That(result, Is.EqualTo("DefaultValue"));
    }

    [Test]
    public void GetProperty_WorksWithValueTypes()
    {
        var bag = new PropertyBag();
        bag.SetProperty("IntKey", 42);
        bag.SetProperty("LongKey", 12345678901234L);
        bag.SetProperty("BoolKey", true);
        bag.SetProperty("ByteKey", (byte)255);

        Assert.That(bag.GetProperty<int>("IntKey"), Is.EqualTo(42));
        Assert.That(bag.GetProperty<long>("LongKey"), Is.EqualTo(12345678901234L));
        Assert.That(bag.GetProperty<bool>("BoolKey"), Is.True);
        Assert.That(bag.GetProperty<byte>("ByteKey"), Is.EqualTo((byte)255));
    }

    [Test]
    public void GetProperty_WorksWithNullableTypes()
    {
        var bag = new PropertyBag();
        var now = DateTime.UtcNow;
        bag.SetProperty<DateTime?>("DateKey", now);

        var result = bag.GetProperty<DateTime?>("DateKey");

        Assert.That(result, Is.EqualTo(now));
    }

    [Test]
    public void GetProperty_WorksWithComplexTypes()
    {
        var bag = new PropertyBag();
        var dict = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        bag.SetProperty("DictKey", dict);

        var result = bag.GetProperty<Dictionary<string, string>>("DictKey");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(2));
        Assert.That(result["key1"], Is.EqualTo("value1"));
    }

    #endregion

    #region TryGetProperty Tests

    [Test]
    public void TryGetProperty_ReturnsTrue_WhenPropertyExists()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", "TestValue");

        var result = bag.TryGetProperty<string>("TestKey", out var value);

        Assert.That(result, Is.True);
        Assert.That(value, Is.EqualTo("TestValue"));
    }

    [Test]
    public void TryGetProperty_ReturnsFalse_WhenPropertyDoesNotExist()
    {
        var bag = new PropertyBag();

        var result = bag.TryGetProperty<string>("NonExistent", out var value);

        Assert.That(result, Is.False);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void TryGetProperty_ReturnsFalse_WhenTypeDoesNotMatch()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", 42); // Set as int

        var result = bag.TryGetProperty<string>("TestKey", out var value); // Try to get as string

        Assert.That(result, Is.False);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void TryGetProperty_WorksWithValueTypes()
    {
        var bag = new PropertyBag();
        bag.SetProperty("IntKey", 42);

        var result = bag.TryGetProperty<int>("IntKey", out var value);

        Assert.That(result, Is.True);
        Assert.That(value, Is.EqualTo(42));
    }

    #endregion

    #region SetProperty Tests

    [Test]
    public void SetProperty_AddsNewProperty()
    {
        var bag = new PropertyBag();

        bag.SetProperty("TestKey", "TestValue");

        Assert.That(bag.HasProperty("TestKey"), Is.True);
        Assert.That(bag.GetProperty<string>("TestKey"), Is.EqualTo("TestValue"));
    }

    [Test]
    public void SetProperty_OverwritesExistingProperty()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", "OldValue");

        bag.SetProperty("TestKey", "NewValue");

        Assert.That(bag.GetProperty<string>("TestKey"), Is.EqualTo("NewValue"));
    }

    [Test]
    public void SetProperty_CanChangeType()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", "StringValue");

        bag.SetProperty("TestKey", 42);

        Assert.That(bag.GetProperty<int>("TestKey"), Is.EqualTo(42));
        Assert.That(bag.GetProperty<string>("TestKey"), Is.Null); // Type changed
    }

    [Test]
    public void SetProperty_CanSetNullValue()
    {
        var bag = new PropertyBag();

        bag.SetProperty<string?>("TestKey", null);

        Assert.That(bag.HasProperty("TestKey"), Is.True);
        Assert.That(bag.GetProperty<string>("TestKey"), Is.Null);
    }

    #endregion

    #region HasProperty Tests

    [Test]
    public void HasProperty_ReturnsTrue_WhenPropertyExists()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", "TestValue");

        Assert.That(bag.HasProperty("TestKey"), Is.True);
    }

    [Test]
    public void HasProperty_ReturnsFalse_WhenPropertyDoesNotExist()
    {
        var bag = new PropertyBag();

        Assert.That(bag.HasProperty("NonExistent"), Is.False);
    }

    [Test]
    public void HasProperty_ReturnsTrue_WhenValueIsNull()
    {
        var bag = new PropertyBag();
        bag.SetProperty<string?>("TestKey", null);

        Assert.That(bag.HasProperty("TestKey"), Is.True);
    }

    #endregion

    #region GetAllProperties Tests

    [Test]
    public void GetAllProperties_ReturnsEmptyDictionary_WhenNoProperties()
    {
        var bag = new PropertyBag();

        var result = bag.GetAllProperties();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetAllProperties_ReturnsAllProperties()
    {
        var bag = new PropertyBag();
        bag.SetProperty("Key1", "Value1");
        bag.SetProperty("Key2", 42);
        bag.SetProperty("Key3", true);

        var result = bag.GetAllProperties();

        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result["Key1"], Is.EqualTo("Value1"));
        Assert.That(result["Key2"], Is.EqualTo(42));
        Assert.That(result["Key3"], Is.EqualTo(true));
    }

    [Test]
    public void GetAllProperties_ReturnsReadOnlyDictionary()
    {
        var bag = new PropertyBag();
        bag.SetProperty("Key1", "Value1");

        var result = bag.GetAllProperties();

        // Verify it's IReadOnlyDictionary
        Assert.That(result, Is.InstanceOf<IReadOnlyDictionary<string, object?>>());
    }

    #endregion

    #region RemoveProperty Tests

    [Test]
    public void RemoveProperty_ReturnsTrue_WhenPropertyExists()
    {
        var bag = new PropertyBag();
        bag.SetProperty("TestKey", "TestValue");

        var result = bag.RemoveProperty("TestKey");

        Assert.That(result, Is.True);
        Assert.That(bag.HasProperty("TestKey"), Is.False);
    }

    [Test]
    public void RemoveProperty_ReturnsFalse_WhenPropertyDoesNotExist()
    {
        var bag = new PropertyBag();

        var result = bag.RemoveProperty("NonExistent");

        Assert.That(result, Is.False);
    }

    [Test]
    public void RemoveProperty_RemovesOnlyTargetProperty()
    {
        var bag = new PropertyBag();
        bag.SetProperty("Key1", "Value1");
        bag.SetProperty("Key2", "Value2");

        bag.RemoveProperty("Key1");

        Assert.That(bag.HasProperty("Key1"), Is.False);
        Assert.That(bag.HasProperty("Key2"), Is.True);
    }

    #endregion

    #region Clear Tests

    [Test]
    public void Clear_RemovesAllProperties()
    {
        var bag = new PropertyBag();
        bag.SetProperty("Key1", "Value1");
        bag.SetProperty("Key2", "Value2");
        bag.SetProperty("Key3", "Value3");

        bag.Clear();

        Assert.That(bag.GetAllProperties().Count, Is.EqualTo(0));
        Assert.That(bag.HasProperty("Key1"), Is.False);
        Assert.That(bag.HasProperty("Key2"), Is.False);
        Assert.That(bag.HasProperty("Key3"), Is.False);
    }

    [Test]
    public void Clear_OnEmptyBag_DoesNotThrow()
    {
        var bag = new PropertyBag();

        Assert.DoesNotThrow(() => bag.Clear());
    }

    #endregion

    #region MergeFrom Tests

    [Test]
    public void MergeFrom_PreferSource_OverwritesExistingProperties()
    {
        var target = new PropertyBag();
        target.SetProperty("Key1", "TargetValue1");
        target.SetProperty("Key2", "TargetValue2");

        var source = new PropertyBag();
        source.SetProperty("Key1", "SourceValue1");
        source.SetProperty("Key3", "SourceValue3");

        target.MergeFrom(source, PropertyMergeStrategy.PreferSource);

        Assert.That(target.GetProperty<string>("Key1"), Is.EqualTo("SourceValue1"));
        Assert.That(target.GetProperty<string>("Key2"), Is.EqualTo("TargetValue2"));
        Assert.That(target.GetProperty<string>("Key3"), Is.EqualTo("SourceValue3"));
    }

    [Test]
    public void MergeFrom_PreferTarget_KeepsExistingProperties()
    {
        var target = new PropertyBag();
        target.SetProperty("Key1", "TargetValue1");
        target.SetProperty("Key2", "TargetValue2");

        var source = new PropertyBag();
        source.SetProperty("Key1", "SourceValue1");
        source.SetProperty("Key3", "SourceValue3");

        target.MergeFrom(source, PropertyMergeStrategy.PreferTarget);

        Assert.That(target.GetProperty<string>("Key1"), Is.EqualTo("TargetValue1"));
        Assert.That(target.GetProperty<string>("Key2"), Is.EqualTo("TargetValue2"));
        Assert.That(target.GetProperty<string>("Key3"), Is.EqualTo("SourceValue3"));
    }

    [Test]
    public void MergeFrom_OnlyMissing_AddsOnlyNewProperties()
    {
        var target = new PropertyBag();
        target.SetProperty("Key1", "TargetValue1");
        target.SetProperty("Key2", "TargetValue2");

        var source = new PropertyBag();
        source.SetProperty("Key1", "SourceValue1");
        source.SetProperty("Key3", "SourceValue3");

        target.MergeFrom(source, PropertyMergeStrategy.OnlyMissing);

        Assert.That(target.GetProperty<string>("Key1"), Is.EqualTo("TargetValue1"));
        Assert.That(target.GetProperty<string>("Key2"), Is.EqualTo("TargetValue2"));
        Assert.That(target.GetProperty<string>("Key3"), Is.EqualTo("SourceValue3"));
    }

    [Test]
    public void MergeFrom_DefaultsToPreferSource()
    {
        var target = new PropertyBag();
        target.SetProperty("Key1", "TargetValue1");

        var source = new PropertyBag();
        source.SetProperty("Key1", "SourceValue1");

        target.MergeFrom(source); // No strategy specified

        Assert.That(target.GetProperty<string>("Key1"), Is.EqualTo("SourceValue1"));
    }

    [Test]
    public void MergeFrom_EmptySource_DoesNotModifyTarget()
    {
        var target = new PropertyBag();
        target.SetProperty("Key1", "TargetValue1");

        var source = new PropertyBag();

        target.MergeFrom(source);

        Assert.That(target.GetAllProperties().Count, Is.EqualTo(1));
        Assert.That(target.GetProperty<string>("Key1"), Is.EqualTo("TargetValue1"));
    }

    [Test]
    public void MergeFrom_EmptyTarget_AddsAllSourceProperties()
    {
        var target = new PropertyBag();

        var source = new PropertyBag();
        source.SetProperty("Key1", "SourceValue1");
        source.SetProperty("Key2", "SourceValue2");

        target.MergeFrom(source);

        Assert.That(target.GetAllProperties().Count, Is.EqualTo(2));
        Assert.That(target.GetProperty<string>("Key1"), Is.EqualTo("SourceValue1"));
        Assert.That(target.GetProperty<string>("Key2"), Is.EqualTo("SourceValue2"));
    }

    #endregion

    #region StandardProperties Tests

    [Test]
    public void StandardProperties_FileName_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(StandardProperties.FileName, "test.txt");

        Assert.That(bag.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
    }

    [Test]
    public void StandardProperties_FileSize_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(StandardProperties.FileSize, 12345L);

        Assert.That(bag.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(12345L));
    }

    [Test]
    public void StandardProperties_IsDirectory_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(StandardProperties.IsDirectory, true);

        Assert.That(bag.GetProperty<bool>(StandardProperties.IsDirectory), Is.True);
    }

    [Test]
    public void StandardProperties_Dates_WorkCorrectly()
    {
        var bag = new PropertyBag();
        var now = DateTime.UtcNow;

        bag.SetProperty<DateTime?>(StandardProperties.CreatedDate, now);
        bag.SetProperty<DateTime?>(StandardProperties.ModifiedDate, now.AddDays(1));
        bag.SetProperty<DateTime?>(StandardProperties.AccessedDate, now.AddDays(2));

        Assert.That(bag.GetProperty<DateTime?>(StandardProperties.CreatedDate), Is.EqualTo(now));
        Assert.That(bag.GetProperty<DateTime?>(StandardProperties.ModifiedDate), Is.EqualTo(now.AddDays(1)));
        Assert.That(bag.GetProperty<DateTime?>(StandardProperties.AccessedDate), Is.EqualTo(now.AddDays(2)));
    }

    #endregion

    #region NdfsProperties Tests

    [Test]
    public void NdfsProperties_UserId_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(NdfsProperties.UserId, (ushort)42);

        Assert.That(bag.GetProperty<ushort>(NdfsProperties.UserId), Is.EqualTo((ushort)42));
    }

    [Test]
    public void NdfsProperties_ObjectType_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(NdfsProperties.ObjectType, (byte)1);

        Assert.That(bag.GetProperty<byte>(NdfsProperties.ObjectType), Is.EqualTo((byte)1));
    }

    [Test]
    public void NdfsProperties_AccessMode_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(NdfsProperties.AccessMode, (byte)0xFF);

        Assert.That(bag.GetProperty<byte>(NdfsProperties.AccessMode), Is.EqualTo((byte)0xFF));
    }

    #endregion

    #region AzureProperties Tests

    [Test]
    public void AzureProperties_ContentType_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(AzureProperties.ContentType, "application/octet-stream");

        Assert.That(bag.GetProperty<string>(AzureProperties.ContentType), Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public void AzureProperties_Metadata_WorksCorrectly()
    {
        var bag = new PropertyBag();
        var metadata = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        bag.SetProperty(AzureProperties.Metadata, metadata);

        var result = bag.GetProperty<Dictionary<string, string>>(AzureProperties.Metadata);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!["key1"], Is.EqualTo("value1"));
    }

    #endregion

    #region WindowsProperties Tests

    [Test]
    public void WindowsProperties_Owner_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(WindowsProperties.Owner, "DOMAIN\\User");

        Assert.That(bag.GetProperty<string>(WindowsProperties.Owner), Is.EqualTo("DOMAIN\\User"));
    }

    [Test]
    public void WindowsProperties_Attributes_WorksCorrectly()
    {
        var bag = new PropertyBag();
        bag.SetProperty(WindowsProperties.Attributes, System.IO.FileAttributes.ReadOnly | System.IO.FileAttributes.Hidden);

        var result = bag.GetProperty<System.IO.FileAttributes>(WindowsProperties.Attributes);
        Assert.That(result.HasFlag(System.IO.FileAttributes.ReadOnly), Is.True);
        Assert.That(result.HasFlag(System.IO.FileAttributes.Hidden), Is.True);
    }

    #endregion
}
