# wan24-PoeditParser

This is a small dotnet tool for parsing source code for gettext strings and 
writing the result in the PO format to a file or STDOUT. It can also be used 
to create i8n files, which are easy to use from any app.

**CAUTION**: It can create a PO file from the command line, but using it as 
Poedit extractor didn't work yet (Poedit discards the custom extractor 
configuration, which may be a bug - not sure yet).

It's pre-configured for use with the 
[`wan24-Core`](https://github.com/WAN-Solutions/wan24-Core) translation 
helpers for C#, but it can be customized easily for any environment and any 
programming language by customizing the used regular expressions in your own 
configuration file.

## Usage

### Where to get it

The Poedit parser is available as a dotnet tool and can be installed from the 
command line:

```bash
dotnet tool install -g wan24-PoeditParser
```

The default installation folder is 

- `%USER%\.dotnet\tools` for Windows
- `~/.dotnet/tools` for Linux (or MAC)

**NOTE**: Please ensure that your global .NET tool path is in the `PATH` 
environment variable (open a new Windows terminal after adding the path using 
_Settings_ -> _System_ -> _Extended system settings_ -> _Extended_ -> 
_Environment variables_).

### Steps to i8n

Internationalization (i8n) for apps is a common task to make string used in 
apps translatable. gettext and Poedit are tools which have been around for 
many years now and seem to satisfy developers, translators and end users. 
While gettext is mostly used to extract keywords (terms) from source code 
into PO files, Poedit is a GUI to translate those keywords, and store them as 
MO files, which can then be used by the app together with gettext to access 
a translated term.

The steps to i8n your app are:

1. use i8n methods in your code when you want to translate a term
1. extract keywords (terms) from your source code into a PO file using an 
extractor
1. translate the terms using Poedit and create a MO file
1. load the MO file using your apps gettext-supporting library

`wan24-PoeditParser` is a CLI tool which you can configure as extractor in 
Poedit to automatize things a bit.

If you'd like to use the i8n file format from `wan24-PoeditParser` in your 
.NET app, the last step is replaced by:

- convert the PO/MO file to an i8n file using `wan24-PoeditParser`
- load the i8n file using your .NET app using the `wan24-I8N` library

This is one additional step, but maybe worth it, if you don't want to miss 
features like compressed i8n files use ready-to-use i8n data for the 
`wan24-Core` localization (l10n) features.

#TODO Links to Github

### Default file extensions

Per default these file extensions will be looked up when walking through a 
folder tree:

- `.cs`
- `.razor`
- `.cshtml`
- `.aspx`
- `.cake`
- `.vb`

These extensions try to match all possible .NET C#/VB source code files.

In the `config` folder you'll find configurations for other languages, while 
the `dotnet.json` contains the `wan24-PoeditParser` default configuration, 
which you may use as a template for customizations.

### Default keyword search

Per default keywords will be found by these regular expressions:

- `(Description|DisplayText)\(\s*(\".*[^\\]\")\s*\)`
- `(__?|gettextn?|Translate(Plural)?|GetTerm)\(\s*(\".*[^\\]\")`
- `CliApi[^\s]*\([^\)]*Example\s*\=\s*(\".*[^\\]\")`
- `[^\@\$]\".*[^\\]\".*;.*\/\/.*wan24PoeditParser\:include` (case insensitive)

They'll then be post-processed by these replacing regular expressions:

- `^.*(Description|DisplayText)\(\s*(\".*[^\\]\")\s*\).*$` -> `$2`
- `^.*(__?|gettextn?|Translate(Plural)?|GetTerm)\(\s*(\".*[^\\]\").*$` -> `$3`
- `^.*CliApi[^\s]*\([^\)]*Example\s*\=\s*(\".*[^\\]\").*$` -> `$1`
- `^.*[^\@\$](\".*[^\\]\").*;.*\/\/.*wan24PoeditParser\:include.*$` -> `$1` 
(case insensitive)
- `^\s*(\".*[^\\]\").+$` -> `$1`

To force including any string (from a constant definition, for example), 
simply add a comment `// wan24PoeditParser:include` at the end of the line - 
example:

```cs
public const string NAME = "Any PO included keyword";// wan24PoeditParser:include
```

**NOTE**: (Multiline) concatenated string value definitions (like 
`"Part a" + "Part b"`) or interpolations can't be parsed. The matched keyword 
must be C style escaped.

### Poedit extractor configuration

Command to extract keywords from source code to a PO file for Poedit:

```bash
wan24PoeditParser (-singleThread) (-failOnError) (--config "/path/to/customConfig.json") (--ext ".ext" ...) (-mergeOutput) --output %o %C %F
```

Optional arguments, which will override the default configuration:

| Argument | Description |
| -------- | ----------- |
| `--config` | In case you want to load a custom configuration file |
| `-singleThread`* | Disable multithreading |
| `--ext`* | File extensions to use when walking through a folder tree |
| `-mergeOutput`* | Keywords will be merged to the existing output PO file |
| `-failOnError`* | The whole process will fail on any error |

(*) Will also override the custom configuration, if any

Minimal working example using all default settings:

```bash
wan24PoeditParser --output %o %C %F
```

One input file list entry:

```bash
--input %f
```

Configured source code encoding (default is UTF-8):

```bash
--encoding %c
```

### Custom parser configuration

In the `wan24PoeditParserConfig.json` file in the root folder of this 
repository you find the default configuration. You can download and modify it 
for your needs, and use it with the `--config` parameter in the Poedit 
extractor configuration.

The configuration allows to define regular expressions, where

- an array with two elements is a regular expression (and its `RegexOptions` 
enumeration value) which needs to match the string to use
- an array with three elements is used to replace a pattern (the 3rd element 
is the replacement), if the regular expression does match

Example parser JSON configuration:

```json
{
	"SingleThread": false,// (optional) Set to true to disable multithreading (may be overridden by -singleThread)
	"Encoding": "UTF-8",// (optional) Source encoding to use (default is UTF-8; may be overridden by --encoding)
	"Patterns": [// (optional)
		["Any regular expression", "None"],// Search expression example
		["Any regular search expression", "None", "Replacement"],// Replacement expression example
		...
	],
	"FileExtensions": [// (optional) File extensions to include when walking through a folder tree (may be overridden by --ext)
		".ext",
		...
	],
	"MergeOutput": true,// (optional) Merge the extracted keywords to the existing output PO file
	"FailOnError": true,// (optional) To fail thewhole process on any error
	"Merge": false// (optional) Set to true to merge your custom configuration with the default configuration
}
```

The parser looks for any matching search-only expression, then applies all 
matching replacement expressions to refer to the keyword to use, finally. If 
no replacement matched the search expression string, the full search match 
will be the used keyword.

During merging, lists will be combined, and single options will be overwritten.

There are some more optional keys:

- `Core`: [`wan24-Core`](https://github.com/WAN-Solutions/wan24-Core) 
configuration using a `AppConfig` structure
- `CLI`: [`wan24-CLI`](https://github.com/nd1012/wan24-CLI) configuration 
using a `CliAppConfig` structure

### Build, extract, display and use an i8n file

i8n files contain optional compressed translation terms. They can be created 
from PO/MO and/or JSON dictionary (keyword as key, translation array of 
strings as value) input files like this:

```bash
wan24PoeditParser i8n -compress --poInput /path/to/input.po --output /path/to/output.i8n
```

An i8n file can be embedded into an app, for example.

To convert all `*.json|po|mo` files in the current folder to `*.i8n` files:

```bash
wan24PoeditParser i8n buildmany -compress -verbose
```

To display some i8n file informations:

```bash
wan24PoeditParser i8n display --input /path/to/input.i8n
```

To extract some i8n file to a JSON file (prettified):

```bash
wan24PoeditParser i8n extract --input /path/to/input.i8n --jsonOutput /path/to/output.json
```

To extract some i8n file to a PO file:

```bash
wan24PoeditParser i8n extract --input /path/to/input.i8n --poOutput /path/to/output.po
```

**NOTE**: The default plural header for Poedit is 
`nplurals=2; plural=(n != 1);`, which can be overridden by 
`--poeditPluralHeader EXPRESSION`.

**NOTE**: For more options and usage instructions please use the CLI API help 
(see below).

#TODO Add wan24-I8N usage instructions

**TIPP**: You can use the i8n API for converting, merging and validating the 
supported source formats also.

#### i8n file structure in detail

If you didn't skip writing a header during build, the first byte contains the 
version number and a flag (bit 8), if the body is compressed. The file body is 
a JSON encoded dictionary, having the keyword as ID, and the translations as 
value (an array of strings with none, one or multiple (plural) translations).

If compressed, the `wan24-Compression` default compression algorithm was used. 
This is Brotli at the time of writing. But please note that 
`wan24-Compression` writes a non-standard header before the body, which is 
required for compatibility of newer `wan24-Compression` library versions with 
older compressed contents.

**NOTE**: For using compressed i8n files, you'll have to use the 
[`wan24-Compression`](https://www.nuget.org/packages/wan24-Compression) NuGet 
package in your .NET app for decompressing the body.

Please see the `I8NApi(.Internals).cs` source code in this GitHub repository 
for C# code examples.

**TIPP**: Use compression and the i8n header only, if you're using the i8n 
file from a .NET app. Without a header and compression you can simply 
deserialize the JSON dictionary from the i8n file using any modern programming 
language.

### Manual usage from the command line

If you want to call the dotnet tool manually and use advanced options, you can 
display help like this:

```bash
wan24PoeditParser help (--api API (--method METHOD)) (-details)
```

For individual usage support, please 
[open an issue here](https://github.com/nd1012/wan24-PoeditParser/issues).

**NOTE**: The `wan4-Core` CLI configuration (`CliConfig`) will be applied, so 
advanced configuration is possible using those special command line arguments.
