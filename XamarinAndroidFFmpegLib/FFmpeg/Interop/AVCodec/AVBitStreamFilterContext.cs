#region LGPL License
//
// AVBitStreamFilterContext.cs
//
// Author:
//   Justin Cherniak (justin.cherniak@gmail.com
//
// Copyright (C) 2008 Justin Cherniak
//
// This library is free software; you can redistribute it and/or modify
// it  under the terms of the GNU Lesser General Public License version
// 2.1 as published by the Free Software Foundation.
//
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307
// USA
//
#endregion

using System;
using System.Runtime.InteropServices;

namespace XamarinAndroidFFmpegLib.Interop.Codec.BitStream
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AVBitStreamFilterContext
    {
        public IntPtr priv_data;
        public AVBitStreamFilter* filter;
        public AVCodecParserContext* parser;
        public AVBitStreamFilterContext* next;
    };
}
