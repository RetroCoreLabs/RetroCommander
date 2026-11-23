//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Desktop;

/// <summary>
/// File, New Image: creates a blank disk of any kind the RetroIO drivers can make.
/// </summary>
/// <remarks>
/// The window is driven entirely by the drivers' blank-disk metadata (<see cref="IBlankImageProvider"/>,
/// read through <see cref="BlankImageCatalogue"/>). Its steps:
///  - pick a machine (every <see cref="BlankImageLayout.Machine"/> the drivers name).
///  - pick a disk (that machine's layouts, by name and size).
///  - fill in the layout's own options; each option checks its own value (<see cref="BlankImageOption.Check"/>).
///  - choose the output file; the extension comes from the layout.
/// There is no format name, size, rule or extension in this file. A format RetroIO learns to
/// create appears here with no change, and a test fails if a format name is ever written into it.
/// </remarks>
public partial class NewImageDialog : Window
{
    private readonly BlankImageChoice[] _all;
    private readonly List<BlankImageChoice> _disks = new List<BlankImageChoice>();

    // The option controls of the selected layout, in the layout's option order.
    private readonly List<OptionInput> _inputs = new List<OptionInput>();

    // Guards the change handlers while controls are filled in by code.
    private bool _loading = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewImageDialog"/> class, with every blank disk
    /// the drivers describe, and the first machine selected.
    /// </summary>
    public NewImageDialog()
    {
        InitializeComponent();
        _all = BlankImageCatalogue.List();
        MachineList.ItemsSource = BlankImageCatalogue.Machines(_all);
        _loading = false;
        if (_all.Length > 0)
        {
            MachineList.SelectedIndex = 0;
        }
        Validate();
    }

    /// <summary>
    /// Gets the full path of the created image; null until one was created.
    /// </summary>
    public string? CreatedImagePath { get; private set; }

    /// <summary>
    /// Gets the disk that was created, so the caller can mount it as that exact layout.
    /// </summary>
    public BlankImageChoice? CreatedChoice { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user asked for the new image to be mounted.
    /// </summary>
    public bool ShouldMount { get; private set; }

    /// <summary>
    /// Gets the layout picked now, or null when none is.
    /// </summary>
    internal BlankImageChoice? SelectedDisk =>
        DiskCombo.SelectedIndex >= 0 && DiskCombo.SelectedIndex < _disks.Count ? _disks[DiskCombo.SelectedIndex] : null;

    /// <summary>
    /// Selects a machine by name, as a click in the list would; for tests.
    /// </summary>
    /// <param name="machine">
    /// A machine name the drivers give.
    /// </param>
    /// <returns>
    /// True when the machine is in the list.
    /// </returns>
    internal bool SelectMachine(string machine)
    {
        string[] machines = (string[])MachineList.ItemsSource!;
        for (int i = 0; i < machines.Length; i++)
        {
            if (string.Equals(machines[i], machine, StringComparison.OrdinalIgnoreCase))
            {
                MachineList.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Selects a disk of the current machine by layout id, as a pick in the list would; for tests.
    /// </summary>
    /// <param name="layoutId">
    /// The layout id the driver gives.
    /// </param>
    /// <returns>
    /// True when the layout belongs to the selected machine.
    /// </returns>
    internal bool SelectDisk(string layoutId)
    {
        for (int i = 0; i < _disks.Count; i++)
        {
            if (_disks[i].Layout.Id == layoutId)
            {
                DiskCombo.SelectedIndex = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets an option's value, as typing or picking it would; for tests.
    /// </summary>
    /// <param name="optionId">
    /// The option id the driver gives.
    /// </param>
    /// <param name="value">
    /// The value, as text ("true"/"false" for a tick box).
    /// </param>
    internal void SetOption(string optionId, string value)
    {
        for (int i = 0; i < _inputs.Count; i++)
        {
            if (_inputs[i].Option.Id == optionId)
            {
                _inputs[i].SetValue(value);
            }
        }
    }

    /// <summary>
    /// Gets the option controls shown now; for tests.
    /// </summary>
    internal int OptionCount => _inputs.Count;

    /// <summary>
    /// Reads the controls into the values to create the disk with; UI thread only.
    /// </summary>
    /// <returns>
    /// The option values by option id.
    /// </returns>
    internal Dictionary<string, string> OptionValues()
    {
        Dictionary<string, string> values = new Dictionary<string, string>(_inputs.Count);
        for (int i = 0; i < _inputs.Count; i++)
        {
            values[_inputs[i].Option.Id] = _inputs[i].GetValue();
        }
        return values;
    }

    /// <summary>
    /// Checks every input and shows the first problem; returns whether Create may be pressed.
    /// </summary>
    /// <returns>
    /// True when every option is accepted by its driver and an output file is chosen.
    /// </returns>
    internal bool Validate()
    {
        string? error = null;
        if (SelectedDisk == null)
        {
            error = "Pick a disk.";
        }

        for (int i = 0; i < _inputs.Count && error == null; i++)
        {
            error = _inputs[i].Option.Check(_inputs[i].GetValue());
        }

        string path = (OutputPath.Text ?? string.Empty).Trim();
        if (error == null && path.Length > 0 && File.Exists(path))
        {
            error = "The file already exists; choose another name.";
        }

        ValidationText.Text = error ?? string.Empty;
        ValidationText.IsVisible = error != null;
        bool ok = error == null && path.Length > 0;
        CreateButton.IsEnabled = ok;

        // An unformatted disk has no file system, so there is nothing to mount.
        bool unformatted = UnformattedCheck.IsChecked == true;
        MountAfterCreate.IsEnabled = !unformatted;
        if (unformatted)
        {
            MountAfterCreate.IsChecked = false;
        }
        return ok;
    }

    private void OnMachineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || MachineList.SelectedItem is not string machine)
        {
            return;
        }

        _loading = true;
        MachineTitle.Text = machine;
        _disks.Clear();
        List<string> names = new List<string>();
        for (int i = 0; i < _all.Length; i++)
        {
            if (string.Equals(_all[i].Layout.Machine, machine, StringComparison.OrdinalIgnoreCase))
            {
                _disks.Add(_all[i]);
                names.Add(_all[i].Layout.Name);
            }
        }
        DiskCombo.ItemsSource = names;
        _loading = false;
        DiskCombo.SelectedIndex = _disks.Count > 0 ? 0 : -1;
    }

    private void OnDiskSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        BuildOptions(SelectedDisk);
        ShowDetails(SelectedDisk);
        Validate();
    }

    private void OnInputChanged(object? sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            Validate();
        }
    }

    // One row per option of the layout: a label and a control of the option's kind.
    private void BuildOptions(BlankImageChoice? choice)
    {
        _inputs.Clear();
        OptionsGrid.Children.Clear();
        OptionsGrid.RowDefinitions.Clear();
        if (choice == null)
        {
            return;
        }

        IReadOnlyList<BlankImageOption> options = choice.Layout.Options;
        for (int i = 0; i < options.Count; i++)
        {
            OptionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock label = new TextBlock
            {
                Text = options[i].Label + ":",
                MinWidth = 120,
                MaxWidth = 260,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            label.Classes.Add("Label");
            Grid.SetRow(label, i);
            OptionsGrid.Children.Add(label);

            OptionInput input = OptionInput.For(options[i], () => { if (!_loading) Validate(); });
            Grid.SetRow(input.Control, i);
            Grid.SetColumn(input.Control, 1);
            OptionsGrid.Children.Add(input.Control);
            _inputs.Add(input);
        }
    }

    private void ShowDetails(BlankImageChoice? choice)
    {
        if (choice == null)
        {
            DetailsText.Text = string.Empty;
            return;
        }

        // The driver's internal format name is not shown: it names the driver, not the disk
        // (PC hard disks come from the PC floppy driver, so its name would call them floppies).
        BlankImageLayout l = choice.Layout;
        string text = l.SizeBytes.ToString("N0") + " bytes, file type " + l.Extension;
        if (l.Details.Length > 0)
        {
            text += "\n" + l.Details;
        }
        if (l.SameBytesAs.Count > 0)
        {
            // Said plainly, because the disk will only open as this machine's layout when it is
            // mounted from here; detection alone cannot tell these machines apart while it is empty.
            string[] same = new string[l.SameBytesAs.Count];
            for (int i = 0; i < same.Length; i++)
            {
                same[i] = DescribeLayout(l.SameBytesAs[i]);
            }
            text += "\nAn empty disk of this kind is byte for byte the same as: " + string.Join("; ", same)
                + ". It is opened as the disk picked here; once it holds files it is recognised by itself.";
        }
        DetailsText.Text = text;
    }

    // "Kaypro 4, 400 KB floppy - ..." for a layout id, from the drivers' own names.
    private string DescribeLayout(string layoutId)
    {
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i].Layout.Id == layoutId)
            {
                return _all[i].Layout.Machine + ", " + _all[i].Layout.Name;
            }
        }
        return layoutId;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        BlankImageChoice? choice = SelectedDisk;
        if (choice == null)
        {
            return;
        }

        string ext = choice.Layout.Extension;
        string bare = ext.TrimStart('.');
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Disk Image",
            DefaultExtension = bare,
            SuggestedFileName = "new" + ext,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Disk image (*" + ext + ")") { Patterns = new[] { "*" + ext } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file != null)
        {
            OutputPath.Text = file.Path.LocalPath;
        }
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        BlankImageChoice? choice = SelectedDisk;
        string path = (OutputPath.Text ?? string.Empty).Trim();
        if (choice == null || !Validate())
        {
            return;
        }

        CreateButton.IsEnabled = false;
        try
        {
            // Read every control here, on the UI thread; the disk is written on a background
            // thread (a hard disk can be hundreds of MB) and controls may not be touched there.
            Dictionary<string, string> values = OptionValues();
            bool unformatted = UnformattedCheck.IsChecked == true;
            await Task.Run(() => BlankImageCatalogue.Create(choice, values, path, unformatted));

            CreatedImagePath = path;
            CreatedChoice = choice;
            ShouldMount = !unformatted && MountAfterCreate.IsChecked == true;
            Close(true);
        }
        catch (Exception ex)
        {
            ValidationText.Text = "Create failed: " + ex.Message;
            ValidationText.IsVisible = true;
            CreateButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    // One option's control: a text box, a list or a tick box, by the option's kind.
    private sealed class OptionInput
    {
        private OptionInput(BlankImageOption option, Control control)
        {
            Option = option;
            Control = control;
        }

        public delegate void Changed();

        public BlankImageOption Option { get; }

        public Control Control { get; }

        public static OptionInput For(BlankImageOption option, Changed changed)
        {
            switch (option.Kind)
            {
                case BlankImageOptionKind.Choice:
                    {
                        ComboBox combo = new ComboBox { ItemsSource = option.Choices, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
                        int at = -1;
                        for (int i = 0; i < option.Choices.Count; i++)
                        {
                            if (option.Choices[i] == option.Default)
                            {
                                at = i;
                            }
                        }
                        combo.SelectedIndex = at >= 0 ? at : (option.Choices.Count > 0 ? 0 : -1);
                        combo.SelectionChanged += (_, _) => changed();
                        return new OptionInput(option, combo);
                    }

                case BlankImageOptionKind.Flag:
                    {
                        CheckBox box = new CheckBox { IsChecked = option.Default == "true" };
                        box.IsCheckedChanged += (_, _) => changed();
                        return new OptionInput(option, box);
                    }

                default:
                    {
                        TextBox text = new TextBox { Text = option.Default };
                        if (option.MaxLength > 0)
                        {
                            text.MaxLength = option.MaxLength;
                        }
                        text.TextChanged += (_, _) => changed();
                        return new OptionInput(option, text);
                    }
            }
        }

        public string GetValue()
        {
            return Control switch
            {
                ComboBox c => c.SelectedItem as string ?? string.Empty,
                CheckBox b => b.IsChecked == true ? "true" : "false",
                TextBox t => t.Text ?? string.Empty,
                _ => string.Empty
            };
        }

        public void SetValue(string value)
        {
            switch (Control)
            {
                case ComboBox c:
                    c.SelectedItem = value;
                    break;
                case CheckBox b:
                    b.IsChecked = value == "true";
                    break;
                case TextBox t:
                    t.Text = value;
                    break;
            }
        }
    }
}
