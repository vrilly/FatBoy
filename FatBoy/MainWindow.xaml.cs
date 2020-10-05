using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
                FileList list = (FileList)this.FindResource("FileListData");
                Stream imgFile;
                if ((imgFile = saveFileDialog.OpenFile()) == null)
                    throw new Exception();
                Fat fat = new Fat(ref imgFile);
                fat.addFiles(list.GetFatFiles());
                fat.writeImage();
                imgFile.Flush();
                imgFile.Close();
            }
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            FileList list = (FileList)this.FindResource("FileListData");
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string drop in dropped)
                {
                    if (Path.HasExtension(drop) == false)
                        continue;
                    string filename = Path.GetFileNameWithoutExtension(drop);
                    string ext = System.IO.Path.GetExtension(drop);
                    string basename = filename.PadRight(8).Substring(0,8) + ext.PadRight(4).Substring(1,3);
                    FileInfo info = new FileInfo(drop);
                    list.Add(new FileObject(basename,(int) info.Length, drop));
                }
            }
        }
    }
}
