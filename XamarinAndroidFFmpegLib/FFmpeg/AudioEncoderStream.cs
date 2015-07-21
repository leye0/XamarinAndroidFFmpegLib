#region LGPL License
//
// AudioEncoderStream.cs
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
using XamarinAndroidFFmpegLib.Audio;
using XamarinAndroidFFmpegLib.Interop.AVIO;
using XamarinAndroidFFmpegLib.Interop.Format.Output;
using XamarinAndroidFFmpegLib.Interop.Util;


#endregion

using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using XamarinAndroidFFmpegLib.Interop;
using XamarinAndroidFFmpegLib.Interop.Codec;
using XamarinAndroidFFmpegLib.Interop.Format;

namespace XamarinAndroidFFmpegLib
{
    public unsafe class AudioEncoderStream : Stream
    {
        #region Private Instance Variables

        private AVFormatContext m_avFormatCtx;
        private AVCodecContext m_avCodecCtx;
        private AVStream m_avStream;
        private bool m_disposed;
        private bool m_fileOpen;
        private string m_filename;
        private FifoMemoryStream m_buffer;
        private int m_totalWritten;

        #endregion

        #region Properties

//        public int FrameSize
//        {
//            get { return m_avCodecCtx.frame_size * m_avCodecCtx.channels * 2; } //2 == Sample Size (16-bit)
//        }
//
		public int FrameSize
		{
			get { return Math.Max (m_avCodecCtx.frame_size, 16384); } // TODO: This is really strange. Seems to be used to set buffer size.
		}

        public string Filename { get { return m_filename; } }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override long Length { get { return m_totalWritten; } }

        public override long Position
        {
            get { return Length; }
            set { throw new NotSupportedException(); }
        }

        #endregion

        public AudioEncoderStream(string filename, AudioCodec Codec, int Bitrate, int SampleRate, int Channels, bool VBR)
            : this(filename, new EncoderInformation(Codec, Bitrate, SampleRate, Channels, VBR)) { }

		// Leon Pelletier - I didn't know how to get the frame size. Hardcoding it. :(
		public AudioEncoderStream(string filename, EncoderInformation EncoderInfo)
        {
            // Initialize instance variables
            m_filename = filename;
            m_disposed = m_fileOpen = false;
            m_buffer = new FifoMemoryStream();
			// Refered to this for encoding: based on http://stackoverflow.com/questions/19679833/muxing-avpackets-into-mp4-file

			AVOutputFormat* outFmt = FFmpeg.av_guess_format(EncoderInfo.Codec.ShortName, filename, null);
			AVFormatContext outFmtCtx = FFmpeg.avformat_alloc_context ();//*outFmtCtx, ref *outFmt, null, m_filename);
			AVStream * outStrm = FFmpeg.av_new_stream(ref outFmtCtx, 0);

			AVCodec * codec = null;
			FFmpeg.avcodec_get_context_defaults3(ref *outStrm->codec, codec);
			outStrm->codec->coder_type = (int) AVMediaType.AVMEDIA_TYPE_AUDIO;;
		
            // Initialize the output format context
			m_avFormatCtx = FFmpeg.avformat_alloc_context();

            // Get output format
			m_avFormatCtx.oformat = FFmpeg.av_guess_format(EncoderInfo.Codec.ShortName, null, null);

			if (m_avFormatCtx.oformat == null)
                throw new EncoderException("Could not find output format.");

			// Initialize the new output stream
            AVStream* stream = FFmpeg.av_new_stream(ref m_avFormatCtx, 1);
            if (stream == null)
                throw new EncoderException("Could not alloc output audio stream");

            m_avStream = *stream;

			// Initialize output codec context
            m_avCodecCtx = *m_avStream.codec;

			AVCodec* outCodec = FFmpeg.avcodec_find_encoder(EncoderInfo.Codec.CodecID);

			FFmpeg.avcodec_get_context_defaults3 (ref m_avCodecCtx, outCodec);

			if (outCodec == null)
                throw new EncoderException("Could not find encoder");
			
			m_avCodecCtx.sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16; // TODO: Strange to hardcode all this stuff.
			m_avCodecCtx.bit_rate = EncoderInfo.Bitrate;
			m_avCodecCtx.sample_rate = EncoderInfo.SampleRate;
			m_avCodecCtx.time_base = new AVRational() { num = 1, den = EncoderInfo.SampleRate };
			m_avCodecCtx.channels = EncoderInfo.Channels;

			// TODO: Channel layout could be passed in the method or in another way:
			m_avCodecCtx.channel_layout = (ulong) (EncoderInfo.Channels == 1 ? FFmpeg.AV_CH_FRONT_CENTER : FFmpeg.AV_CH_FRONT_LEFT | FFmpeg.AV_CH_FRONT_RIGHT);

			m_avCodecCtx.codec_type = AVMediaType.AVMEDIA_TYPE_AUDIO;
			
			var openCodecSuccess = FFmpeg.avcodec_open (ref m_avCodecCtx, outCodec);
			if (openCodecSuccess < 0) {
			
				openCodecSuccess = FFmpeg.avcodec_open2 (ref m_avCodecCtx, outCodec, null);

				throw new EncoderException("Could not open codec.");
			}
            

			// TODO: Not sure if this is the way to go, but I know from Libavcodec DOC that some codecs don't provide frame_size (PCM)
			if (m_avCodecCtx.frame_size == 0) {
				m_avCodecCtx.frame_size = m_avCodecCtx.bits_per_raw_sample * m_avCodecCtx.channels;
			}

            // Open and prep file
			if (File.Exists(m_filename)) File.Delete(m_filename);
			var successCode = FFmpeg.avio_open(ref m_avFormatCtx.pb, m_filename, FFmpeg.URL_RDWR);

			if (successCode < 0)
                throw new EncoderException("Could not open output file.");

            m_fileOpen = true;

			FFmpeg.avformat_write_header(ref m_avFormatCtx, null);
        }

        public override void Flush()
        {
            while (m_buffer.Length > 0)
                EncodeAndWritePacket();
        }

        private void EncodeAndWritePacket()
        {
            byte[] frameBuffer = new byte[FrameSize];
            m_buffer.Read(frameBuffer, 0, frameBuffer.Length);

            fixed (byte* pcmSamples = frameBuffer)
            {
                if (m_disposed)
                    throw new ObjectDisposedException(this.ToString());

				AVFrame* frame = FFmpeg.	avcodec_alloc_frame();
                AVPacket outPacket = new AVPacket();
                FFmpeg.av_init_packet(ref outPacket);

                byte[] buffer = new byte[FFmpeg.FF_MIN_BUFFER_SIZE];
                fixed (byte* encodedData = buffer)
                {
                    try
                    {
                        outPacket.size = FFmpeg.avcodec_encode_audio(ref m_avCodecCtx, encodedData, FFmpeg.FF_MIN_BUFFER_SIZE, (short*)pcmSamples);
						outPacket.pts = 0;
						if (m_avCodecCtx.coded_frame != null) {
							outPacket.pts = m_avCodecCtx.coded_frame->pts;	
						}
                        
						outPacket.flags |= PacketFlags.Key;
                        outPacket.stream_index = m_avStream.index;
                        outPacket.data = (IntPtr)encodedData;
					
                        if (outPacket.size > 0)
                        {
							var writePacket = FFmpeg.av_write_frame(ref m_avFormatCtx, ref outPacket);
							if (writePacket != 0)
                                throw new IOException("Error while writing encoded audio frame to file");
                        }
                    }
					catch (Exception e) {
						var ab = e.Message;
					}
                    finally
                    {
                        FFmpeg.av_free_packet(ref outPacket);
                    }
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (m_disposed)
                throw new ObjectDisposedException(this.ToString());

            m_buffer.Write(buffer, offset, count);

            while (m_buffer.Length >= FrameSize)
                EncodeAndWritePacket();

            m_totalWritten += count;
        }

        protected override void Dispose(bool Disposing)
        {
            if (!m_disposed)
            {
                if (Disposing)
                {
                    m_filename = null;
                }

                if (m_avCodecCtx.codec != null)
                    FFmpeg.avcodec_close(ref m_avCodecCtx);

                for (int i = 0; i < m_avFormatCtx.nb_streams; i++)
                {
                    IntPtr ptr = (IntPtr)m_avFormatCtx.streams[i]->codec;
                    FFmpeg.av_freep(ref ptr);

                    ptr = (IntPtr)m_avFormatCtx.streams[i];
                    FFmpeg.av_freep(ref ptr);
                }

				if (m_fileOpen) {
					FFmpeg.avio_close(m_avFormatCtx.pb);
				}
					//FFmpeg.url_fclose((byte*)m_avFormatCtx.pb);

            }

            m_disposed = true;
        }

        #region Unsupported Stream Methods

        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }

        #endregion
    }

    public class AudioCodec
    {
        private string m_shortname;
        private AVCodecID m_id;

        public string ShortName { get { return m_shortname; } }
		public AVCodecID CodecID { get { return m_id; } }

		public AudioCodec(string ShortName, AVCodecID ID)
        {
            m_shortname = ShortName;
            m_id = ID;
        }

        public static readonly AudioCodec AAC = new AudioCodec("mp4", AVCodecID.CODEC_ID_AAC);
        public static readonly AudioCodec AC3 = new AudioCodec("ac3", AVCodecID.CODEC_ID_AC3);
        public static readonly AudioCodec FLAC = new AudioCodec("flac", AVCodecID.CODEC_ID_FLAC);
        public static readonly AudioCodec MP2 = new AudioCodec("mp2", AVCodecID.CODEC_ID_MP2);
        public static readonly AudioCodec MP3 = new AudioCodec("mp3", AVCodecID.CODEC_ID_MP3);
        public static readonly AudioCodec PCM = new AudioCodec("wav", AVCodecID.CODEC_ID_PCM_S16BE);
        public static readonly AudioCodec Vorbis = new AudioCodec("ogg", AVCodecID.CODEC_ID_VORBIS);
        public static readonly AudioCodec WMA = new AudioCodec("asf", AVCodecID.CODEC_ID_WMAV2);
    }

    public class EncoderInformation
    {
        public readonly AudioCodec Codec;
        /// <summary>
        /// The bitrate of the audio if doing constant bitrate encoding (VBR == false)
        /// </summary>
        public readonly int Bitrate;

        public readonly int SampleRate;

        public readonly int Channels;

        /// <summary>
        /// Quality Scale if using VBR (valid values, 1-100)
        /// </summary>
        public readonly float QualityScale;

        /// <summary> Sample Size (in bits) </summary>
        public readonly int SampleSize;

        public readonly bool VBR;

        public EncoderInformation(AudioCodec Codec, int Bitrate, int SampleRate, int Channels, bool VBR)
        {
            this.Codec = Codec;
            this.Bitrate = Bitrate;
            this.SampleRate = SampleRate;
            this.Channels = Channels;
            this.VBR = VBR;
            this.QualityScale = 0;
            this.SampleSize = sizeof(short);
        }

        public static EncoderInformation Deserialize(String xmlString)
        {
            StringReader reader = new StringReader(xmlString);

            XmlSerializer s = new XmlSerializer(typeof(EncoderInformation));
            EncoderInformation info;
            info = (EncoderInformation)s.Deserialize(reader);

            return info;
        }

        public string SerializeToXML()
        {
            StringBuilder sb = new StringBuilder();

            XmlSerializer s = new XmlSerializer(typeof(EncoderInformation));
            TextWriter w = new StringWriter(sb);
            s.Serialize(w, this);

            return sb.ToString();
        }

        public int FFmpegQualityScale
        {
            get
            {
                float ffqscale = 0;

                if (this.Codec == AudioCodec.AAC)
                {
                    ffqscale = QualityScale * 5;
                    ffqscale = ffqscale < 10 ? 10 : ffqscale;
                }

                if (this.Codec == AudioCodec.MP3)
                {
                    ffqscale = (float)Math.Round(QualityScale / 11);
                    ffqscale = ffqscale > 9 ? 9 : ffqscale;
                }
                if (this.Codec == AudioCodec.Vorbis)
                {
                    ffqscale = QualityScale / 100;
                }

                return (int)Math.Round(ffqscale) * FFmpeg.FF_QP2LAMBDA;
            }
        }
    }

    public class EncoderException : ApplicationException
    {
        public EncoderException() { }
        public EncoderException(string Message) : base(Message) { }
    }
}
