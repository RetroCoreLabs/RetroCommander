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
using RetroCommander.Core.Transfer;

namespace RetroCommander.Core.Tests;

[TestFixture]
public class PropertyMapperTests
{
    #region StandardPropertyMapper Tests

    [Test]
    public void StandardPropertyMapper_MapsFileName()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
    }

    [Test]
    public void StandardPropertyMapper_MapsFileSize()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileSize, 12345L);
        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(12345L));
    }

    [Test]
    public void StandardPropertyMapper_MapsIsDirectory()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.IsDirectory, true);
        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<bool>(StandardProperties.IsDirectory), Is.True);
    }

    [Test]
    public void StandardPropertyMapper_MapsDates()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var now = DateTime.UtcNow;
        source.SetProperty<DateTime?>(StandardProperties.CreatedDate, now);
        source.SetProperty<DateTime?>(StandardProperties.ModifiedDate, now.AddDays(1));
        source.SetProperty<DateTime?>(StandardProperties.AccessedDate, now.AddDays(2));

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<DateTime?>(StandardProperties.CreatedDate), Is.EqualTo(now));
        Assert.That(target.GetProperty<DateTime?>(StandardProperties.ModifiedDate), Is.EqualTo(now.AddDays(1)));
        Assert.That(target.GetProperty<DateTime?>(StandardProperties.AccessedDate), Is.EqualTo(now.AddDays(2)));
    }

    [Test]
    public void StandardPropertyMapper_MapsAttributes()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.IsReadOnly, true);
        source.SetProperty(StandardProperties.IsHidden, true);

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<bool>(StandardProperties.IsReadOnly), Is.True);
        Assert.That(target.GetProperty<bool>(StandardProperties.IsHidden), Is.True);
    }

    [Test]
    public void StandardPropertyMapper_IgnoresNonStandardProperties()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        source.SetProperty("CustomProperty", "custom-value");
        source.SetProperty(NdfsProperties.UserId, (ushort)42);

        mapper.MapProperties(source, target);

        Assert.That(target.HasProperty(StandardProperties.FileName), Is.True);
        Assert.That(target.HasProperty("CustomProperty"), Is.False);
        Assert.That(target.HasProperty(NdfsProperties.UserId), Is.False);
    }

    [Test]
    public void StandardPropertyMapper_DoesNotOverwriteMissingProperties()
    {
        var mapper = new StandardPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        // Source only has FileName
        source.SetProperty(StandardProperties.FileName, "test.txt");

        // Target already has FileSize
        target.SetProperty(StandardProperties.FileSize, 9999L);

        mapper.MapProperties(source, target);

        // FileName should be copied, FileSize should remain
        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        Assert.That(target.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(9999L));
    }

    #endregion

    #region DirectPropertyMapper Tests

    [Test]
    public void DirectPropertyMapper_CopiesAllProperties()
    {
        var mapper = new DirectPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        source.SetProperty(StandardProperties.FileSize, 12345L);
        source.SetProperty(NdfsProperties.UserId, (ushort)42);
        source.SetProperty("CustomProperty", "custom-value");

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        Assert.That(target.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(12345L));
        Assert.That(target.GetProperty<ushort>(NdfsProperties.UserId), Is.EqualTo((ushort)42));
        Assert.That(target.GetProperty<string>("CustomProperty"), Is.EqualTo("custom-value"));
    }

    [Test]
    public void DirectPropertyMapper_OverwritesExistingProperties()
    {
        var mapper = new DirectPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "new.txt");
        target.SetProperty(StandardProperties.FileName, "old.txt");

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("new.txt"));
    }

    [Test]
    public void DirectPropertyMapper_PreservesComplexTypes()
    {
        var mapper = new DirectPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var metadata = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        source.SetProperty(AzureProperties.Metadata, metadata);

        mapper.MapProperties(source, target);

        var targetMetadata = target.GetProperty<Dictionary<string, string>>(AzureProperties.Metadata);
        Assert.That(targetMetadata, Is.Not.Null);
        Assert.That(targetMetadata!.Count, Is.EqualTo(2));
        Assert.That(targetMetadata["key1"], Is.EqualTo("value1"));
    }

    #endregion

    #region NdfsToAzurePropertyMapper Tests

    [Test]
    public void NdfsToAzurePropertyMapper_MapsStandardProperties()
    {
        var mapper = new NdfsToAzurePropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        source.SetProperty(StandardProperties.FileSize, 12345L);

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        Assert.That(target.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(12345L));
    }

    [Test]
    public void NdfsToAzurePropertyMapper_StoresNdfsMetadataInAzureFormat()
    {
        var mapper = new NdfsToAzurePropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(NdfsProperties.UserId, (ushort)42);
        source.SetProperty(NdfsProperties.ObjectType, (byte)2);
        source.SetProperty(NdfsProperties.AccessMode, (byte)0xFF);
        source.SetProperty(NdfsProperties.ParityBit, true);
        source.SetProperty(NdfsProperties.UserName, "TestUser");

        mapper.MapProperties(source, target);

        var metadata = target.GetProperty<Dictionary<string, string>>(AzureProperties.Metadata);
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!["ndfs-userid"], Is.EqualTo("42"));
        Assert.That(metadata["ndfs-objecttype"], Is.EqualTo("2"));
        Assert.That(metadata["ndfs-accessmode"], Is.EqualTo("255"));
        Assert.That(metadata["ndfs-paritybit"], Is.EqualTo("True"));
        Assert.That(metadata["ndfs-username"], Is.EqualTo("TestUser"));
    }

    [Test]
    public void NdfsToAzurePropertyMapper_StoresEffectiveDateInIso8601()
    {
        var mapper = new NdfsToAzurePropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var effectiveDate = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
        source.SetProperty<DateTime?>(NdfsProperties.EffectiveDate, effectiveDate);

        mapper.MapProperties(source, target);

        var metadata = target.GetProperty<Dictionary<string, string>>(AzureProperties.Metadata);
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!["ndfs-effectivedate"], Does.Contain("2024-06-15"));
    }

    [Test]
    public void NdfsToAzurePropertyMapper_SetsDefaultContentType()
    {
        var mapper = new NdfsToAzurePropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.dat");

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(AzureProperties.ContentType), Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public void NdfsToAzurePropertyMapper_DoesNotOverwriteExistingContentType()
    {
        var mapper = new NdfsToAzurePropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        target.SetProperty(AzureProperties.ContentType, "text/plain");

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(AzureProperties.ContentType), Is.EqualTo("text/plain"));
    }

    #endregion

    #region AzureToNdfsPropertyMapper Tests

    [Test]
    public void AzureToNdfsPropertyMapper_MapsStandardProperties()
    {
        var mapper = new AzureToNdfsPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        source.SetProperty(StandardProperties.FileSize, 12345L);

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        Assert.That(target.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(12345L));
    }

    [Test]
    public void AzureToNdfsPropertyMapper_RestoresNdfsMetadataFromAzure()
    {
        var mapper = new AzureToNdfsPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var metadata = new Dictionary<string, string>
        {
            ["ndfs-userid"] = "42",
            ["ndfs-objecttype"] = "2",
            ["ndfs-accessmode"] = "255",
            ["ndfs-paritybit"] = "True",
            ["ndfs-username"] = "TestUser"
        };
        source.SetProperty(AzureProperties.Metadata, metadata);

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<ushort>(NdfsProperties.UserId), Is.EqualTo((ushort)42));
        Assert.That(target.GetProperty<byte>(NdfsProperties.ObjectType), Is.EqualTo((byte)2));
        Assert.That(target.GetProperty<byte>(NdfsProperties.AccessMode), Is.EqualTo((byte)255));
        Assert.That(target.GetProperty<bool>(NdfsProperties.ParityBit), Is.True);
        Assert.That(target.GetProperty<string>(NdfsProperties.UserName), Is.EqualTo("TestUser"));
    }

    [Test]
    public void AzureToNdfsPropertyMapper_RestoresEffectiveDateFromAzure()
    {
        var mapper = new AzureToNdfsPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var metadata = new Dictionary<string, string>
        {
            ["ndfs-effectivedate"] = "2024-06-15T12:30:45.0000000Z"
        };
        source.SetProperty(AzureProperties.Metadata, metadata);

        mapper.MapProperties(source, target);

        var effectiveDate = target.GetProperty<DateTime?>(NdfsProperties.EffectiveDate);
        Assert.That(effectiveDate, Is.Not.Null);
        Assert.That(effectiveDate!.Value.Year, Is.EqualTo(2024));
        Assert.That(effectiveDate.Value.Month, Is.EqualTo(6));
        Assert.That(effectiveDate.Value.Day, Is.EqualTo(15));
    }

    [Test]
    public void AzureToNdfsPropertyMapper_SetsDefaults_WhenNoMetadata()
    {
        var mapper = new AzureToNdfsPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");

        mapper.MapProperties(source, target);

        // Should set defaults
        Assert.That(target.GetProperty<ushort>(NdfsProperties.UserId), Is.EqualTo((ushort)0));
        Assert.That(target.GetProperty<byte>(NdfsProperties.ObjectType), Is.EqualTo((byte)1));
        Assert.That(target.GetProperty<byte>(NdfsProperties.AccessMode), Is.EqualTo((byte)0xFF));
        Assert.That(target.GetProperty<bool>(NdfsProperties.ParityBit), Is.False);
    }

    [Test]
    public void AzureToNdfsPropertyMapper_MapsModifiedDateToEffectiveDate_WhenNoNdfsDate()
    {
        var mapper = new AzureToNdfsPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var now = DateTime.UtcNow;
        source.SetProperty<DateTime?>(StandardProperties.ModifiedDate, now);

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<DateTime?>(NdfsProperties.EffectiveDate), Is.EqualTo(now));
    }

    [Test]
    public void AzureToNdfsPropertyMapper_HandlesMalformedMetadata()
    {
        var mapper = new AzureToNdfsPropertyMapper();
        var source = new PropertyBag();
        var target = new PropertyBag();

        var metadata = new Dictionary<string, string>
        {
            ["ndfs-userid"] = "not-a-number",
            ["ndfs-objecttype"] = "invalid",
            ["ndfs-accessmode"] = "xyz"
        };
        source.SetProperty(AzureProperties.Metadata, metadata);

        // Should not throw, just use defaults
        Assert.DoesNotThrow(() => mapper.MapProperties(source, target));

        // Defaults should be set since parsing failed
        Assert.That(target.HasProperty(NdfsProperties.UserId), Is.False);
    }

    #endregion

    #region PropertyMapperRegistry Tests

    [Test]
    public void PropertyMapperRegistry_RegisterAndRetrieveMapper()
    {
        var registry = new PropertyMapperRegistry();
        var mapper = new DirectPropertyMapper();

        registry.RegisterMapper("TypeA", "TypeB", mapper);
        var retrieved = registry.GetMapper("TypeA", "TypeB");

        Assert.That(retrieved, Is.SameAs(mapper));
    }

    [Test]
    public void PropertyMapperRegistry_ReturnsFallbackForUnregistered()
    {
        var registry = new PropertyMapperRegistry();

        var mapper = registry.GetMapper("UnknownType", "AnotherUnknown");

        // Should return fallback (StandardPropertyMapper)
        Assert.That(mapper, Is.Not.Null);
        Assert.That(mapper, Is.TypeOf<StandardPropertyMapper>());
    }

    [Test]
    public void PropertyMapperRegistry_CreateDefault_HasNdfsToWindows()
    {
        // NDFS-specific metadata is preserved on export via a .xat sidecar file written by
        // SmartFileOperations, not via the property mapper — see PropertyMapperRegistry.CreateDefault.
        var registry = PropertyMapperRegistry.CreateDefault();

        var mapper = registry.GetMapper("NDFS", "Windows");

        Assert.That(mapper, Is.TypeOf<StandardPropertyMapper>());
    }

    [Test]
    public void PropertyMapperRegistry_CreateDefault_HasWindowsToNdfs()
    {
        var registry = PropertyMapperRegistry.CreateDefault();

        var mapper = registry.GetMapper("Windows", "NDFS");

        Assert.That(mapper, Is.TypeOf<StandardPropertyMapper>());
    }

    [Test]
    public void PropertyMapperRegistry_CreateDefault_HasNdfsToAzure()
    {
        var registry = PropertyMapperRegistry.CreateDefault();

        var mapper = registry.GetMapper("NDFS", "Azure");

        Assert.That(mapper, Is.TypeOf<NdfsToAzurePropertyMapper>());
    }

    [Test]
    public void PropertyMapperRegistry_CreateDefault_HasAzureToNdfs()
    {
        var registry = PropertyMapperRegistry.CreateDefault();

        var mapper = registry.GetMapper("Azure", "NDFS");

        Assert.That(mapper, Is.TypeOf<AzureToNdfsPropertyMapper>());
    }

    [Test]
    public void PropertyMapperRegistry_CreateDefault_HasDirectMappersForSameType()
    {
        var registry = PropertyMapperRegistry.CreateDefault();

        Assert.That(registry.GetMapper("NDFS", "NDFS"), Is.TypeOf<DirectPropertyMapper>());
        Assert.That(registry.GetMapper("Azure", "Azure"), Is.TypeOf<DirectPropertyMapper>());
        Assert.That(registry.GetMapper("Windows", "Windows"), Is.TypeOf<DirectPropertyMapper>());
        Assert.That(registry.GetMapper("TNFS", "TNFS"), Is.TypeOf<DirectPropertyMapper>());
        Assert.That(registry.GetMapper("CBM", "CBM"), Is.TypeOf<DirectPropertyMapper>());
    }

    [Test]
    public void PropertyMapperRegistry_OverwritesExistingMapper()
    {
        var registry = new PropertyMapperRegistry();
        var mapper1 = new DirectPropertyMapper();
        var mapper2 = new StandardPropertyMapper();

        registry.RegisterMapper("TypeA", "TypeB", mapper1);
        registry.RegisterMapper("TypeA", "TypeB", mapper2);

        var retrieved = registry.GetMapper("TypeA", "TypeB");
        Assert.That(retrieved, Is.SameAs(mapper2));
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public void RoundTrip_NdfsToAzureToNdfs_PreservesMetadata()
    {
        var toAzure = new NdfsToAzurePropertyMapper();
        var toNdfs = new AzureToNdfsPropertyMapper();

        var original = new PropertyBag();
        original.SetProperty(StandardProperties.FileName, "test.txt");
        original.SetProperty(StandardProperties.FileSize, 12345L);
        original.SetProperty(NdfsProperties.UserId, (ushort)42);
        original.SetProperty(NdfsProperties.ObjectType, (byte)2);
        original.SetProperty(NdfsProperties.AccessMode, (byte)0xFF);
        original.SetProperty(NdfsProperties.ParityBit, true);
        original.SetProperty(NdfsProperties.UserName, "TestUser");

        var azure = new PropertyBag();
        toAzure.MapProperties(original, azure);

        var restored = new PropertyBag();
        toNdfs.MapProperties(azure, restored);

        Assert.That(restored.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        Assert.That(restored.GetProperty<long>(StandardProperties.FileSize), Is.EqualTo(12345L));
        Assert.That(restored.GetProperty<ushort>(NdfsProperties.UserId), Is.EqualTo((ushort)42));
        Assert.That(restored.GetProperty<byte>(NdfsProperties.ObjectType), Is.EqualTo((byte)2));
        Assert.That(restored.GetProperty<byte>(NdfsProperties.AccessMode), Is.EqualTo((byte)0xFF));
        Assert.That(restored.GetProperty<bool>(NdfsProperties.ParityBit), Is.True);
        Assert.That(restored.GetProperty<string>(NdfsProperties.UserName), Is.EqualTo("TestUser"));
    }

    #endregion

    #region Through the registry (the way SmartFileOperations calls them)

    // The tests above call the concrete mapper classes, where the subclass method is the one
    // found. The real code gets an IPropertyMapper from the registry, so these go through that.
    // They failed while the subclass methods hid the base method with "new" instead of
    // overriding it: only the standard properties were copied and the NDFS/Azure metadata was lost.

    [Test]
    public void Registry_NdfsToAzure_StoresNdfsMetadataWhenCalledThroughTheInterface()
    {
        IPropertyMapper mapper = PropertyMapperRegistry.CreateDefault().GetMapper("NDFS", "Azure");
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        source.SetProperty(NdfsProperties.UserId, (ushort)42);
        source.SetProperty(NdfsProperties.UserName, "TestUser");

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        var metadata = target.GetProperty<Dictionary<string, string>>(AzureProperties.Metadata);
        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!["ndfs-userid"], Is.EqualTo("42"));
        Assert.That(metadata["ndfs-username"], Is.EqualTo("TestUser"));
        Assert.That(target.GetProperty<string>(AzureProperties.ContentType), Is.EqualTo("application/octet-stream"));
    }

    [Test]
    public void Registry_AzureToNdfs_RestoresNdfsPropertiesWhenCalledThroughTheInterface()
    {
        IPropertyMapper mapper = PropertyMapperRegistry.CreateDefault().GetMapper("Azure", "NDFS");
        var source = new PropertyBag();
        var target = new PropertyBag();

        source.SetProperty(StandardProperties.FileName, "test.txt");
        source.SetProperty(AzureProperties.Metadata, new Dictionary<string, string>
        {
            ["ndfs-userid"] = "42",
            ["ndfs-username"] = "TestUser"
        });

        mapper.MapProperties(source, target);

        Assert.That(target.GetProperty<string>(StandardProperties.FileName), Is.EqualTo("test.txt"));
        Assert.That(target.GetProperty<ushort>(NdfsProperties.UserId), Is.EqualTo((ushort)42));
        Assert.That(target.GetProperty<string>(NdfsProperties.UserName), Is.EqualTo("TestUser"));
    }

    #endregion
}
