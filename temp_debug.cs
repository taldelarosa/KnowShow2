using EpisodeIdentifier.Core.Services;
using Microsoft.Extensions.Logging;
using System;

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigurationService>();
var service = new ConfigurationService(logger, null, "/tmp/test-config.json");
Console.WriteLine("Service created successfully");

