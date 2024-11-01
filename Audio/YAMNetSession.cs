using Microsoft.ML.OnnxRuntime;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime.Tensors;


namespace Barkiver.Audio
{
	public class YAMNetSession : IDisposable
	{
		int _sampleIndex;
		float[] _sampleBuffer;
		AudioFeatureBuffer _featureBuffer;
		InferenceSession _sess;
		string[] _classMap;
		private const int NumClasses = 521;

		public YAMNetSession()
		{
			_sess = new InferenceSession("D:\\git\\other\\yamnet\\yamnet.onnx");
			_sampleBuffer = new float[400 + 95 * 160];
			_featureBuffer = new AudioFeatureBuffer();

			_classMap = new string[NumClasses];
			using (var reader = File.OpenText("D:\\git\\other\\yamnet\\assets\\yamnet_class_map.csv"))
			{
				string line = reader.ReadLine(); // Discard the first line.
				while ((line = reader.ReadLine()) != null)
				{
					if (!string.IsNullOrWhiteSpace(line))
					{
						string[] parts = line.Split(',');
						int classId = int.Parse(parts[0]);
						_classMap[classId] = parts[2];
					}
				}
			}
		}

		public void AddAudioBytes(byte[] audioBytes, int audioOffset, int audioBytesLength)
		{
			var waveform = MemoryMarshal.Cast<byte, short>(audioBytes).ToArray();
			int waveformOffset = audioOffset / sizeof(short);
			int waveformLength = audioBytesLength / sizeof(short);
			while (waveformLength > 0)
			{
				int written = _featureBuffer.Write(waveform, waveformOffset, waveformLength);
				if (written == 0)
				{
					break;
				}
				waveformOffset += written;
				waveformLength -= written;

				while (_featureBuffer.OutputCount >= 96 * 64)
				{
					try
					{
						var features = new float[96 * 64];
						Array.Copy(_featureBuffer.OutputBuffer, 0, features, 0, 96 * 64);
						Analyze(features);
					}
					finally
					{
						_featureBuffer.ConsumeOutput(48 * 64);
					}
				}
			}
		}

		public void Analyze(float[] features)
		{
			var container = new List<NamedOnnxValue>();
			var input = new DenseTensor<float>(features, [1, 1, 96, 64]);
			container.Add(NamedOnnxValue.CreateFromTensor("mfcc:0", input));
			var res = _sess.Run(container, ["activation_10"]);
			foreach (var score in res)
			{
				var s = score.AsTensor<float>();
				float m = -10000.0f;
				int k = -1;
				for (int l = 0; l < s.Dimensions[0]; l++)
				{
					for (int j = 0; j < s.Dimensions[1]; j++)
					{
						if (m < s[l, j])
						{
							k = j;
							m = s[l, j];
						}
					}
					Console.WriteLine("YAMNet: {1} ({0})", k, _classMap[k]);
				}
			}
		}

		public void Dispose()
		{
			_sess.Dispose();
		}
	}
}
