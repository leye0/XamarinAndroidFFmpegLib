#region LGPL License
//
// AudioDecoderStream.cs
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
using System.Diagnostics;
using System.IO;
using XamarinAndroidFFmpegLib.Interop;
using XamarinAndroidFFmpegLib.Interop.Codec;
using XamarinAndroidFFmpegLib.Interop.Format;
using System.Runtime.InteropServices;

namespace XamarinAndroidFFmpegLib
{
	public unsafe class AudioDecoderStream : DecoderStream, IAudioStream
	{
		private AVFrame* m_avFrame = null;
		private byte[] m_frameBuffer;

		internal AudioDecoderStream(MediaFile file, ref AVStream stream)
			: base(file, ref stream)
		{
			m_frameBuffer = new byte[FFmpeg.AVCODEC_MAX_AUDIO_FRAME_SIZE];

			// TODO: Added to confirm to decode audio4
			m_avFrame = FFmpeg.avcodec_alloc_frame();
			m_buffer = new byte[FFmpeg.AVCODEC_MAX_AUDIO_FRAME_SIZE];
		}

		/// <summary>
		/// Number of channels in the audio stream.
		/// </summary>
		public int Channels
		{
			get { return m_avCodecCtx.channels; }
		}

		/// <summary>
		/// Sample rate of the stream in bits per second
		/// </summary>
		public int SampleRate
		{
			get { return m_avCodecCtx.sample_rate; }
		}

		/// <summary>
		/// Returns the sample size in bits.
		/// </summary>
		public int SampleSize
		{
			get
			{
				switch (m_avCodecCtx.sample_fmt)
				{
				case AVSampleFormat.AV_SAMPLE_FMT_U8:
					return 8;
				case AVSampleFormat.AV_SAMPLE_FMT_S16:
					return 16;
				case AVSampleFormat.AV_SAMPLE_FMT_S32:
					return 32;
				default:
					throw new Exception("Unknown sample size.");
				}
			}
		}

		/// <summary>
		/// Size of one frame in bytes
		/// </summary>
		public int FrameSize
		{
			get { return m_buffer.Length; }
		}

		/// <summary>
		/// Average bytes per second of the stream
		/// </summary>
		public override int UncompressedBytesPerSecond
		{
			get { return (Channels * SampleRate * SampleSize) / 8; }
		}

		public bool ReadFrame(out byte[] frame)
		{
			if (m_frameBuffer == null)
				m_frameBuffer = new byte[FrameSize];

			// read whole frame from the stream
			if (Read(m_frameBuffer, 0, FrameSize) <= 0)
			{

				frame = null;
				return false;
			}
			else
			{
				Marshal.Copy((IntPtr)m_avFrame, m_frameBuffer, 0, FrameSize);
				frame = m_frameBuffer;
				return true;
			}
		}

		// TODO: Added for test
		public bool ReadFrame2(out byte[] frame, int frameSize)
		{
			m_frameBuffer = new byte[frameSize];

			// read whole frame from the stream
			if (Read(m_frameBuffer, 0, frameSize) <= 0)
			{

				frame = null;
				return false;
			}
			else
			{
				Marshal.Copy((IntPtr)m_avFrame, m_frameBuffer, 0, frameSize);
				frame = m_frameBuffer;
				return true;
			}
		}

		protected override bool DecodePacket(ref AVPacket packet)
		{
			var totalOutput = 0;
			int packetSize = packet.size;
			while (packetSize - totalOutput > 0) {
				int usedInputBytes = FFmpeg.avcodec_decode_audio4 (ref m_avCodecCtx, m_avFrame + totalOutput, out m_bufferUsedSize, ref packet);
				totalOutput += usedInputBytes;

				if (usedInputBytes < 0) //Error in packet, ignore packet
					break;
			}

			m_bufferUsedSize = totalOutput;
			return true;
		}
	}
}
