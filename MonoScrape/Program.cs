using ArgParse;
using Awesomium.Core;
using Awesomium.Core.Data;
using Deveel;
using System.Collections.Generic;
using System.IO;
using System;

namespace MonoScrape {
	internal class Program {
		protected Boolean Verbose;
		protected Boolean Running = true;
		protected Logger log = Logger.DefaultLogger;
		protected Browser browser;
		protected SortedDictionary<String, Command> commands;
		protected List<String> preScripts = new List<String>();
		public JSObject Bridge = null;
		
		public class Command {
			public String Name;
			public String Help;
			public Action<String> Callback;
			
			// --------------------------------------------------
			// Command
			// --------------------------------------------------
			public Command(String name="", Action<String> callback=null, String help="") {
				this.Name = name;
				this.Help = help;
				this.Callback = callback ?? new Action<String>(Noop);
			}
			
			// --------------------------------------------------
			// Noop
			// --------------------------------------------------
			public void Noop(String line) {}
		}
		
		// --------------------------------------------------
		// Program
		// --------------------------------------------------
		public Program() {
			// Setup the commands
			commands = new SortedDictionary<String, Command>() {
				{ "add-pre-script" , new Command("add-pre-script" , Command_AddPreScript , "Add a script to run before every page load") }              , 
				{ "exit"           , new Command("exit"           , Command_Quit         , "Alias for quit") }                                          , 
				{ "goto-url"       , new Command("goto-url"       , Command_GotoURL      , "Load a URL into the browser") }                             , 
				{ "include"        , new Command("include"        , Command_Include      , "Include a javascript resource") }                           , 
				{ "js"             , new Command("js"             , Command_JS           , "Run a line of javascript") }                                , 
				{ "quit"           , new Command("quit"           , Command_Quit         , "Quits the application entirely") }                          , 
				{ "run"            , new Command("run"            , Command_Run          , "Run a scraper script which is just a series of commands") } , 
				{ "screenshot"     , new Command("screenshot"     , Command_Screenshot   , "Save a screenshot of the current page") }                   , 
			};
		}
		
		// --------------------------------------------------
		// Command_AddPreScript
		// --------------------------------------------------
		public void Command_AddPreScript(String line) {
			String[] parts = line.Split(new char[] {' '}, 2);
			String filename = parts.Length > 1 ? parts[1] : "";
			preScripts.Add(filename);
		}
		
		// --------------------------------------------------
		// Command_GotoURL
		// --------------------------------------------------
		public void Command_GotoURL(String line) {
			// Get the url from the line
			String url = line.Substring(line.IndexOf(' ') + 1);
			// Tell the browser to go to the url
			browser.LoadURL(url);
		}
		
		// --------------------------------------------------
		// Command_Include
		// --------------------------------------------------
		public void Command_Include(String line) {
			String[] parts = line.Split(new char[] {' '}, 2);
			String file = parts.Length > 1 ? parts[1] : "";
			Include(file);
		}
		
		// --------------------------------------------------
		// Command_JS
		// --------------------------------------------------
		public void Command_JS(String line) {
			String[] parts = line.Split(new char[] {' '}, 2);
			String js = (parts.Length > 1) ? parts[1] : "";
			JSValue result = browser.RunJS(js);
			Console.WriteLine(result.ToString());
		}
		
		// --------------------------------------------------
		// Command_Quit
		// --------------------------------------------------
		public void Command_Quit(String line) {
			Quit();
		}
		
		// --------------------------------------------------
		// Command_Run
		// --------------------------------------------------
		public void Command_Run(String line) {
			String[] parts = line.Split(new char[] {' '}, 2);
			String filename = parts.Length > 1 ? parts[1] : "";
			// Show an error if the filename is empty
			if(filename.Trim() == "") log.Error("Syntax: run <file>");
			// Run the file
			else RunFile(filename);
		}
		
		// --------------------------------------------------
		// Command_Screenshot
		// --------------------------------------------------
		public void Command_Screenshot(String line) {
			String[] parts = line.Split(new char[] {' '}, 2);
			String filename = parts.Length > 1 ? parts[1] : "";
			// Make sure the filename isn't an empty string
			if(filename.Trim() == "") filename = null;
			browser.SavePNG(filename ?? "browser.png");
		}
		
		// --------------------------------------------------
		// Include
		// --------------------------------------------------
		public void Include(String jsFile) {
			browser.Include("assets/" + jsFile);
		}
		
		// --------------------------------------------------
		// Main
		// --------------------------------------------------
		public static void Main(String[] args) {
			// Setup the CLI options we can receive
			Options options = new Options(
				// --verbose
				new Option("verbose") {
					ShortOption = 'v',
					HelpText = "Increase the verbosity of the program",
					ValuePresence = Option.ValueEnum.Prohibited
				},
				// --debug
				new Option("debug") {
					ShortOption = 'd',
					HelpText = "Enable debugging options",
					ValuePresence = Option.ValueEnum.Prohibited
				}
			);
			
			// Parse the arguments
			options.Parse(args);
			
			// Run the program
			new Program() {
				Verbose = options["verbose"]
			}.Run();
		}
		
		// --------------------------------------------------
		// ProcessLine
		// --------------------------------------------------
		public void ProcessLine(String line) {
			// Trim up the line (remove whitespace
			line = line.Trim();
			
			// Ignore empty lines
			if(line != "") {
				// Get the first word of the command
				String firstWord = line.Split(new char[] {' '}, 2)[0];
				// A list to hold the found candidates
				List<Command> candidates = new List<Command>();
				
				// Go through the commands and find the one that matches what the user typed
				foreach(Command command in commands.Values) {
					// If this command matches
					if(command.Name.StartsWith(firstWord)) {
						// Remember this command as a match
						candidates.Add(command);
						// Don't quit if this is only the first match (look for a subsequent match)
						if(candidates.Count > 1) break;
					}
					
					// If we have a candidate, then stop 
					else if(candidates.Count > 0) break;
				}
				
				// More than one command found, user needs to type more letters
				if(candidates.Count > 1) log.Error("Ambiguous command: {0}", firstWord);
				// No commands found
				else if(candidates.Count == 0) log.Error("Command not found: {0}", firstWord);
				// One command found, run it
				else candidates[0].Callback(line);
			}
		}
		
		// --------------------------------------------------
		// Quit
		// --------------------------------------------------
		public void Quit() {
			// Set Running to false primarily to abort the while() input loop
			Running = false;
			// Tell the user goodbye
			log.Info("Bye.");
		}
		
		// --------------------------------------------------
		// Run
		// --------------------------------------------------
		public void Run() {
			// Setup our configuration
			WebConfig config = new WebConfig() {
				LogLevel   = LogLevel.Verbose,
				UserScript = File.ReadAllText("assets/UserScript.js"),
				
			};
			// Initialize the Awesomium WebCore 
			WebCore.Initialize(config);
			// Make an in-memory session for working with multiple instances
			WebSession session = WebCore.CreateWebSession(new WebPreferences() {});
			// Add the scrape datasource to the session
			session.AddDataSource("scrape", new DirectoryDataSource("assets"));
			// Create our browser 
			this.browser = new Browser(session);
			// Create the bridge
			Bridge = browser.CreateGlobalJavascriptObject("Scrape");
			// Make the include function (with no return value)
			Bridge.Bind("include", true, (o,e)=>Include(e.Arguments[0]));
			// Make the log function
			Bridge.Bind("log", true, (o,e) =>log.Info(e.Arguments[0]));
			
			// The input loop
			while(Running) {
				// Get a line of input from the user
				String line = Readline.ReadLine("> ");
				// Handle CTRL-D
				if(line == null) Quit();
				else {
					// Trim whitespace
					line = line.Trim();
					// Add the line to the history
					History.AddHistory(line);
					// Process the line
					ProcessLine(line);
				}
			}
			
			// WebCore isn't smart enough to clean itself up
			WebCore.Shutdown();
		}
		
		// --------------------------------------------------
		// RunFile
		// --------------------------------------------------
		public void RunFile(String filename) {
			// Cleanup the filename
			filename = filename.Trim();
			// Error when file not found
			if(!File.Exists(filename)) 
				log.Error("RunFile cannot find file: {0}", filename);
			
			else {
				// I'm going to use a text reader to go through the file line by line
				StreamReader reader = File.OpenText(filename);
				
				while(!reader.EndOfStream) {
					ProcessLine(reader.ReadLine());
				}
			}
		}
	}
}
