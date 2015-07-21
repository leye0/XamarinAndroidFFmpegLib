// Here, helpers mainly comes from this code:
// https://github.com/thespooler/ffmpeg-shard


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Opengl;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using XamarinAndroidFFmpegLib;
using XamarinAndroidFFmpegLib.Interop;
using XamarinAndroidFFmpegLib.Interop.AVIO;
using XamarinAndroidFFmpegLib.Interop.Codec;
using XamarinAndroidFFmpegLib.Interop.Format;
using XamarinAndroidFFmpegLib.Interop.Format.Input;
using XamarinAndroidFFmpegLib.Interop.Format.Output;
using XamarinAndroidFFmpegLib.Interop.Util;

namespace XamarinAndroidFFmpegTest
{
	[Activity (Label = "Xamarin Android FFMpeg Library Binding", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		string _workingDirectory;

		TextView _progress;
		ImageView _image;
		Button _previous;
		Button _next;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.Main);
			_progress = FindViewById<TextView> (Resource.Id.progress);
			_image = FindViewById<ImageView> (Resource.Id.image);
			_previous = FindViewById<Button> (Resource.Id.prev);
			_next = FindViewById<Button> (Resource.Id.next);

			Task.Run (() => {
				Start ();
			});
		}

		private unsafe void Start() {

			_workingDirectory = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
			var framesFolder = System.IO.Path.Combine (_workingDirectory, "frames");
			if (!Directory.Exists(framesFolder)) {
				Directory.CreateDirectory (framesFolder);
			}

			CreateSampleFile(Resource.Raw.cat1, _workingDirectory, "cat1.mp4");

			var filename = "cat1.mp4";

			MovieSource = System.IO.Path.Combine (_workingDirectory, filename);

			FFmpeg.avcodec_register_all ();
			FFmpeg.av_register_all ();

			var listAvailableVideoInputFormats = false;
			var listAvailableVideoOutputFormats = false;
			var listAvailableVideoCodecs = false;
			var listAvailableAudioEncoders = false;

		
			if (listAvailableVideoInputFormats) {
				ListInputFormats ();
			}

			if (listAvailableVideoOutputFormats) {
				ListOutputFormats ();
			}

			if (listAvailableVideoCodecs) {
				ListCodecs ();
			}

			if (listAvailableAudioEncoders) {
				ListAudioCodecs ();
			}

			ListAudioCodecs ();

			_sourcePixelFormat = XamarinAndroidFFmpegLib.Interop.Util.PixelFormat.PIX_FMT_NV21;//PIX_FMT_YUV420P;


			MediaFile file = new MediaFile(MovieSource);
			foreach (DecoderStream stream in file.Streams)
			{

				// Leon Pelletier: The same thing we are doing on video can be achieved for the audio. I recommend not using AudioDecoderStream and 
				// rather exploring libavcodec without helpers for the audio part as some part of the original code is broken, and Interops having
				// been coded on the fly, there might be a need to re-code audio Interops too. Have fun.
				//				if (stream.GetType () == typeof(AudioDecoderStream)) {
				//
				//					AudioDecoderStream audioStream = file.Streams [1] as AudioDecoderStream;
				//					var audioFrameBuffer = new byte[audioStream.Length];
				//					using (AudioEncoderStream d = new AudioEncoderStream (System.IO.Path.Combine (_workingDirectory, "yeeeh.wav"), new EncoderInformation (AudioCodec.PCM, 128000, 48000, 1, false))) {
				//
				//						while (audioStream.ReadFrame (out audioFrameBuffer)) {
				//							try {
				//								d.Write (audioFrameBuffer, 0, audioFrameBuffer.Length);							
				//							} catch (Exception e) {
				//								var ab = e.Message;
				//							}
				//
				//						}
				//					}
				//				}

				if (stream.GetType() == typeof(VideoDecoderStream)) {

					VideoDecoderStream videoStream = stream as VideoDecoderStream;

					if (videoStream != null) {
						_videoScalingStream = new VideoScalingStream(videoStream, videoStream.Width, videoStream.Height, _sourcePixelFormat);

						var fps = _videoScalingStream.FrameRate;
						var start = DateTime.Now;

						int i = 0;
						var fileOutput = System.IO.Path.Combine (_workingDirectory, string.Format ("{0}.mp4", "movie"));

						AVOutputFormat* outFmt = FFmpeg.av_guess_format("mp2", "a.mp2", null);
						AVFormatContext outFmtCtx = FFmpeg.avformat_alloc_context ();//*outFmtCtx, ref *outFmt, null, m_filename);
						outFmtCtx.oformat = outFmt;

						AVStream * outStrm = FFmpeg.av_new_stream(ref outFmtCtx, 0);

						AVCodec * codec = null;
						FFmpeg.avcodec_get_context_defaults3(ref *outStrm->codec, codec);
						outStrm->codec->coder_type = (int) AVMediaType.AVMEDIA_TYPE_VIDEO;;
						outStrm->codec->codec_type = (int)AVMediaType.AVMEDIA_TYPE_VIDEO;
						outStrm->codec->pix_fmt = XamarinAndroidFFmpegLib.Interop.Util.PixelFormat.PIX_FMT_YUV420P;
						outStrm->codec->width = _videoScalingStream.Width;
						outStrm->codec->height = _videoScalingStream.Height;
						outStrm->codec->me_cmp = 1; // Motion estimation comparison. I think it's one-based and 1 is none.
						outStrm->codec->time_base = new AVRational ();
						outStrm->codec->time_base.num = 1; // Frame-per-second, numerator.
						outStrm->codec->time_base.den = (int) fps; // Frame-per-second, denominator.

						FFmpeg.avio_open(outFmtCtx.pb, fileOutput, FFmpeg.URL_WRONLY);
						FFmpeg.avformat_write_header(ref outFmtCtx, null);

						var frameCount = (double)file.Duration.TotalMilliseconds / (1000 / fps);

						while (_videoScalingStream.ReadFrame (out _videoFrameBuffer)) {
							var progress = ((int)(((double)i * 100d) / frameCount)).ToString () + "%";
							i++;
							FrameBufferToImage (_videoFrameBuffer, "frame-" + i.ToString().PadLeft(4, '0'), progress);
						}
						RunOnUiThread (() => _progress.Text = "Done! Check at " + _workingDirectory + "/frames/");
					}
				}
			}
		}

		private VideoScalingStream _videoScalingStream = null;

		XamarinAndroidFFmpegLib.Interop.Util.PixelFormat _sourcePixelFormat;

		private string MovieSource;
		private string AndroidPicturesFolder = Android.OS.Environment.GetExternalStoragePublicDirectory (Android.OS.Environment.DirectoryPictures).ToString();

		byte[] _videoFrameBuffer;
		private Bitmap videoFrameBitmap;
		FileStream bitmapFile = null;

		Bitmap YuvToBitmap(byte[] data, int width, int height) {
			var output = new MemoryStream();
			var yuvImage = new YuvImage(data, Android.Graphics.ImageFormatType.Nv21, width, height, null);
			yuvImage.CompressToJpeg(new Rect(0, 0, width, height), 100, output);
			byte[] imageBytes = output.ToArray ();
			return BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length);
		}


		void FrameBufferToImage(byte[] videoFrameBuffer, string fileName, string progress) {
			
			try {
				var newBitmapPath = System.IO.Path.Combine (_workingDirectory, "frames/", string.Format ("{0}.bmp", fileName));
				bitmapFile = new FileStream (newBitmapPath, FileMode.Create);
				videoFrameBitmap = YuvToBitmap(videoFrameBuffer, _videoScalingStream.Width, _videoScalingStream.Height);
				RunOnUiThread(() => 
					{
						_image.SetImageBitmap(videoFrameBitmap);
						_progress.Text = progress + " " + newBitmapPath;
						_progress.ForceLayout();
					}

				
				);
				videoFrameBitmap.Compress (Bitmap.CompressFormat.Jpeg, 30, Stream.Synchronized (bitmapFile)); // bmp is your Bitmap instance

			} catch (Exception) {
			} finally {
				try {
					if (bitmapFile != null) {
						bitmapFile.Close ();
					}
				} catch (System.IO.IOException) {
				}
			}

		}

		unsafe void ListCodecs() {
			AVCodec codec;
			var res = FFmpeg.av_codec_next(&codec);
			while((res = FFmpeg.av_codec_next(res)) != null)
			{
				var name = res->longname;

				AVCodec* avCodecAsEncoder = FFmpeg.avcodec_find_encoder(res->id);
				if (avCodecAsEncoder != null) {
					AVCodecContext avCodecContext = new AVCodecContext ();		
					FFmpeg.avcodec_get_context_defaults3 (ref avCodecContext, avCodecAsEncoder);
					avCodecContext.time_base = new AVRational ();
					avCodecContext.time_base.num = 1;
					avCodecContext.time_base.den = 30;
					avCodecContext.me_method = 1;
					avCodecContext.width = _videoScalingStream.Width;
					avCodecContext.height = _videoScalingStream.Height;
					avCodecContext.gop_size = 30;
					avCodecContext.bit_rate = avCodecContext.width * avCodecContext.height * 4;
					avCodecContext.pix_fmt = XamarinAndroidFFmpegLib.Interop.Util.PixelFormat.PIX_FMT_RGBA;

					if (FFmpeg.avcodec_open (ref avCodecContext, avCodecAsEncoder) >= 0) {
						System.Diagnostics.Debug.WriteLine ("[SUPPORTED ENCODER] - " + name);
					}
				} else {
					System.Diagnostics.Debug.WriteLine ("[NOT SUPPORTING] - " + name);
				}
			}
		}

		unsafe void ListAudioCodecs() {
			AVCodec codec;
			var res = FFmpeg.av_codec_next(&codec);
			while((res = FFmpeg.av_codec_next(res)) != null)
			{
				var name = res->longname;
				var sName = res->name;

				AVCodec* avCodecAsEncoder = FFmpeg.avcodec_find_encoder(res->id);
				if (avCodecAsEncoder != null) {
					AVCodecContext avCodecContext = new AVCodecContext ();		
					FFmpeg.avcodec_get_context_defaults3 (ref avCodecContext, avCodecAsEncoder);

					// Provides a very simple setting for audio
					//					avCodecContext.codec_id = res->id;	
					//					avCodecContext.sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16;
					//					avCodecContext.bit_rate    = 32000;
					//					avCodecContext.sample_rate = 48000;
					//					avCodecContext.time_base = new AVRational() { num = 1, den = avCodecContext.sample_rate };
					//					avCodecContext.channels = 1;
					//					avCodecContext.channel_layout = FFmpeg.AV_CH_FRONT_CENTER;

					if (FFmpeg.avcodec_open (ref avCodecContext, avCodecAsEncoder) >= 0) {
						System.Diagnostics.Debug.WriteLine ("[SUPPORTED AUDIO ENCODER] - " + name);
						var m_avFormatCtx = FFmpeg.avformat_alloc_context();
						m_avFormatCtx.oformat = FFmpeg.av_guess_format(sName, null, null);
						var ext = "n.n";
						if (m_avFormatCtx.oformat != null) {
							var debugLongname = m_avFormatCtx.oformat->long_name;
							ext = m_avFormatCtx.oformat->extensions;

							if (0 == (m_avFormatCtx.oformat->flags & (int)FFmpeg.AVFMT_NOFILE)) {

								var successCode = FFmpeg.avio_open ((AVIOContext*)m_avFormatCtx.pb, System.IO.Path.Combine (_workingDirectory, ext), FFmpeg.URL_WRONLY);
								if (successCode >= 0) {
									System.Diagnostics.Debug.WriteLine ("[SUPPORTED FILE IO] - " + name);
								}
							}
						}
					}
				} else {
					System.Diagnostics.Debug.WriteLine ("[NOT SUPPORTING AUDIO ENCODER] - " + name);
				}
			}
		}

		unsafe void ListInputFormats() {
			AVInputFormat inputFormat;
			var res = FFmpeg.av_iformat_next(&inputFormat);
			while((res = FFmpeg.av_iformat_next(res)) != null)
			{
				var name = res->name;
				System.Diagnostics.Debug.WriteLine (name);
			}
		}

		unsafe void ListOutputFormats() {
			AVOutputFormat outputFormat;
			var res = FFmpeg.av_oformat_next(&outputFormat);
			while((res = FFmpeg.av_oformat_next(res)) != null)
			{
				var name = res->name;
				System.Diagnostics.Debug.WriteLine (name);
			}
		}

		private void CreateSampleFile(int resource, string destinationFolder, string filename) {
			var data = new byte[0];
			using (var file = Resources.OpenRawResource (resource))
			using (var fileInMemory = new MemoryStream ()) {
				file.CopyTo (fileInMemory);
				data = fileInMemory.ToArray ();
			}
			var fileName = System.IO.Path.Combine (destinationFolder, filename);
			System.IO.File.WriteAllBytes (fileName, data);
		}

		void RemoveSampleFile(string sourceFolder, string name) {
			System.IO.File.Delete (System.IO.Path.Combine (sourceFolder, name));
		}
	}
}


