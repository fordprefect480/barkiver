namespace Barkiver.ConsoleApp;

public record BarkLogEntry()
{
	public DateTimeOffset DateFrom { get; set; }
	public DateTimeOffset DateTo { get; set; }
}