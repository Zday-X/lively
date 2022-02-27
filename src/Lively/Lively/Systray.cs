﻿using Lively.Common;
using Lively.Common.Services;
using Lively.Core;
using Lively.Core.Display;
using Lively.Core.Suspend;
using Lively.Helpers.Theme;
using Lively.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Lively
{

    //TODO: Switch to wpf-notifyicon library instead.
    public class Systray : ISystray
    {
        private readonly NotifyIcon _notifyIcon = new NotifyIcon();
        private ToolStripMenuItem pauseTrayBtn, customiseWallpaperBtn, updateTrayBtn;
        private bool disposedValue;

        private readonly IRunnerService runner;
        private readonly IDesktopCore desktopCore;
        private readonly IDisplayManager displayManager;
        private readonly IUserSettingsService userSettings;
        private readonly IPlayback playbackMonitor;

        public Systray(IRunnerService runner,
            IUserSettingsService userSettings,
            IDesktopCore desktopCore,
            IAppUpdaterService appUpdater,
            IDisplayManager displayManager,
            IPlayback playbackMonitor)
        {
            this.runner = runner;
            this.desktopCore = desktopCore;
            this.userSettings = userSettings;
            this.displayManager = displayManager;
            this.playbackMonitor = playbackMonitor;

            //NotifyIcon Fix: https://stackoverflow.com/questions/28833702/wpf-notifyicon-crash-on-first-run-the-root-visual-of-a-visualtarget-cannot-hav/29116917
            //Error: "The root Visual of a VisualTarget cannot have a parent.."
            System.Windows.Controls.ToolTip tt = new System.Windows.Controls.ToolTip();
            tt.IsOpen = true;
            tt.IsOpen = false;

            //Show UI
            _notifyIcon.DoubleClick += (s, args) => runner.ShowUI();
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.Icon = Properties.Resources.appicon;
            _notifyIcon.Text = "Lively Wallpaper";
            _notifyIcon.Visible = userSettings.Settings.SysTrayIcon;
            _notifyIcon.ContextMenuStrip = new ContextMenuStrip
            {
                ForeColor = Color.AliceBlue,
                Padding = new Padding(0),
                Margin = new Padding(0),
                //Font = new System.Drawing.Font("Segoe UI", 10F),
            };
            _notifyIcon.ContextMenuStrip.Renderer = new ContextMenuTheme.RendererDark();
            _notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;

            _notifyIcon.ContextMenuStrip.Items.Add("Open Lively", Properties.Resources.icons8_home_screen_96).Click += (s, e) => runner.ShowUI();
            _notifyIcon.ContextMenuStrip.Items.Add("Close all wallpaper(s)", null).Click += (s, e) => desktopCore.CloseAllWallpapers(true);
            pauseTrayBtn = new ToolStripMenuItem("Pause Wallpaper(s)", null);
            pauseTrayBtn.Click += (s, e) =>
            {
                playbackMonitor.WallpaperPlayback = (playbackMonitor.WallpaperPlayback == PlaybackState.play) ? 
                    playbackMonitor.WallpaperPlayback = PlaybackState.paused : PlaybackState.play;
            };
            _notifyIcon.ContextMenuStrip.Items.Add(pauseTrayBtn);
            customiseWallpaperBtn = new ToolStripMenuItem("Customise Wallpaper", null)
            {
                Enabled = false,
                Visible = false,
            };
            customiseWallpaperBtn.Click += CustomiseWallpaper;
            _notifyIcon.ContextMenuStrip.Items.Add(customiseWallpaperBtn);

            if (!Constants.ApplicationType.IsMSIX)
            {
                updateTrayBtn = new ToolStripMenuItem("Checking for update", null)
                {
                    Enabled = false
                };
                updateTrayBtn.Click += (s, e) => App.AppUpdateDialog(appUpdater.LastCheckUri, appUpdater.LastCheckChangelog);
                _notifyIcon.ContextMenuStrip.Items.Add(updateTrayBtn);
            }

            _notifyIcon.ContextMenuStrip.Items.Add(new ContextMenuTheme.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add("Report bug", Properties.Resources.icons8_website_bug_96).Click += (s, e) => 
                LinkHandler.OpenBrowser("https://github.com/rocksdanister/lively/wiki/Common-Problems");
            _notifyIcon.ContextMenuStrip.Items.Add(new ContextMenuTheme.StripSeparatorCustom().stripSeparator);
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", Properties.Resources.icons8_close_96).Click += (s, e) => App.ShutDown();

            playbackMonitor.PlaybackStateChanged += Playback_PlaybackStateChanged;
            desktopCore.WallpaperChanged += DesktopCore_WallpaperChanged;
            appUpdater.UpdateChecked += (s, e) => { SetUpdateMenu(e.UpdateStatus); };
        }

        public void Visibility(bool visible)
        {
            _notifyIcon.Visible = visible;
        }

        /// <summary>
        /// Fix for traymenu opening to the nearest screen instead of the screen in which cursor is located.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ContextMenuStrip menuStrip = (sender as ContextMenuStrip);
            if (displayManager.IsMultiScreen())
            {
                //Finding screen in which cursor is present.
                var screen = displayManager.GetDisplayMonitorFromPoint(Cursor.Position);

                var mousePos = Cursor.Position;
                //Converting global cursor pos. to given screen pos.
                mousePos.X += -1 * screen.Bounds.X;
                mousePos.Y += -1 * screen.Bounds.Y;

                //guessing taskbar pos. based on cursor pos. on display.
                bool isLeft = mousePos.X < screen.Bounds.Width * .5;
                bool isTop = mousePos.Y < screen.Bounds.Height * .5;

                //menu popup pos. rule.
                if (isLeft && isTop)
                {
                    //not possible?
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.Default);
                }
                if (isLeft && !isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveRight);
                }
                else if (!isLeft && isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.BelowLeft);
                }
                else if (!isLeft && !isTop)
                {
                    menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
                }
            }
            else
            {
                menuStrip.Show(Cursor.Position, ToolStripDropDownDirection.AboveLeft);
            }
        }

        public void ShowBalloonNotification(int timeout, string title, string msg)
        {
            _notifyIcon.ShowBalloonTip(timeout, title, msg, ToolTipIcon.None);
        }

        private void Playback_PlaybackStateChanged(object sender, PlaybackState e)
        {
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                pauseTrayBtn.Checked = e == PlaybackState.paused;
                //_notifyIcon.Icon = (e == PlaybackState.paused) ? Properties.Icons.appicon_gray : Properties.Icons.appicon;
            }));
        }

        private void DesktopCore_WallpaperChanged(object sender, EventArgs e)
        {
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                customiseWallpaperBtn.Enabled = desktopCore.Wallpapers.Any(x => x.LivelyPropertyCopyPath != null);
            }));
        }

        private void CustomiseWallpaper(object sender, EventArgs e)
        {
            var items = desktopCore.Wallpapers.Where(x => x.LivelyPropertyCopyPath != null);
            if (items.Count() == 0)
            {
                //not possible, menu should be disabled.
                //nothing..
            }
            else if (items.Count() == 1)
            {
                //quick wallpaper customise tray widget.
                runner.ShowCustomisWallpaperePanel();
                
            }
            else if (items.Count() > 1)
            {
                switch (userSettings.Settings.WallpaperArrangement)
                {
                    case WallpaperArrangement.per:
                        //multiple different wallpapers.. open control panel.
                        runner.ShowControlPanel();
                        break;
                    case WallpaperArrangement.span:
                    case WallpaperArrangement.duplicate:
                        runner.ShowCustomisWallpaperePanel();
                        break;
                }
            }
        }

        private void SetUpdateMenu(AppUpdateStatus status)
        {
            switch (status)
            {
                case AppUpdateStatus.uptodate:
                    updateTrayBtn.Enabled = false;
                    updateTrayBtn.Text = "Up to date";
                    break;
                case AppUpdateStatus.available:
                    updateTrayBtn.Enabled = true;
                    updateTrayBtn.Text = "Update available!";
                    break;
                case AppUpdateStatus.invalid:
                    updateTrayBtn.Enabled = false;
                    updateTrayBtn.Text = "Unique tag";
                    break;
                case AppUpdateStatus.notchecked:
                    updateTrayBtn.Enabled = false;
                    break;
                case AppUpdateStatus.error:
                    updateTrayBtn.Enabled = true;
                    updateTrayBtn.Text = "Update check failed";
                    break;
            }
        }

        #region dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon?.Icon?.Dispose();
                    _notifyIcon?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Systray()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion //dispose
    }
}