using Barkiver.Audio;
using NAudio.Wave;

namespace Barkiver;

public class Barkiver
{
	private readonly YAMNetSession _session;

	public Barkiver() => _session = new YAMNetSession();

	public void Barkive()
	{
		var waveIn = CreateWaveIn();
		waveIn.StartRecording();
		Console.ReadLine();
		waveIn.StopRecording();
	}

	private IWaveIn CreateWaveIn()
	{
		for (int i = 0; i < WaveIn.DeviceCount; i++)
		{
			var cap = WaveIn.GetCapabilities(i);
			Console.WriteLine(cap.ProductName);
		}

		IWaveIn waveIn = new WaveInEvent
		{
			DeviceNumber = 0,
			WaveFormat = new WaveFormat(16000, 16, 1)
		};

		waveIn.DataAvailable += OnDataAvailable;
		waveIn.RecordingStopped += OnRecordingStopped;

		return waveIn;
	}

	private void OnRecordingStopped(object sender, StoppedEventArgs e)
	{
		Console.WriteLine("stop");
	}

	private void OnDataAvailable(object sender, WaveInEventArgs e)
	{
		_session.AddAudioBytes(e.Buffer, 0, e.BytesRecorded);
	}

	public void Dispose()
	{
		_session.Dispose();
	}
}
