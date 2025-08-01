// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Import
{
    public partial class FileImportScreen : OsuScreen
    {
        public override bool HideOverlaysOnEnter => true;

        private OsuFileSelector fileSelector;
        private Container contentContainer;
        private TextFlowContainer currentFileText;

        private RoundedButton importButton;
        private RoundedButton importAllButton;

        private const float duration = 300;
        private const float button_height = 50;
        private const float button_vertical_margin = 15;

        [Resolved]
        private OsuGameBase game { get; set; }

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [BackgroundDependencyLoader(true)]
        private void load()
        {
            InternalChild = contentContainer = new Container
            {
                Masking = true,
                CornerRadius = 10,
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(0.9f, 0.8f),
                Children = new Drawable[]
                {
                    fileSelector = new OsuFileSelector(validFileExtensions: game.HandledExtensions.ToArray())
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.65f
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.35f,
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                Colour = colourProvider.Background4,
                                RelativeSizeAxes = Axes.Both
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Bottom = button_height * 2 + button_vertical_margin * 3 },
                                Child = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Child = currentFileText = new TextFlowContainer(t => t.Font = OsuFont.Default.With(size: 30))
                                    {
                                        AutoSizeAxes = Axes.Y,
                                        RelativeSizeAxes = Axes.X,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        TextAnchor = Anchor.Centre
                                    },
                                    ScrollContent =
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                    }
                                },
                            },
                            importButton = new RoundedButton
                            {
                                Text = "Import selected file",
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                RelativeSizeAxes = Axes.X,
                                Height = button_height,
                                Width = 0.9f,
                                Margin = new MarginPadding { Bottom = button_height + button_vertical_margin * 2 },
                                Action = () => startImport(fileSelector.CurrentFile.Value?.FullName)
                            },

                            importAllButton = new RoundedButton
                            {
                                Text = "Import all files from directory",
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.BottomCentre,
                                RelativeSizeAxes = Axes.X,
                                Height = button_height,
                                Width = 0.9f,
                                TooltipText = "Imports all osu files from selected directory",
                                Margin = new MarginPadding { Vertical = button_vertical_margin },
                                Action = () => startDirectoryImport(fileSelector.CurrentPath.Value?.FullName)
                            }
                        }
                    }
                }
            };

            fileSelector.CurrentFile.BindValueChanged(fileChanged, true);
            fileSelector.CurrentPath.BindValueChanged(directoryChanged);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            contentContainer.ScaleTo(0.95f).ScaleTo(1, duration, Easing.OutQuint);
            this.FadeInFromZero(duration);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            contentContainer.ScaleTo(0.95f, duration, Easing.OutQuint);
            this.FadeOut(duration, Easing.OutQuint);

            return base.OnExiting(e);
        }

        private void directoryChanged(ValueChangedEvent<DirectoryInfo> directoryChangedEvent)
        {
            // this should probably be done by the selector itself, but let's do it here for now.
            fileSelector.CurrentFile.Value = null;

            DirectoryInfo newDirectory = directoryChangedEvent.NewValue;
            importAllButton.Enabled.Value =
                // this will be `null` if the user clicked the "Computer" option (showing drives)
                // handling that is difficult due to platform differences, and nobody sane wants that to work with the "import all" button anyway
                newDirectory != null
                // extra safety against various I/O errors (lack of access, deleted directory, etc.)
                && newDirectory.Exists
                // there must be at least one file in the current directory for the game to import (non-recursive)
                && newDirectory.EnumerateFiles().Any(file => game.HandledExtensions.Contains(file.Extension));
        }

        private void fileChanged(ValueChangedEvent<FileInfo> selectedFile)
        {
            importButton.Enabled.Value = selectedFile.NewValue != null;
            currentFileText.Text = selectedFile.NewValue?.Name ?? "Select a file";
        }

        private void startImport(params string[] paths)
        {
            if (paths.Length == 0)
                return;

            Task.Factory.StartNew(async () =>
            {
                await game.Import(paths).ConfigureAwait(false);

                // some files will be deleted after successful import, so we want to refresh the view.
                Schedule(() =>
                {
                    // should probably be exposed as a refresh method.
                    fileSelector.CurrentPath.TriggerChange();
                });
            }, TaskCreationOptions.LongRunning);
        }

        private void startDirectoryImport(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            // get only files that match extensions handled by the game
            IEnumerable<string> filesToImport = Directory.EnumerateFiles(path)
                                                         .Where(file => game.HandledExtensions.Contains(Path.GetExtension(file)));
            if (!filesToImport.Any())
                return;

            startImport(filesToImport.ToArray());
        }
    }
}
