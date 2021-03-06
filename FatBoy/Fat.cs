﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

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

        public void write(Stream fStream, long baseOffset = 0)
        {
            if (data == null)
            {
                return;
            }

            fStream.Seek(baseOffset + offset, SeekOrigin.Begin);
            fStream.Write(data);
        }
    }
    public class Fat
    {
        public class FatFile
        {
            public uint[] clusters;
            public HeaderObject fileName = new HeaderObject(0x0); // File name + ext
            public HeaderObject fileAttr = new HeaderObject(0xB); // File attributes
            public HeaderObject firstCluster = new HeaderObject(0x1A); // First cluster
            public HeaderObject fileSize = new HeaderObject(0x1C); // File size
            public string filePath; // Path to file on host system
            public FatFile(string fileName)
            {
                this.fileName.data = Encoding.ASCII.GetBytes(string.Format("{0,-11}", fileName));
            }

            public FatFile(string fileName, int fileSize, string path)
            {
                this.fileName.data = Encoding.ASCII.GetBytes(fileName);
                this.fileSize.data = BitConverter.GetBytes(fileSize);
                filePath = path;
            }

            public static FatFile CreateVolLabel(string label)
            {
                FatFile volLabel = new FatFile(label);
                volLabel.fileAttr.data = new byte[] { 0x08 };
                return volLabel;
            }

            public void writeDirent(Stream fStream, long offset)
            {
                fileName.write(fStream, offset);
                fileAttr.write(fStream, offset);
                firstCluster.write(fStream, offset);
                fileSize.write(fStream, offset);
            }
            public void writeFileToImage(Stream fStream)
            {
                long offset = 0x2600; // Offset to first cluster
                offset += 512; //rootdir sector
                offset += 16384 * (clusters[0] - 2);
                FileStream inStream = File.OpenRead(filePath);
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
            HeaderObject fatId = new HeaderObject(0, new byte[] { 0xF0, 0xFF, 0xFF });
            FatFile[] files;
            byte[] cluster_map_pair = new byte[3];

            public ClusterMap(FatFile[] files)
            {
                foreach (FatFile file in files)
                {
                    List<uint> clusters = new List<uint>();
                    uint cluster_amount = (uint)(BitConverter.ToInt32(file.fileSize.data) / bytes_per_cluster) + 1;
                    file.firstCluster.data = BitConverter.GetBytes((short)next_free);
                    for (uint i = 0; i < cluster_amount; i++)
                    {
                        clusters.Add(next_free + i);
                    }

                    next_free += cluster_amount;
                    file.clusters = clusters.ToArray();
                    if (next_free > 90)
                    {
                        MessageBox.Show("Critical: Ran out of clusters.");
                    }
                }
                this.files = files;
            }

            void writeFAT(Stream fStream, long offset)
            {
                fatId.write(fStream, offset);
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
                {
                    fStream.Write(cluster_map_pair);
                }
            }
            public void write(Stream fStream)
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
            public void write(Stream fStream, long offset)
            {
                vollabel.writeDirent(fStream, offset);
                offset += 32;
                foreach (FatFile file in files)
                {
                    file.writeDirent(fStream, offset);
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
        public Fat(Stream imageFile)
        {
            files = new List<FatFile>();
            this.imageFile = imageFile;
        }

        public void addFiles(FatFile[] files) => this.files.AddRange(files);
        public void writeImage()
        {
            foreach (HeaderObject element in VolumeBootRecord)
            {
                element.write(imageFile);
            }

            clusterMap = new ClusterMap(files.ToArray());
            directoryTable = new DirectoryTable(files.ToArray());
            clusterMap.write(imageFile);
            directoryTable.write(imageFile, 0x2600);
            imageFile.Seek((2880 * 512) - 1, SeekOrigin.Begin);
            imageFile.WriteByte(0x00);
            foreach (FatFile file in files)
            {
                file.writeFileToImage(imageFile);
            }
        }
    }
}
