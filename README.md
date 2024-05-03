# Static Code Analyzer

## About
This is an internship project to create a static code analyzer. A static code analyzer is a tool to find problems in code without running it.
This is a learning project and not meant to compete with any existing static code analyzers.
The analyzer contains a self-made C# lexer and parser. The analyzer supports a (large but incomplete) subset of the C# language.

## Getting Started

### Running locally
Please make sure that you have the .NET 8 runtime installed.

1) Install the [latest version of the NuGet package](https://www.nuget.org/packages/StaticCodeAnalysis.StaticCodeAnalyzer.CLI/) using the command listed in NuGet.
2) Navigate to a C# project of choice in your terminal.
3) Run the following command `analyzer analyze`
4) Use the suggested name or type a new one
5) Wait for the analysis to complete and the web application to start up.
6) Navigate to your project and open the latest report.

You should now see 2 columns at the left side of your screen. The leftmost one will contain a list of directories and a toggle to include files as well. Clicking on one of the directories will open it and display its contents.
In the following column, a list of issues is displayed. By default, only the issues in the currently selected directory (in the left column) are displayed. 
When "filter on files" is enabled it'll only display the issues in the currently selected file.

NOTE: *The first time either `analyzer analyze` or `analyzer launch` is ran, a web server will be started and keep running in the terminal. While all actions can be performed in both the webapp and terminal,
it may make more sense to open a terminal specifically for hosting the server so you can run any commands in other ones.*

### Running in CI (example using GitHub Actions)

Running the analyzer in CI is very similar to running it locally, except it needs to be launched with `--output-console` and there won't be any web interface (neither the API or web application will be available).

1) Make sure that the .NET 8 runtime is installed (for example using `actions/setup-dotnet@v4` with `dotnet-version: 8.0.x`)
1) Install the [latest version of the NuGet package](https://www.nuget.org/packages/StaticCodeAnalysis.StaticCodeAnalyzer.CLI/)
2) Run the analyzer with `--output-console`

Example step for github actions (see the [self analyze](https://github.com/Noah-Dekens1/StaticCodeAnalyzer/blob/main/.github/workflows/self-analyze.yml) step for an example)
```
 - name: Run Analyzer
   run: dotnet tool install --global StaticCodeAnalysis.StaticCodeAnalyzer.CLI && analyzer analyze --output-console
```

Whether the analyzer succeeds depends on the provided [configuration](https://github.com/Noah-Dekens1/StaticCodeAnalyzer/wiki/Reference-Guide.md#configuration-options). To view all the issues you can read the output log of the action, a separate report is not published.

### Configuring the analyzer

Whether running locally or in CI environments. You'll probably want to configure the analyzer to filter out specific issues or decide when an analysis is considered to be successfull.
The analyzer's config is stored in a `analyzer-config.json` file in the root of the repository.

#### Creating a config file
To create a default variant of the config file, you can use either the command-line interface or the web application.

**Using the command-line interface**
1) Navigate to your project in your terminal
2) Enter the command `analyzer create config`

**Using the web application**
1) Launch the web application using `analyzer launch` if not already running.
2) In the web application open your project or create a new one.
3) Use the "Create config" button followed by the "Edit config" to open the config file in your default editor

#### Editing the config file
The various options are present in the default config file. To see all the different options and descriptions of config items you can check out the wiki in this project for a full [reference guide](https://github.com/Noah-Dekens1/StaticCodeAnalyzer/wiki/Reference-Guide.md).
