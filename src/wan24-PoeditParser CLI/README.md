# wan24-PoeditParser

This is a small dotnet tool for parsing source code for gettext strings and 
writing the result in the PO format to a file or STDOUT.

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

- `%USER%\.dotnet\tools` in Windows
- `~/.dotnet/tools` in Linux (or MAC)

**NOTE**: Please ensure that your global .NET tool path is in the `PATH` 
environment variable (open a new Windows terminal after adding the path using 
_Settings_ -> _System_ -> _Extended system settings_ -> _Extended_ -> 
_Environment variables_).

### Default file extensions

Per default `.cs` file extensions will be looked up when walking through a 
folder tree.

### Default keyword search

Per default keywords will be found by these regular expressions:

- `^.*((Description|DisplayText)\(\s*(\"".*[^\\]\"")\s*\)).*$` (`$3`)
- `^.*((_|gettextn?|Translate(Plural)?)\(\s*(\"".*[^\\]\"")).*$` (`$4`)
- `^.*(CliApi[^\s]*\([^\)]*Example\s*\=\s*(\"".*[^\\]\"")).*$` (`$2`)

**NOTE**: (Multiline) concatenated string value definitions (like 
`"Part a" + "Part b"`) or interpolations can't be parsed. The matched keyword 
must be C style escaped.

### Poedit extractor configuration

Command to extract translations:

```bash
wan24PoeditParser (-singleThread) (--config "/path/to/wan24PoeditParserConfig.json") (--ext ".ext" ...) --output %o %C %F
```

**NOTE**: The `--config` parameter is optional and used in case you want to 
define a custom configuration file. Using the optional `-singleThread` you can 
disable multithreading (it'll override the configuration). The optional 
`--ext` parameter defines the file extensions to use when walking through a 
folder tree and overrides the configuration.

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
enumeration value) which needs to match the string to use, and export the 
whole match as `$1`
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
	"Merge": false// (optional) Set to true to merge your custom configuration with the default configuration
}
```

The parser looks for any matching search expression, then applies all matching 
replacement expressions to refer to the keyword to use, finally. If no 
replacement matched the search expression string, the full search match will 
be the used keyword.

During merging, lists will be combined, and single options will be overwritten.

### Parsing from the command line

If you want to call the parser manually and use advanced options, you can 
display help like this:

```bash
wan24PoeditParser help (--api API (--method METHOD)) (-details)
```
