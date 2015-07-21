using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinAndroidFFmpegLib.Interop.Codec
{
    public unsafe struct AVProfile
    {
        int profile;
        char* name; ///< short name for the profile
    }
}
