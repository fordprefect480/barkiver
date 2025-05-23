using Barkiver.Audio;
using NAudio.Wave;
using Spectre.Console;

namespace Barkiver.ConsoleApp;

public class BarkiverRunner
{
	private readonly YAMNetSession _session;
	private Table feed;
	private Table log;
	private LiveDisplayContext context;

	public BarkiverRunner() => _session = new YAMNetSession();

	public void Barkive()
	{
		var waveIn = CreateWaveIn();

		//var layout = new Layout("Root")
		//	.SplitColumns(
		//		new Layout("Left"),
		//		new Layout("Right")
		//			.SplitRows(
		//				new Layout("Top"),
		//				new Layout("Bottom")));

		//		// Update the left column
		//		layout["Left"].Update(
		//			new Panel(Align.Center(new Markup("Hello [blue]World![/]"), VerticalAlignment.Middle))
		//				.Expand());

		feed = new Table().LeftAligned().Width(50);
		feed.AddColumn("Live");

		log = new Table().LeftAligned().Width(50);
		log.AddColumn("Log");

		// Animate
		AnsiConsole.Live(log)
			.AutoClear(false)
			.Overflow(VerticalOverflow.Ellipsis)
			.Cropping(VerticalOverflowCropping.Top)
			.Start(ctx =>
			{
				context = ctx;
				waveIn.StartRecording();
				Console.ReadLine();
				waveIn.StopRecording();
			});
	}

	private IWaveIn CreateWaveIn()
	{
		for (int i = 0; i < WaveIn.DeviceCount; i++)
		{
			var cap = WaveIn.GetCapabilities(i);
			AnsiConsole.WriteLine(cap.ProductName);
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
		AnsiConsole.WriteLine("stop");
	}

	private void OnDataAvailable(object sender, WaveInEventArgs e)
	{
		_session.AddAudioBytes(e.Buffer, 0, e.BytesRecorded, context, log);
	}
}
