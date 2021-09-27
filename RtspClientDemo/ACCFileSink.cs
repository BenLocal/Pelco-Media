using Microsoft.VisualBasic.CompilerServices;
using Pelco.Media.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RtspClientDemo
{
    public class ACCFileSink : SinkBase
    {
        private FileStream _fileStream;
        public readonly uint _objectType = 0;
        public readonly uint _frequencyIndex = 0;
        public readonly uint _channelConfiguration = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config_string">
        /// sdp中config信息：
        /// eg: 96 profile-level-id=1;mode=AAC-hbr;sizelength=13;indexlength=3;indexdeltalength=3;config=1490
        /// 其中最后config=1490就是该字段的值
        /// </param>
        public ACCFileSink(string configString)
        {
            string filename = "rtsp_capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".aac";
            _fileStream = new FileStream(filename, FileMode.Create);

            /***
           5 bits: object type
               if (object type == 31)
               6 bits + 32: object type
           4 bits: frequency index
               if (frequency index == 15)
               24 bits: frequency
           4 bits: channel configuration
           var bits: AOT Specific Config
            ***/

            // config is a string in hex eg 1490 or 0x1210
            // Read each ASCII character and add to a bit array

            BitStream bs = new BitStream();
            bs.AddHexString(configString);

            // Read 5 bits
            _objectType = bs.Read(5);

            // Read 4 bits
            _frequencyIndex = bs.Read(4);

            // Read 4 bits
            _channelConfiguration = bs.Read(4);
        }

        public override bool WriteBuffer(ByteBuffer buffer)
        {
            buffer.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);
            while (buffer.Position < buffer.Length)
            {
                var length = buffer.ReadInt32();
                var acc = buffer.ReadSlice(length);

                // ASDT header format
                int protection_absent = 1;
                BitStream bs = new BitStream();
                bs.AddValue(0xFFF, 12); // (a) Start of data
                bs.AddValue(0, 1); // (b) Version ID, 0 = MPEG4
                bs.AddValue(0, 2); // (c) Layer always 2 bits set to 0
                bs.AddValue(protection_absent, 1); // (d) 1 = No CRC
                bs.AddValue((int)_objectType - 1, 2); // (e) MPEG Object Type / Profile, minus 1
                bs.AddValue((int)_frequencyIndex, 4); // (f)
                bs.AddValue(0, 1); // (g) private bit. Always zero
                bs.AddValue((int)_channelConfiguration, 3); // (h)
                bs.AddValue(0, 1); // (i) originality
                bs.AddValue(0, 1); // (j) home
                bs.AddValue(0, 1); // (k) copyrighted id
                bs.AddValue(0, 1); // (l) copyright id start
                bs.AddValue(length + 7, 13); // (m) AAC data + size of the ASDT header
                bs.AddValue(2047, 11); // (n) buffer fullness ???
                int num_acc_frames = 1;
                bs.AddValue(num_acc_frames - 1, 1); // (o) num of AAC Frames, minus 1

                // If Protection was On, there would be a 16 bit CRC
                if (protection_absent == 0) bs.AddValue(0xABCD /*CRC*/, 16); // (p)

                byte[] header = bs.ToArray();
                _fileStream.Write(header, 0, header.Length);
                _fileStream.Write(acc.ReadRawBytes(), 0, acc.Length);
            }
            _fileStream.Flush(true);

            return true;
        }
    }
}
