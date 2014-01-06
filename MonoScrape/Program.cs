using ArgParse;
using Awesomium.Core.Data;
using Awesomium.Core;
using Deveel;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace MonoScrape {
	public class Program {
		protected Boolean Verbose;
		protected volatile Boolean Running = true;
		protected Logger log = Logger.DefaultLogger;
		protected Browser browser;
		protected SortedDictionary<String, Command> commands;
		protected Queue<String> lines = new Queue<String>();
		
		public String UserAgent {
			get {
				return ((ResourceInterceptor) WebCore.ResourceInterceptor).UserAgent;
			}
			
			set {
				String agent = value;
				if(AgentStrings.ContainsKey(agent)) agent = AgentStrings[agent];
				((ResourceInterceptor) WebCore.ResourceInterceptor).UserAgent = agent;
			}
		}
		public JSObject Bridge = null;
		public Dictionary<String, String> AgentStrings = new Dictionary<String, String>() {
			{"IE6"  , "Mozilla/4.0 (compatible; MSIE 6.1; Windows XP); en-US"}                                                                      , 
			{"IE7"  , "Mozilla/5.0 (Windows; U; MSIE 7.0; Windows NT 6.0; en-US)"}                                                                  , 
			{"IE8"  , "Mozilla/5.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; GTB7.4; InfoPath.2; SV1; .NET CLR 3.3.69573; WOW64; en-US)"} , 
			{"IE9"  , "Mozilla/5.0 (Windows; U; MSIE 9.0; Windows NT 9.0; en-US)"}                                                                  , 
			{"IE10" , "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; WOW64; Trident/6.0)"}                                                    , 
		};
		
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
		// Program (Constructor)
		// --------------------------------------------------
		public Program() {
			// Setup the commands
			commands = new SortedDictionary<String, Command>() {
				{ "add-post-script" , new Command("add-post-script" , Command_AddPostScript , "Add a script to run after every page load") }               , 
				{ "agent"           , new Command("agent"           , Command_UserAgent     , "Alias for user-agent") }                                    , 
				{ "back"            , new Command("back"            , Command_GoBack        , "Go back one page in history") }                             , 
				{ "exit"            , new Command("exit"            , Command_Quit          , "Alias for quit") }                                          , 
				{ "goto-url"        , new Command("goto-url"        , Command_GotoURL       , "Load a URL into the browser") }                             , 
				{ "include"         , new Command("include"         , Command_Include       , "Include a javascript resource") }                           , 
				{ "js"              , new Command("js"              , Command_JS            , "Run a line of javascript") }                                , 
				{ "quit"            , new Command("quit"            , Command_Quit          , "Quits the application entirely") }                          , 
				{ "run"             , new Command("run"             , Command_Run           , "Run a C# script") }                                         , 
				{ "scrape"          , new Command("scrape"          , Command_Scrape        , "Run a scraper script which is just a series of commands") } , 
				{ "screenshot"      , new Command("screenshot"      , Command_Screenshot    , "Save a screenshot of the current page") }                   , 
				{ "tree"            , new Command("tree"            , Command_Tree          , "Prints a tree of the current page") }                       , 
				{ "user-agent"      , new Command("user-agent"      , Command_UserAgent     , "Set the current user agent for subsequent browser loads") } , 
			};
		}
		
		// --------------------------------------------------
		// Click
		// --------------------------------------------------
		public void Click(JSObject target) {
			target.Invoke("click");
		}
		
		// --------------------------------------------------
		// Command_AddPostScript
		// --------------------------------------------------
		public void Command_AddPostScript(String line) {
			browser.AddPostScript(line);
		}
		
		// --------------------------------------------------
		// Command_GoBack
		// --------------------------------------------------
		public void Command_GoBack(String line) {
			browser.GoBack();
		}
		
		// --------------------------------------------------
		// Command_GotoURL
		// --------------------------------------------------
		public void Command_GotoURL(String line) {
			// Tell the browser to go to the url
			browser.LoadURL(line);
		}
		
		// --------------------------------------------------
		// Command_Include
		// --------------------------------------------------
		public void Command_Include(String line) {
			Include(line);
		}
		
		// --------------------------------------------------
		// Command_JS
		// --------------------------------------------------
		public void Command_JS(String line) {
			JSValue result = browser.RunJS(line);
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
		public void Command_Run(String filename) {
			RunScript(filename);
		}
		
		// --------------------------------------------------
		// Command_Scrape
		// --------------------------------------------------
		public void Command_Scrape(String line) {
			// Show an error if the filename is empty
			if(line.Trim() == "") log.Error("Syntax: run <file>");
			// Run the file
			else RunScrapeFile(line);
		}
		
		// --------------------------------------------------
		// Command_Screenshot
		// --------------------------------------------------
		public void Command_Screenshot(String line) {
			// Make sure the filename isn't an empty string
			if(line.Trim() == "") line = null;
			browser.SavePNG(line ?? "browser.png");
		}
		
		// --------------------------------------------------
		// Command_Tree
		// --------------------------------------------------
		public void Command_Tree(String line) {
			JSObject document = browser.RunJS("document");
			
			Action<JSObject, int> Recursor;
			
			Recursor = (o,depth) => {
				log.Info(new String('\t', depth) + o["tagName"]);
				
				if(o["hasChildNodes"]) {
					JSObject children = o["childNodes"];
					JSValue length = children["length"];
					
					if(length.IsNumber) {
						for(int i = 0, iMax=(int)length; i<iMax; ++i) {
							Recursor(children[i.ToString()], depth+1);
						}
					}
				}
			};
			
			Recursor(document, 0);
		}
		
		// --------------------------------------------------
		// Command_UserAgent
		// --------------------------------------------------
		public void Command_UserAgent(String line) {
			UserAgent = line;
		}
		
		// --------------------------------------------------
		// CompileCode
		// --------------------------------------------------
		public IScriptlet CompileCode(String code) {
			// Create a code provider
			// This class implements the 'CodeDomProvider' class as its base. All of the current .Net languages (at least Microsoft ones)
			// come with thier own implemtation, thus you can allow the user to use the language of thier choice (though i recommend that
			// you don't allow the use of c++, which is too volatile for scripting use - memory leaks anyone?)
			CSharpCodeProvider csProvider = new CSharpCodeProvider();

			// Setup our options
			CompilerParameters compileParameters = new CompilerParameters() {
				GenerateExecutable = false , // We want a Dll (or "Class Library" as its called in .Net)
				GenerateInMemory = true    , // Saves us from deleting the Dll when we are done with it. Though you
				                             // could set this to false and save start-up time by next time by not
				                             // having to re-compile And set any others you want, there a quite a few,
				                             // take some time to look through them all and decide which fit your
				                             // application best!
			};

			// Add the current assembly to the script
			compileParameters.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
			// Compile our code
			CompilerResults result = csProvider.CompileAssemblyFromSource(compileParameters, code);
			
			// Document any compilation errors/warning
			if(result.Errors.Count > 0) {
				foreach(CompilerError error in result.Errors)
					log.Error(error.ErrorText);
			}
			// If we didn't compile
			if(result.CompiledAssembly == null) return null;
			
			// Go through all the namespaces
			foreach(Type type in result.CompiledAssembly.GetExportedTypes()) {
				if(typeof(IScriptlet).IsAssignableFrom(type))
					return (IScriptlet) Activator.CreateInstance(type);
			}
			
			return null;
		}
		
		// --------------------------------------------------
		// DumpText
		// --------------------------------------------------
		public void DumpText(String file, String text) {
			File.WriteAllText("data/" + file, text);
		}
		
		// --------------------------------------------------
		// Include
		// --------------------------------------------------
		public void Include(String jsFile) {
			browser.Include("assets/" + jsFile);
		}
		
		// --------------------------------------------------
		// LoadText
		// --------------------------------------------------
		public String LoadText(String file) {
			// Don't return anything if the file cannot be found
			if(!File.Exists("data/" + file)) return "";
			// Read and return the text from the file
			return File.ReadAllText("data/" + file);
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
			// Get a file to run
			String scriptFile = options.Parameters.Count > 0 ? options.Parameters[0] : null;
			
			// Run the program
			new Program() {
				Verbose = options["verbose"]
			}.Run(scriptFile);
		}
		
		// --------------------------------------------------
		// ProcessLine
		// --------------------------------------------------
		public void ProcessLine(String line) {
			// Trim up the line (remove whitespace
			line = line.Trim();
			
			// Ignore empty lines
			if(line != "") {
				// Allow line comments
				if(line[0] == '#') return;
				String[] parts = line.Split(new char[] {' '}, 2);
				// Get the first word of the command
				String firstWord = parts[0];
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
				else candidates[0].Callback(parts.Length > 1 ? parts[1] : "");
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
		public void Run(String scriptFile) {
			// Setup our configuration
			WebConfig config = new WebConfig() {
				LogLevel   = LogLevel.None,
				UserScript = File.ReadAllText("assets/UserScript.js"),
				RemoteDebuggingPort = 8001,
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
			// Make the back function
			Bridge.Bind("back", true, (o,e) => Command_GoBack(""));
			// Make the click function
			Bridge.Bind("click", true, (o,e) => Click(e.Arguments[0]));
			// Make a function for writing data
			Bridge.Bind("dumptext", true, (o,e) => DumpText(e.Arguments[0], e.Arguments[1]));
			// Make the include function (with no return value)
			Bridge.Bind("include", true, (o,e)=>Include(e.Arguments[0]));
			// Make the loadtext function
			Bridge.Bind("loadtext", true, (o,e) => e.Result = LoadText(e.Arguments[0]));
			// Make the log function
			Bridge.Bind("log", true, (o,e) =>log.Info(e.Arguments[0]));
			// Make the screenshot function
			Bridge.Bind("screenshot", true, (o,e) => Command_Screenshot(""));
			// Add a user agent interceptor
			WebCore.ResourceInterceptor = new ResourceInterceptor();
			// If a scriptFile was passed, then setup a line for that
			if(scriptFile != null) lines.Enqueue("scrape " + scriptFile);
			
			Thread repl = new Thread(() => {
				try {
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
							// Add this line to the queue
							lines.Enqueue(line);
						}
					}
				} catch(ThreadInterruptedException) {
				}
			});
			repl.Start();
			
			// The input loop
			while(Running) {
				Thread.Sleep(200);
				WebCore.Update();
				if(lines.Count > 0) ProcessLine(lines.Dequeue());
			}
			
			// Force quite the repl thread
			// FIXME: TODO: Change this to interrupt
			repl.Abort();
			// WebCore isn't smart enough to clean itself up
			WebCore.Shutdown();
		}
		
		// --------------------------------------------------
		// RunScrapeFile
		// --------------------------------------------------
		public void RunScrapeFile(String filename) {
			// Cleanup the filename
			filename = filename.Trim();
			// Error when file not found
			if(!File.Exists(filename)) 
				log.Error("RunScrapeFile cannot find file: {0}", filename);
			
			else {
				// I'm going to use a text reader to go through the file line by line
				StreamReader reader = File.OpenText(filename);
				
				while(!reader.EndOfStream) {
					ProcessLine(reader.ReadLine());
				}
			}
		}
		
		// --------------------------------------------------
		// RunScript
		// --------------------------------------------------
		public void RunScript(String script) {
			if(!File.Exists(script)) script = "assets/" + script;
			if(!File.Exists(script)) log.Error("Cannot find file: {0}", script);
			
			else {
				IScriptlet scriptlet = CompileCode(File.ReadAllText(script));
				if(scriptlet != null) scriptlet.Run(this, browser);
			}
		}
	}
}
