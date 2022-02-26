﻿using Lively.Common;
using Lively.Common.Helpers;
using Lively.Common.Helpers.Archive;
using Lively.Common.Helpers.Files;
using Lively.Common.Helpers.MVVM;
using Lively.Grpc.Client;
using Lively.Models;
using Lively.UI.WinUI.Helpers;
using Lively.UI.WinUI.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace Lively.UI.WinUI.ViewModels
{
    public class AddWallpaperViewModel : ObservableObject
    {
        public ILibraryModel NewWallpaper { get; private set; }
        public event EventHandler OnRequestClose;

        private readonly IUserSettingsClient userSettings;
        //private readonly LibraryViewModel libraryVm;
        private readonly LibraryUtil libraryUtil;

        public AddWallpaperViewModel(IUserSettingsClient userSettings, LibraryUtil libraryUtil)
        {
            this.userSettings = userSettings;
            //this.libraryVm = libraryVm;
            this.libraryUtil = libraryUtil;
            //this.appWindow = appWindow;

            WebUrlText = userSettings.Settings.SavedURL;
        }

        private string _webUrlText;
        public string WebUrlText
        {
            get { return _webUrlText; }
            set
            {
                _webUrlText = value;
                OnPropertyChanged();
            }
        }

        private RelayCommand _browseWebCommand;
        public RelayCommand BrowseWebCommand => _browseWebCommand ??= new RelayCommand(WebBrowseAction);

        private void WebBrowseAction()
        {
            Uri uri;
            try
            {
                uri = LinkHandler.SanitizeUrl(WebUrlText);
            }
            catch
            {
                return;
            }

            WebUrlText = uri.OriginalString;
            try
            {
                NewWallpaper = libraryUtil.AddWallpaperLink(uri.OriginalString);
                OnRequestClose?.Invoke(this, EventArgs.Empty);
            }
            catch {
                //TODO
            }
        }

        private RelayCommand _browseFileCommand;
        public RelayCommand BrowseFileCommand => _browseFileCommand ??= new RelayCommand(async () => await FileBrowseAction());

        private async Task FileBrowseAction()
        {
            var filePicker = new FileOpenPicker();
            filePicker.SetOwnerWindow(App.Services.GetRequiredService<MainWindow>());
            //filePicker.FileTypeFilter.Add("*");
            foreach (var item in LocalizationUtil.GetLocalizedSupportedFileDialogFilter(true))
            {
                filePicker.FileTypeFilter.Add(item);
            }
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var item = await libraryUtil.AddWallpaperFile(file.Path);

                    if (item.DataType == LibraryItemType.processing)
                    {
                        NewWallpaper = item;
                        OnRequestClose?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception e)
                {
                    //TODO
                    /*
                    System.Windows.MessageBox.Show(
                         e.Message,
                         Properties.Resources.TextError);
                    */
                }
            }
        }
    }
}
