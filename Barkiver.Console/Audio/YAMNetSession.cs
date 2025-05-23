using Microsoft.ML.OnnxRuntime;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime.Tensors;
using Spectre.Console;
using System.Net.Http.Headers;
using Barkiver.ConsoleApp;
using System.Text.Json;


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
		private readonly Dictionary<DateTimeOffset, bool> barkBuckets = [];

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

		public void AddAudioBytes(byte[] audioBytes, int audioOffset, int audioBytesLength, LiveDisplayContext ctx, Table table)
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
						Analyze(features, ctx, table);
					}
					finally
					{
						_featureBuffer.ConsumeOutput(48 * 64);
					}
				}
			}
		}

		public void Analyze(float[] features, LiveDisplayContext ctx, Table table)
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
					if (table.Rows.Count == 20) table.RemoveRow(0);
					if (_classMap[k] != "Silence")
					{
						var nowBucket = Get5MinuteBucket();
						if (barkBuckets.Count != 0)
						{
							var latestBucket =  barkBuckets.Keys.Max();
							if (latestBucket < nowBucket)
							{
								if (barkBuckets[latestBucket] == true)
									Task.Run(() => UploadToStorage(latestBucket));
							}
						}
						barkBuckets[nowBucket] = true;
						table.AddRow($"{DateTime.Now.ToLongTimeString()}: {_classMap[k]}");
					}
					else
					{
						//table.AddRow("");
					}
					ctx.Refresh();
				}
			}
		}

		public async Task UploadToStorage(DateTimeOffset latestBucket)
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("x-api-key", "abc");
			client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

			var logEntry = new BarkLogEntry { DateFrom = latestBucket, DateTo = latestBucket.AddMinutes(5) };
			var jsonContent = new StringContent(JsonSerializer.Serialize(logEntry), Encoding.UTF8, "application/json");
			var response = await client.PostAsync("https://localhost:7223/api/log", jsonContent);
			if (response.IsSuccessStatusCode)
			{
				var content = await response.Content.ReadAsStringAsync();
				AnsiConsole.WriteLine($"Response from API: {content}");
			}
			else
			{
				AnsiConsole.WriteLine($"Failed to call API. Status code: {response.StatusCode}");
			}
		}

		private DateTimeOffset Get5MinuteBucket()
		{
			DateTime utcNow = DateTime.UtcNow;

			// Round down to the previous 5-minute interval
			DateTime roundedDown = new DateTime(
				utcNow.Year,
				utcNow.Month,
				utcNow.Day,
				utcNow.Hour,
				utcNow.Minute / 5 * 5,  // Round down to the nearest 5 minutes
				0,                       // Set seconds to 0
				0,                       // Set milliseconds to 0
				utcNow.Kind              // Preserve the original DateTime kind (UTC)
			);

			return roundedDown;
		}

		public void Dispose()
		{
			_sess.Dispose();
		}
	}
}
