//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using Avalonia.Controls;
using Avalonia.Interactivity;
using RetroCommander.Core.Metadata;

namespace RetroCommander.Desktop;

public partial class NdfsInfoDialog : Window
{
    public bool MountConfirmed { get; private set; }
    public bool DontShowAgain { get; private set; }

    public NdfsInfoDialog()
    {
        InitializeComponent();
    }

    public NdfsInfoDialog(NdfsMetadata metadata) : this()
    {
        PopulateFields(metadata);
    }

    private void PopulateFields(NdfsMetadata metadata)
    {
        // NO business logic here - just display the metadata!

        // Volume Info
        DirNameText.Text = metadata.DirectoryName;
        VolumeLabelText.Text = metadata.VolumeLabel;
        DiskTypeText.Text = metadata.DiskType;

        // Capacity
        if (metadata.HasExtendedInfo)
        {
            TotalCapacityText.Text = $"{metadata.TotalCapacityMB:F2} MB ({metadata.TotalCapacityPages:N0} pages)";
            UsedSpaceText.Text = $"{metadata.UsedSpaceMB:F2} MB ({metadata.UsedSpacePages:N0} pages)";
            FreeSpaceText.Text = $"{metadata.FreeSpaceMB:F2} MB ({metadata.FreeSpacePages:N0} pages)";
            UsedPercentText.Text = $"{metadata.UsedPercent:F1}%";
        }
        else
        {
            TotalCapacityText.Text = "Unknown (No Extended Info)";
            UsedSpaceText.Text = "Unknown";
            FreeSpaceText.Text = $"{metadata.FreeSpaceMB:F2} MB ({metadata.FreeSpacePages:N0} pages)";
            UsedPercentText.Text = "Unknown";
        }

        // System Info
        // The extended-info system number (word 1755B) is the only system identifier NDFS stores.
        // 0 means no owning system was recorded — not "system zero".
        if (metadata.CpuSystemNumber.HasValue)
        {
            CpuSystemNumberText.Text = metadata.CpuSystemNumber.Value == 0
                ? "0 (none recorded)"
                : metadata.CpuSystemNumber.Value.ToString();
        }
        else
        {
            CpuSystemNumberText.Text = "N/A";
        }

        BootFormatText.Text = metadata.BootFormat;
        BootableText.Text = metadata.IsBootable ? "Yes ✓" : "No";

        // Technical Details
        ObjectFileBlockText.Text = metadata.ObjectFileBlock?.ToString() ?? "N/A";
        UserFileBlockText.Text = metadata.UserFileBlock?.ToString() ?? "N/A";
        BitFileBlockText.Text = metadata.BitFileBlock?.ToString() ?? "N/A";
        ChecksumText.Text = metadata.ChecksumStatus;
        ExtInfoText.Text = metadata.HasExtendedInfo ? "Valid ✓" : "Not Available";

        // Warnings
        if (metadata.Warnings.Count > 0)
        {
            WarningsPanel.IsVisible = true;
            WarningsTextBlock.Text = string.Join("\n", metadata.Warnings);
        }
        else
        {
            WarningsPanel.IsVisible = false;
        }
    }

    private void OnMountClick(object? sender, RoutedEventArgs e)
    {
        MountConfirmed = true;
        DontShowAgain = DontShowCheckBox.IsChecked == true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        MountConfirmed = false;
        DontShowAgain = false;
        Close(false);
    }
}
