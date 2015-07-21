using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinAndroidFFmpegLib
{
    interface IAudioStream:IMediaStream
    {
        int Channels { get; }
        int SampleRate { get; }
        int SampleSize { get; }
		bool ReadFrame(out byte[] frame);
    }
}
