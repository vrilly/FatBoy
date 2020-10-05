using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;

namespace FatBoy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        int totalBytesUsed = 0;
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

        private void updateStorage()
        {
            TextBlock textBlock = (TextBlock)FindName("uistatus");
            textBlock.Text = totalBytesUsed / 1000 + " / 1464 KB Used.";
        }

        private void appendFileToList(string path)
        {
            FileList list = (FileList)FindResource("FileListData");
            if (Path.HasExtension(path) == false || list.Count >= 15)
            {
                return;
            }

            string filename = Path.GetFileNameWithoutExtension(path);
            string ext = System.IO.Path.GetExtension(path);
            string basename = filename.ToUpper().PadRight(8).Substring(0, 8) + ext.ToUpper().PadRight(4).Substring(1, 3);
            FileInfo info = new FileInfo(path);
            if (totalBytesUsed + info.Length >= 1464320)
            {
                return;
            }
            list.Add(new FileObject(basename, (int)info.Length, path));
            totalBytesUsed += (int)info.Length;
            updateStorage();
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string path in dropped)
                {
                    appendFileToList(path);
                }
            }
        }

        private void addFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All Files (*.*)|*.*";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string path in openFileDialog.FileNames)
                    appendFileToList(path);
            }
        }

        private void exitButton_Click(object sender, RoutedEventArgs e) => Close();

        private void menuRemovefile_Click(object sender, RoutedEventArgs e)
        {
            FileList list = (FileList)FindResource("FileListData");
            ListView listView = (ListView)FindName("FileContainer");
            if (listView.SelectedIndex == -1)
                return;
            totalBytesUsed -= ((FileObject)listView.SelectedItem).FileSizeRaw;
            list.Remove((FileObject)listView.SelectedItem);
            updateStorage();
        }

        private void newFile_Click(object sender, RoutedEventArgs e)
        {
            FileList list = (FileList)FindResource("FileListData");
            list.Clear();
            totalBytesUsed = 0;
            updateStorage();
        }
    }
}
