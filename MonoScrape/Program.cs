using System;
using ArgParse;

namespace MonoScrape {
	internal class Program {
		protected Boolean Verbose;
		
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
				}
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
		// Run
		// --------------------------------------------------
		public void Run() {
		}
	}
}
