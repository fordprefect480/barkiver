using Azure.Data.Tables;
using Azure;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetValue<string>("AzureTableStorage:ConnectionString");
var tableName = builder.Configuration.GetValue<string>("AzureTableStorage:TableName");
builder.Services.AddSingleton(new TableServiceClient(connectionString));
builder.Services.AddSingleton(sp =>
{
	var tableServiceClient = sp.GetRequiredService<TableServiceClient>();
	return tableServiceClient.GetTableClient(tableName);
});

var app = builder.Build();
app.UseHttpsRedirection();
app.MapGet("/log", async (TableClient tableClient) =>
{
	var logs = new List<BarkLogEntry>();
	await foreach (var log in tableClient.QueryAsync<BarkLogEntry>(x => x.Timestamp > DateTime.UtcNow.AddDays(-1)))
	{
		logs.Add(log);
	}
	return logs;
});

app.MapPost("/log", async (BarkLogEntry newLogEntry, TableClient tableClient) =>
{
	newLogEntry.PartitionKey = builder.Configuration.GetValue<string>("AzureTableStorage:PartitionKey") ?? throw new Exception("Missing app setting: 'AzureTableStorage:PartitionKey'");
	newLogEntry.RowKey = $"{Guid.NewGuid()}";
	await tableClient.AddEntityAsync(newLogEntry);
	return TypedResults.Ok();
});

app.Run();

internal record BarkLogEntry() : ITableEntity
{
	public DateTimeOffset DateFrom { get; set; }
	public DateTimeOffset DateTo { get; set;  }
	public string PartitionKey { get; set; }
	public string RowKey { get; set; }
	public DateTimeOffset? Timestamp { get; set; }
	public ETag ETag { get; set; }
}
