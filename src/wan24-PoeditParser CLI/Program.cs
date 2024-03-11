using wan24.CLI;
using wan24.Core;
using wan24.PoeditParser;

await Bootstrap.Async().DynamicContext();
CliConfig.Apply(new(args));
Translation.Current ??= Translation.Dummy;
CliApi.CommandLine = "wan24PoeditParser";
CliApi.HelpHeader = "wan24-PoeditParser help\n(c) 2024 Andreas Zimmermann, wan24.de";
return await CliApi.RunAsync(args, exportedApis: [typeof(CliHelpApi), typeof(ParserApi)]);
