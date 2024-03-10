using Microsoft.Extensions.Logging;
using wan24.CLI;
using wan24.Core;
using wan24.PoeditParser;

await Bootstrap.Async().DynamicContext();
CliConfig.Apply(new(args));
#if DEBUG
Settings.LogLevel = LogLevel.Trace;
Logging.Logger = new VividConsoleLogger(LogLevel.Trace, await FileLogger.CreateAsync(Path.Combine(ENV.AppFolder, "wan24PoeditParser.log"), LogLevel.Trace).DynamicContext());
#endif
Translation.Current ??= Translation.Dummy;
CliApi.CommandLine = "wan24PoeditParser";
return await CliApi.RunAsync(args, exportedApis: [typeof(CliHelpApi), typeof(ParserApi)]);
