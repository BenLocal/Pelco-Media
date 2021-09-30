using Pelco.Media.Pipeline;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RtspClientDemo
{
    public class H264FileSink : SinkBase
    {
        private ByteBuffer _sps;
        private ByteBuffer _pps;
        private bool _h264_sps_pps_fired;
        private FileStream _fileStream;

        public H264FileSink()
        {
            string filename = "rtsp_capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".264";
            _fileStream = new FileStream(filename, FileMode.Create);
        }

        public override bool WriteBuffer(ByteBuffer buffer)
        {
            // write file
            if (!_h264_sps_pps_fired)
            {
                WriteSpsAndPpsBuffer(buffer);
            }

            if (_h264_sps_pps_fired)
            {
                WriteNalsBuffer(buffer);
            }

            return true;
        }

        private void WriteNalsBuffer(ByteBuffer buffer)
        {
            buffer.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);
            while (buffer.Position < buffer.Length)
            {
                var length = buffer.ReadInt32();
                var nal = buffer.ReadSlice(length);

                _fileStream.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);  // Write Start Code
                _fileStream.Write(nal.ReadRawBytes(), 0, nal.Length);
            }
            _fileStream.Flush(true);
        }

        private void WriteSpsAndPpsBuffer(ByteBuffer buffer)
        {
            buffer.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);
            while (buffer.Position < buffer.Length)
            {
                var length = buffer.ReadInt32();
                var nal = buffer.ReadSlice(length);

                nal.SetPosition(0, ByteBuffer.PositionOrigin.BEGINNING);
                var nalHeader = nal.ReadByte();
                var nalType = nalHeader & 0x1F;
                if (nalType == 7) _sps = nal;
                else if (nalType == 8) _pps = nal;
                if (_sps != null && _pps != null)
                {
                    // write file header
                    _fileStream.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);  // Write Start Code
                    _fileStream.Write(_sps.ReadRawBytes(), 0, _sps.Length);
                    _fileStream.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 }, 0, 4);  // Write Start Code
                    _fileStream.Write(_pps.ReadRawBytes(), 0, _pps.Length);
                    _fileStream.Flush(true);

                    _h264_sps_pps_fired = true;

                    return;
                }
            }
        }
    }
}
