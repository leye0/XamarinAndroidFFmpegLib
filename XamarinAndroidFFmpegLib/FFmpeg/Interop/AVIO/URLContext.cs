#region LGPL License
//
// URLContext.cs
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

namespace XamarinAndroidFFmpegLib.Interop.AVIO
{
    public unsafe struct URLContext
    {
        public URLProtocol* prot;
        public int flags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool is_streamed;

        public int max_packet_size;  /**if non zero, the stream is packetized with this max packet size */

        public IntPtr priv_data;

        private byte* filename_ptr; /** specified filename */
        public string filename { get { return Utils.GetString(filename_ptr); } }
    }
}
