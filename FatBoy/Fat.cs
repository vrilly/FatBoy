using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;

namespace FatBoy
{
    public class HeaderObject
    {
        public uint offset;
        public byte[] data;
        public HeaderObject(uint offset, byte[] data)
        {
            this.offset = offset;
            this.data = data;
        }
        public HeaderObject(uint offset)
        {
            this.offset = offset;
        }

        public void write(ref Stream fStream, long baseOffset = 0)
        {
            if (this.data == null)
                return;
            fStream.Seek(baseOffset + this.offset, SeekOrigin.Begin);
            fStream.Write(data);
        }
    }
    public class Fat
    {
        public class FatFile
        {
            public uint[] clusters;
            public HeaderObject file_name = new HeaderObject(0x0); // File name + ext
            public HeaderObject file_attr = new HeaderObject(0xB); // File attributes
            public HeaderObject first_cluster = new HeaderObject(0x1A); // First cluster
            public HeaderObject file_size = new HeaderObject(0x1C); // File size
            public string file_path; // Path to file on host system
            public FatFile(string fileName)
            {
                this.file_name.data = Encoding.ASCII.GetBytes(String.Format("{0,-11}", fileName));
            }

            public FatFile(string fileName, int fileSize, string path)
            {
                file_name.data = Encoding.ASCII.GetBytes(fileName);
                file_size.data = BitConverter.GetBytes(fileSize);
                file_path = path;
            }

            public static FatFile CreateVolLabel(string label)
            {
                FatFile volLabel = new FatFile(label);
                volLabel.file_attr.data = new byte[] { 0x08 };
                return volLabel;
            }

            public void writeDirent(ref Stream fStream, long offset)
            {
                file_name.write(ref fStream, offset);
                file_attr.write(ref fStream, offset);
                first_cluster.write(ref fStream, offset);
                file_size.write(ref fStream, offset);
            }
            public void writeFileToImage(Stream fStream)
            {
                long offset = 0x2600; // Offset to first cluster
                offset += 512; //rootdir sector
                offset += 16384 * (this.clusters[0] - 2);
                FileStream inStream = File.OpenRead(this.file_path);
                fStream.Seek(offset, SeekOrigin.Begin);
                byte[] buffer = new byte[inStream.Length];
                inStream.Read(buffer);
                fStream.Write(buffer);
                inStream.Close();
            }

        }
        class ClusterMap
        {
            uint next_free = 2;
            int bytes_per_cluster = 16384;
            HeaderObject fat_id = new HeaderObject(0, new byte[] { 0xF0, 0xFF, 0xFF });
            FatFile[] files;
            byte[] cluster_map_pair = new byte[3];

            public ClusterMap(FatFile[] files)
            { 
                foreach (FatFile file in files)
                {
                    List<uint> clusters = new List<uint>();
                    uint cluster_amount = (uint)(BitConverter.ToInt32(file.file_size.data) / bytes_per_cluster) + 1;
                    file.first_cluster.data = BitConverter.GetBytes((short)next_free);
                    for (uint i = 0; i < cluster_amount; i++)
                        clusters.Add(next_free + i);
                    next_free += cluster_amount;
                    file.clusters = clusters.ToArray();
                    Debug.Assert(next_free < 90);
                }
                this.files = files;
            }

            void writeFAT(Stream fStream, long offset)
            {
                fat_id.write(ref fStream, offset);
                bool half = false;
                foreach (FatFile file in files)
                {
                    uint last = file.clusters.Last();
                    foreach (uint cluster in file.clusters)
                    {
                        ushort mapvalue = (ushort)(cluster == last ? 0xFFF : cluster + 1);
                        if (!half)
                        {
                            half = true;
                            cluster_map_pair[0] = (byte)mapvalue;
                            cluster_map_pair[1] = (byte)(mapvalue >> 8);
                        }
                        else
                        {
                            half = false;

                            cluster_map_pair[2] = (byte)(mapvalue >> 4);
                            mapvalue &= 0x00F;
                            cluster_map_pair[1] ^= (byte)(mapvalue << 4);
                            fStream.Write(cluster_map_pair);
                            cluster_map_pair[2] = 0;
                        }
                    }
                }
                if (half)
                    fStream.Write(cluster_map_pair);
            }
            public void write(ref Stream fStream)
            {
                writeFAT(fStream, 512);
                writeFAT(fStream, 10 * 512);
            }
        }

        class DirectoryTable
        {
            FatFile vollabel = FatFile.CreateVolLabel("FATBOY");
            FatFile[] files;
            public DirectoryTable(FatFile[] fatFiles)
            {
                files = fatFiles;
            }
            public void write(ref Stream fStream, long offset)
            {
                vollabel.writeDirent(ref fStream, offset);
                offset += 32;
                foreach (FatFile file in files)
                {
                    file.writeDirent(ref fStream, offset);
                    offset += 32;
                }
            }
        }

        HeaderObject[] VolumeBootRecord = new HeaderObject[]
            {
                new HeaderObject(0x0, new byte[] { 0xEB, 0x3C, 0x90 }), // JMP instr
                new HeaderObject(0x3, Encoding.ASCII.GetBytes("fatboy")), //OEM Label
                new HeaderObject(0xB, BitConverter.GetBytes((ushort)512)), // Bytes per sector
                new HeaderObject(0xD, new byte[] { (byte)32u }), // Sectors per cluster 16k
                new HeaderObject(0xE, BitConverter.GetBytes((ushort)1)), // Reserved sectors
                new HeaderObject(0x10, new byte[] { (byte)2u }), // Number of FATs
                new HeaderObject(0x11, BitConverter.GetBytes((ushort)16)), // Number of root dirents
                new HeaderObject(0x13, BitConverter.GetBytes((ushort)2880)), // Number of sectors
                new HeaderObject(0x15, new byte[] { 0xF0 }), // 1.44M HD floppy
                new HeaderObject(0x16, BitConverter.GetBytes((ushort)9)), // Sectors per FAT
                new HeaderObject(0x26, new byte[] { 0x29 }), // Extended boot sig
                new HeaderObject(0x27, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }), // Serial Number
                new HeaderObject(0x02B, Encoding.ASCII.GetBytes("FATBOY     FAT12   ")) // VL/FAT
            };
        List<FatFile> files;
        ClusterMap clusterMap; // FAT
        DirectoryTable directoryTable; // Root table
        Stream imageFile;
        public Fat(ref Stream imageFile) {
            files = new List<FatFile>();
            this.imageFile = imageFile;
        }

        public void addFiles(FatFile[] files) => this.files.AddRange(files);
        public void writeImage()
        {
            foreach (HeaderObject element in VolumeBootRecord)
                element.write(ref imageFile);
            clusterMap = new ClusterMap(files.ToArray());
            directoryTable = new DirectoryTable(files.ToArray());
            clusterMap.write(ref imageFile);
            directoryTable.write(ref imageFile, 0x2600);
            imageFile.Seek((2880 * 512) - 1, SeekOrigin.Begin);
            imageFile.WriteByte(0x00);
            foreach (FatFile file in files)
                file.writeFileToImage(imageFile);
        }
    }
}
