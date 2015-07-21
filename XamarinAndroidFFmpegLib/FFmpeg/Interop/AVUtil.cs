using System;
using System.Collections.Generic;
using System.Text;
using System.Security;

namespace XamarinAndroidFFmpegLib.Interop
{
    [SuppressUnmanagedCodeSecurity]
    public unsafe partial class FFmpeg
    {
        public const string AVUTIL_DLL_NAME = "libffmpeg.so";
    }

	public static partial class Utils
	{
		public unsafe static string GetString(IntPtr buf) { return GetString((byte*)buf); }
		public unsafe static string GetString(byte* buf)
		{
			if ((IntPtr)buf == IntPtr.Zero)
				return null;

			StringBuilder s = new StringBuilder();

			for (int i = 0; ; i++)
			{
				try
				{
					if (buf[i] == '\0')
						break;

					s.Append((char)buf[i]);
				}
				catch (AccessViolationException e)
				{
					throw new ArgumentException("Data in buffer not null terminated or bad pointer", e);
				}
			}

			return s.ToString();
		}

//		public static T GetDelegate<T>(IntPtr ptr) where T : class
//		{
//			if (!(typeof(T).IsSubclassOf(typeof(Delegate))))
//				throw new ArgumentException("You must call this class using a delegate type.");
//
//			if (ptr == IntPtr.Zero)
//				return null;
//			else
//				return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
//		}
	}
}
