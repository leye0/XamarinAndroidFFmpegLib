using System;
using System.Collections.Generic;
using System.Text;
using XamarinAndroidFFmpegLib.Interop.Util;

namespace XamarinAndroidFFmpegLib
{
    public interface IVideoStream:IMediaStream
    {
        int Width { get; }
        int Height { get; }
        double FrameRate { get; }
        long FrameCount { get; }
        int FrameSize { get; }
        PixelFormat PixelFormat { get; }
        bool ReadFrame(out byte[] frame);
    }
}
