using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinAndroidFFmpegLib.Interop
{
    internal class BrokenAttribute : Attribute
    {
        public BrokenAttribute(string a) { }
    }

    internal class EnumAttribute : BrokenAttribute
    {
        public EnumAttribute(string a) :base(a){ }
    }
}
