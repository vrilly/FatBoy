﻿using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using Path = System.IO.Path;

namespace FatBoy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void saveImage_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Raw Disk Image file (*.img)|*.img";
            if (saveFileDialog.ShowDialog() == true)
            {
                FileList list = (FileList)FindResource("FileListData");
                Stream imgFile;
                if ((imgFile = saveFileDialog.OpenFile()) == null)
                {
                    throw new Exception();
                }

                Fat fat = new Fat(imgFile);
                fat.addFiles(list.GetFatFiles());
                fat.writeImage();
                imgFile.Flush();
                imgFile.Close();
            }
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            FileList list = (FileList)FindResource("FileListData");
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string drop in dropped)
                {
                    if (Path.HasExtension(drop) == false)
                    {
                        continue;
                    }

                    string filename = Path.GetFileNameWithoutExtension(drop);
                    string ext = System.IO.Path.GetExtension(drop);
                    string basename = filename.PadRight(8).Substring(0, 8) + ext.PadRight(4).Substring(1, 3);
                    FileInfo info = new FileInfo(drop);
                    list.Add(new FileObject(basename, (int)info.Length, drop));
                }
            }
        }
    }
}