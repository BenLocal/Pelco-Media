using Pelco.Media.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Pelco.Media.Tests.Common
{
    public class ByteBufferTest
    {
        [Fact]
        public void TestReadSlice()
        {
            ByteBuffer byteBuffer = new ByteBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 2, 8);

            var a = byteBuffer.Slice(4, 1);

            Assert.Equal(a.ReadByte(), 7);

            var a1 = byteBuffer.Slice(4, 4);
            Assert.Equal(a1.ReadByte(), 7);
            Assert.Equal(a1.ReadByte(), 8);
            Assert.Equal(a1.ReadByte(), 9);
            Assert.Equal(a1.ReadByte(), 10);

            var a2 = byteBuffer.Slice(4, 3);
            Assert.Equal(a2.ReadByte(), 7);
            Assert.Equal(a2.ReadByte(), 8);
            Assert.Equal(a2.ReadByte(), 9);

            Assert.Throws<ArgumentOutOfRangeException>(() =>  byteBuffer.Slice(4, 5));
        }
    }
}
